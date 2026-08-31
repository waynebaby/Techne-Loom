namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class PlanStepContract
{
    public List<string> InputPaths { get; set; } = [];

    public string? ResultFile { get; set; }

    public List<string> RequiredEvidence { get; set; } = [];

    public string ApplyMode { get; set; } = "atomic";

    public string? WeaveBackTargetNodeId { get; set; }
}