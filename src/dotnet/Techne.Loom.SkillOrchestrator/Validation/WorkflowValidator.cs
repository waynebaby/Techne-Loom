using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.Abstractions.TaskTracking.Model;

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
        var result = new WorkflowValidationResult();
        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();
        var incoming = BuildIncomingTransitionLookup(states, transitions);

        ValidateGovernedTemplateContract(instance, transitions, result);
        ValidateStructure(instance, states, transitions, result);
        ValidateExplicitPredicates(instance, transitions, result);
        ValidateOutputBindings(transitions, result);
        ValidateSeamOwnership(instance, transitions, result);
        ValidateBusinessContract(instance, transitions, result);
        ValidateDoneReachability(instance, states, transitions, incoming, result);

        return result;
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
        if (!string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal)) return;
        foreach (var transition in transitions.Values)
        {
            if (!transition.GuardExpressionWasExplicitlyDeclared || !transition.SucceedExpressionWasExplicitlyDeclared || !SimpleExpressionEvaluator.IsWellFormedExpression(transition.GuardExpression) || !SimpleExpressionEvaluator.IsWellFormedExpression(transition.SucceedExpression))
            {
                result.Add(BusinessGateRule,
                    $"Transition '{transition.Id}' must declare non-empty, supported guardExpression and succeedExpression.",
                    $"transition:{transition.Id}",
                    "Declare both executable predicates in the workflow JSON; implicit or unsupported predicates are not governed evidence.");
            }
        }
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

            foreach (var binding in bindings)
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
            if (transition.BlockedRoutes is { Count: > 0 } && transition.StepKind != WorkflowStepKind.WaitResume)
            {
                result.Add(
                    SeamOwnershipRule,
                    $"Transition '{transition.Id}' declares blockedRoutes but is not a WaitResume seam.",
                    $"transition:{transition.Id}",
                    "Declare blockedRoutes only on runtime-owned wait boundaries such as WaitResume.");
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
            if (string.Equals(instance.TemplateKind, GovernedTemplateKind, StringComparison.Ordinal) && !SimpleExpressionEvaluator.IsWellFormedExpression(gate.Value.PassExpression))
            {
                result.Add(
                    BusinessGateRule,
                    $"Gate '{gate.Key}' must declare a machine-checkable passExpression.",
                    $"validation.gates.{gate.Key}/passExpression",
                    "Declare an executable boolean expression over the gate's runtime evidence before using the gate for route completion.");
            }

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
                if (outputFamilies.Length > 0 && publisher.Satisfies.Count == 0 && (string.IsNullOrWhiteSpace(publisher.Transition.OutputPath) || string.Equals(publisher.Transition.SucceedExpression, "true", StringComparison.OrdinalIgnoreCase)))
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
