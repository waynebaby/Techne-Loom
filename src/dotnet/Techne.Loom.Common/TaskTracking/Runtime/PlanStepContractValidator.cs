using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record PlanStepContractDiagnostic(
    string TransitionId,
    string Location,
    string Message,
    string Suggestion);

public static class PlanStepContractValidator
{
    public static IReadOnlyList<PlanStepContractDiagnostic> Validate(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Validate(instance.GetTransitionNodes().Values, instance.Nodes.Keys);
    }

    public static IReadOnlyList<PlanStepContractDiagnostic> Validate(IEnumerable<TransitionBase> transitions)
        => Validate(transitions, knownNodeIds: null);

    private static IReadOnlyList<PlanStepContractDiagnostic> Validate(
        IEnumerable<TransitionBase> transitions,
        IEnumerable<string>? knownNodeIds)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        var knownNodes = knownNodeIds is null
            ? null
            : new HashSet<string>(knownNodeIds, StringComparer.Ordinal);
        var diagnostics = new List<PlanStepContractDiagnostic>();
        foreach (var transition in transitions)
        {
            if (transition.StepKind == WorkflowStepKind.Plan && transition is not CommandTransition)
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/$kind",
                    $"Plan transition '{transition.Id}' must be a command transition so its structured result can be applied.",
                    "Use a command transition for a Plan step."));
                continue;
            }

            if (transition is not CommandTransition commandTransition)
            {
                continue;
            }

            if (transition.StepKind != WorkflowStepKind.Plan)
            {
                if (commandTransition.Plan is not null)
                {
                    diagnostics.Add(new PlanStepContractDiagnostic(
                        transition.Id,
                        $"transition:{transition.Id}/plan",
                        $"Transition '{transition.Id}' declares a plan contract but its stepKind is '{transition.StepKind}'.",
                        "Set stepKind to 'plan' or remove the plan contract."));
                }

                continue;
            }

            if (commandTransition.Plan is null)
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/plan",
                    $"Plan transition '{transition.Id}' must declare a plan contract.",
                    "Declare inputPaths, resultFile, requiredEvidence, and applyMode."));
                continue;
            }

            if (commandTransition.Plan.InputPaths.Count == 0 || commandTransition.Plan.InputPaths.Any(string.IsNullOrWhiteSpace))
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/plan/inputPaths",
                    $"Plan transition '{transition.Id}' must declare non-empty input paths.",
                    "Provide at least one context or workflow input path."));
            }

            if (string.IsNullOrWhiteSpace(commandTransition.Plan.ResultFile))
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/plan/resultFile",
                    $"Plan transition '{transition.Id}' must declare a resultFile.",
                    "Provide the path-only file for the immutable plan result."));
            }

            if (commandTransition.Plan.RequiredEvidence.Count == 0 || commandTransition.Plan.RequiredEvidence.Any(string.IsNullOrWhiteSpace))
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/plan/requiredEvidence",
                    $"Plan transition '{transition.Id}' must declare non-empty required evidence paths.",
                    "Provide at least one evidence path for the plan result."));
            }

            if (!string.Equals(commandTransition.Plan.ApplyMode, "atomic", StringComparison.Ordinal))
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/plan/applyMode",
                    $"Plan transition '{transition.Id}' uses unsupported applyMode '{commandTransition.Plan.ApplyMode}'.",
                    "Set applyMode to 'atomic'."));
            }

            if (string.IsNullOrWhiteSpace(commandTransition.TargetNodeId)
                && string.IsNullOrWhiteSpace(commandTransition.Plan.WeaveBackTargetNodeId))
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/targetNodeId",
                    $"Plan transition '{transition.Id}' must declare a targetNodeId or weaveBackTargetNodeId.",
                    "Point the Plan result to a workflow state through targetNodeId or weaveBackTargetNodeId."));
            }

            if (!string.IsNullOrWhiteSpace(commandTransition.Plan.WeaveBackTargetNodeId)
                && knownNodes is not null
                && !knownNodes.Contains(commandTransition.Plan.WeaveBackTargetNodeId))
            {
                diagnostics.Add(new PlanStepContractDiagnostic(
                    transition.Id,
                    $"transition:{transition.Id}/plan/weaveBackTargetNodeId",
                    $"Plan transition '{transition.Id}' references missing weave-back target node '{commandTransition.Plan.WeaveBackTargetNodeId}'.",
                    "Point weaveBackTargetNodeId to an existing workflow node or omit it."));
            }
        }

        return diagnostics;
    }
}