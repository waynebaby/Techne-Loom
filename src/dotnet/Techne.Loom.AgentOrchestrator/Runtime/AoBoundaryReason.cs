namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoBoundaryReason
{
    public const string ClarificationRequired = "clarification_required";
    public const string DelegationRequired = "delegation_required";
    public const string ToolProbeRequired = "tool_probe_required";
    public const string WeaveOutRequired = "weave_out_required";

    public static string Normalize(string? value)
    {
        return value switch
        {
            "clarification" => ClarificationRequired,
            "clarification_required" => ClarificationRequired,
            "delegation" => DelegationRequired,
            "delegation_required" => DelegationRequired,
            "tool_probe" => ToolProbeRequired,
            "tool_probe_required" => ToolProbeRequired,
            "weave_out" => WeaveOutRequired,
            "weave_out_required" => WeaveOutRequired,
            _ => ClarificationRequired,
        };
    }
}
