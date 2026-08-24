using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.SkillOrchestrator.Analysis;

public sealed record SkillWorkflowAnalysisReport(
    string InstanceId,
    int StateCount,
    int TransitionCount,
    IReadOnlyDictionary<WorkflowStepKind, int> StepKindCounts,
    IReadOnlyList<string> RequestedInputFields,
    IReadOnlyList<string> PublishedOutputFamilies,
    IReadOnlyList<WorkflowBranchAnalysis> Branches,
    IReadOnlyList<WorkflowLoopAnalysis> Loops,
    IReadOnlyList<WorkflowSeamAnalysis> UserSeams,
    IReadOnlyList<WorkflowSeamAnalysis> RuntimeSeams,
    IReadOnlyList<string> GateIds,
    IReadOnlyList<string> DeclaredUserOwnedFields,
    IReadOnlyList<string> ReservedRuntimeOwnedFields,
    IReadOnlyList<WorkflowNodeArtifactMapping> NodeArtifactMap,
    SkillWorkflowDataflowReport Dataflow,
    bool HasTuringCompleteControlRisk);

public sealed record WorkflowBranchAnalysis(
    string StateId,
    string GroupId,
    IReadOnlyList<string> TransitionIds,
    IReadOnlyList<string> GuardExpressions,
    bool IsSwitchLike);

public sealed record WorkflowLoopAnalysis(
    string SourceStateId,
    string TargetStateId,
    string TransitionId,
    bool IsSelfLoop);

public sealed record WorkflowSeamAnalysis(
    string TransitionId,
    WorkflowStepKind StepKind,
    string? OwnedInputMode);

public sealed record WorkflowNodeArtifactMapping(
    string NodeId,
    string NodeKind,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> OutputFamilies,
    IReadOnlyList<string> GateIds);