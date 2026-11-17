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
    private readonly ImmutableHashSet<string> _authorizedStateIds;
    private WorkflowFragmentSnapshot _snapshot;

    private WorkflowFragmentDesignSession(ImmutableHashSet<string> authorizedStateIds, WorkflowFragmentSnapshot snapshot)
    {
        _authorizedStateIds = authorizedStateIds;
        _snapshot = snapshot;
    }

    internal WorkflowFragmentSnapshot Current => Volatile.Read(ref _snapshot);

    internal static async Task<WorkflowFragmentDesignSession> StartAsync(
        string baselineJson,
        IEnumerable<string> authorizedStateIds,
        CancellationToken cancellationToken = default)
    {
        var baseline = WorkflowDraftImport.Read(baselineJson);
        var runtime = SkillCli.GetRuntimeVersion();
        if (baseline.RuntimeBinding != "dotnet-so" || baseline.RuntimeVersion != runtime)
        {
            throw new InvalidOperationException($"The workflow design session requires SO version '{runtime}'.");
        }

        var sourceGraph = WorkflowFragmentGraph.Create(baseline);
        var scope = authorizedStateIds.ToImmutableHashSet(StringComparer.Ordinal);
        if (scope.Any(id => !sourceGraph.Locations.ContainsKey(new(WorkflowFragmentKind.State, id))))
        {
            throw new InvalidOperationException("The authorized scope references an unknown state fragment.");
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
        WorkflowStateDescriptionEdit edit,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var previous = Current;
        if (expectedGeneration != previous.Generation)
        {
            throw new InvalidOperationException("The workflow design session changed. Reload its generation before editing.");
        }

        if (!_authorizedStateIds.Contains(edit.StateId))
        {
            throw new InvalidOperationException($"The state '{edit.StateId}' is outside the authorized edit scope.");
        }

        var baseline = WorkflowDraftImport.Read(previous.WorkflowJson);
        var state = (StateNode)baseline.Nodes[edit.StateId];
        if (string.Equals(state.Description, edit.Description, StringComparison.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureCurrent(previous);
            return previous;
        }

        var candidate = await ComposeAsync(previous.WorkflowJson, edit, cancellationToken).ConfigureAwait(false);
        var candidateJson = WorkflowJsonSerializer.Serialize(candidate);
        var edited = new WorkflowFragmentId(WorkflowFragmentKind.State, edit.StateId);
        var priorRevision = previous.Fragments[edited];
        var generation = checked(previous.Generation + 1);
        var impacted = WorkflowFragmentGraph.Impact(
            WorkflowFragmentGraph.Create(baseline), WorkflowFragmentGraph.Create(candidate),
            [edited], hasUnindexedDependencies: false);
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
        WorkflowStateDescriptionEdit edit,
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
