namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class WorkflowValidationContract
{
    public Dictionary<string, WorkflowValidationGate> Gates { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, WorkflowRouteValidationProfile> Routes { get; set; } = new(StringComparer.Ordinal);

    public List<string> DeclaredUserOwnedFields { get; set; } = [];

    public List<string> ReservedRuntimeOwnedFields { get; set; } = [];

    public WorkflowGovernanceEntryContract? GovernanceEntry { get; set; }
}

public sealed class WorkflowGovernanceEntryContract
{
    public string PreferredTransport { get; set; } = "mcp_stdio";

    public List<string> AllowedTransports { get; set; } = ["mcp_stdio", "cli"];

    public string EvidenceFamily { get; set; } = "mcp_startup_evidence";

    public string McpAttemptEvidenceFamily { get; set; } = "mcp_registration_attempt_evidence";

    public string RuntimeLaunchDescriptorField { get; set; } = "runtime_launch_descriptor_ref";

    public List<string> CliFallbackReasons { get; set; } =
    [
        "mcp_transport_unavailable",
        "mcp_handshake_unsupported",
        "mcp_tool_unavailable",
    ];
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