using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal sealed record AoBoundaryPlan(
    string Reason,
    string CurrentNodeId,
    string TransitionId,
    IReadOnlyList<string> PendingRequirements,
    IReadOnlyList<string> NextFrontier,
    string Hint,
    AoWeaveOutRequest? WeaveOutRequest = null);
