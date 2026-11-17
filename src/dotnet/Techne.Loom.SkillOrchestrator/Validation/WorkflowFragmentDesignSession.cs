using System.Collections.Immutable;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal enum WorkflowFragmentLockAction { Freeze, Unfreeze }

internal sealed record WorkflowFragmentLockEvent(
    WorkflowFragmentId Fragment,
    WorkflowFragmentLockAction Action,
    long ContentRevision,
    long Generation,
    DateTimeOffset RecordedAtUtc,
    string Reason);

// Session history records when validation ran. Reusing its result would also
// require the exact validator inputs, including the owning context catalog.
internal sealed record WorkflowFragmentRevision(long ContentRevision, long LastValidatedGeneration, bool Frozen);

internal sealed record WorkflowFragmentSnapshot(
    long Generation,
    string WorkflowJson,
    ImmutableDictionary<WorkflowFragmentId, WorkflowFragmentRevision> Fragments,
    ImmutableArray<WorkflowFragmentLockEvent> LockEvents);

internal sealed class WorkflowFragmentDesignSession
{
    private readonly ImmutableHashSet<WorkflowFragmentId> _authorizedFragments;
    private WorkflowFragmentSnapshot _snapshot;

    private WorkflowFragmentDesignSession(ImmutableHashSet<WorkflowFragmentId> authorizedFragments, WorkflowFragmentSnapshot snapshot)
    {
        _authorizedFragments = authorizedFragments;
        _snapshot = snapshot;
    }

    internal WorkflowFragmentSnapshot Current => Volatile.Read(ref _snapshot);

    internal static WorkflowFragmentDesignSession Restore(
        IEnumerable<WorkflowFragmentId> authorizedFragments,
        WorkflowFragmentSnapshot snapshot)
    {
        var locations = WorkflowFragmentGraph.Create(WorkflowDraftImport.Read(snapshot.WorkflowJson)).Locations;
        var scope = authorizedFragments.ToImmutableHashSet();
        if (snapshot.Generation < 0
            || !locations.Keys.ToHashSet().SetEquals(snapshot.Fragments.Keys)
            || !scope.IsSubsetOf(snapshot.Fragments.Keys)
            || snapshot.Fragments.Values.Any(revision => !revision.Frozen
                || revision.ContentRevision < 0
                || revision.ContentRevision > revision.LastValidatedGeneration
                || revision.LastValidatedGeneration > snapshot.Generation)
            || snapshot.LockEvents.IsDefaultOrEmpty
            || snapshot.LockEvents.Any(item => item is null
                || !snapshot.Fragments.ContainsKey(item.Fragment)
                || item.Generation < 0
                || item.Generation > snapshot.Generation
                || item.RecordedAtUtc == default
                || item.ContentRevision < 0
                || item.ContentRevision > snapshot.Fragments[item.Fragment].ContentRevision)
            || !HasCompleteLockHistory(snapshot, scope))
        {
            throw new InvalidOperationException("The workflow design session contains inconsistent fragment history.");
        }

        return new WorkflowFragmentDesignSession(scope, snapshot);
    }

    internal static async Task<WorkflowFragmentDesignSession> StartAsync(
        string baselineJson,
        IEnumerable<WorkflowFragmentId> authorizedFragments,
        CancellationToken cancellationToken = default,
        ContextFieldCatalog? catalog = null)
    {
        var baseline = WorkflowDraftImport.Read(baselineJson);
        var runtime = SkillCli.GetRuntimeVersion();
        if (baseline.RuntimeBinding != "dotnet-so" || baseline.RuntimeVersion != runtime)
        {
            throw new InvalidOperationException($"The workflow design session requires SO version '{runtime}'.");
        }

        var sourceGraph = WorkflowFragmentGraph.Create(baseline);
        var scope = authorizedFragments.ToImmutableHashSet();
        if (scope.Any(id => !sourceGraph.Locations.ContainsKey(id)))
        {
            throw new InvalidOperationException("The authorized scope references an unknown fragment.");
        }

        var validation = catalog is null
            ? await WorkflowValidator.ValidateWithContextFieldCatalogAsync(baseline, cancellationToken).ConfigureAwait(false)
            : WorkflowValidator.ValidateWithContextFieldCatalog(baseline, catalog);
        validation.ThrowIfInvalid();
        var fragments = sourceGraph.Locations.Keys.ToImmutableDictionary(id => id, _ => new WorkflowFragmentRevision(0, 0, true));
        var recordedAt = DateTimeOffset.UtcNow;
        var events = fragments.Keys.Select(id => new WorkflowFragmentLockEvent(
            id, WorkflowFragmentLockAction.Freeze, 0, 0, recordedAt, "Owning baseline validation passed.")).ToImmutableArray();
        return new WorkflowFragmentDesignSession(scope, new WorkflowFragmentSnapshot(0, baselineJson, fragments, events));
    }

