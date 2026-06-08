using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Abstractions.TaskTracking.Runtime;

public interface ICommandDispatcher
{
    Task<object?> ExecuteAsync(
        CommandInvocation invocation,
        IReadOnlyDictionary<string, object?> workflowContextReference,
        IProgress<object>? progress,
        CancellationToken ct);
}

public interface ICommandExecutionReviewer
{
    Task<(bool IsPassed, string Note, bool IsCommandUpdated)> ReviewAsync(
        CommandInvocation invocation,
        object? result,
        IReadOnlyDictionary<string, object?> workflowContextReference,
        CancellationToken ct);
}
