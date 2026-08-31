using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.SkillOrchestrator.Analysis;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal static class WorkflowValidator
{
    private const string GovernedTemplateKind = "so-governed-target-skill";
    private static readonly string[] BuiltInReservedRuntimeOwnedFields =
    [
        "workflow_file",
        "event_log_file",
        "resolved_so_dll_path",
        "render_execution_artifact",
        "compile_audit_path",
        "audit_artifacts",
        "audit_artifacts.mermaid_file",
        "audit_artifacts.html_file",
        "audit_artifacts.workflow_backup_file",
        "runtime_provenance",
    ];

    private const string StructuralRule = "SO1000";
    private const string SeamOwnershipRule = "SO2000";
    private const string BusinessGateRule = "SO3000";
    private const string DoneReachabilityRule = "SO4000";

    public static WorkflowValidationResult Validate(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var result = new WorkflowValidationResult();
        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();
        var incoming = BuildIncomingTransitionLookup(states, transitions);

        RunPhase(result, "structure", ["parse"], () => ValidateStructure(instance, states, transitions, result));
        RunPhase(result, "local_contracts", [], () =>
        {
            ValidateOutputBindings(instance, transitions, result);
            ValidateExternalResultProjection(instance, transitions, result);
            ValidatePlanContracts(instance, transitions, result);
        });
        RunPhase(result, "expressions", [], () => ValidateExplicitPredicates(instance, transitions, result));
        RunPhase(result, "governance", [], () =>
        {
            ValidateGovernedTemplateContract(instance, transitions, result);
            ValidateWorkflowIdentity(instance, transitions, result);
            McpFirstGovernedRouteValidator.Validate(instance, states, transitions, result);
            ValidateSeamOwnership(instance, transitions, result);
            ValidateBusinessContract(instance, transitions, result);
        });
        RunPhase(result, "dataflow", ["structure"], () => ValidateDataflow(instance, result));
        RunPhase(result, "reachability", ["structure"], () => ValidateDoneReachability(instance, states, transitions, incoming, result));

        result.Normalize();
        return result;
    }



    private static void RunPhase(WorkflowValidationResult result, string phase, IReadOnlyList<string> prerequisites, Action action)
    {
        var blockedBy = prerequisites
            .Where(result.HasBlockingInPhase)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        if (blockedBy.Length > 0)
        {
            result.AddBlockedPhase(phase, blockedBy, "one or more prerequisite checks failed");
            return;
        }

        try
        {
            action();
        }
        catch (Exception exception)
        {
            result.Add(
                "LOOM.COMPILE.PHASE_FAILURE",
                $"Validation phase '{phase}' could not complete: {exception.Message}",
                $"phase:{phase}",
                "Repair the workflow shape reported by this phase and rerun compile.",
                code: "LOOM.COMPILE.PHASE_FAILURE",
                category: "resource",
                phase: phase);
        }
    }


    private static void ValidateWorkflowIdentity(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        var missingFields = new[]
        {
            (Name: "taskType", Value: instance.TaskType),
            (Name: "workflowKind", Value: instance.WorkflowKind),
            (Name: "caseId", Value: instance.CaseId),
            (Name: "runId", Value: instance.RunId),
        }
        .Where(static field => string.IsNullOrWhiteSpace(field.Value))
        .Select(static field => field.Name)
        .ToArray();

        if (missingFields.Length > 0)
        {
            result.Add(
                BusinessGateRule,
                $"Governed workflow instances must declare taskType, workflowKind, caseId, and runId. Missing: {string.Join(", ", missingFields)}.",
                "workflow identity",
                "Declare the workflow business task, workflow kind, case id, and run id before compile or run.");
            return;
        }

        if (!WorkflowIdentityContract.IsKnownWorkflowKind(instance.WorkflowKind))
        {
            result.Add(
                BusinessGateRule,
                $"Workflow kind '{instance.WorkflowKind}' is not supported by the governed SO contract.",
                "workflowKind",
                $"Use one of: {WorkflowIdentityContract.SoSelfBootstrapWorkflowKind}, {WorkflowIdentityContract.TargetSkillEnhancementWorkflowKind}, or {WorkflowIdentityContract.TargetSkillBusinessWorkflowKind}.");
        }
        else if (WorkflowIdentityContract.IsEnhancementWorkflowKind(instance.WorkflowKind)
            && !string.Equals(instance.TaskType, WorkflowIdentityContract.SkillEnhancementTaskType, StringComparison.Ordinal))
        {
            result.Add(
                BusinessGateRule,
                $"Workflow kind '{instance.WorkflowKind}' is an enhancement workflow and requires taskType '{WorkflowIdentityContract.SkillEnhancementTaskType}', but received '{instance.TaskType}'.",
                "taskType/workflowKind",
                "Use a target-specific taskType with target_skill_business, or use skill_enhancement for an enhancement workflow.");
        }
        else if (WorkflowIdentityContract.IsTargetSkillBusinessWorkflowKind(instance.WorkflowKind)
            && string.Equals(instance.TaskType, WorkflowIdentityContract.SkillEnhancementTaskType, StringComparison.Ordinal))
        {
            result.Add(
                BusinessGateRule,
                $"Workflow kind '{WorkflowIdentityContract.TargetSkillBusinessWorkflowKind}' cannot use taskType '{WorkflowIdentityContract.SkillEnhancementTaskType}'.",
                "taskType/workflowKind",
                "Use a target-specific business task such as requirement_generation or model_generation.");
        }

        if (!WorkflowIdentityContract.IsTargetSkillBusinessWorkflowKind(instance.WorkflowKind))
        {
            return;
        }

        var enhancementFamilies = transitions.Values
            .SelectMany(static transition => (transition.PublishesOutputFamilies ?? [])
                .Concat(transition.PublishesBlockedOutputFamilies ?? []))
            .Where(WorkflowIdentityContract.IsEnhancementOutputFamily)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static family => family, StringComparer.Ordinal)
            .ToArray();
        if (enhancementFamilies.Length > 0)
        {
            result.Add(
                BusinessGateRule,
                $"Target business workflow '{instance.TaskType}' must not publish SO enhancement output families: {string.Join(", ", enhancementFamilies)}.",
                "workflowKind/target business outputs",
                "Move enhancement review, aggregate, repair, and post-fix evidence to the outer SO enhancement workflow.");
        }

        var enhancementSubagents = transitions.Values
            .OfType<CommandTransition>()
            .Select(transition => transition.Command.Parameters is null ? null : GetString(transition.Command.Parameters, "subagentRelativePath"))
            .Where(WorkflowIdentityContract.IsEnhancementSubagentPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (enhancementSubagents.Length > 0)
        {
            result.Add(
                BusinessGateRule,
                $"Target business workflow '{instance.TaskType}' must not invoke SO enhancement subagents: {string.Join(", ", enhancementSubagents)}.",
                "workflowKind/target business subagents",
                "Keep SO enhancement subagents in the outer skill-enhancement workflow and route this workflow to target business steps.");
        }
    }
    private static void ValidateDataflow(WorkflowInstance instance, WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        var report = new SkillWorkflowDataflowAnalyzer().Analyze(instance);
        foreach (var issue in report.Issues)
        {
            var location = issue.TransitionId is not null
                ? $"transition:{issue.TransitionId}"
                : $"validation.gates.{issue.GateId}/requiredOutputFamilies/{issue.OutputFamily}";
            result.Add(
                BusinessGateRule,
                $"Dataflow validation failed for output family '{issue.OutputFamily}': {issue.Reason}",
                location,
                "Ensure the required family has a reachable concrete producer through outputPath or explicit outputBindings on the governed route.");
        }
    }

    private static void ValidateGovernedTemplateContract(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        if (instance.Validation is null)
        {
            result.Add(
                BusinessGateRule,
                "Loom-governanced target-skill workflows must declare a root validation contract.",
                "validation",
                "Add validation.gates, validation.routes, declaredUserOwnedFields, and reservedRuntimeOwnedFields to the workflow root.");
            return;
        }

        if (instance.Validation.Gates.Count == 0)
        {
            result.Add(
                BusinessGateRule,
                "Loom-governanced target-skill workflows must declare at least one business-output gate.",
                "validation.gates",
                "Add route-aware business-output gates to validation.gates.");
        }

        if (instance.Validation.Routes.Count == 0)
        {
            result.Add(
                DoneReachabilityRule,
                "Loom-governanced target-skill workflows must declare at least one Loom-governanced route profile.",
                "validation.routes",
                "Add route profiles with terminal and, when needed, blocked gate requirements.");
        }

        if (transitions.Values.Any(static transition => transition.StepKind == WorkflowStepKind.AskUser)
            && instance.Validation.DeclaredUserOwnedFields.Count == 0)
        {
            result.Add(
                SeamOwnershipRule,
                "Loom-governanced target-skill workflows with AskUser seams must declare validation.declaredUserOwnedFields.",
                "validation.declaredUserOwnedFields",
                "List every field that a user-owned seam may request.");
        }
    }

    private static void ValidateStructure(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        ValidateStateReference(instance.StartNodeId, "startNodeId", states, result);
        ValidateStateReference(instance.CurrentNodeId, "currentNodeId", states, result);

        if (!string.IsNullOrWhiteSpace(instance.EndNodeId))
        {
            ValidateStateReference(instance.EndNodeId, "endNodeId", states, result);
        }

        foreach (var state in states.Values)
        {
            if (string.IsNullOrWhiteSpace(state.WorkflowPhase))
            {
                result.Add(
                    StructuralRule,
                    $"State '{state.Id}' must declare a non-empty workflowPhase so compile can place the node into the correct workflow swimlane/stage.",
                    $"state:{state.Id}/workflowPhase",
                    "Set workflowPhase to the overall workflow stage this state belongs to, for example '01 Intake', '02 Planning', or another stable stage label.");
            }

            foreach (var group in state.Groups)
            {
                foreach (var transitionId in group.TransitionIds)
                {
                    if (!transitions.TryGetValue(transitionId, out var transition))
                    {
                        result.Add(
                            StructuralRule,
                            $"State '{state.Id}' references missing transition '{transitionId}'.",
                            $"state:{state.Id}/group:{group.Id}",
                            "Reference only transitions that exist in workflow.nodes.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(transition.TargetNodeId))
                    {
                        ValidateStateReference(transition.TargetNodeId, $"transition '{transition.Id}' targetNodeId", states, result, $"transition:{transition.Id}");
                    }
                }
            }
        }
    }

    private static void ValidateExplicitPredicates(WorkflowInstance instance, IReadOnlyDictionary<string, TransitionBase> transitions, WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        var compiler = new ExpressionCompilerRouter();
        foreach (var transition in transitions.Values.OrderBy(static transition => transition.Id, StringComparer.Ordinal))
        {
            var binding = instance.ExpressionBinding;
            var guardField = $"transition:{transition.Id}/guardExpression";
            var succeedField = $"transition:{transition.Id}/succeedExpression";
            var guardResult = compiler.Compile(binding, transition.GuardExpression, guardField);
            var succeedResult = compiler.Compile(binding, transition.SucceedExpression, succeedField);
            if (!transition.GuardExpressionWasExplicitlyDeclared)
            {
                result.Add(
                    BusinessGateRule,
                    $"Transition '{transition.Id}' must explicitly declare guardExpression.",
                    guardField,
                    "Generate guardExpression as a synchronous C# predicate.",
                    code: "SO3001",
                    category: "contract",
                    phase: "expressions");
            }
            if (!transition.SucceedExpressionWasExplicitlyDeclared)
            {
                result.Add(
                    BusinessGateRule,
                    $"Transition '{transition.Id}' must explicitly declare succeedExpression.",
                    succeedField,
                    "Generate succeedExpression as a synchronous C# predicate.",
                    code: "SO3001",
                    category: "contract",
                    phase: "expressions");
            }
            if (!guardResult.IsSuccess)
            {
                result.Add(
                    BusinessGateRule,
                    $"Transition '{transition.Id}' has an invalid guardExpression. {guardResult.Feedback.Message}",
                    guardField,
                    "Generate guardExpression as a synchronous C# expression using context.Get<T>(\"path\") or context[\"path\"].",
                    code: "SO3002",
                    category: guardResult.Feedback.DiagnosticCategory,
                    phase: "expressions",
                    expressionFeedback: guardResult.Feedback);
            }
            if (!succeedResult.IsSuccess)
            {
                result.Add(
                    BusinessGateRule,
                    $"Transition '{transition.Id}' has an invalid succeedExpression. {succeedResult.Feedback.Message}",
                    succeedField,
                    "Generate succeedExpression as a synchronous C# expression using context.Get<T>(\"path\") or context[\"path\"].",
                    code: "SO3003",
                    category: succeedResult.Feedback.DiagnosticCategory,
                    phase: "expressions",
                    expressionFeedback: succeedResult.Feedback);
            }
        }

        ValidateGatePassExpressions(instance, result);
    }


    private static void ValidateStateReference(
        string? stateId,
        string fieldName,
        IReadOnlyDictionary<string, StateNode> states,
        WorkflowValidationResult result,
        string? location = null)
    {
        if (string.IsNullOrWhiteSpace(stateId))
        {
            result.Add(StructuralRule, $"Workflow {fieldName} is required.", location ?? fieldName, "Provide an existing state id.");
            return;
        }

        if (!states.ContainsKey(stateId))
        {
            result.Add(
                StructuralRule,
                $"Workflow {fieldName} '{stateId}' does not reference an existing state node.",
                location ?? fieldName,
                "Point the field to a declared state node.");
        }
    }

    private static void ValidateOutputBindings(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        foreach (var transition in transitions.Values.OfType<CommandTransition>())
        {
            if (transition.Command.Parameters?.TryGetValue("outputBindings", out var bindingsValue) != true || bindingsValue is null)
            {
                continue;
            }

            IEnumerable<KeyValuePair<string, object?>>? bindings = bindingsValue switch
            {
                IDictionary<string, object?> mutable => mutable,
                IReadOnlyDictionary<string, object?> readOnly => readOnly,
                _ => null,
            };

            if (bindings is null)
            {
                result.Add(
                    StructuralRule,
                    $"Transition '{transition.Id}' declares outputBindings with an unsupported shape.",
                    $"transition:{transition.Id}/outputBindings",
                    "Use an object map whose values are literal values, '$result', or '$context:<path>' references.");
                continue;
            }

            var bindingMap = bindings.ToDictionary(static binding => binding.Key, static binding => binding.Value, StringComparer.Ordinal);
            ValidateOutputBindingCycles(transition, bindingMap, result);

            foreach (var binding in bindingMap)
            {
                if (string.IsNullOrWhiteSpace(binding.Key))
                {
                    result.Add(
                        StructuralRule,
                        $"Transition '{transition.Id}' declares an outputBindings entry with an empty target path.",
                        $"transition:{transition.Id}/outputBindings",
                        "Use non-empty outputBindings keys that point to concrete context paths.");
                }

                if (binding.Value is not string text || !text.StartsWith("$", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(text, "$result", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(transition.OutputPath)
                        && binding.Key.StartsWith($"{transition.OutputPath}.", StringComparison.Ordinal))
                    {
                        result.Add(
                            StructuralRule,
                            $"Transition '{transition.Id}' binds '$result' into descendant output path '{binding.Key}', which would create a self-referential result object under outputPath '{transition.OutputPath}'.",
                            $"transition:{transition.Id}/outputBindings/{binding.Key}",
                            "Bind '$result' to a sibling path or use a '$context:<path>' projection instead of a descendant of outputPath.");
                    }

                    continue;
                }

                const string contextPrefix = "$context:";
                if (text.StartsWith(contextPrefix, StringComparison.Ordinal))
                {
                    if (text.Length == contextPrefix.Length)
                    {
                        result.Add(
                            StructuralRule,
                            $"Transition '{transition.Id}' declares outputBindings reference '{text}' without a context path.",
                            $"transition:{transition.Id}/outputBindings/{binding.Key}",
                            "Use '$context:<path>' with a non-empty dotted context path.");
                    }

                    continue;
                }

                result.Add(
                    StructuralRule,
                    $"Transition '{transition.Id}' declares unsupported outputBindings expression '{text}'.",
                    $"transition:{transition.Id}/outputBindings/{binding.Key}",
                    "Use literal values, '$result', or '$context:<path>' in outputBindings.");
            }
        }
    }

    private static void ValidateOutputBindingCycles(
        CommandTransition transition,
        IReadOnlyDictionary<string, object?> bindings,
        WorkflowValidationResult result)
    {
        var contextSources = bindings
            .Where(static binding => binding.Value is string value && value.StartsWith("$context:", StringComparison.Ordinal))
            .ToDictionary(
                static binding => binding.Key,
                static binding => ((string)binding.Value!)["$context:".Length..],
                StringComparer.Ordinal);
        var payloadPaths = GetStringList(transition.Command.Parameters, "requiredInputs")
            .Concat(string.IsNullOrWhiteSpace(GetString(transition.Command.Parameters, "resumeOutputKey"))
                ? Enumerable.Empty<string>()
                : new[] { GetString(transition.Command.Parameters, "resumeOutputKey")! })
            .ToHashSet(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool HasCycle(string target)
        {
            if (!visiting.Add(target))
            {
                return true;
            }

            if (contextSources.TryGetValue(target, out var source)
                && !payloadPaths.Contains(source)
                && (!string.Equals(target, source, StringComparison.Ordinal)
                    || string.Equals(transition.OutputPath, target, StringComparison.Ordinal))
                && contextSources.ContainsKey(source)
                && HasCycle(source))
            {
                return true;
            }

            visiting.Remove(target);
            visited.Add(target);
            return false;
        }

        foreach (var target in contextSources.Keys)
        {
            if (!visited.Contains(target) && HasCycle(target))
            {
                result.Add(
                    StructuralRule,
                    $"Transition '{transition.Id}' contains a cyclic $context output binding involving '{target}'.",
                    $"transition:{transition.Id}/outputBindings/{target}",
                    "Bind from an existing context path or $result without making output bindings depend on one another cyclically.");
                break;
            }
        }
    }
    private static void ValidateExternalResultProjection(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var transition in transitions.Values.OfType<CommandTransition>())
        {
            if (!IsExternalStep(transition.StepKind))
            {
                continue;
            }

            var parameters = transition.Command.Parameters;
            var projectionMode = GetString(parameters, "projectionMode");
            if ((!string.IsNullOrWhiteSpace(transition.OutputPath) || !string.IsNullOrWhiteSpace(GetString(parameters, "resumeOutputKey")))
                && string.IsNullOrWhiteSpace(projectionMode))
            {
                result.Add(
                    BusinessGateRule,
                    $"External transition '{transition.Id}' must declare projectionMode for its resume result projection.",
                    $"transition:{transition.Id}/projectionMode",
                    "Set projectionMode to 'canonical' and use resumeOutputKey -> outputPath projection explicitly.");
            }
            if (!string.IsNullOrWhiteSpace(projectionMode)
                && !string.Equals(projectionMode, "canonical", StringComparison.Ordinal)
                && !string.Equals(projectionMode, "legacyNested", StringComparison.Ordinal))
            {
                result.Add(
                    StructuralRule,
                    $"External transition '{transition.Id}' declares unsupported projectionMode '{projectionMode}'.",
                    $"transition:{transition.Id}/projectionMode",
                    "Use 'canonical' for resumeOutputKey -> outputPath projection or explicitly mark a legacy migration as 'legacyNested'.");
            }

            var resumeOutputKey = GetString(parameters, "resumeOutputKey");
            var requiredInputs = GetStringList(parameters, "requiredInputs");
            if (string.Equals(projectionMode, "canonical", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(resumeOutputKey)
                && !string.IsNullOrWhiteSpace(transition.OutputPath))
            {
                result.Add(
                    BusinessGateRule,
                    $"External transition '{transition.Id}' uses canonical projection without resumeOutputKey.",
                    $"transition:{transition.Id}",
                    "Extract a named payload result with resumeOutputKey before writing outputPath.");
            }

            if (!string.IsNullOrWhiteSpace(transition.OutputPath)
                && string.IsNullOrWhiteSpace(resumeOutputKey)
                && requiredInputs.Count == 1
                && requiredInputs.Contains(transition.OutputPath, StringComparer.Ordinal)
                && GetOutputBindings(transition).Count == 0
                && !string.Equals(projectionMode, "legacyNested", StringComparison.Ordinal))
            {
                result.Add(
                    StructuralRule,
                    $"External transition '{transition.Id}' requires outputPath '{transition.OutputPath}' as a payload input without resumeOutputKey, which creates an implicit wrapper projection.",
                    $"transition:{transition.Id}",
                    "Use payload.result (or another named payload path), set resumeOutputKey to that path, and write the extracted value to outputPath.");
            }

            if (string.IsNullOrWhiteSpace(transition.OutputPath) && !string.IsNullOrWhiteSpace(resumeOutputKey))
            {
                result.Add(
                    StructuralRule,
                    $"External transition '{transition.Id}' declares resumeOutputKey '{resumeOutputKey}' without outputPath.",
                    $"transition:{transition.Id}",
                    "Declare the context destination for the extracted resume result or remove resumeOutputKey.");
            }

            if (!string.IsNullOrWhiteSpace(resumeOutputKey)
                && requiredInputs.Count > 0
                && !requiredInputs.Contains(resumeOutputKey, StringComparer.Ordinal))
            {
                result.Add(
                    StructuralRule,
                    $"External transition '{transition.Id}' declares resumeOutputKey '{resumeOutputKey}' but does not validate that payload path.",
                    $"transition:{transition.Id}/requiredInputs",
                    "Include the resumeOutputKey path in requiredInputs so the payload contract is explicit.");
            }
        }
    }

    private static void ValidateGateFailureGuidance(
        WorkflowInstance instance,
        WorkflowValidationGate gate,
        string gateId,
        WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        var guidance = gate.FailureGuidance;
        if (guidance is null
            || string.IsNullOrWhiteSpace(guidance.Summary)
            || string.IsNullOrWhiteSpace(guidance.NextAction)
            || guidance.EvidenceReferences.Count == 0)
        {
            result.Add(
                BusinessGateRule,
                $"Gate '{gateId}' must declare failureGuidance with summary, nextAction, and evidenceReferences.",
                $"validation.gates.{gateId}/failureGuidance",
                "Add an actionable failureGuidance object with at least one target-relative evidence reference.");
            return;
        }

        foreach (var reference in guidance.EvidenceReferences)
        {
            var normalizedPath = (reference.Path ?? string.Empty).Replace('\\', '/');
            var escapesRoot = normalizedPath.Equals("..", StringComparison.Ordinal)
                || normalizedPath.StartsWith("../", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(reference.Path)
                || Path.IsPathFullyQualified(reference.Path)
                || escapesRoot
                || reference.StartLine <= 0
                || reference.EndLine < reference.StartLine
                || string.IsNullOrWhiteSpace(reference.Quote))
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gateId}' contains an invalid failureGuidance evidence reference.",
                    $"validation.gates.{gateId}/failureGuidance/evidenceReferences",
                    "Use a target-relative path, positive 1-based line bounds, and a non-empty exact quote.");
            }
        }
    }

    private static void ValidateGatePassExpressions(WorkflowInstance instance, WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal)
            || instance.Validation is null)
        {
            return;
        }

        var compiler = new ExpressionCompilerRouter();
        foreach (var gate in instance.Validation.Gates.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            if (gate.Value.PassExpression is null)
            {
                continue;
            }

            var field = $"validation.gates.{gate.Key}/passExpression";
            var compileResult = compiler.Compile(instance.ExpressionBinding, gate.Value.PassExpression, field);
            if (!compileResult.IsSuccess)
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gate.Key}' has an invalid C# passExpression. {compileResult.Feedback.Message}",
                    field,
                    "Generate passExpression as a synchronous C# expression using context.Get<T>(\"path\") or context[\"path\"].",
                    code: "SO3004",
                    category: compileResult.Feedback.DiagnosticCategory,
                    phase: "expressions",
                    expressionFeedback: compileResult.Feedback);
            }
        }
    }

    private static void ValidateGatePassExpressionEvidence(
        WorkflowInstance instance,
        WorkflowValidationGate gate,
        string gateId,
        WorkflowValidationResult result)
    {
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            return;
        }

        var source = gate.PassExpression?.Source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(gate.InstanceBinding))
        {
            result.Add(
                BusinessGateRule,
                $"Gate '{gateId}' must bind its evidence to the current workflow instance.",
                $"validation.gates.{gateId}/instanceBinding",
                "Set instanceBinding to 'current_workflow_instance'.");
        }

        if (source.Contains("gate_outputs_present", StringComparison.Ordinal))
        {
            result.Add(
                BusinessGateRule,
                $"Gate '{gateId}' passExpression must bind declared output families directly instead of using gate_outputs_present.",
                $"validation.gates.{gateId}/passExpression",
                "Reference every required output family explicitly; valueSemantics are checked before passExpression evaluation.");
        }

        var requiredFamilies = gate.RequiredOutputFamilies
            .Concat(gate.RequiredMachineReadableOutputFamilies)
            .Concat(gate.RequiredHumanReviewableOutputFamilies)
            .Distinct(StringComparer.Ordinal);
        foreach (var family in requiredFamilies)
        {
            if (!source.Contains($"\"{family}\"", StringComparison.Ordinal))
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gateId}' passExpression does not reference required output family '{family}'.",
                    $"validation.gates.{gateId}/passExpression",
                    $"Reference '{family}' explicitly in the C# predicate.");
            }
        }
    }

    private static void ValidateGateValueSemantics(
        WorkflowInstance instance,
        WorkflowValidationGate gate,
        string gateId,
        WorkflowValidationResult result)
    {
        var requiredFamilies = gate.RequiredOutputFamilies
            .Concat(gate.RequiredMachineReadableOutputFamilies)
            .Concat(gate.RequiredHumanReviewableOutputFamilies)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var allowedSemantics = new HashSet<string>(StringComparer.Ordinal)
        {
            "present",
            "nonEmptyString",
            "nonEmptyArray",
            "nonEmptyObject",
            "booleanTrue",
        };

        if (!string.IsNullOrWhiteSpace(gate.InstanceBinding)
            && !string.Equals(gate.InstanceBinding, "current_workflow_instance", StringComparison.Ordinal)
            && !string.Equals(gate.InstanceBinding, "current", StringComparison.Ordinal))
        {
            result.Add(
                BusinessGateRule,
                $"Gate '{gateId}' declares unsupported instanceBinding '{gate.InstanceBinding}'.",
                $"validation.gates.{gateId}/instanceBinding",
                "Use 'current_workflow_instance' or 'current' so evidence is bound to the active workflow instance.");
        }

        if (string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            foreach (var family in requiredFamilies)
            {
                if (!gate.ValueSemantics.ContainsKey(family))
                {
                    result.Add(
                        BusinessGateRule,
                        $"Gate '{gateId}' does not declare value semantics for required output family '{family}'.",
                        $"validation.gates.{gateId}/valueSemantics/{family}",
                        "Declare present, nonEmptyString, nonEmptyArray, nonEmptyObject, or booleanTrue for every required output family.");
                }
            }
        }
        foreach (var semantic in gate.ValueSemantics)
        {
            if (!requiredFamilies.Contains(semantic.Key))
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gateId}' declares value semantics for non-required output family '{semantic.Key}'.",
                    $"validation.gates.{gateId}/valueSemantics",
                    "Declare value semantics only for a required output family.");
            }

            if (!allowedSemantics.Contains(semantic.Value))
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gateId}' declares unsupported value semantics '{semantic.Value}' for output family '{semantic.Key}'.",
                    $"validation.gates.{gateId}/valueSemantics/{semantic.Key}",
                    "Use present, nonEmptyString, nonEmptyArray, nonEmptyObject, or booleanTrue.");
            }
        }
    }
    private static bool HasConcreteOutputProducer(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        TransitionBase transition,
        string outputFamily)
    {
        if (string.Equals(transition.OutputPath, outputFamily, StringComparison.Ordinal))
        {
            return true;
        }

        var bindings = GetOutputBindings(transition);
        if (!bindings.TryGetValue(outputFamily, out var binding))
        {
            return false;
        }

        if (string.Equals(binding as string, "$result", StringComparison.Ordinal))
        {
            return true;
        }

        if (binding is string contextReference && contextReference.StartsWith("$context:", StringComparison.Ordinal))
        {
            var sourcePath = contextReference["$context:".Length..];
            var potentialPaths = instance.Context.Keys
                .Concat(transitions.Values.SelectMany(GetProducedContextPaths))
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.Ordinal);
            return potentialPaths.Contains(sourcePath)
                || potentialPaths.Any(path => sourcePath.StartsWith($"{path}.", StringComparison.Ordinal));
        }

        return binding is not null
            && (binding is not string literal || !string.IsNullOrWhiteSpace(literal));
    }

    private static IReadOnlyDictionary<string, object?> GetOutputBindings(TransitionBase transition)
    {
        if (transition is not CommandTransition commandTransition
            || commandTransition.Command.Parameters?.TryGetValue("outputBindings", out var value) != true
            || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        IEnumerable<KeyValuePair<string, object?>>? bindings = value switch
        {
            IDictionary<string, object?> mutable => mutable,
            IReadOnlyDictionary<string, object?> readOnly => readOnly,
            _ => null,
        };

        return bindings?.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetProducedContextPaths(TransitionBase transition)
    {
        if (!string.IsNullOrWhiteSpace(transition.OutputPath))
        {
            yield return transition.OutputPath;
        }

        foreach (var binding in GetOutputBindings(transition).Keys)
        {
            if (!string.IsNullOrWhiteSpace(binding))
            {
                yield return binding;
            }
        }
    }

    private static bool IsExternalStep(WorkflowStepKind stepKind)
    {
        return stepKind is WorkflowStepKind.ModelThink
            or WorkflowStepKind.Plan
            or WorkflowStepKind.McpCall
            or WorkflowStepKind.SubagentCall
            or WorkflowStepKind.AskUser
            or WorkflowStepKind.WaitResume;
    }
    private static void ValidatePlanContracts(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        foreach (var diagnostic in PlanStepContractValidator.Validate(instance))
        {
            result.Add(StructuralRule, diagnostic.Message, diagnostic.Location, diagnostic.Suggestion);
        }
    }

    private static void ValidateSeamOwnership(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        var reservedRuntimeOwnedFields = new HashSet<string>(BuiltInReservedRuntimeOwnedFields, StringComparer.Ordinal);
        foreach (var field in instance.Validation?.ReservedRuntimeOwnedFields ?? [])
        {
            reservedRuntimeOwnedFields.Add(field);
        }

        var declaredUserOwnedFields = new HashSet<string>(instance.Validation?.DeclaredUserOwnedFields ?? [], StringComparer.Ordinal);

        foreach (var transition in transitions.Values.OfType<CommandTransition>())
        {
            if (transition.StepKind != WorkflowStepKind.AskUser)
            {
                continue;
            }

            var requiredInputs = GetStringList(transition.Command.Parameters, "requiredInputs");
            var ownedInputMode = transition.OwnedInputMode ?? GetString(transition.Command.Parameters, "ownedInputMode");
            if (!string.IsNullOrWhiteSpace(ownedInputMode) && !string.Equals(ownedInputMode, "user", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(
                    SeamOwnershipRule,
                    $"AskUser transition '{transition.Id}' must declare user-owned input mode.",
                    $"transition:{transition.Id}",
                    "Set command.parameters.ownedInputMode to 'user' or remove the field.");
            }

            foreach (var requiredInput in requiredInputs)
            {
                if (reservedRuntimeOwnedFields.Contains(requiredInput))
                {
                    result.Add(
                        SeamOwnershipRule,
                        $"AskUser transition '{transition.Id}' requests runtime-owned field '{requiredInput}'.",
                        $"transition:{transition.Id}/requiredInputs",
                        "Move runtime-owned fields to WaitResume or blocked-resume payloads instead of AskUser.");
                }

                if (declaredUserOwnedFields.Count > 0 && !declaredUserOwnedFields.Contains(requiredInput))
                {
                    result.Add(
                        SeamOwnershipRule,
                        $"AskUser transition '{transition.Id}' requests undeclared user-owned field '{requiredInput}'.",
                        $"transition:{transition.Id}/requiredInputs",
                        "Declare user-owned fields under validation.declaredUserOwnedFields so AskUser seams are explicit.");
                }
            }

            if (transition.BlockedRoutes is { Count: > 0 })
            {
                result.Add(
                    SeamOwnershipRule,
                    $"AskUser transition '{transition.Id}' must not be used as a blocked runtime-owned route boundary.",
                    $"transition:{transition.Id}",
                    "Move blocked route handling to a WaitResume seam or another runtime-owned boundary.");
            }
        }

        foreach (var transition in transitions.Values)
        {
            var ownedInputMode = transition.OwnedInputMode
                ?? (transition is CommandTransition commandTransition
                    ? GetString(commandTransition.Command.Parameters, "ownedInputMode")
                    : null);
            var runtimeOwnedBoundary = transition.StepKind == WorkflowStepKind.WaitResume
                || string.Equals(ownedInputMode, "runtime", StringComparison.OrdinalIgnoreCase);
            if (transition.BlockedRoutes is { Count: > 0 } && !runtimeOwnedBoundary)
            {
                result.Add(
                    SeamOwnershipRule,
                    $"Transition '{transition.Id}' declares blockedRoutes but is not a runtime-owned boundary.",
                    $"transition:{transition.Id}",
                    "Declare blockedRoutes on a WaitResume seam or set ownedInputMode to 'runtime'.");
            }
        }
    }

    private static void ValidateBusinessContract(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        WorkflowValidationResult result)
    {
        var validation = instance.Validation;
        if (validation is null)
        {
            return;
        }

        foreach (var route in validation.Routes)
        {
            foreach (var gateId in route.Value.RequiredTerminalGateIds.Concat(route.Value.RequiredBlockedGateIds))
            {
                if (!validation.Gates.ContainsKey(gateId))
                {
                    result.Add(
                        BusinessGateRule,
                        $"Route '{route.Key}' references missing gate '{gateId}'.",
                        $"validation.routes.{route.Key}",
                        "Define the gate under validation.gates before referencing it from a route.");
                }
            }
        }

        foreach (var gate in validation.Gates)
        {
            ValidateGateFailureGuidance(instance, gate.Value, gate.Key, result);
            ValidateGateValueSemantics(instance, gate.Value, gate.Key, result);


            ValidateGatePassExpressionEvidence(instance, gate.Value, gate.Key, result);

            if (gate.Value.RequiredOutputFamilies.Count == 0
                && gate.Value.RequiredMachineReadableOutputFamilies.Count == 0
                && gate.Value.RequiredHumanReviewableOutputFamilies.Count == 0)
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gate.Key}' does not declare any required output families.",
                    $"validation.gates.{gate.Key}",
                    "Declare required_output_families and, when needed, machine-readable or human-reviewable subsets.");
            }
        }

        var gatePublishers = transitions.Values
            .Select(transition =>
            {
                var commandParameters = transition is CommandTransition commandTransition ? commandTransition.Command.Parameters : null;
                return new
                {
                    Transition = transition,
                    Satisfies = GetTransitionStrings(transition.SatisfiesGateIds, commandParameters, "satisfiesGateIds"),
                    Publishes = GetTransitionStrings(transition.PublishesOutputFamilies, commandParameters, "publishesOutputFamilies"),
                    BlockedPublishes = GetTransitionStrings(transition.PublishesBlockedOutputFamilies, commandParameters, "publishesBlockedOutputFamilies"),
                    TerminalRoutes = GetTransitionStrings(transition.TerminalRoutes, commandParameters, "terminalRoutes"),
                    BlockedRoutes = GetTransitionStrings(transition.BlockedRoutes, commandParameters, "blockedRoutes"),
                };
            })
            .ToArray();
        if (string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
            foreach (var publisher in gatePublishers)
            {
                var outputFamilies = publisher.Publishes.Concat(publisher.BlockedPublishes).Distinct(StringComparer.Ordinal).ToArray();
                if (outputFamilies.Length > 0 && publisher.Satisfies.Count == 0 && (string.IsNullOrWhiteSpace(publisher.Transition.OutputPath) || string.Equals(publisher.Transition.SucceedExpression.Source, "true", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(BusinessGateRule,
                        $"Transition '{publisher.Transition.Id}' is an ungated output publisher without a concrete outputPath/succeedExpression proof.",
                        $"transition:{publisher.Transition.Id}",
                        "Bind output families to a concrete result path and require a non-default success predicate, or attach a business gate.");
                }
            }
        }



        if (string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal))
        {
        foreach (var transition in transitions.Values.Where(transition => transition is not CommandTransition && transition.SatisfiesGateIds is { Count: > 0 }))
        {
            result.Add(BusinessGateRule,
                $"Transition '{transition.Id}' declares gate satisfaction but is not a command transition with a runtime publisher.",
                $"transition:{transition.Id}",
                "Use a command transition with explicit output bindings for gate-producing work.");
        }

        }
        foreach (var gate in validation.Gates)
        {
            var matchingTransitions = gatePublishers.Where(candidate => candidate.Satisfies.Contains(gate.Key, StringComparer.Ordinal)).ToArray();
            if (matchingTransitions.Length == 0)
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gate.Key}' is never satisfied by any transition.",
                    $"validation.gates.{gate.Key}",
                    "Add command.parameters.satisfiesGateIds to at least one transition that emits the required business outputs.");
                continue;
            }

            foreach (var transition in matchingTransitions)
            {
                foreach (var outputFamily in gate.Value.RequiredOutputFamilies.Concat(gate.Value.RequiredMachineReadableOutputFamilies).Concat(gate.Value.RequiredHumanReviewableOutputFamilies).Distinct(StringComparer.Ordinal))
                {
                    if (!transition.Publishes.Contains(outputFamily, StringComparer.Ordinal) && !transition.BlockedPublishes.Contains(outputFamily, StringComparer.Ordinal))
                    {
                        result.Add(
                            BusinessGateRule,
                            $"Transition '{transition.Transition.Id}' satisfies gate '{gate.Key}' but does not publish required output family '{outputFamily}'.",
                            $"transition:{transition.Transition.Id}",
                            "Align publishesOutputFamilies or publishesBlockedOutputFamilies with the gate contract.");
                        continue;
                    }

                    if (!HasConcreteOutputProducer(instance, transitions, transition.Transition, outputFamily))
                    {
                        result.Add(
                            BusinessGateRule,
                            $"Transition '{transition.Transition.Id}' declares required output family '{outputFamily}' for gate '{gate.Key}' without a concrete outputPath or outputBindings producer.",
                            $"transition:{transition.Transition.Id}",
                            "Project the result to the required family through outputPath or an explicit outputBindings entry.");
                    }
                }
            }
        }

        foreach (var route in validation.Routes)
        {
            foreach (var blockedGateId in route.Value.RequiredBlockedGateIds)
            {
                var matchingTransitions = gatePublishers.Where(candidate => candidate.BlockedRoutes.Contains(route.Key, StringComparer.Ordinal) && candidate.Satisfies.Contains(blockedGateId, StringComparer.Ordinal)).ToArray();
                if (matchingTransitions.Length == 0)
                {
                    result.Add(
                        BusinessGateRule,
                        $"Route '{route.Key}' has no blocked boundary transition that satisfies gate '{blockedGateId}'.",
                        $"validation.routes.{route.Key}",
                        "Add a WaitResume or other runtime-owned boundary that declares blockedRoutes and satisfiesGateIds for the required blocked gate.");
                    continue;
                }

                if (!validation.Gates.TryGetValue(blockedGateId, out var blockedGate))
                {
                    continue;
                }

                var leakedGatePublishers = gatePublishers
                    .Where(candidate => candidate.Satisfies.Contains(blockedGateId, StringComparer.Ordinal) && !candidate.BlockedRoutes.Contains(route.Key, StringComparer.Ordinal))
                    .ToArray();
                foreach (var transition in leakedGatePublishers)
                {
                    result.Add(
                        BusinessGateRule,
                        $"Blocked gate '{blockedGateId}' for route '{route.Key}' is also satisfied by non-blocked transition '{transition.Transition.Id}'.",
                        $"transition:{transition.Transition.Id}",
                        "Declare a dedicated blocked gate for strongest-earned blocked outputs instead of reusing a compile or terminal gate.");
                }

                foreach (var transition in matchingTransitions)
                {
                    foreach (var outputFamily in blockedGate.RequiredOutputFamilies.Concat(blockedGate.RequiredMachineReadableOutputFamilies).Concat(blockedGate.RequiredHumanReviewableOutputFamilies).Distinct(StringComparer.Ordinal))
                    {
                        if (!transition.BlockedPublishes.Contains(outputFamily, StringComparer.Ordinal))
                        {
                            result.Add(
                                BusinessGateRule,
                                $"Blocked boundary transition '{transition.Transition.Id}' satisfies gate '{blockedGateId}' for route '{route.Key}' but does not publish blocked output family '{outputFamily}'.",
                                $"transition:{transition.Transition.Id}",
                                "Align publishesBlockedOutputFamilies with the blocked gate contract.");
                        }
                    }
                }
            }
        }
    }

    private static void ValidateDoneReachability(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        IReadOnlyDictionary<string, List<TransitionBase>> incoming,
        WorkflowValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(instance.EndNodeId) || !states.TryGetValue(instance.EndNodeId, out var endState))
        {
            return;
        }

        var validation = instance.Validation;
        if (validation is null || validation.Routes.Count == 0)
        {
            return;
        }

        if (!incoming.TryGetValue(endState.Id, out var endIncoming) || endIncoming.Count == 0)
        {
            result.Add(
                DoneReachabilityRule,
                $"End state '{endState.Id}' has no incoming transitions.",
                $"state:{endState.Id}",
                "Ensure terminal routes reach the declared end state through governed transitions.");
            return;
        }

        var reachableStates = FindReachableStates(instance.StartNodeId, states, transitions);
        foreach (var terminalTransition in endIncoming)
        {
            var reachablePredecessor = states.Values.Any(state => reachableStates.Contains(state.Id, StringComparer.Ordinal)
                && state.Groups.Any(group => group.TransitionIds.Contains(terminalTransition.Id, StringComparer.Ordinal)));
            if (!reachablePredecessor)
            {
                result.Add(DoneReachabilityRule,
                    $"Terminal transition '{terminalTransition.Id}' is not reachable from start state '{instance.StartNodeId}'.",
                    $"transition:{terminalTransition.Id}",
                    "Ensure a reachable state group references the terminal transition before declaring the route complete.");
            }
        }

        var routeNames = validation.Routes.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var transition in endIncoming)
        {
            var commandParameters = transition is CommandTransition commandTransition ? commandTransition.Command.Parameters : null;
            var terminalRoutes = GetTransitionStrings(transition.TerminalRoutes, commandParameters, "terminalRoutes");
            var satisfiesGateIds = GetTransitionStrings(transition.SatisfiesGateIds, commandParameters, "satisfiesGateIds");

            if (terminalRoutes.Count == 0)
            {
                result.Add(
                    DoneReachabilityRule,
                    $"Terminal transition '{transition.Id}' reaches done without declaring terminalRoutes.",
                    $"transition:{transition.Id}",
                    "Declare the governed routes that terminate through this transition.");
                continue;
            }

            foreach (var routeName in terminalRoutes)
            {
                if (!routeNames.Contains(routeName))
                {
                    result.Add(
                        DoneReachabilityRule,
                        $"Terminal transition '{transition.Id}' references unknown route '{routeName}'.",
                        $"transition:{transition.Id}/terminalRoutes",
                        "Reference a route declared under validation.routes.");
                    continue;
                }

                var requiredGates = validation.Routes[routeName].RequiredTerminalGateIds;
                if (requiredGates.Count == 0)
                {
                    result.Add(
                        DoneReachabilityRule,
                        $"Route '{routeName}' has no required terminal gate ids.",
                        $"validation.routes.{routeName}",
                        "Declare at least one terminal business-output gate for each governed route.");
                    continue;
                }

                foreach (var requiredGate in requiredGates)
                {
                    if (!satisfiesGateIds.Contains(requiredGate, StringComparer.Ordinal))
                    {
                        result.Add(
                            DoneReachabilityRule,
                            $"Terminal transition '{transition.Id}' reaches done for route '{routeName}' without satisfying required gate '{requiredGate}'.",
                            $"transition:{transition.Id}",
                            "Add the required gate to satisfiesGateIds or route the workflow through a gate-satisfying transition before done.");
                    }
                }
            }
        }
    }

    private static HashSet<string> FindReachableStates(
        string startStateId,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(startStateId);
        while (pending.Count > 0)
        {
            var stateId = pending.Pop();
            if (!reachable.Add(stateId) || !states.TryGetValue(stateId, out var state)) continue;
            foreach (var transitionId in state.Groups.SelectMany(group => group.TransitionIds))
            {
                if (transitions.TryGetValue(transitionId, out var reachableTransition) && reachableTransition is ToBeRefinedTransition)
                {
                    continue;
                }

                if (transitions.TryGetValue(transitionId, out var transition) && !string.IsNullOrWhiteSpace(transition.TargetNodeId))
                {
                    pending.Push(transition.TargetNodeId);
                }
            }
        }

        return reachable;
    }

    private static IReadOnlyDictionary<string, List<TransitionBase>> BuildIncomingTransitionLookup(
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions)
    {
        var incoming = new Dictionary<string, List<TransitionBase>>(StringComparer.Ordinal);
        foreach (var state in states.Values)
        {
            incoming[state.Id] = [];
        }

        foreach (var transition in transitions.Values)
        {
            if (!string.IsNullOrWhiteSpace(transition.TargetNodeId) && incoming.TryGetValue(transition.TargetNodeId, out var targetIncoming))
            {
                targetIncoming.Add(transition);
            }
        }

        return incoming;
    }

    private static List<string> GetTransitionStrings(IReadOnlyList<string>? declared, IReadOnlyDictionary<string, object?>? parameters, string fallbackKey)
    {
        if (declared is { Count: > 0 })
        {
            return declared.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        return GetStringList(parameters, fallbackKey);
    }

    private static string? GetString(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            null => null,
            string text => text,
            _ => Convert.ToString(value),
        };
    }

    private static List<string> GetStringList(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var value) || value is null)
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
}
