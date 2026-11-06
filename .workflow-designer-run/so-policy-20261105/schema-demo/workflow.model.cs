using System;
using System.Collections.Generic;
using Techne.Loom.Abstractions.TaskTracking.Model;

public static class WorkflowModelReferenceFacade
{
    public const string SchemaId = "techne-loom.workflow-instance";
    public const string SchemaVersion = "2";
    public const string RuntimeType = "Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance";
    public const string NodeKindDiscriminator = "$kind";

    public static WorkflowInstance NewWorkflowInstance() => new();
    public static StateNode NewStateNode() => new();
    public static CommandTransition NewCommandTransition() => new();
    public static ExpressionTransition NewExpressionTransition() => new();
    public static ToBeRefinedTransition NewToBeRefinedTransition() => new();
    public static TransitionGroup NewTransitionGroup() => new();
    public static CommandInvocation NewCommandInvocation() => new();
    public static ExpressionDefinition Predicate(string source) => new() { Kind = "predicate", Source = source, ResultType = "bool" };
    public static WorkflowScriptVerificationSuite NewVerificationSuite() => new();

    public static IReadOnlyList<string> RootFields { get; } = new[] { "instanceId", "nodes", "templateKind", "taskType", "workflowKind", "caseId", "runId", "runtimeBinding", "runtimeVersion", "expressionBinding", "contractBinding", "validation", "lastGateEvaluation", "startNodeId", "currentNodeId", "endNodeId", "status", "context", "history", "version", "activeWaitGroups", "fanoutCancellationSignals", "lastActivityUtc", "lastHeartbeatUtc", "leaseOwner", "leaseExpiresUtc" };
    public static IReadOnlyList<string> RequiredRootFields { get; } = new[] { "instanceId", "nodes", "startNodeId", "currentNodeId", "status", "context", "history", "version", "activeWaitGroups", "fanoutCancellationSignals" };
    public static IReadOnlyList<string> ExpressionDefinitionFields { get; } = new[] { "kind", "source", "entryPoint", "resultType" };
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> NodeFields { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["command"] = new[] { "$kind", "id", "name", "description", "workflowPhase", "targetNodeId", "outputPath", "priority", "guardExpression", "passExpression", "stepKind", "terminalRoutes", "blockedRoutes", "satisfiesGateIds", "publishesOutputFamilies", "publishesBlockedOutputFamilies", "ownedInputMode", "plan", "userInput", "command", "executionTimeout", "currentRetryCount", "maxRetries" },
        ["expr"] = new[] { "$kind", "id", "name", "description", "workflowPhase", "targetNodeId", "outputPath", "priority", "guardExpression", "passExpression", "stepKind", "terminalRoutes", "blockedRoutes", "satisfiesGateIds", "publishesOutputFamilies", "publishesBlockedOutputFamilies", "ownedInputMode" },
        ["state"] = new[] { "$kind", "id", "name", "description", "workflowPhase", "groups", "expiration", "entranceTime", "waitBehavior", "correlationKeyPath", "stateFailedExpression" },
        ["tbr"] = new[] { "$kind", "id", "name", "description", "workflowPhase", "targetNodeId", "outputPath", "priority", "guardExpression", "passExpression", "stepKind", "terminalRoutes", "blockedRoutes", "satisfiesGateIds", "publishesOutputFamilies", "publishesBlockedOutputFamilies", "ownedInputMode", "designNotes" },
    };
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredNodeFields { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["command"] = new[] { "$kind", "id", "targetNodeId", "stepKind", "guardExpression", "passExpression", "command" },
        ["expr"] = new[] { "$kind", "id", "targetNodeId", "stepKind", "guardExpression", "passExpression" },
        ["state"] = new[] { "$kind", "id", "workflowPhase", "groups" },
        ["tbr"] = new[] { "$kind", "id", "targetNodeId", "stepKind", "guardExpression", "passExpression", "designNotes" },
    };
    public static IReadOnlyList<string> TransitionGroupFields { get; } = new[] { "id", "strategy", "passExpression", "groupTimeout", "cancelLosers", "timeoutTransition", "timeoutTargetStateId", "joinTargetStateId", "noPassTargetStateId", "errorHandlingTargetStateId", "transitionIds" };
    public static IReadOnlyList<string> RequiredTransitionGroupFields { get; } = new[] { "id", "transitionIds" };
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedValues { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["commandInvocationKind"] = new[] { "commandLine", "nativeCode", "http", "tool", "pythonScript" },
        ["concurrencyStrategy"] = new[] { "firstPass", "whenAnyResponse", "whenAnyPass", "whenAllPass", "whenExpression" },
        ["userInputQuestionType"] = new[] { "singleChoice", "multipleChoice", "text", "number", "boolean", "file", "audio" },
        ["waitBehavior"] = new[] { "blockUntilComplete", "waitForSignal" },
        ["workflowKind"] = new[] { "so_self_bootstrap", "target_skill_enhancement", "target_skill_business" },
        ["workflowStatus"] = new[] { "drafting", "readyToStart", "running", "waitingExternal", "failed", "succeeded" },
        ["workflowStepKind"] = new[] { "modelThink", "plan", "toolCall", "mcpCall", "subagentCall", "askUser", "conditionBranch", "waitResume", "stateUpdate", "artifactEmit", "memoryRead", "memoryWrite" },
    };
    public static IReadOnlyDictionary<string, string> CommandParameterContracts { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["content"] = "artifact content for ArtifactEmit",
        ["outputBindings"] = "object map of context paths to literal, $result, $context:<path>, or $payload:<path> from an external result",
        ["path"] = "artifact destination path for ArtifactEmit",
        ["projectionMode"] = "canonical or legacyNested for external resume projection",
        ["requiredInputs"] = "payload-relative or existing context paths",
        ["resumeOutputKey"] = "payload-relative path extracted before outputPath projection",
        ["updates"] = "object map of context paths to values for StateUpdate or MemoryWrite",
    };
}
