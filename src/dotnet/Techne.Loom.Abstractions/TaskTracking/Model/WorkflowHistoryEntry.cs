namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed record WorkflowHistoryEntry(
    DateTimeOffset Timestamp,
    string NodeId,
    TaskNodeType NodeType,
    ExecutionStatus Status,
    IReadOnlyDictionary<string, object?>? ContextChanges = null,
    string? Message = null);
