
public static class WorkflowDemoBuilder

{

    public static WorkflowInstance Build(WorkflowScriptInput input)

    {

        var context = input.Context.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        if (!context.ContainsKey("request"))

        {

            context["request"] = "complex demo request";

        }

        if (!context.ContainsKey("needs_rework"))

        {

            context["needs_rework"] = false;

        }

        var artifactPath = context.TryGetValue("artifactPath", out var configuredArtifactPath)

            ? Convert.ToString(configuredArtifactPath) ?? "complex-demo-artifact.txt"

            : "complex-demo-artifact.txt";

        ExpressionDefinition Predicate(string source) => new() { Kind = "predicate", Source = source, ResultType = "bool" };

        CommandInvocation Invocation(string name, CommandInvocationKind kind, Dictionary<string, object?>? parameters = null) => new() { Name = name, Kind = kind, Parameters = parameters };

        StateNode State(string id, string name, string phase, string groupId, params string[] transitionIds) => new() { Id = id, Name = name, WorkflowPhase = phase, Groups = [new TransitionGroup { Id = groupId, TransitionIds = transitionIds.ToList() }] };

        var start = State("state.start", "Start", "01 Intake", "group.memory_read", "transition.memory_read");

        var prepared = State("state.prepared", "Prepared", "02 Preparation", "group.state_update", "transition.state_update");

        var persisted = State("state.persisted", "Persisted", "03 Memory", "group.memory_write", "transition.memory_write");

        var tool = State("state.tool", "Tool result", "04 Tool", "group.tool", "transition.tool_call");

        var model = State("state.model", "Model review", "05 Model", "group.model", "transition.model_think");

        var wait = State("state.wait_resume", "External review", "06 External review", "group.wait", "transition.wait_resume");

        var review = State("state.review", "Decision", "07 Decision", "group.condition", "transition.approve", "transition.rework");

        var rework = State("state.rework", "Rework", "08 Rework", "group.rework", "transition.reset_rework");

        var artifact = State("state.artifact", "Artifact", "09 Delivery", "group.artifact", "transition.emit_artifact");

        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "10 Complete", Groups = [] };

