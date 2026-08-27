using System.Security.Cryptography;
using System.Text.Json;
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
        _expressionEvaluator = expressionEvaluator ?? new CSharpExpressionEvaluator();
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
                .Where(transition => _expressionEvaluator.EvaluateBoolean(transition.GuardExpression.Source, instance.Context))
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

        if (instance.Status == WorkflowStatus.Succeeded)
        {
            throw new InvalidOperationException(
                $"Workflow instance '{instance.InstanceId}' is terminally {instance.Status} and cannot be resumed. " +
                "Create a fresh runtime workflow copy from the source template; do not resume this persisted state.");
        }

        if (instance.Status == WorkflowStatus.Failed)
        {
            RestoreFailedInstanceForResume(instance, transitionId);
            return Task.CompletedTask;
        }

        if (instance.Status != WorkflowStatus.WaitingExternal)
        {
            throw new InvalidOperationException(
                $"Workflow instance '{instance.InstanceId}' is '{instance.Status}' and cannot be resumed. " +
                "Resume requires a persisted WaitingExternal state with an active wait group.");
        }

        var matchingWaitGroups = instance.ActiveWaitGroups
            .Where(group => string.Equals(group.InstanceId, instance.InstanceId, StringComparison.Ordinal)
                && string.Equals(group.TransitionId, transitionId, StringComparison.Ordinal)
                && string.Equals(group.CorrelationKey, correlationKey, StringComparison.Ordinal))
            .ToArray();
        if (matchingWaitGroups.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple active wait groups for transition '{transitionId}' and correlation '{correlationKey ?? "<null>"}' were found; resume cannot disambiguate the persisted state.");
        }

        var waitGroup = matchingWaitGroups.SingleOrDefault();

        if (waitGroup is null)
        {
            throw new InvalidOperationException($"Active wait group '{transitionId}' was not found.");
        }

        var entry = waitGroup.GetNextPendingEntry() ?? waitGroup.Entries.FirstOrDefault();
        if (entry is null)
        {
            throw new InvalidOperationException($"Wait group '{transitionId}' has no entries to complete.");
        }

        ValidateResumePayload(waitGroup, instance, payload);

        var instanceBeforeResume = WorkflowInstanceCloner.Clone(instance);
        var waitGroupIndex = instance.ActiveWaitGroups.IndexOf(waitGroup);
        var waitGroupBeforeResume = waitGroupIndex >= 0
            ? instanceBeforeResume.ActiveWaitGroups[waitGroupIndex]
            : null;

        waitGroup.TryCompleteEntry(entry.WaitId, payload);

        if (!waitGroup.Completed)
        {
            instance.Status = WorkflowStatus.WaitingExternal;
            return Task.CompletedTask;
        }

        var contextBeforeResume = instanceBeforeResume.Context;
        foreach (var pair in waitGroup.AggregatedContext)
        {
            instance.Context[pair.Key] = pair.Value;
        }

        if (instance.Nodes.TryGetValue(waitGroup.TransitionId, out var transitionNode) && transitionNode is TransitionBase completedTransition)
        {
            object? outputValue = null;
            if (completedTransition is CommandTransition commandTransition)
            {
                outputValue = ResolveResumeOutputValue(commandTransition, waitGroup.AggregatedContext);
                if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
                {
                    PathValueAccessor.SetValue(instance.Context, commandTransition.OutputPath, outputValue);
                }

                ApplyOutputBindings(instance.Context, commandTransition, outputValue);
            }

            var transitionSucceeded = _expressionEvaluator.EvaluateBoolean(completedTransition.SucceedExpression.Source, instance.Context);
            var gatesPassed = EvaluatePublishedGates(instance, completedTransition);
            if (completedTransition is CommandTransition resumedCommand && instance.LastGateEvaluation is { } resumedEvaluation)
            {
                instance.LastGateEvaluation = EnrichGateEvaluation(resumedEvaluation, resumedCommand, payload);
            }
            if (!transitionSucceeded || !gatesPassed)
            {
                var failureTarget = completedTransition is CommandTransition failedCommand
                    && failedCommand.Command.Parameters?.TryGetValue("gateFailureTargetStateId", out var failureTargetValue) == true
                    ? Convert.ToString(failureTargetValue)
                    : null;
                if (!string.IsNullOrWhiteSpace(failureTarget))
                {
                    instance.ActiveWaitGroups.Remove(waitGroup);
                    instance.CurrentNodeId = failureTarget;
                    MarkStateEntrance(instance);
                    instance.Status = WorkflowStatus.Running;
                }
                else
                {
                    instance.Context.Clear();
                    foreach (var pair in contextBeforeResume) instance.Context[pair.Key] = pair.Value;
                    if (waitGroupIndex >= 0 && waitGroupBeforeResume is not null)
                    {
                        instance.ActiveWaitGroups[waitGroupIndex] = waitGroupBeforeResume;
                    }

                    instance.Status = WorkflowStatus.WaitingExternal;
                }

                var failureMessage = FormatTransitionFailure(instance, completedTransition, "Gate evidence incomplete; retry or repair required");
                var failureContext = BuildResumeFailureContext(payload, instance.LastGateEvaluation);
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transitionId, TaskNodeType.Transition, ExecutionStatus.Failed, failureContext, failureMessage));
                return Task.CompletedTask;
            }
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

    private void RestoreFailedInstanceForResume(WorkflowInstance instance, string transitionId)
    {
        var assessment = WorkflowResumePolicy.AssessFailedInstance(instance, transitionId);
        if (!assessment.IsRecoverable)
        {
            var message = assessment.RejectionReason switch
            {
                FailedResumeRejectionReason.NoFailedTransition =>
                    $"Workflow instance '{instance.InstanceId}' is Failed but has no failed transition to recover.",
                FailedResumeRejectionReason.RequestedTransitionMismatch =>
                    $"Failed workflow instance '{instance.InstanceId}' can only resume from its most recent failed transition '{assessment.FailedTransitionId}'.",
                FailedResumeRejectionReason.PreviousStateMissing =>
                    $"Failed workflow instance '{instance.InstanceId}' cannot recover because its previous state '{instance.CurrentNodeId}' is missing.",
                FailedResumeRejectionReason.TransitionNotOwned =>
                    $"Failed workflow instance '{instance.InstanceId}' cannot recover transition '{transitionId}' from previous state '{assessment.PreviousStateId}'.",
                _ => $"Workflow instance '{instance.InstanceId}' cannot recover from its Failed state.",
            };
            throw new InvalidOperationException(message);
        }

        var state = (StateNode)instance.Nodes[assessment.PreviousStateId!];

        instance.ActiveWaitGroups.Clear();
        instance.Status = WorkflowStatus.Running;
        instance.History.Add(new WorkflowHistoryEntry(
            _clock.UtcNow,
            state.Id,
            TaskNodeType.State,
            ExecutionStatus.Started,
            Message: $"Recovered from failed transition '{transitionId}' and resumed from previous state"));
    }

    private static void ValidateResumePayload(PendingWaitGroup waitGroup, WorkflowInstance instance, Dictionary<string, object?>? payload)
    {
        if (!instance.Nodes.TryGetValue(waitGroup.TransitionId, out var node) || node is not CommandTransition commandTransition)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath) && (payload is null || payload.Count == 0))
        {
            throw new InvalidOperationException($"Resume payload for transition '{waitGroup.TransitionId}' must provide a non-empty result for outputPath '{commandTransition.OutputPath}'.");
        }

        if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath)
            && commandTransition.Command.Parameters?.TryGetValue("resumeOutputKey", out var resumeOutputKeyValue) == true)
        {
            var resumeOutputKey = Convert.ToString(resumeOutputKeyValue);
            if (string.IsNullOrWhiteSpace(resumeOutputKey) || payload is null || PathValueAccessor.GetValue(payload, resumeOutputKey) is null)
            {
                throw new InvalidOperationException($"Resume payload for transition '{waitGroup.TransitionId}' must include resume output '{resumeOutputKey}' for outputPath '{commandTransition.OutputPath}'.");
            }
        }

        if (commandTransition.Command.Parameters?.TryGetValue("requiredInputs", out var requiredInputsValue) != true || requiredInputsValue is not IEnumerable<object?> requiredItems)
        {
            return;
        }

        var requiredInputs = requiredItems
            .Select(Convert.ToString)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();

        if (requiredInputs.Length == 0)
        {
            return;
        }

        payload ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var missing = requiredInputs
            .Where(requiredInput => PathValueAccessor.GetValue(payload, requiredInput) is null
                && PathValueAccessor.GetValue(instance.Context, requiredInput) is null)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Resume payload for transition '{waitGroup.TransitionId}' is missing required inputs: {string.Join(", ", missing)}.");
        }

        if (commandTransition.Command.Parameters?.TryGetValue("mustMatchContextInputs", out var matchInputsValue) == true
            && matchInputsValue is IEnumerable<object?> matchItems)
        {
            var matchInputs = matchItems
                .Select(Convert.ToString)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray();

            foreach (var matchInput in matchInputs)
            {
                var payloadValue = PathValueAccessor.GetValue(payload, matchInput);
                var contextValue = PathValueAccessor.GetValue(instance.Context, matchInput);

                if (payloadValue is null || contextValue is null)
                {
                    continue;
                }

                if (!AreEquivalentResumeValues(payloadValue, contextValue))
                {
                    throw new InvalidOperationException(
                        $"Resume payload for transition '{waitGroup.TransitionId}' must keep '{matchInput}' aligned with the existing runtime context.");
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, object?> BuildResumeFailureContext(
        IReadOnlyDictionary<string, object?>? payload,
        GateEvaluationResult? gateEvaluation)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["received_payload_top_level_keys"] = payload?.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray() ?? [],
            ["gate_evaluation"] = gateEvaluation,
        };
    }

    private static bool AreEquivalentResumeValues(object payloadValue, object contextValue)
    {
        if (payloadValue is string payloadText && contextValue is string contextText)
        {
            if (Path.IsPathFullyQualified(payloadText) || Path.IsPathFullyQualified(contextText))
            {
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                return string.Equals(Path.GetFullPath(payloadText), Path.GetFullPath(contextText), comparison);
            }

            return string.Equals(payloadText, contextText, StringComparison.Ordinal);
        }

        return JsonSerializer.Serialize(payloadValue) == JsonSerializer.Serialize(contextValue);
    }

    private static object? ResolveResumeOutputValue(CommandTransition commandTransition, IReadOnlyDictionary<string, object?> payload)
    {
        if (commandTransition.Command.Parameters?.TryGetValue("resumeOutputKey", out var resumeOutputKeyValue) == true)
        {
            var resumeOutputKey = Convert.ToString(resumeOutputKeyValue);
            if (!string.IsNullOrWhiteSpace(resumeOutputKey))
            {
                return PathValueAccessor.GetValue(payload, resumeOutputKey);
            }
        }

        return new Dictionary<string, object?>(payload, StringComparer.Ordinal);
    }

    private bool EvaluatePublishedGates(WorkflowInstance instance, TransitionBase transition)
    {
        var evaluation = EvaluatePublishedGatesDetailed(instance, transition);
        instance.LastGateEvaluation = EnrichGateEvaluation(evaluation, transition, payload: null);
        return evaluation.Passed;
    }

    private GateEvaluationResult EvaluatePublishedGatesDetailed(WorkflowInstance instance, TransitionBase transition)
    {
        var commandParameters = transition is CommandTransition commandTransition ? commandTransition.Command.Parameters : null;
        IReadOnlyList<string>? gateIds = transition.SatisfiesGateIds;
        if ((gateIds is null || gateIds.Count == 0) && commandParameters?.TryGetValue("satisfiesGateIds", out var declaredGateIds) == true)
        {
            gateIds = declaredGateIds is IEnumerable<object?> values
                ? values.Select(Convert.ToString).Where(static value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
                : [];
        }

        var stepKind = transition.StepKind.ToString();
        if (instance.Validation is null || gateIds is null || gateIds.Count == 0)
        {
            return GateEvaluationResult.Succeeded(instance.InstanceId, transition.Id, stepKind);
        }

        var resolvedOutputPaths = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var gateId in gateIds)
        {
            if (!instance.Validation.Gates.TryGetValue(gateId, out var gate))
            {
                return new GateEvaluationResult
                {
                    InstanceId = instance.InstanceId,
                    TransitionId = transition.Id,
                    StepKind = stepKind,
                    GateId = gateId,
                    FailedCheck = "invalid_gate",
                    NextAction = "Declare the gate before referencing it from the transition.",
                };
            }

            var governed = string.Equals(instance.TemplateKind, "so-governed-target-skill", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(gate.InstanceBinding)
                && !string.Equals(gate.InstanceBinding, "current_workflow_instance", StringComparison.Ordinal)
                && !string.Equals(gate.InstanceBinding, "current", StringComparison.Ordinal))
            {
                return new GateEvaluationResult
                {
                    InstanceId = instance.InstanceId,
                    TransitionId = transition.Id,
                    StepKind = stepKind,
                    GateId = gateId,
                    InstanceBinding = gate.InstanceBinding,
                    FailedCheck = "invalid_instance_binding",
                    NextAction = "Bind gate evidence to the current workflow instance using current_workflow_instance.",
                };
            }
            if (governed && gate.PassExpression is not null && !new ExpressionCompilerRouter().Compile(instance.ExpressionBinding, gate.PassExpression, $"validation.gates.{gateId}/passExpression").IsSuccess)
            {
                return new GateEvaluationResult
                {
                    InstanceId = instance.InstanceId,
                    TransitionId = transition.Id,
                    StepKind = stepKind,
                    GateId = gateId,
                    InstanceBinding = gate.InstanceBinding,
                    FailedCheck = "invalid_gate_pass_expression",
                    PassExpressionSource = gate.PassExpression.Source,
                    NextAction = gate.FailureGuidance?.NextAction ?? "Repair the gate passExpression and compile the workflow again.",
                };
            }

            var requiredFamilies = gate.RequiredOutputFamilies
                .Concat(gate.RequiredMachineReadableOutputFamilies)
                .Concat(gate.RequiredHumanReviewableOutputFamilies)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var missingFamilies = new List<string>();
            var emptyFamilies = new List<string>();
            foreach (var family in requiredFamilies)
            {
                if (!PathValueAccessor.TryGetValue(instance.Context, family, out var value))
                {
                    missingFamilies.Add(family);
                    continue;
                }

                resolvedOutputPaths[family] = family;
                var valueSemantics = gate.ValueSemantics.TryGetValue(family, out var declaredSemantics)
                    ? declaredSemantics
                    : null;
                if (!HasMeaningfulValue(value, valueSemantics))
                {
                    emptyFamilies.Add(family);
                }
            }

            if (missingFamilies.Count > 0 || emptyFamilies.Count > 0)
            {
                instance.Context["gate_outputs_present"] = false;
                return new GateEvaluationResult
                {
                    InstanceId = instance.InstanceId,
                    TransitionId = transition.Id,
                    StepKind = stepKind,
                    GateId = gateId,
                    InstanceBinding = gate.InstanceBinding,
                    FailedCheck = missingFamilies.Count > 0 ? "missing_output_family" : "empty_output_family",
                    MissingOutputFamilies = missingFamilies,
                    EmptyOutputFamilies = emptyFamilies,
                    ResolvedOutputPaths = new Dictionary<string, string?>(resolvedOutputPaths, StringComparer.Ordinal),
                    PassExpressionSource = gate.PassExpression?.Source,
                    NextAction = gate.FailureGuidance?.NextAction ?? "Publish each required output family through outputPath or explicit outputBindings, then retry from a fresh valid boundary.",
                };
            }

            instance.Context["gate_outputs_present"] = true;
            if (!string.IsNullOrWhiteSpace(gate.PassExpression?.Source) && !_expressionEvaluator.EvaluateBoolean(gate.PassExpression.Source, instance.Context))
            {
                return new GateEvaluationResult
                {
                    InstanceId = instance.InstanceId,
                    TransitionId = transition.Id,
                    StepKind = stepKind,
                    GateId = gateId,
                    InstanceBinding = gate.InstanceBinding,
                    FailedCheck = "pass_expression",
                    ResolvedOutputPaths = new Dictionary<string, string?>(resolvedOutputPaths, StringComparer.Ordinal),
                    PassExpressionSource = gate.PassExpression.Source,
                    NextAction = gate.FailureGuidance?.NextAction ?? "Repair the gate passExpression using the resolved evidence and retry the transition.",
                };
            }
        }

        return new GateEvaluationResult
        {
            Passed = true,
            InstanceId = instance.InstanceId,
            TransitionId = transition.Id,
            StepKind = stepKind,
            GateId = gateIds[^1],
            ResolvedOutputPaths = resolvedOutputPaths,
        };
    }

    private static GateEvaluationResult EnrichGateEvaluation(
        GateEvaluationResult evaluation,
        TransitionBase transition,
        IReadOnlyDictionary<string, object?>? payload)
    {
        if (transition is not CommandTransition commandTransition)
        {
            return evaluation;
        }

        var parameters = commandTransition.Command.Parameters;
        var requiredInputs = GetCommandStringList(parameters, "requiredInputs");
        var resumeOutputKey = GetCommandString(parameters, "resumeOutputKey");
        var projectedPaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
        {
            projectedPaths.Add(commandTransition.OutputPath);
        }
        if (parameters?.TryGetValue("outputBindings", out var bindingsValue) == true
            && bindingsValue is IEnumerable<KeyValuePair<string, object?>> bindings)
        {
            projectedPaths.AddRange(bindings.Select(static binding => binding.Key));
        }

        return evaluation with
        {
            ExpectedPayloadShape = string.IsNullOrWhiteSpace(resumeOutputKey)
                ? (string.IsNullOrWhiteSpace(commandTransition.OutputPath) ? "payload is optional" : "non-empty payload object")
                : $"payload must contain '{resumeOutputKey}'",
            ReceivedPayloadTopLevelKeys = payload?.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray() ?? [],
            RequiredInputs = requiredInputs,
            ResumeOutputKey = resumeOutputKey,
            OutputPath = commandTransition.OutputPath,
            ProjectedContextPaths = projectedPaths.Distinct(StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal).ToArray(),
        };
    }
    private static bool HasMeaningfulValue(object? value, string? valueSemantics)
    {
        if (string.IsNullOrWhiteSpace(valueSemantics))
        {
            return HasMeaningfulValue(value);
        }

        return valueSemantics.Trim() switch
        {
            "present" => value is not null && value is not System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined },
            "nonEmptyString" => value switch
            {
                string text => !string.IsNullOrWhiteSpace(text),
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => !string.IsNullOrWhiteSpace(element.GetString()),
                _ => false,
            },
            "nonEmptyArray" => value switch
            {
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } element => element.GetArrayLength() > 0,
                System.Collections.IEnumerable sequence when value is not string => sequence.GetEnumerator().MoveNext(),
                _ => false,
            },
            "nonEmptyObject" => value switch
            {
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } element => element.EnumerateObject().Any(),
                System.Collections.IDictionary dictionary => dictionary.Count > 0,
                _ => false,
            },
            "booleanTrue" => PathValueAccessor.ToBoolean(value),
            _ => HasMeaningfulValue(value),
        };
    }

    private static bool HasMeaningfulValue(object? value)
    {
        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined } => false,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => !string.IsNullOrWhiteSpace(element.GetString()),
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } element => element.GetArrayLength() > 0,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } element => element.EnumerateObject().Any(),
            System.Collections.IDictionary dictionary => dictionary.Count > 0,
            System.Collections.IEnumerable sequence => sequence.GetEnumerator().MoveNext(),
            _ => true,
        };
    }

    private static List<string> GetCommandStringList(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out var value) != true || value is null)
        {
            return [];
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => [text],
            IEnumerable<string> items => items.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
            IEnumerable<object?> items => items.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList(),
            _ => [],
        };
    }

    private static string? GetCommandString(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        return parameters?.TryGetValue(key, out var value) == true && value is not null
            ? Convert.ToString(value)
            : null;
    }
    private static string FormatTransitionFailure(WorkflowInstance instance, TransitionBase transition, string message)
    {
        var evaluation = instance.LastGateEvaluation;
        if (evaluation is null || evaluation.Passed || !string.Equals(evaluation.TransitionId, transition.Id, StringComparison.Ordinal))
        {
            return message;
        }

        var missing = evaluation.MissingOutputFamilies.Count == 0
            ? "none"
            : string.Join(", ", evaluation.MissingOutputFamilies);
        var empty = evaluation.EmptyOutputFamilies.Count == 0
            ? "none"
            : string.Join(", ", evaluation.EmptyOutputFamilies);
        return $"{message} Gate '{evaluation.GateId ?? "unknown"}' failed at '{evaluation.FailedCheck ?? "unknown"}'. Missing families: {missing}. Empty families: {empty}. Next action: {evaluation.NextAction ?? "inspect the structured gate evaluation."}";
    }
    private EngineTickOutcome FailTransition(WorkflowInstance instance, TransitionBase transition, string message)
    {
        var failureMessage = FormatTransitionFailure(instance, transition, message);
        instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: failureMessage));
        instance.Status = WorkflowStatus.Failed;
        return EngineTickOutcome.FailedWith(failureMessage);
    }

    private static void ApplyOutputBindings(IDictionary<string, object?> context, CommandTransition transition, object? result)
    {
        if (transition.Command.Parameters?.TryGetValue("outputBindings", out var bindingsValue) != true || bindingsValue is null)
        {
            return;
        }

        IEnumerable<KeyValuePair<string, object?>>? bindings = bindingsValue switch
        {
            IDictionary<string, object?> mutable => mutable,
            IReadOnlyDictionary<string, object?> readOnly => readOnly,
            _ => null,
        };

        if (bindings is null)
        {
            return;
        }

        var readOnlyContext = new Dictionary<string, object?>(context, StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            PathValueAccessor.SetValue(context, binding.Key, ResolveOutputBindingValue(readOnlyContext, binding.Value, result));
        }
    }

    private static object? ResolveOutputBindingValue(IReadOnlyDictionary<string, object?> context, object? bindingValue, object? result)
    {
        if (bindingValue is string text)
        {
            if (string.Equals(text, "$result", StringComparison.Ordinal))
            {
                return result;
            }

            const string contextPrefix = "$context:";
            if (text.StartsWith(contextPrefix, StringComparison.Ordinal))
            {
                return PathValueAccessor.GetValue(context, text[contextPrefix.Length..]);
            }
        }

        return bindingValue;
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
                if (_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context)
                    && EvaluatePublishedGates(instance, transition))
                {
                    MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Expression transition succeeded");
                    return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
                }

                return EngineTickOutcome.NoProgress(instance.CurrentNodeId);
            }

            if (transition.StepKind is WorkflowStepKind.StateUpdate or WorkflowStepKind.MemoryWrite)
            {
                var updateEvidence = GetDictionaryParameters(transition, "updates");
                ApplyDictionaryParameters(instance.Context, transition, "updates");

                if (transition is CommandTransition stateTransition)
                {
                    var stateResult = string.IsNullOrWhiteSpace(stateTransition.OutputPath)
                        ? null
                        : PathValueAccessor.GetValue(instance.Context, stateTransition.OutputPath);
                    ApplyOutputBindings(instance.Context, stateTransition, stateResult);
                }

                if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context) || !EvaluatePublishedGates(instance, transition))
                {
                    return FailTransition(instance, transition, "State update did not satisfy its published gate evidence.");
                }

                MoveToTarget(instance, transition, ExecutionStatus.Succeeded, $"{transition.StepKind} applied", updateEvidence);
                return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
            }

            if (transition.StepKind == WorkflowStepKind.MemoryRead)
            {
                ExecuteMemoryRead(instance, transition);
                if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context) || !EvaluatePublishedGates(instance, transition))
                {
                    return FailTransition(instance, transition, "Memory read did not satisfy its published gate evidence.");
                }

                MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Memory read applied");
                return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
            }

            if (transition.StepKind == WorkflowStepKind.ArtifactEmit)
            {
                var artifactEvidence = await ExecuteArtifactEmitAsync(instance, transition, ct).ConfigureAwait(false);
                if (!_expressionEvaluator.EvaluateBoolean(transition.SucceedExpression.Source, instance.Context) || !EvaluatePublishedGates(instance, transition))
                {
                    return FailTransition(instance, transition, "Artifact emit did not satisfy its published gate evidence.");
                }

                var artifactContextChanges = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [transition.OutputPath ?? "artifact.path"] = artifactEvidence.Path,
                    ["artifact.content"] = artifactEvidence.Content,
                };
                MoveToTarget(instance, transition, ExecutionStatus.Succeeded, "Artifact emitted", artifactContextChanges);
                return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
            }

            if (transition is CommandTransition commandTransition)
            {
                var result = await _commandDispatcher.ExecuteAsync(commandTransition.Command, instance.Context, _commandProgress, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(commandTransition.OutputPath))
                {
                    PathValueAccessor.SetValue(instance.Context, commandTransition.OutputPath, result);
                }

                ApplyOutputBindings(instance.Context, commandTransition, result);

                var succeed = _expressionEvaluator.EvaluateBoolean(commandTransition.SucceedExpression.Source, instance.Context);
                var gatePassed = EvaluatePublishedGates(instance, commandTransition);
                if (succeed && gatePassed)
                {
                    MoveToTarget(instance, commandTransition, ExecutionStatus.Succeeded, "Command OK");
                    return EngineTickOutcome.ProgressedTo(instance.CurrentNodeId);
                }

                var failureMessage = FormatTransitionFailure(instance, commandTransition, $"Transition '{commandTransition.Id}' did not satisfy '{commandTransition.SucceedExpression}'.");
                instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, commandTransition.Id, TaskNodeType.Transition, ExecutionStatus.Failed, Message: failureMessage));
                instance.Status = WorkflowStatus.Failed;
                return EngineTickOutcome.FailedWith(failureMessage);
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
        string? assetRoot = null;
        var selected = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters.TryGetValue("keys", out var keysValue) && keysValue is IEnumerable<object?> keys)
        {
            foreach (var key in keys.Select(Convert.ToString).Where(static key => !string.IsNullOrWhiteSpace(key)))
            {
                selected[key!] = PathValueAccessor.GetValue(instance.Context, key!);
            }
        }

        if (parameters.TryGetValue("checkedInAssets", out var assetsValue) && assetsValue is IEnumerable<object?> assets)
        {
            assetRoot = ResolveCheckedInAssetRoot(instance, parameters);
            var snapshots = new List<Dictionary<string, object?>>();

            foreach (var asset in assets.Select(Convert.ToString).Where(static asset => !string.IsNullOrWhiteSpace(asset)))
            {
                var resolvedPath = ResolveCheckedInAssetPath(assetRoot, asset!);
                if (!File.Exists(resolvedPath))
                {
                    throw new InvalidOperationException($"Checked-in asset '{asset}' was not found at '{resolvedPath}'.");
                }

                snapshots.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = asset,
                    ["resolvedPath"] = resolvedPath,
                    ["content"] = File.ReadAllText(resolvedPath),
                });
            }

            selected["checkedInAssetRoot"] = assetRoot;
            selected["checkedInAssets"] = snapshots;
        }

        var manifestPathValue = parameters.TryGetValue("documentCopyManifestPath", out var manifestValue)

            ? Convert.ToString(manifestValue)

            : null;

        if (!string.IsNullOrWhiteSpace(manifestPathValue))

        {

            assetRoot ??= ResolveCheckedInAssetRoot(instance, parameters);

            var resolvedManifestPath = ResolveCheckedInAssetPath(assetRoot, manifestPathValue!);

            if (!File.Exists(resolvedManifestPath))

            {

                throw new InvalidOperationException($"Document-copy manifest '{manifestPathValue}' was not found at '{resolvedManifestPath}'.");

            }



            selected["documentCopyManifest"] = ValidateDocumentCopyManifest(assetRoot, resolvedManifestPath, parameters);

        }



        var nodeToFileMapPathValue = parameters.TryGetValue("nodeToFileMapPath", out var nodeToFileMapValue)

            ? Convert.ToString(nodeToFileMapValue)

            : null;

        if (!string.IsNullOrWhiteSpace(nodeToFileMapPathValue))

        {

            assetRoot ??= ResolveCheckedInAssetRoot(instance, parameters);

            var resolvedNodeToFileMapPath = ResolveCheckedInAssetPath(assetRoot, nodeToFileMapPathValue!);

            if (!File.Exists(resolvedNodeToFileMapPath))

            {

                throw new InvalidOperationException($"Node-to-file map '{nodeToFileMapPathValue}' was not found at '{resolvedNodeToFileMapPath}'.");

            }



            var manifestTargetPaths = selected.TryGetValue("documentCopyManifest", out var manifestEvidenceValue)
                && manifestEvidenceValue is IReadOnlyDictionary<string, object?> manifestEvidence
                && manifestEvidence.TryGetValue("targetPaths", out var targetPathsValue)
                && targetPathsValue is IEnumerable<string> targetPaths
                ? targetPaths
                : Array.Empty<string>();
            selected["nodeToFileMap"] = ValidateNodeToFileMap(assetRoot, resolvedNodeToFileMapPath, manifestTargetPaths);
        }

        if (!string.IsNullOrWhiteSpace(transition.OutputPath))
        {
            PathValueAccessor.SetValue(instance.Context, transition.OutputPath, selected);
        }
    }

    private static Dictionary<string, object?> ValidateDocumentCopyManifest(
        string assetRoot,
        string manifestPath,
        IReadOnlyDictionary<string, object?> parameters)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' must contain a JSON object.");
        }

        var schemaVersion = GetRequiredManifestString(root, "schema_version", manifestPath);
        if (!string.Equals(schemaVersion, "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' has unsupported schema_version '{schemaVersion}'.");
        }

        var targetSkillRoot = GetRequiredManifestString(root, "target_skill_root", manifestPath);
        var targetBoundProduct = GetRequiredManifestString(root, "target_bound_product", manifestPath);
        var targetBoundChannel = GetRequiredManifestString(root, "target_bound_channel", manifestPath);
        var targetBoundVersion = GetRequiredManifestString(root, "target_bound_version", manifestPath);
        if (!string.Equals(targetBoundProduct, "so", StringComparison.Ordinal)
            && !string.Equals(targetBoundProduct, "ao", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' has unsupported target_bound_product '{targetBoundProduct}'.");
        }

        if (!string.Equals(targetBoundChannel, "beta", StringComparison.Ordinal)
            && !string.Equals(targetBoundChannel, "released", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' has unsupported target_bound_channel '{targetBoundChannel}'.");
        }

        var packageLockPath = ResolveCheckedInAssetPath(assetRoot, "assets/so-workflow/so-package-lock.json");
        if (!File.Exists(packageLockPath))
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' requires package lock '{packageLockPath}'.");
        }

        using var packageLockDocument = JsonDocument.Parse(File.ReadAllText(packageLockPath));
        var packageLock = packageLockDocument.RootElement;
        var lockVersion = GetRequiredManifestString(packageLock, "resolved_version", packageLockPath);
        if (!string.Equals(lockVersion, targetBoundVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' target_bound_version '{targetBoundVersion}' does not match package lock resolved_version '{lockVersion}'.");
        }

        foreach (var bundleName in new[] { "runtime_bundle", "self_contained_runtime_bundle" })
        {
            if (!packageLock.TryGetProperty(bundleName, out var bundle))
            {
                continue;
            }

            if (bundle.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Package lock '{packageLockPath}' property '{bundleName}' must be an array.");
            }

            foreach (var package in bundle.EnumerateArray())
            {
                if (package.ValueKind != JsonValueKind.Object
                    || !package.TryGetProperty("resolved_version", out var packageVersion)
                    || packageVersion.ValueKind != JsonValueKind.String
                    || !string.Equals(packageVersion.GetString(), targetBoundVersion, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Package lock '{packageLockPath}' contains a '{bundleName}' member with a version different from '{targetBoundVersion}'.");
                }
            }
        }

        if (!root.TryGetProperty("documents", out var documentEntries)
            || documentEntries.ValueKind != JsonValueKind.Array
            || documentEntries.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' must contain a non-empty documents array.");
        }

        var documents = new List<Dictionary<string, object?>>();
        var targetPaths = new List<string>();
        foreach (var entry in documentEntries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains a non-object document entry.");
            }

            var targetPath = GetRequiredManifestString(entry, "target_path", manifestPath).Replace('\\', '/');
            if (Path.IsPathFullyQualified(targetPath)
                || targetPath.StartsWith("/", StringComparison.Ordinal)
                || targetPath.Split('/').Any(static segment => string.Equals(segment, "..", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains unsafe target_path '{targetPath}'.");
            }

            var sourceProduct = GetRequiredManifestString(entry, "source_product", manifestPath);
            if (!string.Equals(sourceProduct, "so", StringComparison.Ordinal)
                && !string.Equals(sourceProduct, "ao", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains unsupported source_product '{sourceProduct}'.");
            }
            if (!string.Equals(sourceProduct, targetBoundProduct, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_product '{sourceProduct}' does not match target_bound_product '{targetBoundProduct}'.");
            }

            var sourcePackageId = GetRequiredManifestString(entry, "source_package_id", manifestPath);
            var sourcePackageRid = GetRequiredManifestString(entry, "source_package_rid", manifestPath);
            var sourcePackagePath = GetRequiredManifestString(entry, "source_package_path", manifestPath).Replace('\\', '/');
            var contentMode = GetRequiredManifestString(entry, "content_mode", manifestPath);
            var expectedRuntimePackageId = string.Equals(sourceProduct, "so", StringComparison.Ordinal)
                ? $"Techne.Loom.SkillOrchestrator.Runtime.{sourcePackageRid}"
                : $"Techne.Loom.AgentOrchestrator.Runtime.{sourcePackageRid}";
            if (sourcePackageRid.IndexOf('/') >= 0
                || sourcePackageRid.IndexOf('\\') >= 0
                || sourcePackageRid.IndexOf(':') >= 0
                || sourcePackageRid.Any(static character => char.IsWhiteSpace(character)))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains an unsafe source_package_rid '{sourcePackageRid}'.");
            }

            if (sourcePackageRid is not ("win-x64" or "win-arm64" or "linux-x64" or "linux-arm64" or "linux-musl-x64" or "linux-musl-arm64" or "osx-x64" or "osx-arm64"))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains unsupported source_package_rid '{sourcePackageRid}'.");
            }
            if (!string.Equals(sourcePackageId, expectedRuntimePackageId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_package_id '{sourcePackageId}' does not match source_product '{sourceProduct}' and source_package_rid '{sourcePackageRid}'.");
            }

            if (Path.IsPathFullyQualified(sourcePackagePath)
                || sourcePackagePath.StartsWith("/", StringComparison.Ordinal)
                || sourcePackagePath.Split('/').Any(static segment => string.Equals(segment, "..", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains unsafe source_package_path '{sourcePackagePath}'.");
            }

            var expectedPackageDocsPrefix = $"tools/{sourcePackageRid}/docs/en/guides/";
            if (!sourcePackagePath.StartsWith(expectedPackageDocsPrefix, StringComparison.Ordinal)
                || !sourcePackagePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_package_path '{sourcePackagePath}' is not an English package guide page for RID '{sourcePackageRid}'.");
            }

            if (!string.Equals(contentMode, "full-document", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' must use content_mode 'full-document' for '{targetPath}'.");
            }
            var targetFileName = targetPath[(targetPath.LastIndexOf('/') + 1)..];
            var expectedPrefix = $"assets/so-workflow/reference/{sourceProduct}/";
            if (!targetPath.StartsWith(expectedPrefix, StringComparison.Ordinal)
                || targetFileName.StartsWith("so-guide", StringComparison.OrdinalIgnoreCase)
                || targetFileName.StartsWith("ao-guide", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' target_path '{targetPath}' is outside the target-local {sourceProduct} reference policy.");
            }

            var resolvedTargetPath = ResolveCheckedInAssetPath(assetRoot, targetPath);
            if (!File.Exists(resolvedTargetPath))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' target copy '{targetPath}' was not found.");
            }

            var sourcePath = GetRequiredManifestString(entry, "source_path", manifestPath).Replace('\\', '/');
            if (Path.IsPathFullyQualified(sourcePath)
                || sourcePath.StartsWith("/", StringComparison.Ordinal)
                || sourcePath.Split('/').Any(static segment => string.Equals(segment, "..", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains unsafe source_path '{sourcePath}'.");
            }

            var packageRootPrefix = $"tools/{sourcePackageRid}/";
            var expectedSourcePath = sourcePackagePath[packageRootPrefix.Length..];
            if (!string.Equals(sourcePath, expectedSourcePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_path '{sourcePath}' does not correspond to source_package_path '{sourcePackagePath}'.");
            }
            var sourcePackageFileName = sourcePackagePath[(sourcePackagePath.LastIndexOf('/') + 1)..];
            var sourceFileName = sourcePath[(sourcePath.LastIndexOf('/') + 1)..];
            if (!string.Equals(sourcePackageFileName, sourceFileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_package_path '{sourcePackagePath}' does not identify the same guide page as source_path '{sourcePath}'.");
            }
            var sourceChannel = GetRequiredManifestString(entry, "source_channel", manifestPath);
            if (!string.Equals(sourceChannel, "beta", StringComparison.Ordinal)
                && !string.Equals(sourceChannel, "released", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' contains unsupported source_channel '{sourceChannel}'.");
            }
            if (!string.Equals(sourceChannel, targetBoundChannel, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_channel '{sourceChannel}' does not match target_bound_channel '{targetBoundChannel}'.");
            }

            var sourceVersion = GetRequiredManifestString(entry, "source_version", manifestPath);
            if (!string.Equals(sourceVersion, targetBoundVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_version '{sourceVersion}' does not match target_bound_version '{targetBoundVersion}'.");
            }
            var sourceSha256 = GetRequiredManifestString(entry, "source_sha256", manifestPath);
            if (sourceSha256.Length != 64 || sourceSha256.Any(static character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' has an invalid source_sha256 for '{targetPath}'.");
            }

            var resolvedSourcePath = ResolveDocumentCopySourcePath(assetRoot, sourcePath, sourcePackagePath, sourcePackageId, sourcePackageRid, sourceVersion, parameters);
            if (resolvedSourcePath is null)
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_path '{sourcePath}' was not found for provenance verification.");
            }

            var actualSourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(resolvedSourcePath))).ToLowerInvariant();
            if (!string.Equals(sourceSha256, actualSourceSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' source_sha256 for '{sourcePath}' does not match the source file.");
            }

            var sourceContent = File.ReadAllText(resolvedSourcePath).TrimEnd();
            var targetContent = File.ReadAllText(resolvedTargetPath);
            if (sourceContent.Length == 0
                || targetContent.IndexOf(sourceContent, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' target copy '{targetPath}' does not contain the complete source document '{sourcePath}'.");
            }
            var artifactOrigin = GetRequiredManifestString(entry, "artifact_origin", manifestPath);
            if (!string.Equals(artifactOrigin, "verified-copy", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' has unsupported artifact_origin '{artifactOrigin}' for '{targetPath}'.");
            }

            var authorityScope = GetRequiredManifestString(entry, "authority_scope", manifestPath);
            var refreshedBy = GetRequiredManifestString(entry, "refreshed_by", manifestPath);
            targetPaths.Add(targetPath);
            documents.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["targetPath"] = targetPath,
                ["sourcePath"] = sourcePath,
                ["sourcePackageId"] = sourcePackageId,
                ["sourcePackageRid"] = sourcePackageRid,
                ["sourcePackagePath"] = sourcePackagePath,
                ["contentMode"] = contentMode,
                ["sourceProduct"] = sourceProduct,
                ["sourceChannel"] = sourceChannel,
                ["sourceVersion"] = sourceVersion,
                ["sourceSha256"] = sourceSha256,
                ["actualSourceSha256"] = actualSourceSha256,
                ["targetContainsCompleteSource"] = true,
                ["sourceHashVerified"] = true,
                ["sourceResolvedPath"] = resolvedSourcePath,
                ["artifactOrigin"] = artifactOrigin,
                ["authorityScope"] = authorityScope,
                ["refreshedBy"] = refreshedBy,
                ["resolvedTargetPath"] = resolvedTargetPath,
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["manifestPath"] = manifestPath,
            ["packageLockPath"] = packageLockPath,
            ["schemaVersion"] = schemaVersion,
            ["targetSkillRoot"] = targetSkillRoot,
            ["targetBoundProduct"] = targetBoundProduct,
            ["targetBoundChannel"] = targetBoundChannel,
            ["targetBoundVersion"] = targetBoundVersion,
            ["documentCount"] = documents.Count,
            ["targetPaths"] = targetPaths,
            ["documents"] = documents,
        };
    }

    private static Dictionary<string, object?> ValidateNodeToFileMap(
        string assetRoot,
        string mapPath,
        IEnumerable<string> manifestTargetPaths)
    {
        var content = File.ReadAllText(mapPath);
        if (!content.Contains("relative to the target skill root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Node-to-file map '{mapPath}' must declare target-root-relative document paths.");
        }

        var checkedInPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in content.Split('\n'))
        {
            var trimmedLine = line.TrimStart();
            if (!trimmedLine.StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(line, "`([^`]+)`"))
            {
                var candidate = match.Groups[1].Value.Trim().Replace('\\', '/');
                if (candidate.Contains("://", StringComparison.Ordinal)
                    || candidate.StartsWith("<", StringComparison.Ordinal))
                {
                    continue;
                }

                var looksLikePath = candidate.Contains('/', StringComparison.Ordinal)
                    || candidate.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    || candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || candidate.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                if (!looksLikePath)
                {
                    continue;
                }

                if (Path.IsPathFullyQualified(candidate)
                    || candidate.StartsWith("/", StringComparison.Ordinal)
                    || candidate.Split('/').Any(static segment => string.Equals(segment, "..", StringComparison.Ordinal))
                    || candidate.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith(".agents/skills/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Node-to-file map '{mapPath}' contains a path outside the target skill root: '{candidate}'.");
                }

                var fileName = candidate[(candidate.LastIndexOf('/') + 1)..];
                if (fileName.StartsWith("so-guide", StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith("ao-guide", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Node-to-file map '{mapPath}' must not reference a complete SO or AO guide copy: '{candidate}'.");
                }

                var resolvedPath = ResolveCheckedInAssetPath(assetRoot, candidate);
                if (!File.Exists(resolvedPath))
                {
                    throw new InvalidOperationException($"Node-to-file map '{mapPath}' references missing checked-in asset '{candidate}'.");
                }

                checkedInPaths.Add(candidate);
            }
        }

        var missingManifestPaths = manifestTargetPaths
            .Where(path => !checkedInPaths.Contains(path.Replace('\\', '/')))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (missingManifestPaths.Length > 0)
        {
            throw new InvalidOperationException($"Node-to-file map '{mapPath}' does not list manifest document path(s): {string.Join(", ", missingManifestPaths)}.");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = mapPath,
            ["resolvedPath"] = mapPath,
            ["pathPolicy"] = "target-root-relative",
            ["contentLength"] = content.Length,
            ["checkedInPaths"] = checkedInPaths.OrderBy(static path => path, StringComparer.Ordinal).ToArray(),
        };
    }

    private static string? ResolveDocumentCopySourcePath(
        string assetRoot,
        string sourcePath,
        string sourcePackagePath,
        string sourcePackageId,
        string sourcePackageRid,
        string sourceVersion,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var candidatePaths = new List<string>();


        void AddCandidate(string root, string relativePath)
        {
            candidatePaths.Add(Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        void AddPackageCandidates(string packageRoot)
        {
            var fullPackageRoot = Path.GetFullPath(packageRoot);
            if (IsMatchingRuntimePackageRoot(fullPackageRoot, sourcePackageId, sourcePackageRid, sourceVersion))
            {

                AddCandidate(fullPackageRoot, sourcePath);
            }

            var nestedRuntimeRoot = Path.Combine(fullPackageRoot, "tools", sourcePackageRid);
            if (IsMatchingRuntimePackageRoot(nestedRuntimeRoot, sourcePackageId, sourcePackageRid, sourceVersion))
            {

                AddCandidate(fullPackageRoot, sourcePackagePath);
            }
        }

        if (parameters.TryGetValue("documentCopySourceRootPath", out var sourceRootValue))
        {
            var sourceRoot = Convert.ToString(sourceRootValue);
            if (!string.IsNullOrWhiteSpace(sourceRoot))
            {
                var fullSourceRoot = Path.GetFullPath(sourceRoot);
                AddPackageCandidates(fullSourceRoot);
            }
        }

        AddPackageCandidates(AppContext.BaseDirectory);


        foreach (var candidatePath in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }
    private static bool IsMatchingRuntimePackageRoot(
        string runtimeRoot,
        string expectedPackageId,
        string expectedRid,
        string expectedVersion)
    {
        var runtimeManifestPath = Path.Combine(runtimeRoot, "runtime.json");
        if (!File.Exists(runtimeManifestPath))
        {
            return false;
        }

        try
        {
            using var runtimeManifestDocument = JsonDocument.Parse(File.ReadAllText(runtimeManifestPath));
            var runtimeManifest = runtimeManifestDocument.RootElement;
            return runtimeManifest.ValueKind == JsonValueKind.Object
                && HasStringProperty(runtimeManifest, "schema", "techne-loom-runtime-v1")
                && HasStringProperty(runtimeManifest, "package_id", expectedPackageId)
                && HasStringProperty(runtimeManifest, "version", expectedVersion)
                && HasStringProperty(runtimeManifest, "rid", expectedRid)
                && HasStringProperty(runtimeManifest, "docs_root", $"tools/{expectedRid}/docs/en");
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        static bool HasStringProperty(JsonElement element, string propertyName, string expectedValue)
        {
            return element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal);
        }
    }
    private static string GetRequiredManifestString(JsonElement element, string propertyName, string manifestPath)

    {

        if (!element.TryGetProperty(propertyName, out var value)

            || value.ValueKind != JsonValueKind.String

            || string.IsNullOrWhiteSpace(value.GetString()))

        {

            throw new InvalidOperationException($"Document-copy manifest '{manifestPath}' requires a non-empty string property '{propertyName}'.");

        }



        return value.GetString()!;

    }


    private static string ResolveCheckedInAssetRoot(WorkflowInstance instance, IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("assetRootInput", out var assetRootInputValue))
        {
            var assetRootInput = Convert.ToString(assetRootInputValue);
            if (!string.IsNullOrWhiteSpace(assetRootInput))
            {
                var resolvedFromContext = Convert.ToString(PathValueAccessor.GetValue(instance.Context, assetRootInput));
                if (string.IsNullOrWhiteSpace(resolvedFromContext))
                {
                    throw new InvalidOperationException($"MemoryRead assetRootInput '{assetRootInput}' did not resolve to a path.");
                }

                return Path.GetFullPath(resolvedFromContext);
            }
        }

        if (parameters.TryGetValue("assetRootPath", out var assetRootPathValue))
        {
            var assetRootPath = Convert.ToString(assetRootPathValue);
            if (string.IsNullOrWhiteSpace(assetRootPath))
            {
                throw new InvalidOperationException("MemoryRead assetRootPath must not be empty when provided.");
            }

            return Path.GetFullPath(assetRootPath);
        }

        throw new InvalidOperationException("MemoryRead checkedInAssets requires assetRootInput or assetRootPath.");
    }

    private static string ResolveCheckedInAssetPath(string assetRoot, string assetPath)
    {
        if (Path.IsPathFullyQualified(assetPath))
        {
            throw new InvalidOperationException($"MemoryRead checkedInAssets does not allow absolute asset path '{assetPath}'.");
        }

        var normalizedRoot = Path.GetFullPath(assetRoot);
        var resolvedPath = Path.GetFullPath(Path.Combine(normalizedRoot, assetPath));
        if (!IsPathContainedWithinRoot(normalizedRoot, resolvedPath))
        {
            throw new InvalidOperationException($"Checked-in asset path '{assetPath}' escapes asset root '{normalizedRoot}'.");
        }

        return resolvedPath;
    }

    private static bool IsPathContainedWithinRoot(string assetRoot, string candidatePath)
    {
        var relative = Path.GetRelativePath(assetRoot, candidatePath);
        return relative.Equals(".", StringComparison.Ordinal)
            || (!relative.Equals("..", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
                && !Path.IsPathFullyQualified(relative));
    }

    private static Dictionary<string, object?> GetDictionaryParameters(TransitionBase transition, string key)
    {
        if (transition is CommandTransition commandTransition
            && commandTransition.Command.Parameters?.TryGetValue(key, out var value) == true)
        {
            if (value is IDictionary<string, object?> mutable)
            {
                return mutable.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
            }
            if (value is IReadOnlyDictionary<string, object?> readOnly)
            {
                return readOnly.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
            }
        }
        return new Dictionary<string, object?>(StringComparer.Ordinal);
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

    private static async Task<(string Path, string Content)> ExecuteArtifactEmitAsync(WorkflowInstance instance, TransitionBase transition, CancellationToken ct)
    {
        if (transition is not CommandTransition commandTransition)
        {
            return (string.Empty, string.Empty);
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

        return (path, content);
    }

    private string? ResolveCorrelationKey(StateNode state, IReadOnlyDictionary<string, object?> context)
    {
        return state.CorrelationKeyPath is null
            ? null
            : Convert.ToString(PathValueAccessor.GetValue(context, state.CorrelationKeyPath));
    }

    private void MoveToTarget(WorkflowInstance instance, TransitionBase transition, ExecutionStatus executionStatus, string message, IReadOnlyDictionary<string, object?>? contextChanges = null)
    {
        instance.History.Add(new WorkflowHistoryEntry(_clock.UtcNow, transition.Id, TaskNodeType.Transition, executionStatus, contextChanges, message));
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
