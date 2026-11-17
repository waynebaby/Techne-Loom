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

    internal static async Task<WorkflowFragmentDesignSession> StartAsync(
        string baselineJson,
        IEnumerable<WorkflowFragmentId> authorizedFragments,
        CancellationToken cancellationToken = default)
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

        (await WorkflowValidator.ValidateWithContextFieldCatalogAsync(baseline, cancellationToken).ConfigureAwait(false))
            .ThrowIfInvalid();
        var fragments = sourceGraph.Locations.Keys.ToImmutableDictionary(id => id, _ => new WorkflowFragmentRevision(0, 0, true));
        var events = fragments.Keys.Select(id => new WorkflowFragmentLockEvent(
            id, WorkflowFragmentLockAction.Freeze, 0, 0, "Owning baseline validation passed.")).ToImmutableArray();
        return new WorkflowFragmentDesignSession(scope, new WorkflowFragmentSnapshot(0, baselineJson, fragments, events));
    }

    internal async Task<WorkflowFragmentSnapshot> ApplyAsync(
        long expectedGeneration,
        WorkflowFragmentEdit edit,
        string reason,
        CancellationToken cancellationToken = default)
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

        var candidate = await ComposeAsync(previous.WorkflowJson, edit, cancellationToken).ConfigureAwait(false);
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
        var events = previous.LockEvents.AddRange(ImmutableArray.Create<WorkflowFragmentLockEvent>(
            new(edited, WorkflowFragmentLockAction.Unfreeze, priorRevision.ContentRevision, generation, reason),
            new(edited, WorkflowFragmentLockAction.Freeze, revision, generation, "Owning candidate validation passed.")));
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
        CancellationToken cancellationToken)
    {
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-session-");
        try
        {
            return await WorkflowFragmentEditPilot.ApplyAsync(baselineJson, edit, scratch.FullName, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
