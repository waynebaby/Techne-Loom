namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class WorkflowValidationContract
{
    public Dictionary<string, WorkflowValidationGate> Gates { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, WorkflowRouteValidationProfile> Routes { get; set; } = new(StringComparer.Ordinal);

    public List<string> DeclaredUserOwnedFields { get; set; } = [];

    public List<string> ReservedRuntimeOwnedFields { get; set; } = [];
}

public sealed class WorkflowValidationGate
{
    public string? Description { get; set; }

    public ExpressionDefinition? PassExpression { get; set; }

    public List<string> RequiredOutputFamilies { get; set; } = [];

    public List<string> RequiredMachineReadableOutputFamilies { get; set; } = [];

    public List<string> RequiredHumanReviewableOutputFamilies { get; set; } = [];
    public Dictionary<string, string> ValueSemantics { get; set; } = new(StringComparer.Ordinal);

    public string? InstanceBinding { get; set; }

    public WorkflowGateFailureGuidance? FailureGuidance { get; set; }
}

public sealed class WorkflowRouteValidationProfile
{
    public string? Description { get; set; }

    public List<string> RequiredTerminalGateIds { get; set; } = [];

    public List<string> RequiredBlockedGateIds { get; set; } = [];
}