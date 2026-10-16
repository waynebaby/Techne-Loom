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
            foreach (var waitGroup in instance.ActiveWaitGroups.ToArray())
            {
                if (!IsParallelBatch(waitGroup) || !ProcessExpiredFanoutBatch(instance, waitGroup))
                {
                    continue;
                }

                if (instance.Status == WorkflowStatus.Failed)
                {
                    return EngineTickOutcome.FailedWith("External fan-out group timed out without a valid timeout target.");
                }

                break;
            }

            if (instance.Status == WorkflowStatus.WaitingExternal)
            {
                return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
            }
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
                    .Where(transition => _expressionEvaluator.EvaluateBoolean(transition.GuardExpression.Source, instance.Context, instance.ExpressionBinding))
                    .OrderBy(static transition => transition.Priority)
                    .ToList();
                if (ready.Count == 0)
                {
                    continue;
                }

                if (ready.Count > 0 && IsFanoutStrategy(group.Strategy))
                {
                    return StartFanout(instance, state, group, ready);
                }

                if (ready.Count > 1 && group.Strategy != ConcurrencyStrategy.FirstPass)
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
        CancellationToken ct = default,
        string? executionError = null,
        string? waitId = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ct.ThrowIfCancellationRequested();
        if (executionError is not null && string.IsNullOrWhiteSpace(executionError))
        {
            throw new InvalidOperationException("execution_error must be a non-empty string.");
        }

        if (waitId is not null && string.IsNullOrWhiteSpace(waitId))
        {
            throw new InvalidOperationException("wait_id must be a non-empty string.");
        }

        var hasExecutionError = executionError is not null;
        if (hasExecutionError && payload is { Count: > 0 })
        {
            throw new InvalidOperationException("execution_error cannot be combined with a resume payload.");
        }

        if (hasExecutionError && string.IsNullOrWhiteSpace(waitId))
        {
            throw new InvalidOperationException("An execution_error resume requires a wait_id for the pending attempt.");
        }

        if (IsLateFanoutResultIgnored(instance, transitionId, waitId))
        {
            return Task.CompletedTask;
        }

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

        if (hasExecutionError && !IsParallelBatch(waitGroup))
        {
            throw new InvalidOperationException("External execution errors are supported only for fan-out wait groups.");
        }

        if (IsParallelBatch(waitGroup) && string.IsNullOrWhiteSpace(waitId))
        {
            throw new InvalidOperationException("A fan-out resume requires the wait_id for the pending attempt.");
        }

        if (IsParallelBatch(waitGroup) && ProcessExpiredFanoutBatch(instance, waitGroup))
        {
            return Task.CompletedTask;
        }

        var entry = waitId is null
            ? waitGroup.GetNextPendingEntry()
            : waitGroup.Entries.FirstOrDefault(item =>
                string.Equals(item.WaitId, waitId, StringComparison.Ordinal) && !item.Completed);
        if (entry is null)
        {
            if (waitId is not null)
            {
                throw new InvalidOperationException($"wait_id '{waitId}' does not match a pending wait entry for transition '{transitionId}'.");
            }

            throw new InvalidOperationException($"Wait group '{transitionId}' has no pending entries.");
        }

        if (hasExecutionError)
        {
            HandleFanoutExecutionError(instance, waitGroup, entry, transitionId, executionError!);
            return Task.CompletedTask;
        }

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

        if (IsParallelBatch(waitGroup))
        {
            ProcessFanoutResume(instance, waitGroup, transitionId, resultId);
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

            ApplyOutputBindings(instance.Context, commandTransition, output, payload);
        }

        if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context, instance.ExpressionBinding))
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
                if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context, instance.ExpressionBinding))
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
                if (!_expressionEvaluator.EvaluateBoolean(commandTransition.SucceedExpression.Source, instance.Context, instance.ExpressionBinding))
                {
                    return Fail(instance, $"Transition '{transition.Id}' did not satisfy its pass expression.");
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

    private EngineTickOutcome StartFanout(
        WorkflowInstance instance,
        StateNode state,
        TransitionGroup group,
        IReadOnlyList<TransitionBase> transitions)
    {
        if (transitions.Any(transition => !IsExternalStep(transition.StepKind)))
        {
            return Fail(instance, $"Transition group '{group.Id}' is a fan-out group and requires external transitions only; synchronous transitions must use separate ordered groups.");
        }

        if (group.Strategy is (ConcurrencyStrategy.WhenAnyPass or ConcurrencyStrategy.WhenAllPass or ConcurrencyStrategy.WhenExpression)
            && TryFindFanoutOutputPathConflict(transitions, out var outputConflict))
        {
            return Fail(instance, $"Transition group '{group.Id}' {outputConflict}");
        }

        var expectedTransitionIds = transitions
            .Select(static transition => transition.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        DateTimeOffset? batchExpiresAt = null;
        if (group.GroupTimeout is { } timeout)
        {
            batchExpiresAt = instance.ActiveWaitGroups
                .Where(activeGroup => string.Equals(activeGroup.ConcurrencyGroupId, group.Id, StringComparison.Ordinal))
                .SelectMany(static activeGroup => activeGroup.Entries)
                .Select(static entry => entry.ExpireAt)
                .FirstOrDefault(static expireAt => expireAt.HasValue)
                ?? _clock.UtcNow.Add(timeout);
        }

        foreach (var transition in transitions)
        {
            if (transition is CommandTransition commandTransition)
            {
                commandTransition.CurrentRetryCount = 0;
            }

            RegisterExternalBoundary(instance, state, group, transition, expectedTransitionIds, batchExpiresAt);
        }

        instance.Status = WorkflowStatus.WaitingExternal;
        return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
    }

    private EngineTickOutcome RegisterExternalBoundary(
        WorkflowInstance instance,
        StateNode state,
        TransitionGroup group,
        TransitionBase transition,
        IReadOnlyList<string>? expectedTransitionIds = null,
        DateTimeOffset? batchExpiresAt = null)
    {
        var expected = expectedTransitionIds?.Distinct(StringComparer.Ordinal).ToList() ?? [];
        var isParallelBatch = IsFanoutStrategy(group.Strategy) && expected.Count > 0;
        if (!instance.ActiveWaitGroups.Any(waitGroup => string.Equals(waitGroup.TransitionId, transition.Id, StringComparison.Ordinal)))
        {
            var waitGroup = new PendingWaitGroup
            {
                InstanceId = instance.InstanceId,
                TransitionId = transition.Id,
                ConcurrencyGroupId = isParallelBatch ? group.Id : null,
                ExpectedTransitionIds = isParallelBatch ? expected : [],
                CorrelationKey = ResolveCorrelationKey(state, instance.Context),
                TargetStateId = ResolveTargetStateId(transition),
                TimeoutTargetStateId = group.TimeoutTargetStateId ?? group.TimeoutTransition?.TargetNodeId,
                OriginStrategy = group.Strategy,
                CancelLosers = group.CancelLosers,
            };
            waitGroup.AddEntry(batchExpiresAt ?? (group.GroupTimeout is { } timeout ? _clock.UtcNow.Add(timeout) : null));
            instance.ActiveWaitGroups.Add(waitGroup);
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, ExecutionStatus.Suspended, Message: $"Blocked on {transition.StepKind}"));
        }

        instance.Status = WorkflowStatus.WaitingExternal;
        return EngineTickOutcome.SuspendedAt(instance.CurrentNodeId);
    }

    private void HandleFanoutExecutionError(
        WorkflowInstance instance,
        PendingWaitGroup waitGroup,
        PendingWaitEntry entry,
        string transitionId,
        string errorMessage)
    {
        if (!IsParallelBatch(waitGroup))
        {
            throw new InvalidOperationException("External execution errors are supported only for fan-out wait groups.");
        }

        if (!instance.Nodes.TryGetValue(transitionId, out var node) || node is not CommandTransition transition)
        {
            throw new InvalidOperationException($"Fan-out transition '{transitionId}' is not a command transition and cannot be retried.");
        }

        if (!instance.Nodes.TryGetValue(instance.CurrentNodeId, out var stateNode) || stateNode is not StateNode state)
        {
            throw new InvalidOperationException($"Fan-out source state '{instance.CurrentNodeId}' is missing.");
        }

        var transitionGroup = state.Groups.SingleOrDefault(group =>
            string.Equals(group.Id, waitGroup.ConcurrencyGroupId, StringComparison.Ordinal));
        if (transitionGroup is null)
        {
            throw new InvalidOperationException($"Fan-out transition group '{waitGroup.ConcurrencyGroupId}' is missing from state '{state.Id}'.");
        }

        var batchGroups = GetParallelBatch(instance, waitGroup);
        entry.Completed = true;
        entry.CompletedAt = _clock.UtcNow;
        entry.Error = errorMessage;
        transition.CurrentRetryCount++;

        var retryScheduled = transition.CurrentRetryCount <= transition.MaxRetries;
        var nextWaitId = retryScheduled ? waitGroup.AddEntry(entry.ExpireAt).WaitId : null;
        if (!retryScheduled)
        {
            waitGroup.Completed = true;
            waitGroup.CompletedAt = _clock.UtcNow;
            waitGroup.ErrorMessage = errorMessage;
        }

        var attemptChanges = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["executionError"] = errorMessage,
            ["waitId"] = entry.WaitId,
            ["retryCount"] = transition.CurrentRetryCount,
            ["retryScheduled"] = retryScheduled,
        };
        if (nextWaitId is not null)
        {
            attemptChanges["nextWaitId"] = nextWaitId;
        }

        instance.History.Add(new WorkflowHistoryEntry(
            _clock.UtcNow,
            transitionId,
            TaskNodeType.Transition,
            ExecutionStatus.Failed,
            attemptChanges,
            retryScheduled
                ? "External execution attempt failed; a retry was scheduled."
                : "External execution retries were exhausted."));

        if (!retryScheduled && batchGroups.All(static group => group.Completed))
        {
            RouteFanoutError(instance, transitionGroup, batchGroups, errorMessage);
        }
    }

    private void ProcessFanoutResume(
        WorkflowInstance instance,
        PendingWaitGroup resumedGroup,
        string transitionId,
        string? resultId)
    {
        var batchGroups = GetParallelBatch(instance, resumedGroup);
        if (batchGroups.Count != resumedGroup.ExpectedTransitionIds.Count)
        {
            throw new InvalidOperationException($"Parallel wait group '{resumedGroup.ConcurrencyGroupId}' is missing one or more expected transition waits.");
        }

        if (!instance.Nodes.TryGetValue(instance.CurrentNodeId, out var stateNode) || stateNode is not StateNode state)
        {
            throw new InvalidOperationException($"Fan-out source state '{instance.CurrentNodeId}' is missing.");
        }

        var transitionGroup = state.Groups.SingleOrDefault(group =>
            string.Equals(group.Id, resumedGroup.ConcurrencyGroupId, StringComparison.Ordinal));
        if (transitionGroup is null)
        {
            throw new InvalidOperationException($"Fan-out transition group '{resumedGroup.ConcurrencyGroupId}' is missing from state '{state.Id}'.");
        }

        if (!instance.Nodes.TryGetValue(transitionId, out var transitionNode) || transitionNode is not TransitionBase transition)
        {
            throw new InvalidOperationException($"Fan-out transition '{transitionId}' is missing from workflow '{instance.InstanceId}'.");
        }

        if (transition is CommandTransition { StepKind: WorkflowStepKind.Plan } && !string.IsNullOrWhiteSpace(resultId))
        {
            RecordConsumedPlanResult(instance, resultId);
        }

        var payload = resumedGroup.Entries.LastOrDefault(static entry => entry.Completed && entry.ResultContext is not null)?.ResultContext
            ?? resumedGroup.AggregatedContext;
        try
        {
            var branchContext = BuildFanoutBranchContext(instance, transition, payload, out var projectedContext, out var projectedOutputPaths);
            resumedGroup.Passed = _expressionEvaluator.EvaluateBoolean(transition.PassExpression.Source, branchContext, instance.ExpressionBinding);
            resumedGroup.ErrorMessage = null;
            resumedGroup.ProjectedContext.Clear();
            foreach (var pair in projectedContext)
            {
                resumedGroup.ProjectedContext[pair.Key] = pair.Value;
            }

            resumedGroup.ProjectedOutputPaths.Clear();
            resumedGroup.ProjectedOutputPaths.AddRange(projectedOutputPaths);
        }
        catch (Exception exception)
        {
            resumedGroup.Passed = false;
            resumedGroup.ErrorMessage = exception.Message;
        }

        var passed = resumedGroup.Passed == true;
        instance.History.Add(new WorkflowHistoryEntry(
            _clock.UtcNow,
            transition.Id,
            TaskNodeType.Transition,
            passed ? ExecutionStatus.Succeeded : ExecutionStatus.Failed,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["passed"] = passed,
                ["projectedOutputPaths"] = resumedGroup.ProjectedOutputPaths.ToArray(),
                ["executionError"] = resumedGroup.ErrorMessage,
            },
            resumedGroup.ErrorMessage ?? (passed ? "Fan-out branch passed." : "Fan-out branch did not satisfy passExpression.")));

        var completedGroups = batchGroups.Where(static group => group.Completed).ToArray();
        if (resumedGroup.ErrorMessage is { } branchError)
        {
            if (completedGroups.Length == batchGroups.Count)
            {
                RouteFanoutError(instance, transitionGroup, batchGroups, branchError);
            }

            return;
        }

        switch (transitionGroup.Strategy)
        {
            case ConcurrencyStrategy.WhenAnyResponse:
                CompleteFanoutOutcome(instance, transitionGroup, batchGroups, [resumedGroup], ResolveTargetStateId(transition), passed, "whenAnyResponse selected the first returned branch.");
                return;

            case ConcurrencyStrategy.WhenAnyPass:
                if (passed)
                {
                    CompleteFanoutOutcome(instance, transitionGroup, batchGroups, [resumedGroup], ResolveTargetStateId(transition), true, "whenAnyPass selected the first passing branch.");
                }
                else if (completedGroups.Length == batchGroups.Count)
                {
                    var noPassExecutionError = completedGroups.FirstOrDefault(static group => group.ErrorMessage is not null)?.ErrorMessage;
                    if (noPassExecutionError is not null)
                    {
                        RouteFanoutError(instance, transitionGroup, batchGroups, noPassExecutionError);
                    }
                    else
                    {
                        CompleteFanoutOutcome(instance, transitionGroup, batchGroups, completedGroups, transitionGroup.NoPassTargetStateId, false, "All fan-out branches failed passExpression.");
                    }
                }

                return;

            case ConcurrencyStrategy.WhenAllPass:
                if (completedGroups.Length == batchGroups.Count)
                {
                    var joinExecutionError = completedGroups.FirstOrDefault(static group => group.ErrorMessage is not null)?.ErrorMessage;
                    if (joinExecutionError is not null)
                    {
                        RouteFanoutError(instance, transitionGroup, batchGroups, joinExecutionError);
                    }
                    else if (completedGroups.Any(static group => group.Passed != true))
                    {
                        CompleteFanoutOutcome(instance, transitionGroup, batchGroups, completedGroups, transitionGroup.NoPassTargetStateId, false, "A fan-out branch failed; whenAllPass cannot join.");
                    }
                    else
                    {
                        CompleteFanoutOutcome(instance, transitionGroup, batchGroups, completedGroups, transitionGroup.JoinTargetStateId, true, "All fan-out branches passed and joined.");
                    }
                }

                return;

            case ConcurrencyStrategy.WhenExpression:
                if (transitionGroup.PassExpression is null)
                {
                    RouteFanoutError(instance, transitionGroup, batchGroups, "whenExpression has no group passExpression.");
                    return;
                }

                var expressionExecutionError = batchGroups.FirstOrDefault(static group => group.ErrorMessage is not null)?.ErrorMessage;
                if (expressionExecutionError is not null)
                {
                    if (completedGroups.Length == batchGroups.Count)
                    {
                        RouteFanoutError(instance, transitionGroup, batchGroups, expressionExecutionError);
                    }

                    return;
                }

                if (!TryMergeProjectedOutputs(completedGroups, out var expressionOutputs, out var outputConflict))
                {
                    RouteFanoutError(instance, transitionGroup, batchGroups, outputConflict!);
                    return;
                }

                var expressionContext = CloneContext(instance.Context);
                ApplyProjectedOutputs(expressionContext, expressionOutputs);
                bool expressionPassed;
                try
                {
                    expressionPassed = _expressionEvaluator.EvaluateBoolean(transitionGroup.PassExpression.Source, expressionContext, instance.ExpressionBinding);
                }
                catch (Exception exception)
                {
                    if (completedGroups.Length == batchGroups.Count)
                    {
                        RouteFanoutError(instance, transitionGroup, batchGroups, exception.Message);
                    }

                    return;
                }

                if (expressionPassed)
                {
                    CompleteFanoutOutcome(instance, transitionGroup, batchGroups, completedGroups, transitionGroup.JoinTargetStateId, true, "whenExpression became true for the completed branch outputs.");
                }
                else if (completedGroups.Length == batchGroups.Count)
                {
                    CompleteFanoutOutcome(instance, transitionGroup, batchGroups, completedGroups, transitionGroup.NoPassTargetStateId, false, "All fan-out branches completed without satisfying passExpression.");
                }

                return;

            default:
                RouteFanoutError(instance, transitionGroup, batchGroups, $"Unsupported fan-out strategy '{transitionGroup.Strategy}'.");
                return;
        }
    }

    private bool ProcessExpiredFanoutBatch(WorkflowInstance instance, PendingWaitGroup representative)
    {
        var batchGroups = GetParallelBatch(instance, representative);
        if (batchGroups.Count != representative.ExpectedTransitionIds.Count)
        {
            throw new InvalidOperationException($"Parallel wait group '{representative.ConcurrencyGroupId}' is missing one or more expected transition waits.");
        }

        var expiredTransitionIds = batchGroups
            .Where(group => group.Entries.Any(entry => !entry.Completed && entry.ExpireAt is { } expireAt && expireAt <= _clock.UtcNow))
            .Select(static group => group.TransitionId)
            .ToHashSet(StringComparer.Ordinal);
        if (expiredTransitionIds.Count == 0)
        {
            return false;
        }

        if (!instance.Nodes.TryGetValue(instance.CurrentNodeId, out var stateNode) || stateNode is not StateNode state)
        {
            throw new InvalidOperationException($"Fan-out source state '{instance.CurrentNodeId}' is missing.");
        }

        var transitionGroup = state.Groups.SingleOrDefault(group =>
            string.Equals(group.Id, representative.ConcurrencyGroupId, StringComparison.Ordinal));
        if (transitionGroup is null)
        {
            throw new InvalidOperationException($"Fan-out transition group '{representative.ConcurrencyGroupId}' is missing from state '{state.Id}'.");
        }

        RequestFanoutCancellation(instance, transitionGroup.Id, transitionGroup.CancelLosers, batchGroups, "fanout_timeout");
        foreach (var waitGroup in batchGroups)
        {
            var timedOut = expiredTransitionIds.Contains(waitGroup.TransitionId);
            foreach (var pendingEntry in waitGroup.Entries.Where(static entry => !entry.Completed))
            {
                pendingEntry.Completed = true;
                pendingEntry.CompletedAt = _clock.UtcNow;
                pendingEntry.Error = timedOut ? "expired" : "fanout_timeout_cancelled";
            }

            waitGroup.TimedOut = timedOut;
            waitGroup.Completed = true;
            waitGroup.CompletedAt ??= _clock.UtcNow;
            instance.ActiveWaitGroups.Remove(waitGroup);
            if (timedOut)
            {
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, waitGroup.TransitionId, TaskNodeType.Transition, ExecutionStatus.Failed, Message: "WaitExpired"));
            }
        }

        var timeoutTarget = transitionGroup.TimeoutTargetStateId ?? transitionGroup.TimeoutTransition?.TargetNodeId;
        if (!string.IsNullOrWhiteSpace(timeoutTarget)
            && instance.Nodes.TryGetValue(timeoutTarget, out var timeoutNode)
            && timeoutNode is StateNode)
        {
            instance.CurrentNodeId = timeoutTarget;
            instance.Status = WorkflowStatus.Running;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transitionGroup.Id, TaskNodeType.Transition, ExecutionStatus.Succeeded, Message: "WaitTimeoutTransitionApplied"));
        }
        else
        {
            instance.Status = WorkflowStatus.Failed;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transitionGroup.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: "WaitTimedOutWithoutFallback"));
        }

        return true;
    }

    private void CompleteFanoutOutcome(
        WorkflowInstance instance,
        TransitionGroup transitionGroup,
        IReadOnlyList<PendingWaitGroup> batchGroups,
        IReadOnlyList<PendingWaitGroup> outputGroups,
        string? targetStateId,
        bool passed,
        string message)
    {
        if (!TryMergeProjectedOutputs(outputGroups, out var projectedOutputs, out var outputConflict))
        {
            RouteFanoutError(instance, transitionGroup, batchGroups, outputConflict!);
            return;
        }

        ApplyProjectedOutputs(instance.Context, projectedOutputs);
        RequestFanoutCancellation(instance, transitionGroup.Id, transitionGroup.CancelLosers, batchGroups, "fanout_completed");
        foreach (var waitGroup in batchGroups)
        {
            instance.ActiveWaitGroups.Remove(waitGroup);
        }

        instance.History.Add(new WorkflowHistoryEntry(
            _clock.UtcNow,
            transitionGroup.Id,
            TaskNodeType.Transition,
            passed ? ExecutionStatus.Succeeded : ExecutionStatus.Failed,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["strategy"] = transitionGroup.Strategy.ToString(),
                ["resolvedTransitions"] = outputGroups.Select(static group => group.TransitionId).ToArray(),
                ["passed"] = passed,
            },
            message));

        if (string.IsNullOrWhiteSpace(targetStateId)
            && passed
            && transitionGroup.Strategy == ConcurrencyStrategy.WhenAllPass)
        {
            var matchingTargets = outputGroups
                .Select(static group => group.TargetStateId)
                .Where(static target => !string.IsNullOrWhiteSpace(target))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (matchingTargets.Length == 1)
            {
                targetStateId = matchingTargets[0];
            }
        }

        if (string.IsNullOrWhiteSpace(targetStateId)
            || !instance.Nodes.TryGetValue(targetStateId, out var targetNode)
            || targetNode is not StateNode)
        {
            instance.Status = WorkflowStatus.Failed;
            instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transitionGroup.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: $"Fan-out group '{transitionGroup.Id}' resolved without a valid target state."));
            return;
        }

        instance.CurrentNodeId = targetStateId;
        instance.Status = WorkflowStatus.Running;
    }

    private void RouteFanoutError(
        WorkflowInstance instance,
        TransitionGroup transitionGroup,
        IReadOnlyList<PendingWaitGroup> batchGroups,
        string errorMessage)
    {
        RequestFanoutCancellation(instance, transitionGroup.Id, transitionGroup.CancelLosers, batchGroups, "fanout_error");
        foreach (var waitGroup in batchGroups)
        {
            instance.ActiveWaitGroups.Remove(waitGroup);
        }

        instance.History.Add(new WorkflowHistoryEntry(
            _clock.UtcNow,
            transitionGroup.Id,
            TaskNodeType.Transition,
            ExecutionStatus.Failed,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["strategy"] = transitionGroup.Strategy.ToString(),
                ["executionError"] = errorMessage,
            },
            $"Fan-out group failed: {errorMessage}"));

        if (!string.IsNullOrWhiteSpace(transitionGroup.ErrorHandlingTargetStateId)
            && instance.Nodes.TryGetValue(transitionGroup.ErrorHandlingTargetStateId, out var targetNode)
            && targetNode is StateNode)
        {
            instance.CurrentNodeId = transitionGroup.ErrorHandlingTargetStateId;
            instance.Status = WorkflowStatus.Running;
            return;
        }

        instance.Status = WorkflowStatus.Failed;
    }

    private void RequestFanoutCancellation(
        WorkflowInstance instance,
        string concurrencyGroupId,
        bool cancelLosers,
        IReadOnlyList<PendingWaitGroup> batchGroups,
        string reason)
    {
        var pendingEntries = batchGroups
            .Where(static waitGroup => !waitGroup.Completed)
            .SelectMany(waitGroup => waitGroup.Entries
                .Where(static entry => !entry.Completed)
                .Select(entry => (WaitGroup: waitGroup, Entry: entry)))
            .ToArray();

        foreach (var (waitGroup, entry) in pendingEntries)
        {
            var signalId = $"{instance.InstanceId}:{entry.WaitId}";
            if (instance.FanoutCancellationSignals.Any(signal => string.Equals(signal.SignalId, signalId, StringComparison.Ordinal)))
            {
                continue;
            }

            var requestedAtUtc = _clock.UtcNow;
            var signal = new FanoutCancellationSignal(
                signalId,
                instance.InstanceId,
                concurrencyGroupId,
                waitGroup.TransitionId,
                entry.WaitId,
                cancelLosers,
                true,
                reason,
                requestedAtUtc);
            instance.FanoutCancellationSignals.Add(signal);
            instance.History.Add(new WorkflowHistoryEntry(
                requestedAtUtc,
                waitGroup.TransitionId,
                TaskNodeType.Transition,
                ExecutionStatus.Skipped,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["concurrencyGroupId"] = concurrencyGroupId,
                    ["waitId"] = entry.WaitId,
                    ["signalId"] = signalId,
                    ["cancellationRequested"] = cancelLosers,
                    ["lateResultsIgnored"] = true,
                    ["reason"] = reason,
                },
                cancelLosers
                    ? $"Cancellation requested for transition '{waitGroup.TransitionId}' (wait '{entry.WaitId}') after {reason}; late results will be ignored."
                    : $"Transition '{waitGroup.TransitionId}' wait '{entry.WaitId}' was logically cancelled after {reason}; late results will be ignored.",
                signal));
        }
    }

    private Dictionary<string, object?> BuildFanoutBranchContext(
        WorkflowInstance instance,
        TransitionBase transition,
        IReadOnlyDictionary<string, object?> payload,
        out Dictionary<string, object?> projectedContext,
        out List<string> projectedOutputPaths)
    {
        var branchContext = CloneContext(instance.Context);
        projectedContext = new Dictionary<string, object?>(StringComparer.Ordinal);
        projectedOutputPaths = [];
        if (transition is not CommandTransition commandTransition)
        {
            return branchContext;
        }

        var output = ResolveResumeOutputValue(commandTransition, payload);
        if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
        {
            PathValueAccessor.SetValue(branchContext, commandTransition.OutputPath, output);
            projectedOutputPaths.Add(commandTransition.OutputPath);
        }

        ApplyOutputBindings(branchContext, commandTransition, output, payload);
        projectedOutputPaths.AddRange(GetOutputBindingPaths(commandTransition));
        projectedOutputPaths = projectedOutputPaths.Distinct(StringComparer.Ordinal).ToList();
        for (var leftIndex = 0; leftIndex < projectedOutputPaths.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < projectedOutputPaths.Count; rightIndex++)
            {
                if (AreOutputPathsOverlapping(projectedOutputPaths[leftIndex], projectedOutputPaths[rightIndex]))
                {
                    throw new InvalidOperationException($"Transition '{transition.Id}' declares overlapping output projections '{projectedOutputPaths[leftIndex]}' and '{projectedOutputPaths[rightIndex]}'.");
                }
            }
        }

        foreach (var outputPath in projectedOutputPaths)
        {
            if (PathValueAccessor.TryGetValue(branchContext, outputPath, out var value))
            {
                PathValueAccessor.SetValue(projectedContext, outputPath, DeepValueCloner.Clone(value));
            }
        }

        return branchContext;
    }

    public static bool TryFindFanoutOutputPathConflict(IReadOnlyList<TransitionBase> transitions, out string? conflict)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        var outputOwners = new List<(string Path, string TransitionId)>();
        foreach (var transition in transitions)
        {
            var outputPaths = new List<string>();
            if (!string.IsNullOrWhiteSpace(transition.OutputPath))
            {
                outputPaths.Add(transition.OutputPath);
            }

            if (transition is CommandTransition commandTransition)
            {
                outputPaths.AddRange(GetOutputBindingPaths(commandTransition));
            }

            foreach (var outputPath in outputPaths.Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal))
            {
                foreach (var existing in outputOwners)
                {
                    if (!string.Equals(existing.TransitionId, transition.Id, StringComparison.Ordinal)
                        && AreOutputPathsOverlapping(existing.Path, outputPath))
                    {
                        conflict = $"Transitions '{existing.TransitionId}' and '{transition.Id}' declare overlapping output projections '{existing.Path}' and '{outputPath}'.";
                        return true;
                    }
                }

                outputOwners.Add((outputPath, transition.Id));
            }
        }

        conflict = null;
        return false;
    }

    private static IReadOnlyList<string> GetOutputBindingPaths(CommandTransition transition)
    {
        return transition.Command.Parameters?.TryGetValue("outputBindings", out var bindingsValue) == true
            && bindingsValue is IEnumerable<KeyValuePair<string, object?>> bindings
                ? bindings.Select(static binding => binding.Key).ToArray()
                : [];
    }

    private static bool TryMergeProjectedOutputs(
        IReadOnlyList<PendingWaitGroup> groups,
        out Dictionary<string, object?> projectedOutputs,
        out string? conflict)
    {
        projectedOutputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        conflict = null;
        var owners = new List<(string Path, string TransitionId)>();
        foreach (var waitGroup in groups)
        {
            foreach (var outputPath in waitGroup.ProjectedOutputPaths)
            {
                var existingOwner = owners.FirstOrDefault(owner => AreOutputPathsOverlapping(owner.Path, outputPath));
                if (!string.IsNullOrWhiteSpace(existingOwner.Path))
                {
                    conflict = $"Fan-out transitions '{existingOwner.TransitionId}' and '{waitGroup.TransitionId}' have overlapping output projections '{existingOwner.Path}' and '{outputPath}'.";
                    return false;
                }

                if (PathValueAccessor.TryGetValue(waitGroup.ProjectedContext, outputPath, out var value))
                {
                    projectedOutputs[outputPath] = DeepValueCloner.Clone(value);
                    owners.Add((outputPath, waitGroup.TransitionId));
                }
            }
        }

        return true;
    }

    private static bool AreOutputPathsOverlapping(string left, string right)
    {
        var normalizedLeft = left.Trim('.');
        var normalizedRight = right.Trim('.');
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal)
            || normalizedLeft.StartsWith(normalizedRight + ".", StringComparison.Ordinal)
            || normalizedRight.StartsWith(normalizedLeft + ".", StringComparison.Ordinal);
    }

    private static void ApplyProjectedOutputs(IDictionary<string, object?> context, IReadOnlyDictionary<string, object?> projectedOutputs)
    {
        foreach (var pair in projectedOutputs)
        {
            PathValueAccessor.SetValue(context, pair.Key, DeepValueCloner.Clone(pair.Value));
        }
    }

    private static Dictionary<string, object?> CloneContext(IReadOnlyDictionary<string, object?> source)
        => source.ToDictionary(static pair => pair.Key, static pair => DeepValueCloner.Clone(pair.Value), StringComparer.Ordinal);

    private static object? ResolveResumeOutputValue(CommandTransition transition, IReadOnlyDictionary<string, object?> payload)
    {
        if (transition.Command.Parameters?.TryGetValue("resumeOutputKey", out var outputKeyValue) != true)
        {
            return payload;
        }

        var outputKey = Convert.ToString(outputKeyValue);
        return string.IsNullOrWhiteSpace(outputKey) ? payload : PathValueAccessor.GetValue(payload, outputKey);
    }

    private static string? ResolveTargetStateId(TransitionBase transition)
        => transition is CommandTransition { Plan: { WeaveBackTargetNodeId: not null } plan }
            ? plan.WeaveBackTargetNodeId
            : transition.TargetNodeId;

    private static bool IsParallelBatch(PendingWaitGroup waitGroup)
        => IsFanoutStrategy(waitGroup.OriginStrategy)
            && !string.IsNullOrWhiteSpace(waitGroup.ConcurrencyGroupId)
            && waitGroup.ExpectedTransitionIds.Count > 0;

    private static bool IsFanoutStrategy(ConcurrencyStrategy strategy)
        => strategy is ConcurrencyStrategy.WhenAnyResponse
            or ConcurrencyStrategy.WhenAnyPass
            or ConcurrencyStrategy.WhenAllPass
            or ConcurrencyStrategy.WhenExpression;

    private static IReadOnlyList<PendingWaitGroup> GetParallelBatch(WorkflowInstance instance, PendingWaitGroup waitGroup)
    {
        return waitGroup.ExpectedTransitionIds
            .Select(transitionId => instance.ActiveWaitGroups.FirstOrDefault(candidate =>
                string.Equals(candidate.ConcurrencyGroupId, waitGroup.ConcurrencyGroupId, StringComparison.Ordinal)
                && string.Equals(candidate.TransitionId, transitionId, StringComparison.Ordinal)))
            .OfType<PendingWaitGroup>()
            .ToArray();
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

    public static bool IsLateFanoutResultIgnored(WorkflowInstance instance, string transitionId, string? waitId)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return !string.IsNullOrWhiteSpace(waitId)
            && instance.FanoutCancellationSignals.Any(signal =>
                signal.LateResultsIgnored
                && string.Equals(signal.TransitionId, transitionId, StringComparison.Ordinal)
                && string.Equals(signal.WaitId, waitId, StringComparison.Ordinal));
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

    private static void ApplyOutputBindings(
        Dictionary<string, object?> context,
        CommandTransition transition,
        object? result,
        IReadOnlyDictionary<string, object?>? resumePayload = null)
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
                string text when text.StartsWith("$payload:", StringComparison.Ordinal) => GetResumePayloadProjectionValue(resumePayload, text[9..]),
                _ => binding.Value,
            };
            PathValueAccessor.SetValue(context, binding.Key, value);
        }
    }

    private static object? GetResumePayloadProjectionValue(IReadOnlyDictionary<string, object?>? resumePayload, string path)
    {
        if (resumePayload is null
            || string.IsNullOrWhiteSpace(path)
            || !PathValueAccessor.TryGetValue(resumePayload, path, out var value))
        {
            throw new InvalidOperationException($"Resume projection source '{path}' is missing from the submitted result.");
        }

        return value;
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