    internal async Task<WorkflowFragmentSnapshot> ApplyAsync(
        long expectedGeneration,
        WorkflowFragmentEdit edit,
        string reason,
        CancellationToken cancellationToken = default,
        ContextFieldCatalog? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var previous = Current;
        if (expectedGeneration != previous.Generation)
        {
            throw new InvalidOperationException("The workflow design session changed. Reload its generation before editing.");
        }

        var edited = edit.Fragment;
        if (!_authorizedFragments.Contains(edited))
        {
            throw new InvalidOperationException($"Fragment '{edited}' is outside the authorized edit scope.");
        }

        var baseline = WorkflowDraftImport.Read(previous.WorkflowJson);
        var baselineGraph = WorkflowFragmentGraph.Create(baseline);
        if (edit.Stage(baseline) is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureCurrent(previous);
            return previous;
        }

        var candidate = await ComposeAsync(previous.WorkflowJson, edit, cancellationToken, catalog).ConfigureAwait(false);
        var candidateJson = WorkflowJsonSerializer.Serialize(candidate);
        var priorRevision = previous.Fragments[edited];
        var generation = checked(previous.Generation + 1);
        var impacted = WorkflowFragmentGraph.Impact(
            baselineGraph, WorkflowFragmentGraph.Create(candidate),
            [edited], hasUnindexedDependencies: edit.ChangesDataflow);
        var fragments = previous.Fragments.ToBuilder();
        foreach (var id in impacted)
        {
            fragments[id] = fragments[id] with { LastValidatedGeneration = generation };
        }

        var revision = checked(priorRevision.ContentRevision + 1);
        fragments[edited] = new WorkflowFragmentRevision(revision, generation, true);
        var recordedAt = DateTimeOffset.UtcNow;
        var events = previous.LockEvents.AddRange(ImmutableArray.Create<WorkflowFragmentLockEvent>(
            new(edited, WorkflowFragmentLockAction.Unfreeze, priorRevision.ContentRevision, generation, recordedAt, reason),
            new(edited, WorkflowFragmentLockAction.Freeze, revision, generation, recordedAt, "Owning candidate validation passed.")));
        var next = new WorkflowFragmentSnapshot(generation, candidateJson, fragments.ToImmutable(), events);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, next, previous), previous))
        {
            throw new InvalidOperationException("A concurrent edit already published this workflow design generation.");
        }

        return next;
    }

    private void EnsureCurrent(WorkflowFragmentSnapshot expected)
    {
        if (!ReferenceEquals(Current, expected))
        {
            throw new InvalidOperationException("A concurrent edit already published this workflow design generation.");
        }
    }

    private static async Task<WorkflowInstance> ComposeAsync(
        string baselineJson,
        WorkflowFragmentEdit edit,
        CancellationToken cancellationToken,
        ContextFieldCatalog? catalog)
    {
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-session-");
        try
        {
            return await WorkflowFragmentEditPilot.ApplyAsync(baselineJson, edit, scratch.FullName, cancellationToken, catalog)
                .ConfigureAwait(false);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private static bool HasCompleteLockHistory(WorkflowFragmentSnapshot snapshot, ImmutableHashSet<WorkflowFragmentId> scope)
    {
        var revisions = new Dictionary<WorkflowFragmentId, long>();
        var index = 0;
        while (index < snapshot.LockEvents.Length && snapshot.LockEvents[index].Generation == 0)
        {
            var initial = snapshot.LockEvents[index++];
            if (initial.Action != WorkflowFragmentLockAction.Freeze || initial.ContentRevision != 0
                || !revisions.TryAdd(initial.Fragment, 0))
            {
                return false;
            }
        }

        var remaining = snapshot.LockEvents.Length - index;
        if (revisions.Count != snapshot.Fragments.Count || remaining % 2 != 0 || remaining / 2 != snapshot.Generation)
        {
            return false;
        }

        for (long generation = 1; index < snapshot.LockEvents.Length; generation++)
        {
            var unfreeze = snapshot.LockEvents[index++];
            var freeze = snapshot.LockEvents[index++];
            if (unfreeze.Generation != generation || freeze.Generation != generation
                || unfreeze.Fragment != freeze.Fragment
                || !scope.Contains(unfreeze.Fragment) || string.IsNullOrWhiteSpace(unfreeze.Reason)
                || unfreeze.Action != WorkflowFragmentLockAction.Unfreeze || freeze.Action != WorkflowFragmentLockAction.Freeze
                || unfreeze.ContentRevision != revisions[unfreeze.Fragment]
                || freeze.ContentRevision != unfreeze.ContentRevision + 1)
            {
                return false;
            }

            revisions[freeze.Fragment] = freeze.ContentRevision;
        }

        return revisions.All(pair => snapshot.Fragments[pair.Key].ContentRevision == pair.Value);
    }
}
