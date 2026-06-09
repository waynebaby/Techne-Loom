using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed class DefaultCommandExecutionReviewer : ICommandExecutionReviewer
{
    public Task<(bool IsPassed, string Note, bool IsCommandUpdated)> ReviewAsync(
        CommandInvocation invocation,
        object? result,
        IReadOnlyDictionary<string, object?> workflowContextReference,
        CancellationToken ct)
    {
        return Task.FromResult((true, "accepted", false));
    }
}
