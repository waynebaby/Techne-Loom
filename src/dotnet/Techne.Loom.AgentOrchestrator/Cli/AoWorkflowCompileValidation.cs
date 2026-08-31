using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal sealed class AoCompileValidationResult
{
    internal static readonly IReadOnlyList<(string Name, string[] Prerequisites)> PhaseDefinitions =
    [
        ("parse", []),
        ("structure", ["parse"]),
        ("local_contracts", ["structure"]),
        ("expressions", ["structure", "local_contracts"]),
        ("governance", ["structure", "expressions"]),
        ("dataflow", ["structure", "governance"]),
        ("reachability", ["structure", "dataflow"]),
    ];

    public List<WorkflowCompileDiagnostic> Diagnostics { get; } = [];

    public bool HasErrors => Diagnostics.Any(static diagnostic => IsBlocking(diagnostic.Severity));

    public bool HasBlockingInPhase(string phase)
        => Diagnostics.Any(diagnostic => string.Equals(diagnostic.Phase, phase, StringComparison.OrdinalIgnoreCase) && IsBlocking(diagnostic.Severity));

    public void Add(
        string ruleId,
        string message,
        string? location = null,
        string? suggestion = null,
        string? code = null,
        string? category = null,
        string severity = "error",
        string phase = "structure",
        IEnumerable<string>? blockedBy = null,
        ExpressionCompileFeedback? expressionFeedback = null)
    {
        Diagnostics.Add(new WorkflowCompileDiagnostic
        {
            RuleId = ruleId,
            Code = code ?? ruleId,
            Category = category ?? "contract",
            Severity = severity,
            Message = message,
            Location = location,
            SuggestedFix = suggestion,
            Phase = phase,
            BlockedBy = blockedBy?.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToList() ?? [],
            ExpressionFeedback = expressionFeedback,
        });
    }

    public void AddBlockedPhase(string phase, IEnumerable<string> blockedBy, string reason)
    {
        Add(
            "LOOM.COMPILE.PHASE_BLOCKED",
            $"Validation phase '{phase}' was blocked because its prerequisite phase reported an unsafe workflow: {reason}",
            $"phase:{phase}",
            "Repair the prerequisite diagnostics before rerunning compile.",
            code: "LOOM.COMPILE.PHASE_BLOCKED",
            category: "resource",
            severity: "blocked",
            phase: phase,
            blockedBy: blockedBy);
    }

    public WorkflowCompileFeedback ToFeedback(
        string product,
        string runtime,
        string? workflowPath = null,
        string? workflowHash = null)
    {
        Normalize();
        return new WorkflowCompileFeedback
        {
            Product = product,
            Runtime = runtime,
            Status = HasErrors ? "failed" : "succeeded",
            WorkflowPath = workflowPath,
            WorkflowHash = workflowHash,
            Diagnostics = [.. Diagnostics],
            Phases = PhaseDefinitions.Select(definition => new WorkflowCompilePhaseFeedback
            {
                Name = definition.Name,
                Prerequisites = [.. definition.Prerequisites],
                DiagnosticCount = Diagnostics.Count(diagnostic => string.Equals(diagnostic.Phase, definition.Name, StringComparison.Ordinal)),
                BlockedBy = Diagnostics
                    .Where(diagnostic => string.Equals(diagnostic.Phase, definition.Name, StringComparison.Ordinal))
                    .SelectMany(static diagnostic => diagnostic.BlockedBy)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToList(),
                Status = GetPhaseStatus(definition.Name),
            }).ToList(),
            Counts = new WorkflowCompileFeedbackCounts
            {
                Total = Diagnostics.Count,
                Errors = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)),
                Warnings = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
                Info = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "info", StringComparison.OrdinalIgnoreCase)),
                Blocked = Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Severity, "blocked", StringComparison.OrdinalIgnoreCase)),
            },
            Truncated = Diagnostics.Any(static diagnostic => diagnostic.Message.Contains("truncated", StringComparison.OrdinalIgnoreCase))
                || Diagnostics.Any(static diagnostic => diagnostic.ExpressionFeedback?.Truncated == true),
        };
    }

    private void Normalize()
    {
        var normalized = Diagnostics
            .Select(NormalizeDiagnostic)
            .GroupBy(static diagnostic => $"{diagnostic.Code}\u001f{diagnostic.Location}\u001f{diagnostic.Message}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static diagnostic => PhaseOrder(diagnostic.Phase))
            .ThenBy(static diagnostic => SeverityOrder(diagnostic.Severity))
            .ThenBy(static diagnostic => diagnostic.Location, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToList();
        Diagnostics.Clear();
        Diagnostics.AddRange(normalized);
    }

    private string GetPhaseStatus(string phase)
    {
        if (Diagnostics.Any(diagnostic => string.Equals(diagnostic.Phase, phase, StringComparison.OrdinalIgnoreCase) && string.Equals(diagnostic.Severity, "blocked", StringComparison.OrdinalIgnoreCase)))
        {
            return "blocked";
        }

        if (Diagnostics.Any(diagnostic => string.Equals(diagnostic.Phase, phase, StringComparison.OrdinalIgnoreCase) && IsBlocking(diagnostic.Severity)))
        {
            return "failed";
        }

        return "completed";
    }

    private static WorkflowCompileDiagnostic NormalizeDiagnostic(WorkflowCompileDiagnostic diagnostic)
    {
        diagnostic.Code = string.IsNullOrWhiteSpace(diagnostic.Code) ? diagnostic.RuleId : diagnostic.Code.Trim();
        diagnostic.Category = string.IsNullOrWhiteSpace(diagnostic.Category) ? "contract" : diagnostic.Category.Trim().ToLowerInvariant();
        diagnostic.Severity = string.IsNullOrWhiteSpace(diagnostic.Severity) ? "error" : diagnostic.Severity.Trim().ToLowerInvariant();
        diagnostic.Phase = string.IsNullOrWhiteSpace(diagnostic.Phase) ? "structure" : diagnostic.Phase.Trim().ToLowerInvariant();
        diagnostic.BlockedBy = diagnostic.BlockedBy.Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToList();
        return diagnostic;
    }

    private static int PhaseOrder(string phase)
    {
        for (var index = 0; index < PhaseDefinitions.Count; index++)
        {
            if (string.Equals(PhaseDefinitions[index].Name, phase, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static int SeverityOrder(string severity) => severity switch
    {
        "error" => 0,
        "blocked" => 1,
        "warning" => 2,
        "info" => 3,
        _ => 4,
    };

    private static bool IsBlocking(string severity)
        => string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(severity, "blocked", StringComparison.OrdinalIgnoreCase);
}

internal static class AoWorkflowCompileValidator
{
    private const string StructuralRule = "AO1000";
    private const string PlanContractRule = "AO2000";
    private const string ExpressionRule = "AO3000";

    public static AoCompileValidationResult ValidateWorkflowInstance(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var result = new AoCompileValidationResult();
        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();

        RunPhase(result, "structure", [], () => ValidateStructure(instance, states, transitions, result));
        RunPhase(result, "local_contracts", ["structure"], () => ValidatePlanContracts(instance, result));
        RunPhase(result, "expressions", ["structure", "local_contracts"], () => ValidateExpressions(instance, transitions, result));
        RunPhase(result, "governance", ["structure", "expressions"], static () => { });
        RunPhase(result, "dataflow", ["structure", "governance"], static () => { });
        RunPhase(result, "reachability", ["structure", "dataflow"], static () => { });
        return result;
    }

    public static AoCompileValidationResult ValidateWorkflowSnapshot(AoWorkflowSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var result = new AoCompileValidationResult();
        RunPhase(result, "structure", [], () =>
        {
            if (string.IsNullOrWhiteSpace(snapshot.Objective))
            {
                result.Add(StructuralRule, "Workflow snapshot objective is required.", "objective", "Provide a non-empty objective.");
            }
            if (string.IsNullOrWhiteSpace(snapshot.Status))
            {
                result.Add(StructuralRule, "Workflow snapshot status is required.", "status", "Provide a non-empty snapshot status.");
            }
            if (string.IsNullOrWhiteSpace(snapshot.CurrentNodeId))
            {
                result.Add(StructuralRule, "Workflow snapshot current_node_id is required.", "current_node_id", "Provide the current workflow node id.");
            }
        });
        RunPhase(result, "local_contracts", ["structure"], static () => { });
        RunPhase(result, "expressions", ["structure", "local_contracts"], static () => { });
        RunPhase(result, "governance", ["structure", "expressions"], static () => { });
        RunPhase(result, "dataflow", ["structure", "governance"], static () => { });
        RunPhase(result, "reachability", ["structure", "dataflow"], static () => { });
        return result;
    }

    private static void RunPhase(AoCompileValidationResult result, string phase, IReadOnlyList<string> prerequisites, Action action)
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

    private static void ValidateStructure(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, StateNode> states,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        AoCompileValidationResult result)
    {
        ValidateStateReference(instance.StartNodeId, "startNodeId", states, result);
        ValidateStateReference(instance.CurrentNodeId, "currentNodeId", states, result);
        if (!string.IsNullOrWhiteSpace(instance.EndNodeId))
        {
            ValidateStateReference(instance.EndNodeId, "endNodeId", states, result);
        }

        foreach (var state in states.Values.OrderBy(static state => state.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(state.WorkflowPhase))
            {
                result.Add(
                    StructuralRule,
                    $"Workflow state '{state.Id}' must declare a non-empty workflowPhase so compile can place the node into the correct workflow swimlane/stage.",
                    $"state:{state.Id}/workflowPhase",
                    "Set workflowPhase to the overall workflow stage this node belongs to.");
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
                        ValidateStateReference(transition.TargetNodeId, $"transition '{transition.Id}' targetNodeId", states, result, $"transition:{transition.Id}/targetNodeId");
                    }
                }
            }
        }
    }

    private static void ValidatePlanContracts(WorkflowInstance instance, AoCompileValidationResult result)
    {
        foreach (var diagnostic in PlanStepContractValidator.Validate(instance))
        {
            result.Add(
                PlanContractRule,
                diagnostic.Message,
                diagnostic.Location,
                diagnostic.Suggestion,
                code: PlanContractRule,
                category: "contract",
                phase: "local_contracts");
        }
    }

    private static void ValidateExpressions(
        WorkflowInstance instance,
        IReadOnlyDictionary<string, TransitionBase> transitions,
        AoCompileValidationResult result)
    {
        var compiler = new ExpressionCompilerRouter();
        foreach (var transition in transitions.Values.OrderBy(static transition => transition.Id, StringComparer.Ordinal))
        {
            if (transition.GuardExpressionWasExplicitlyDeclared)
            {
                ValidateExpression(
                    compiler,
                    instance,
                    transition.GuardExpression,
                    $"transition:{transition.Id}/guardExpression",
                    transition.Id,
                    null,
                    "AO3001",
                    "guardExpression",
                    result);
            }
            if (transition.SucceedExpressionWasExplicitlyDeclared)
            {
                ValidateExpression(
                    compiler,
                    instance,
                    transition.SucceedExpression,
                    $"transition:{transition.Id}/succeedExpression",
                    transition.Id,
                    null,
                    "AO3002",
                    "succeedExpression",
                    result);
            }
        }

        if (instance.Validation is null)
        {
            return;
        }

        foreach (var gate in instance.Validation.Gates.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            if (gate.Value.PassExpression is null)
            {
                continue;
            }

            ValidateExpression(
                compiler,
                instance,
                gate.Value.PassExpression,
                $"validation.gates.{gate.Key}/passExpression",
                null,
                gate.Key,
                "AO3003",
                "passExpression",
                result);
        }
    }

    private static void ValidateExpression(
        IExpressionCompiler compiler,
        WorkflowInstance instance,
        ExpressionDefinition definition,
        string field,
        string? transitionId,
        string? gateId,
        string code,
        string expressionName,
        AoCompileValidationResult result)
    {
        ExpressionCompileResult compileResult;
        try
        {
            compileResult = compiler.Compile(instance.ExpressionBinding, definition, field);
        }
        catch (Exception exception)
        {
            result.Add(
                ExpressionRule,
                $"The AO {expressionName} at '{field}' could not be compiled: {exception.Message}",
                field,
                "Use a synchronous C# expression supported by the AO expression contract.",
                code: code,
                category: "resource",
                phase: "expressions");
            return;
        }

        if (compileResult.IsSuccess)
        {
            return;
        }

        var feedback = compileResult.Feedback;
        feedback.WorkflowId = instance.InstanceId;
        feedback.TransitionId = transitionId;
        feedback.GateId = gateId;
        result.Add(
            ExpressionRule,
            $"The AO {expressionName} at '{field}' is invalid. {feedback.Message}",
            field,
            "Use a synchronous C# expression supported by the AO expression contract.",
            code: code,
            category: string.IsNullOrWhiteSpace(feedback.DiagnosticCategory) ? "contract" : feedback.DiagnosticCategory,
            phase: "expressions",
            expressionFeedback: feedback);
    }

    private static void ValidateStateReference(
        string? stateId,
        string fieldName,
        IReadOnlyDictionary<string, StateNode> states,
        AoCompileValidationResult result,
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
}

internal sealed class AoCompileException : InvalidOperationException
{
    public AoCompileException(
        string message,
        WorkflowCompileFeedback compileFeedback,
        WorkflowAuditArtifacts? auditArtifacts,
        Exception? innerException)
        : base(message, innerException)
    {
        CompileFeedback = compileFeedback;
        AuditArtifacts = auditArtifacts;
    }

    public WorkflowCompileFeedback CompileFeedback { get; }

    public WorkflowAuditArtifacts? AuditArtifacts { get; }
}
