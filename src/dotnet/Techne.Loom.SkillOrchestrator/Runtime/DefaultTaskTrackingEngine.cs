using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed class DefaultTaskTrackingEngine : ITaskTrackingEngine
{
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ISystemClock _clock;
    private readonly IProgress<object>? _commandProgress;

    public DefaultTaskTrackingEngine(
        IInstanceStore instanceStore,
        IExpressionEvaluator? expressionEvaluator = null,
        ICommandDispatcher? commandDispatcher = null,
        ISystemClock? clock = null,
        IProgress<object>? commandProgress = null)
    {
        InstanceStore = instanceStore;
        _expressionEvaluator = expressionEvaluator ?? new SimpleExpressionEvaluator();
        _commandDispatcher = commandDispatcher ?? new DefaultCommandDispatcher();
        _clock = clock ?? new SystemClock();
        _commandProgress = commandProgress;
    }

    public IInstanceStore InstanceStore { get; set; }

    public Task<WorkflowInstance?> GenerateWorkflowAsync(string workflowUserDescription, CancellationToken ct = default)
    {
        var startNode = new StateNode
        {
            Id = "draft.start",
            Name = "Draft Start",
            Description = workflowUserDescription,
        };

        var instance = new WorkflowInstance
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            StartNodeId = startNode.Id,
            CurrentNodeId = startNode.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [startNode.Id] = startNode,
            },
            Status = WorkflowStatus.Drafting,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["description"] = workflowUserDescription,
            },
        };

        return Task.FromResult<WorkflowInstance?>(instance);
    }

    public async Task<EngineTickOutcome> TickAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (instance.Status == WorkflowStatus.Drafting)
        {
            return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
        }

        if (instance.Status == WorkflowStatus.ReadyToStart)
        {
            instance.Status = WorkflowStatus.Running;
            instance.CurrentNodeId = instance.StartNodeId;
            MarkStateEntrance(instance);
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, instance.CurrentNodeId, TaskNodeType.State, ExecutionStatus.Started, Message: "Start"));
        }

        if (instance.Status is WorkflowStatus.Failed or WorkflowStatus.Succeeded)
        {
            return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
        }

        PruneExpiredWaitEntries(instance);

        if (!instance.Nodes.TryGetValue(instance.CurrentNodeId, out var node) || node is not StateNode state)
        {
            instance.Status = WorkflowStatus.Failed;
            return EngineTickOutcome.FailedWith($"Current node '{instance.CurrentNodeId}' is missing or not a state node.");
        }

        if (string.Equals(instance.CurrentNodeId, instance.EndNodeId, StringComparison.Ordinal) && state.Groups.Count == 0)
        {
            instance.Status = WorkflowStatus.Succeeded;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, state.Id, TaskNodeType.State, ExecutionStatus.Succeeded, Message: "Reached terminal state"));
            return EngineTickOutcome.ProgressedTo(state.Id);
        }

        foreach (var group in state.Groups)
        {
            var transitions = ResolveTransitions(instance, group)
                .Where(transition => _expressionEvaluator.EvaluateBoolean(transition.GuardExpression, instance.Context))
                .OrderBy(transition => transition.Priority)
                .ToList();

            if (transitions.Count == 0)
            {
                continue;
            }

            if (group.Strategy != ConcurrencyStrategy.FirstSuccess && transitions.Count > 1)
            {
                instance.Status = WorkflowStatus.Failed;
                var message = $"TransitionGroup strategy '{group.Strategy}' is not implemented by the current public SO runtime when more than one transition is ready.";
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, group.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: message));
                return EngineTickOutcome.FailedWith(message);
            }

            if (group.Strategy == ConcurrencyStrategy.All)
            {
                var outcome = await ExecuteAllAsync(instance, state, group, transitions, ct).ConfigureAwait(false);
                if (outcome.Progressed || outcome.Suspended || outcome.Failed)
                {
                    return outcome;
                }

                continue;
            }

            foreach (var transition in transitions)
            {
                var outcome = await ExecuteTransitionAsync(instance, state, group, transition, ct).ConfigureAwait(false);
                if (outcome.Progressed || outcome.Suspended || outcome.Failed || outcome.Moved)
                {
                    return outcome;
                }
            }
        }

        if (string.Equals(instance.CurrentNodeId, instance.EndNodeId, StringComparison.Ordinal))
        {
            instance.Status = WorkflowStatus.Succeeded;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, instance.CurrentNodeId, TaskNodeType.State, ExecutionStatus.Succeeded, Message: "Completed"));
            return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
        }

        return EngineTickOutcome.NoProgress(instance.CurrentNodeId, TimeSpan.FromMilliseconds(250));
    }

    public Task ResumeAsync(
        WorkflowInstance instance,
        string transitionId,
        string? correlationKey,
        Dictionary<string, object?>? payload,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var waitGroup = instance.ActiveWaitGroups.FirstOrDefault(group =>
            string.Equals(group.TransitionId, transitionId, StringComparison.Ordinal)
            && (correlationKey is null || string.Equals(group.CorrelationKey, correlationKey, StringComparison.Ordinal)));

        if (waitGroup is null)
        {
            throw new InvalidOperationException($"Active wait group '{transitionId}' was not found.");
        }

        var entry = waitGroup.GetNextPendingEntry() ?? waitGroup.Entries.FirstOrDefault();
        if (entry is null)
        {
            throw new InvalidOperationException($"Wait group '{transitionId}' has no entries to complete.");
        }

        waitGroup.TryCompleteEntry(entry.WaitId, payload);

        if (!waitGroup.Completed)
        {
            instance.Status = WorkflowStatus.WaitingExternal;
            return Task.CompletedTask;
        }

        foreach (var pair in waitGroup.AggregatedContext)
        {
            instance.Context[pair.Key] = pair.Value;
        }

        instance.ActiveWaitGroups.Remove(waitGroup);
        if (!string.IsNullOrWhiteSpace(waitGroup.TargetStateId))
        {
            instance.CurrentNodeId = waitGroup.TargetStateId;
            MarkStateEntrance(instance);
        }

        instance.Status = WorkflowStatus.Running;
        instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transitionId, TaskNodeType.Transition, ExecutionStatus.Succeeded, payload, "Resume applied"));
        return Task.CompletedTask;
    }

    private async Task<EngineTickOutcome> ExecuteAllAsync(
        WorkflowInstance instance,
        StateNode state,
        TransitionGroup group,
        IReadOnlyList<TransitionBase> transitions,
        CancellationToken ct)
    {
        EngineTickOutcome? lastOutcome = null;

        foreach (var transition in transitions)
        {
            lastOutcome = await ExecuteTransitionAsync(instance, state, group, transition, ct).ConfigureAwait(false);
            if (lastOutcome.Failed || lastOutcome.Suspended)
            {
                return lastOutcome;
            }
        }

        return lastOutcome ?? EngineTickOutcome.NoProgress(instance.CurrentNodeId);
    }

    private async Task<EngineTickOutcome> ExecuteTransitionAsync(
        WorkflowInstance instance,
        StateNode state,
        TransitionGroup group,
        TransitionBase transition,
        CancellationToken ct)
    {
        try
        {
            if (IsExternalStep(transition.StepKind))
            {
                return RegisterExternalBoundary(instance, state, group, transition);
            }

            if (transition is ExpressionTransition)
            {
                if (_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression, instance.Context))
                {
                    MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Expression transition succeeded");
                    return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
                }

                return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
            }

            if (transition.StepKind is WorkflowStepKind.StateUpdate or WorkflowStepKind.MemoryWrite)
            {
                ApplyDictionaryParameters(instance.Context, transition, "updates");
                MoveToTarget(instance, transition, ExecutionStatus.Succeeded, $"{transition.StepKind} applied");
                return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
            }

            if (transition.StepKind == WorkflowStepKind.MemoryRead)
            {
                ExecuteMemoryRead(instance, transition);
                MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Memory read applied");
                return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
            }

            if (transition.StepKind == WorkflowStepKind.ArtifactEmit)
            {
                await ExecuteArtifactEmitAsync(instance, transition, ct).ConfigureAwait(false);
                MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Artifact emitted");
                return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
            }

            if (transition is CommandTransition commandTransition)
            {
                var result = await _commandDispatcher.ExecuteAsync(commandTransition.Command, instance.Context, _commandProgress, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
                {
                    PathValueAccessor.SetValue(instance.Context, commandTransition.OutputPath, result);
                }

                if (_expressionEvaluator.EvaluateBoolean(commandTransition.SucceedExpression, instance.Context))
                {
                    MoveToTarget(instance, commandTransition, ExecutionStatus.Succeeded, "Command OK");
                    return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
                }

                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, commandTransition.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: "Command failed success condition"));
                instance.Status = WorkflowStatus.Failed;
                return EngineTickOutcome.FailedWith($"Transition '{commandTransition.Id}' did not satisfy '{commandTransition.SucceedExpression}'.");
            }

            if (transition is ToBeRefinedTransition refineTransition)
            {
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, refineTransition.Id, TaskNodeType.Transition, ExecutionStatus.Skipped, Message: refineTransition.DesignNotes));
                return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
            }

            return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
        }
        catch (Exception ex)
        {
            instance.Status = WorkflowStatus.Failed;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: ex.Message));
            return EngineTickOutcome.FailedWith(ex.Message);
        }
    }

    private EngineTickOutcome RegisterExternalBoundary(
        WorkflowInstance instance,
        StateNode state,
        TransitionGroup group,
        TransitionBase transition)
    {
        if (!instance.ActiveWaitGroups.Any(existing => string.Equals(existing.TransitionId, transition.Id, StringComparison.Ordinal)))
        {
            var pendingGroup = new PendingWaitGroup
            {
                InstanceId = instance.InstanceId,
                TransitionId = transition.Id,
                CorrelationKey = ResolveCorrelationKey(state, instance.Context),
                TargetStateId = transition.TargetNodeId,
                TimeoutTargetStateId = group.TimeoutTargetStateId,
                OriginStrategy = group.Strategy,
            };

            DateTimeOffset? expireAt = group.GroupTimeout is { } timeout ? _clock.UtcNow.Add(timeout) : null;
            pendingGroup.AddEntry(expireAt);
            instance.ActiveWaitGroups.Add(pendingGroup);
        }

        instance.Status = WorkflowStatus.WaitingExternal;
        instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, ExecutionStatus.Suspended, Message: $"Blocked on {transition.StepKind}"));
        return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
    }

    private static bool IsExternalStep(WorkflowStepKind stepKind)
    {
        return stepKind is WorkflowStepKind.ModelThink
            or WorkflowStepKind.McpCall
            or WorkflowStepKind.SubagentCall
            or WorkflowStepKind.AskUser
            or WorkflowStepKind.WaitResume;
    }

    private void PruneExpiredWaitEntries(WorkflowInstance instance)
    {
        foreach (var group in instance.ActiveWaitGroups.ToList())
        {
            var hasExpiredEntry = false;
            foreach (var entry in group.Entries.Where(entry => !entry.Completed && entry.ExpireAt is { } expireAt && expireAt <= _clock.UtcNow))
            {
                hasExpiredEntry = true;
                group.TryCompleteEntry(entry.WaitId, new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["waitExpired"] = true,
                });
                entry.Error = "expired";
                group.TimedOut = true;
                group.Completed = true;
                group.CompletedAt ??= _clock.UtcNow;
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, group.TransitionId, TaskNodeType.Transition, ExecutionStatus.Failed, Message: "WaitExpired"));
            }

            if (group.Completed && !group.CompletionLogged && !group.TimedOut)
            {
                group.CompletionLogged = true;
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, group.TransitionId, TaskNodeType.Transition, ExecutionStatus.Succeeded, group.AggregatedContext, "GroupCompleted"));
            }

            if (!group.Completed || !hasExpiredEntry)
            {
                continue;
            }

            instance.ActiveWaitGroups.Remove(group);
            foreach (var pair in group.AggregatedContext)
            {
                instance.Context[pair.Key] = pair.Value;
            }

            if (!string.IsNullOrWhiteSpace(group.TimeoutTargetStateId))
            {
                instance.CurrentNodeId = group.TimeoutTargetStateId;
                MarkStateEntrance(instance);
                instance.Status = WorkflowStatus.Running;
                group.CompletionLogged = true;
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, group.TransitionId, TaskNodeType.Transition, ExecutionStatus.Succeeded, Message: "WaitTimeoutTransitionApplied"));
                continue;
            }

            instance.Status = WorkflowStatus.Failed;
            group.CompletionLogged = true;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, group.TransitionId, TaskNodeType.Transition, ExecutionStatus.Failed, Message: "WaitTimedOutWithoutFallback"));
        }
    }

    private static IReadOnlyList<TransitionBase> ResolveTransitions(WorkflowInstance instance, TransitionGroup group)
    {
        return group.TransitionIds
            .Select(transitionId => instance.Nodes.TryGetValue(transitionId, out var node) ? node : null)
            .OfType<TransitionBase>()
            .ToList();
    }

    private void ExecuteMemoryRead(WorkflowInstance instance, TransitionBase transition)
    {
        if (transition is not CommandTransition commandTransition)
        {
            return;
        }

        var parameters = commandTransition.Command.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var selected = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters.TryGetValue("keys", out var keysValue) && keysValue is IEnumerable<object?> keys)
        {
            foreach (var key in keys.Select(Convert.ToString).Where(static key => !string.IsNullOrWhiteSpace(key)))
            {
                selected[key!] = PathValueAccessor.GetValue(instance.Context, key!);
            }
        }

        if (!string.IsNullOrWhiteSpace(transition.OutputPath))
        {
            PathValueAccessor.SetValue(instance.Context, transition.OutputPath, selected);
        }
    }

    private static void ApplyDictionaryParameters(IDictionary<string, object?> context, TransitionBase transition, string preferredKey)
    {
        if (transition is not CommandTransition commandTransition || commandTransition.Command.Parameters is null)
        {
            return;
        }

        if (commandTransition.Command.Parameters.TryGetValue(preferredKey, out var nested) && nested is IDictionary<string, object?> updates)
        {
            foreach (var pair in updates)
            {
                PathValueAccessor.SetValue(context, pair.Key, pair.Value);
            }

            return;
        }

        foreach (var pair in commandTransition.Command.Parameters)
        {
            PathValueAccessor.SetValue(context, pair.Key, pair.Value);
        }
    }

    private static async Task ExecuteArtifactEmitAsync(WorkflowInstance instance, TransitionBase transition, CancellationToken ct)
    {
        if (transition is not CommandTransition commandTransition)
        {
            return;
        }

        var parameters = commandTransition.Command.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var path = parameters.TryGetValue("path", out var pathValue) ? Convert.ToString(pathValue) : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("ArtifactEmit requires a 'path' parameter.");
        }

        var content = parameters.TryGetValue("content", out var contentValue)
            ? Convert.ToString(contentValue) ?? string.Empty
            : string.Empty;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(transition.OutputPath))
        {
            PathValueAccessor.SetValue(instance.Context, transition.OutputPath, path);
        }
    }

    private string? ResolveCorrelationKey(StateNode state, IReadOnlyDictionary<string, object?> context)
    {
        return state.CorrelationKeyPath is null
            ? null
            : Convert.ToString(PathValueAccessor.GetValue(context, state.CorrelationKeyPath));
    }

    private void MoveToTarget(WorkflowInstance instance, TransitionBase transition, ExecutionStatus executionStatus, string message)
    {
        instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, executionStatus, Message: message));
        if (!string.IsNullOrWhiteSpace(transition.TargetNodeId))
        {
            instance.CurrentNodeId = transition.TargetNodeId;
            MarkStateEntrance(instance);
        }

        if (string.Equals(instance.CurrentNodeId, instance.EndNodeId, StringComparison.Ordinal))
        {
            instance.Status = WorkflowStatus.Succeeded;
            return;
        }

        instance.Status = WorkflowStatus.Running;
    }

    private void MarkStateEntrance(WorkflowInstance instance)
    {
        if (instance.Nodes.TryGetValue(instance.CurrentNodeId, out var node) && node is StateNode stateNode)
        {
            stateNode.EntranceTime = _clock.UtcNow;
        }
    }
}
