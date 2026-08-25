namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class WorkflowGateFailureGuidance
{
    public string? Summary { get; set; }

    public string? NextAction { get; set; }

    public List<WorkflowEvidenceReference> EvidenceReferences { get; set; } = [];
}

public sealed class WorkflowEvidenceReference
{
    public string Path { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int EndLine { get; set; }

    public string Quote { get; set; } = string.Empty;
}
