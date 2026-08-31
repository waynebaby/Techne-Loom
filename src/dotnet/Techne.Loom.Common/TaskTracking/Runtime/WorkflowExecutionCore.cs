using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class WorkflowExecutionCore
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private const string ConsumedPlanResultIdsKey = "plan.consumed_result_ids";
    private readonly ISystemClock _clock;

    public WorkflowExecutionCore(
        ICommandDispatcher? commandDispatcher = null,
        IExpressionEvaluator? expressionEvaluator = null,
        ISystemClock? clock = null)
    {
        _commandDispatcher = commandDispatcher ?? new LocalCommandDispatcher();
        _expressionEvaluator = expressionEvaluator ?? new CSharpExpressionEvaluator();
        _clock = clock ?? new SystemClock();
    }

    public async Task<EngineTickOutcome> RunUntilBoundaryAsync(
        WorkflowInstance instance,
        int maxTicks = 64,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (maxTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicks));
        }

        ct.ThrowIfCancellationRequested();
        if (instance.Status == WorkflowStatus.Drafting)
        {
            return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
        }

        if (instance.Status == WorkflowStatus.ReadyToStart)
        {
            instance.Status = WorkflowStatus.Running;
            instance.CurrentNodeId = instance.StartNodeId;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, instance.CurrentNodeId, TaskNodeType.State, ExecutionStatus.Started, Message: "Start"));
        }

        if (instance.Status == WorkflowStatus.WaitingExternal)
        {
            return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
        }

        if (instance.Status is WorkflowStatus.Failed or WorkflowStatus.Succeeded)
        {
            return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
        }

        for (var tick = 0; tick < maxTicks; tick++)
        {
            ct.ThrowIfCancellationRequested();
            if (instance.Status == WorkflowStatus.WaitingExternal)
            {
                return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
            }

            if (!instance.Nodes.TryGetValue(instance.CurrentNodeId, out var node) || node is not StateNode state)
            {
                return Fail(instance, $"Current node '{instance.CurrentNodeId}' is missing or not a state node.");
            }

            if (string.Equals(instance.CurrentNodeId, instance.EndNodeId, StringComparison.Ordinal) && state.Groups.Count == 0)
            {
                instance.Status = WorkflowStatus.Succeeded;
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, state.Id, TaskNodeType.State, ExecutionStatus.Succeeded, Message: "Reached terminal state"));
                return EngineTickOutcome.ProgressedTo(state.Id);
            }

            var moved = false;
            foreach (var group in state.Groups)
            {
                var ready = group.TransitionIds
                    .Select(id => instance.Nodes.TryGetValue(id, out var candidate) ? candidate as TransitionBase : null)
                    .Where(static transition => transition is not null)
                    .Cast<TransitionBase>()
                    .Where(transition => _expressionEvaluator.EvaluateBoolean(transition.GuardExpression.Source, instance.Context))
                    .OrderBy(static transition => transition.Priority)
                    .ToList();
                if (ready.Count == 0)
                {
                    continue;
                }

                if (ready.Count > 1 && group.Strategy != ConcurrencyStrategy.FirstSuccess)
                {
                    return Fail(instance, $"Transition group '{group.Id}' has multiple ready transitions but strategy '{group.Strategy}' is not supported by the shared execution core.");
                }

                foreach (var transition in ready)
                {
                    var outcome = await ExecuteTransitionAsync(instance, state, group, transition, ct).ConfigureAwait(false);
                    if (outcome.Suspended || outcome.Failed)
                    {
                        return outcome;
                    }

                    if (outcome.Moved)
                    {
                        moved = true;
                        break;
                    }
                }

                if (moved)
                {
                    break;
                }
            }

            if (moved)
            {
                continue;
            }

            return Fail(instance, $"Workflow made no progress from state '{instance.CurrentNodeId}'.");
        }

        return EngineTickOutcome.NoProgress(instance.CurrentNodeId, TimeSpan.FromMilliseconds(250));
    }

    public Task ResumeAsync(
        WorkflowInstance instance,
        string transitionId,
        string? correlationKey,
        Dictionary<string, object?>? payload,
        string? resultId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ct.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(resultId)
            && instance.Nodes.TryGetValue(transitionId, out var consumedNode)
            && consumedNode is CommandTransition { StepKind: WorkflowStepKind.Plan }
            && IsConsumedPlanResult(instance, resultId))
        {
            return Task.CompletedTask;
        }

        if (instance.Status != WorkflowStatus.WaitingExternal)
        {
            throw new InvalidOperationException($"Workflow instance '{instance.InstanceId}' is '{instance.Status}' and cannot be resumed without an active external wait.");
        }

        var waitGroup = instance.ActiveWaitGroups.SingleOrDefault(group =>
            string.Equals(group.InstanceId, instance.InstanceId, StringComparison.Ordinal)
            && string.Equals(group.TransitionId, transitionId, StringComparison.Ordinal)
            && string.Equals(group.CorrelationKey, correlationKey, StringComparison.Ordinal));
        if (waitGroup is null)
        {
            throw new InvalidOperationException($"Active wait group '{transitionId}' was not found.");
        }

        var entry = waitGroup.GetNextPendingEntry() ?? throw new InvalidOperationException($"Wait group '{transitionId}' has no pending entries.");
        payload ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (instance.Nodes.TryGetValue(transitionId, out var pendingNode)
            && pendingNode is CommandTransition { StepKind: WorkflowStepKind.Plan }
            && string.IsNullOrWhiteSpace(resultId))
        {
            throw new InvalidOperationException($"Plan transition '{transitionId}' requires a non-empty result_id.");
        }
        if (!waitGroup.TryCompleteEntry(entry.WaitId, payload))
        {
            throw new InvalidOperationException($"Wait group '{transitionId}' could not accept result entry '{entry.WaitId}'.");
        }

        if (!waitGroup.Completed)
        {
            return Task.CompletedTask;
        }

        foreach (var pair in waitGroup.AggregatedContext)
        {
            instance.Context[pair.Key] = pair.Value;
        }

        if (!instance.Nodes.TryGetValue(transitionId, out var node) || node is not TransitionBase transition)
        {
            throw new InvalidOperationException($"External transition '{transitionId}' is missing from workflow '{instance.InstanceId}'.");
        }

        object? output = payload;
        if (transition is CommandTransition commandTransition)
        {
            if (commandTransition.Command.Parameters?.TryGetValue("resumeOutputKey", out var outputKeyValue) == true)
            {
                var outputKey = Convert.ToString(outputKeyValue);
                if (!string.IsNullOrWhiteSpace(outputKey))
                {
                    output = PathValueAccessor.GetValue(payload, outputKey);
                }
            }

            if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
            {
                PathValueAccessor.SetValue(instance.Context, commandTransition.OutputPath, output);
            }

            ApplyOutputBindings(instance.Context, commandTransition, output);
        }

        if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context))
        {
            instance.ActiveWaitGroups.Remove(waitGroup);
            FailTask(instance, transition, "External result did not satisfy the transition success expression.");
            return Task.CompletedTask;
        }

        instance.ActiveWaitGroups.Remove(waitGroup);
        if (transition.StepKind == WorkflowStepKind.Plan && !string.IsNullOrWhiteSpace(resultId))
        {
            RecordConsumedPlanResult(instance, resultId);
        }

        var targetNodeId = transition is CommandTransition { Plan: { WeaveBackTargetNodeId: not null } plan }
            ? plan.WeaveBackTargetNodeId
            : transition.TargetNodeId;
        if (!string.IsNullOrWhiteSpace(targetNodeId))
        {
            instance.CurrentNodeId = targetNodeId;
        }

        instance.Status = WorkflowStatus.Running;
        instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transitionId, TaskNodeType.Transition, ExecutionStatus.Succeeded, payload, "External result applied"));
        return Task.CompletedTask;
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
                if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context))
                {
                    return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
                }

                return MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Expression transition succeeded");
            }

            if (transition is CommandTransition commandTransition)
            {
                object? result;
                if (transition.StepKind is WorkflowStepKind.StateUpdate or WorkflowStepKind.MemoryWrite)
                {
                    result = ApplyUpdates(instance.Context, commandTransition.Command.Parameters);
                }
                else
                {
                    result = await _commandDispatcher.ExecuteAsync(commandTransition.Command, instance.Context, null, ct).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
                {
                    PathValueAccessor.SetValue(instance.Context, commandTransition.OutputPath, result);
                }

                ApplyOutputBindings(instance.Context, commandTransition, result);
                if (!_expressionEvaluator.EvaluateBoolean(commandTransition.SucceedExpression.Source, instance.Context))
                {
                    return Fail(instance, $"Transition '{transition.Id}' did not satisfy its succeed expression.");
                }

                return MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Command transition succeeded", result);
            }

            return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
        }
        catch (Exception ex)
        {
            return Fail(instance, ex.Message, transition.Id);
        }
    }

    private EngineTickOutcome RegisterExternalBoundary(
        WorkflowInstance instance,
        StateNode state,
        TransitionGroup group,
        TransitionBase transition)
    {
        if (!instance.ActiveWaitGroups.Any(groupState => string.Equals(groupState.TransitionId, transition.Id, StringComparison.Ordinal)))
        {
            var waitGroup = new PendingWaitGroup
            {
                InstanceId = instance.InstanceId,
                TransitionId = transition.Id,
                CorrelationKey = ResolveCorrelationKey(state, instance.Context),
                TargetStateId = transition.TargetNodeId,
                TimeoutTargetStateId = group.TimeoutTargetStateId,
                OriginStrategy = group.Strategy,
            };
            waitGroup.AddEntry(group.GroupTimeout is { } timeout ? _clock.UtcNow.Add(timeout) : null);
            instance.ActiveWaitGroups.Add(waitGroup);
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, ExecutionStatus.Suspended, Message: $"Blocked on {transition.StepKind}"));
        }

        instance.Status = WorkflowStatus.WaitingExternal;
        return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
    }

    private EngineTickOutcome MoveToTarget(
        WorkflowInstance instance,
        TransitionBase transition,
        ExecutionStatus status,
        string message,
        object? result = null)
    {
        if (string.IsNullOrWhiteSpace(transition.TargetNodeId) || !instance.Nodes.ContainsKey(transition.TargetNodeId))
        {
            return Fail(instance, $"Transition '{transition.Id}' does not target an existing workflow state.", transition.Id);
        }

        instance.CurrentNodeId = transition.TargetNodeId;
        instance.History.Add(new WorkflowHistoryEntry(
            _clock.UtcNow,
            transition.Id,
            TaskNodeType.Transition,
            status,
            result is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = result },
            message));
        return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
    }

    private static EngineTickOutcome FailTask(WorkflowInstance instance, TransitionBase transition, string message)
        => Fail(instance, message, transition.Id);

    private static EngineTickOutcome Fail(WorkflowInstance instance, string message, string? nodeId = null)
    {
        instance.ActiveWaitGroups.Clear();
        instance.Status = WorkflowStatus.Failed;
        instance.History.Add(new WorkflowHistoryEntry(DateTimeOffset.UtcNow, nodeId ?? instance.CurrentNodeId, TaskNodeType.Transition, ExecutionStatus.Failed, Message: message));
        return EngineTickOutcome.FailedWith(message);
    }

    public static bool IsPlanResultConsumed(WorkflowInstance instance, string resultId)
        => IsConsumedPlanResult(instance, resultId);

    public static void RecordPlanResult(WorkflowInstance instance, string resultId)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (string.IsNullOrWhiteSpace(resultId))
        {
            throw new ArgumentException("A non-empty Plan result id is required.", nameof(resultId));
        }

        RecordConsumedPlanResult(instance, resultId);
    }

    private static bool IsConsumedPlanResult(WorkflowInstance instance, string resultId)
        => instance.Context.TryGetValue(ConsumedPlanResultIdsKey, out var value)
            && value is IEnumerable<object?> objects
            && objects.Select(Convert.ToString).Any(value => string.Equals(value, resultId, StringComparison.Ordinal));

    private static void RecordConsumedPlanResult(WorkflowInstance instance, string resultId)
    {
        var values = instance.Context.TryGetValue(ConsumedPlanResultIdsKey, out var existing)
            && existing is IEnumerable<object?> objects
                ? objects.Select(Convert.ToString).Where(static value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToList()
                : [];
        if (!values.Contains(resultId, StringComparer.Ordinal))
        {
            values.Add(resultId);
        }

        instance.Context[ConsumedPlanResultIdsKey] = values;
    }

    private static Dictionary<string, object?> ApplyUpdates(
        IDictionary<string, object?> context,
        Dictionary<string, object?>? parameters)
    {
        var updates = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters?.TryGetValue("updates", out var updatesValue) != true || updatesValue is not IDictionary<string, object?> updateMap)
        {
            return updates;
        }

        foreach (var pair in updateMap)
        {
            PathValueAccessor.SetValue(context, pair.Key, pair.Value);
            updates[pair.Key] = pair.Value;
        }

        return updates;
    }

    private static void ApplyOutputBindings(Dictionary<string, object?> context, CommandTransition transition, object? result)
    {
        if (transition.Command.Parameters?.TryGetValue("outputBindings", out var bindingsValue) != true || bindingsValue is not IDictionary<string, object?> bindings)
        {
            return;
        }

        foreach (var binding in bindings)
        {
            object? value = binding.Value switch
            {
                string text when string.Equals(text, "$result", StringComparison.Ordinal) => result,
                string text when text.StartsWith("$context:", StringComparison.Ordinal) => PathValueAccessor.GetValue(context, text[9..]),
                _ => binding.Value,
            };
            PathValueAccessor.SetValue(context, binding.Key, value);
        }
    }

    private static bool IsExternalStep(WorkflowStepKind stepKind)
        => stepKind is WorkflowStepKind.ModelThink
            or WorkflowStepKind.Plan
            or WorkflowStepKind.McpCall
            or WorkflowStepKind.SubagentCall
            or WorkflowStepKind.AskUser
            or WorkflowStepKind.WaitResume;

    private static string? ResolveCorrelationKey(StateNode state, IReadOnlyDictionary<string, object?> context)
        => string.IsNullOrWhiteSpace(state.CorrelationKeyPath) ? null : Convert.ToString(PathValueAccessor.GetValue(context, state.CorrelationKeyPath));
}