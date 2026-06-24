namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class WorkflowInstance
{
    public string InstanceId { get; set; } = string.Empty;

    public Dictionary<string, ITaskNode> Nodes { get; set; } = [];

    public string? TemplateKind { get; set; }

    public WorkflowValidationContract? Validation { get; set; }

    public string StartNodeId { get; set; } = string.Empty;

    public string CurrentNodeId { get; set; } = string.Empty;

    public string? EndNodeId { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.ReadyToStart;

    public Dictionary<string, object?> Context { get; set; } = new(StringComparer.Ordinal);

    public List<WorkflowHistoryEntry> History { get; set; } = [];

    public int Version { get; set; }

    public List<PendingWaitGroup> ActiveWaitGroups { get; set; } = [];

    public DateTimeOffset? LastActivityUtc { get; set; }

    public DateTimeOffset? LastHeartbeatUtc { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseExpiresUtc { get; set; }

    public IReadOnlyDictionary<string, StateNode> GetStateNodes()
    {
        return Nodes.Values
            .OfType<StateNode>()
            .ToDictionary(static item => item.Id, static item => item, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, TransitionBase> GetTransitionNodes()
    {
        return Nodes.Values
            .OfType<TransitionBase>()
            .ToDictionary(static item => item.Id, static item => item, StringComparer.Ordinal);
    }
}