        var memoryRead = new CommandTransition { Id = "transition.memory_read", Name = "Read request memory", WorkflowPhase = "01 Intake", TargetNodeId = prepared.Id, OutputPath = "memory.snapshot", StepKind = WorkflowStepKind.MemoryRead, GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<object>(\"memory.snapshot\") != null"), Command = Invocation("memory.read", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["keys"] = new object?[] { "request" } }) };

        var stateUpdate = new CommandTransition { Id = "transition.state_update", Name = "Prepare plan", WorkflowPhase = "02 Preparation", TargetNodeId = persisted.Id, OutputPath = "draft.plan", StepKind = WorkflowStepKind.StateUpdate, GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<string>(\"draft.plan\") != null"), Command = Invocation("state.update", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["draft.plan"] = "prepared", ["needs_rework"] = false } }) };

        var memoryWrite = new CommandTransition { Id = "transition.memory_write", Name = "Persist preparation", WorkflowPhase = "03 Memory", TargetNodeId = tool.Id, OutputPath = "memory.persisted", StepKind = WorkflowStepKind.MemoryWrite, GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<string>(\"memory.persisted\") != null"), Command = Invocation("memory.write", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["memory.persisted"] = "written" } }) };

        var toolCall = new CommandTransition { Id = "transition.tool_call", Name = "Call tool", WorkflowPhase = "04 Tool", TargetNodeId = model.Id, OutputPath = "tool.result", StepKind = WorkflowStepKind.ToolCall, GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<string>(\"tool.result\") != null"), PublishesOutputFamilies = ["tool_summary"], Command = Invocation("echo", CommandInvocationKind.Tool, new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = "tool result", ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tool_summary"] = "$result" } }) };

        var modelThink = new CommandTransition { Id = "transition.model_think", Name = "Request model review", WorkflowPhase = "05 Model", TargetNodeId = wait.Id, OutputPath = "model.result", StepKind = WorkflowStepKind.ModelThink, OwnedInputMode = "runtime", GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<string>(\"model.result\") != null"), BlockedRoutes = ["blocked"], SatisfiesGateIds = ["gate.blocked"], PublishesOutputFamilies = ["model_summary"], PublishesBlockedOutputFamilies = ["tool_summary"], Command = Invocation("model.review", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["requiredInputs"] = new object?[] { "tool_summary", "result" }, ["resumeOutputKey"] = "result", ["projectionMode"] = "canonical", ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["model_summary"] = "$result", ["tool_summary"] = "$context:tool_summary" } }) };

        var waitResume = new CommandTransition { Id = "transition.wait_resume", Name = "Wait for external approval", WorkflowPhase = "06 External review", TargetNodeId = review.Id, OutputPath = "review.result", StepKind = WorkflowStepKind.WaitResume, GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<string>(\"review.result\") != null"), BlockedRoutes = ["blocked"], SatisfiesGateIds = ["gate.blocked"], PublishesOutputFamilies = ["review_summary"], PublishesBlockedOutputFamilies = ["tool_summary"], Command = Invocation("review.wait", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["requiredInputs"] = new object?[] { "result" }, ["resumeOutputKey"] = "result", ["projectionMode"] = "canonical", ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["review_summary"] = "$result", ["tool_summary"] = "$context:tool_summary" } }) };

        var approve = new CommandTransition { Id = "transition.approve", Name = "Approve delivery", WorkflowPhase = "07 Decision", TargetNodeId = artifact.Id, StepKind = WorkflowStepKind.ConditionBranch, GuardExpression = Predicate("!context.Get<bool>(\"needs_rework\")"), SucceedExpression = Predicate("true"), Command = Invocation("noop", CommandInvocationKind.NativeCode) };

        var reworkBranch = new CommandTransition { Id = "transition.rework", Name = "Request rework", WorkflowPhase = "07 Decision", TargetNodeId = rework.Id, StepKind = WorkflowStepKind.ConditionBranch, GuardExpression = Predicate("context.Get<bool>(\"needs_rework\")"), SucceedExpression = Predicate("true"), Command = Invocation("noop", CommandInvocationKind.NativeCode) };

        var resetRework = new CommandTransition { Id = "transition.reset_rework", Name = "Reset rework flag", WorkflowPhase = "08 Rework", TargetNodeId = review.Id, OutputPath = "needs_rework", StepKind = WorkflowStepKind.MemoryWrite, GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<bool>(\"needs_rework\") == false"), Command = Invocation("memory.write.rework", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["needs_rework"] = false } }) };

        var emitArtifact = new CommandTransition { Id = "transition.emit_artifact", Name = "Emit completion artifact", WorkflowPhase = "09 Delivery", TargetNodeId = done.Id, OutputPath = "completion_manifest", StepKind = WorkflowStepKind.ArtifactEmit, TerminalRoutes = ["success"], SatisfiesGateIds = ["gate.final"], PublishesOutputFamilies = ["completion_manifest"], GuardExpression = Predicate("true"), SucceedExpression = Predicate("context.Get<string>(\"completion_manifest\") != null"), Command = Invocation("artifact.emit", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = artifactPath, ["content"] = "complex demo artifact" }) };

        var finalGate = new WorkflowValidationGate { Description = "The final artifact path must be present and non-empty.", PassExpression = Predicate("context.Get<string>(\"completion_manifest\") != null"), RequiredOutputFamilies = ["completion_manifest"], ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal) { ["completion_manifest"] = "nonEmptyString" }, InstanceBinding = "current_workflow_instance", FailureGuidance = new WorkflowGateFailureGuidance { Summary = "The completion artifact was not recorded.", NextAction = "Check the artifact path and run the delivery step again.", EvidenceReferences = [new WorkflowEvidenceReference { Path = "workflow.demo.json", StartLine = 1, EndLine = 1, Quote = "completion_manifest" }] } };

        var blockedGate = new WorkflowValidationGate { Description = "A blocked route must retain the earlier tool summary.", PassExpression = Predicate("context.Get<string>(\"tool_summary\") != null"), RequiredOutputFamilies = ["tool_summary"], ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal) { ["tool_summary"] = "nonEmptyString" }, InstanceBinding = "current_workflow_instance", FailureGuidance = new WorkflowGateFailureGuidance { Summary = "The blocked route did not retain the available tool summary.", NextAction = "Keep the previous tool result in context before waiting for the external result.", EvidenceReferences = [new WorkflowEvidenceReference { Path = "workflow.demo.json", StartLine = 1, EndLine = 1, Quote = "tool_summary" }] } };

return new WorkflowInstance { InstanceId = "workflow-schema-demo-complex", TemplateKind = "schema-demo", RuntimeBinding = input.RuntimeBinding, RuntimeVersion = input.RuntimeVersion, ContractBinding = input.ContractBinding, StartNodeId = start.Id, CurrentNodeId = start.Id, EndNodeId = done.Id, Status = WorkflowStatus.ReadyToStart, Context = context, Validation = new WorkflowValidationContract { Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal) { ["gate.final"] = finalGate, ["gate.blocked"] = blockedGate }, Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal) { ["success"] = new WorkflowRouteValidationProfile { RequiredTerminalGateIds = ["gate.final"] }, ["blocked"] = new WorkflowRouteValidationProfile { RequiredBlockedGateIds = ["gate.blocked"] } }, ReservedRuntimeOwnedFields = ["workflow_file", "event_log_file"] }, Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [start.Id] = start, [prepared.Id] = prepared, [persisted.Id] = persisted, [tool.Id] = tool, [model.Id] = model, [wait.Id] = wait, [review.Id] = review, [rework.Id] = rework, [artifact.Id] = artifact, [done.Id] = done, [memoryRead.Id] = memoryRead, [stateUpdate.Id] = stateUpdate, [memoryWrite.Id] = memoryWrite, [toolCall.Id] = toolCall, [modelThink.Id] = modelThink, [waitResume.Id] = waitResume, [approve.Id] = approve, [reworkBranch.Id] = reworkBranch, [resetRework.Id] = resetRework, [emitArtifact.Id] = emitArtifact } };

    }

}
