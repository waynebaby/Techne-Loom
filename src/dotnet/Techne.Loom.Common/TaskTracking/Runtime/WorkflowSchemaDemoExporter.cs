using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowSchemaDemoExporter
{
    public const string SchemaFileName = "workflow.schema.json";
    public const string DemoFileName = "workflow.demo.json";
    public const string ModelFileName = "workflow.model.cs";
    public const string BuilderScriptFileName = "workflow.demo.cs";
    public const string VerifierScriptFileName = "workflow.demo.verify.cs";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions();

    public static Task<WorkflowSchemaDemoExportResult> WriteAsync(

        string outputDirectory,

        string runtimeBinding,

        CancellationToken ct = default)

        => WriteAsync(outputDirectory, runtimeBinding, runtimeVersion: null, ct);



    public static async Task<WorkflowSchemaDemoExportResult> WriteAsync(

        string outputDirectory,

        string runtimeBinding,

        string? runtimeVersion,

        CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(outputDirectory))

        {

            throw new InvalidOperationException("A non-empty schema/demo output directory is required.");

        }



        if (string.IsNullOrWhiteSpace(runtimeBinding))

        {

            throw new InvalidOperationException("A non-empty runtime binding is required for the demo workflow.");

        }



        var normalizedOutputDirectory = Path.GetFullPath(outputDirectory);

        Directory.CreateDirectory(normalizedOutputDirectory);



        var schema = CreateSchemaContract();

        var demo = CreateDemoWorkflow(runtimeBinding);
        demo.RuntimeVersion = runtimeVersion;

        var schemaFile = Path.Combine(normalizedOutputDirectory, SchemaFileName);

        var demoFile = Path.Combine(normalizedOutputDirectory, DemoFileName);

        var modelFile = Path.Combine(normalizedOutputDirectory, ModelFileName);

        var builderScriptFile = Path.Combine(normalizedOutputDirectory, BuilderScriptFileName);

        var verifierScriptFile = Path.Combine(normalizedOutputDirectory, VerifierScriptFileName);

        var schemaJson = JsonSerializer.Serialize(schema, JsonOptions);

        var demoJson = WorkflowJsonSerializer.Serialize(demo);

        var modelSource = WorkflowModelReferenceExporter.Generate(schema);

        var builderSource = CreateDemoBuilderScript();

        var verifierSource = CreateDemoVerifierScript();



        await WriteAtomicallyAsync(schemaFile, schemaJson, ct).ConfigureAwait(false);

        await WriteAtomicallyAsync(demoFile, demoJson, ct).ConfigureAwait(false);

        await WriteAtomicallyAsync(modelFile, modelSource, ct).ConfigureAwait(false);

        await WriteAtomicallyAsync(builderScriptFile, builderSource, ct).ConfigureAwait(false);

        await WriteAtomicallyAsync(verifierScriptFile, verifierSource, ct).ConfigureAwait(false);



        var generatedFiles = new[] { schemaFile, demoFile, modelFile, builderScriptFile, verifierScriptFile };

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var generatedFile in generatedFiles)

        {

            var bytes = await File.ReadAllBytesAsync(generatedFile, ct).ConfigureAwait(false);

            hashes[Path.GetFileName(generatedFile)] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        }



        return new WorkflowSchemaDemoExportResult(

            normalizedOutputDirectory,

            runtimeBinding,

            runtimeVersion,

            schemaFile,

            demoFile,

            modelFile,

            builderScriptFile,

            verifierScriptFile,

            hashes);

    }

    public static WorkflowSchemaContract CreateSchemaContract()
    {
        return new WorkflowSchemaContract(
            SchemaId: "techne-loom.workflow-instance",
            SchemaVersion: "1",
            RuntimeType: "Techne.Loom.Abstractions.TaskTracking.Model.WorkflowInstance",
            PropertyNamingPolicy: "camelCase",
            NodeStorage: "object keyed by node id",
            NodeKindDiscriminator: JsonPolymorphicConsts.TypeDiscriminatorPropertyName,
            RootFields: typeof(WorkflowInstance).GetProperties()
                .Select(static property => ToCamelCase(property.Name))
                .ToArray(),
            RequiredRootFields:
            [
                "instanceId",
                "nodes",
                "startNodeId",
                "currentNodeId",
                "status",
                "context",
                "history",
                "version",
                "activeWaitGroups",
            ],
            NodeFields: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [JsonPolymorphicConsts.StateKind] =
                [
                    "$kind", "id", "name", "description", "workflowPhase", "groups", "expiration",
                    "entranceTime", "waitBehavior", "correlationKeyPath", "stateFailedExpression",
                ],
                [JsonPolymorphicConsts.CommandKind] =
                [
                    "$kind", "id", "name", "description", "workflowPhase", "targetNodeId", "outputPath",
                    "priority", "guardExpression", "succeedExpression", "stepKind", "terminalRoutes",
                    "blockedRoutes", "satisfiesGateIds", "publishesOutputFamilies", "publishesBlockedOutputFamilies",
                    "ownedInputMode", "command", "executionTimeout", "currentRetryCount", "maxRetry",
                ],
                [JsonPolymorphicConsts.ExpressionKind] =
                [
                    "$kind", "id", "name", "description", "workflowPhase", "targetNodeId", "outputPath",
                    "priority", "guardExpression", "succeedExpression", "stepKind", "terminalRoutes",
                    "blockedRoutes", "satisfiesGateIds", "publishesOutputFamilies", "publishesBlockedOutputFamilies",
                    "ownedInputMode",
                ],
                [JsonPolymorphicConsts.ToBeRefinedKind] =
                [
                    "$kind", "id", "name", "description", "workflowPhase", "targetNodeId", "outputPath",
                    "priority", "guardExpression", "succeedExpression", "stepKind", "terminalRoutes",
                    "blockedRoutes", "satisfiesGateIds", "publishesOutputFamilies", "publishesBlockedOutputFamilies",
                    "ownedInputMode", "designNotes",
                ],
            },
            RequiredNodeFields: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [JsonPolymorphicConsts.StateKind] = ["$kind", "id", "workflowPhase", "groups"],
                [JsonPolymorphicConsts.CommandKind] = ["$kind", "id", "targetNodeId", "stepKind", "guardExpression", "succeedExpression", "command"],
                [JsonPolymorphicConsts.ExpressionKind] = ["$kind", "id", "targetNodeId", "stepKind", "guardExpression", "succeedExpression"],
                [JsonPolymorphicConsts.ToBeRefinedKind] = ["$kind", "id", "targetNodeId", "stepKind", "guardExpression", "succeedExpression", "designNotes"],
            },
            ExpressionDefinitionFields: ["kind", "source", "entryPoint", "resultType"],
            ExpressionStringInputCompatibility: "A string may be read as compatibility input when the C# binding is explicit; serialization always writes an ExpressionDefinition object.",
            AllowedValues: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["workflowStatus"] = GetEnumValues<WorkflowStatus>(),
                ["concurrencyStrategy"] = GetEnumValues<ConcurrencyStrategy>(),
                ["waitBehavior"] = GetEnumValues<WaitBehavior>(),
                ["workflowStepKind"] = GetEnumValues<WorkflowStepKind>(),
                ["commandInvocationKind"] = GetEnumValues<CommandInvocationKind>(),
            },
            CommandParameterContracts: new Dictionary<string, string>(StringComparer.Ordinal)

            {

                ["updates"] = "object map of context paths to values for StateUpdate or MemoryWrite",

                ["outputBindings"] = "object map of context paths to literal, $result, or $context:<path>",

                ["resumeOutputKey"] = "payload-relative path extracted before outputPath projection",

                ["projectionMode"] = "canonical or legacyNested for external resume projection",

                ["requiredInputs"] = "payload-relative or existing context paths",

                ["path"] = "artifact destination path for ArtifactEmit",

                ["content"] = "artifact content for ArtifactEmit",

            },            CompileRules:
            [
                "Every state node must have a non-empty workflowPhase.",
                "Every transition id listed by a state group must identify a node in nodes.",
                "Every non-empty targetNodeId must identify a state node.",
                "Governed templates must declare explicit synchronous guardExpression and succeedExpression predicates.",
                "The root expressionBinding uses C# and detailedCompileFeedbackV1.",
            ]);
    }

    public static WorkflowInstance CreateDemoWorkflow(string runtimeBinding)

    {

        ExpressionDefinition Predicate(string source) => new()

        {

            Kind = "predicate",

            Source = source,

            ResultType = "bool",

        };



        CommandInvocation Invocation(string name, CommandInvocationKind kind, Dictionary<string, object?>? parameters = null) => new()

        {

            Name = name,

            Kind = kind,

            Parameters = parameters,

        };



        var start = new StateNode

        {

            Id = "state.start",

            Name = "Start",

            WorkflowPhase = "01 Intake",

            Groups = [new TransitionGroup { Id = "group.memory_read", TransitionIds = ["transition.memory_read"] }],

        };

        var prepared = new StateNode

        {

            Id = "state.prepared",

            Name = "Prepared",

            WorkflowPhase = "02 Preparation",

            Groups = [new TransitionGroup { Id = "group.state_update", TransitionIds = ["transition.state_update"] }],

        };

        var persisted = new StateNode

        {

            Id = "state.persisted",

            Name = "Persisted",

            WorkflowPhase = "03 Memory",

            Groups = [new TransitionGroup { Id = "group.memory_write", TransitionIds = ["transition.memory_write"] }],

        };

        var tool = new StateNode

        {

            Id = "state.tool",

            Name = "Tool result",

            WorkflowPhase = "04 Tool",

            Groups = [new TransitionGroup { Id = "group.tool", TransitionIds = ["transition.tool_call"] }],

        };

        var model = new StateNode

        {

            Id = "state.model",

            Name = "Model review",

            WorkflowPhase = "05 Model",

            Groups = [new TransitionGroup { Id = "group.model", TransitionIds = ["transition.model_think"] }],

        };

        var wait = new StateNode

        {

            Id = "state.wait_resume",

            Name = "External review",

            WorkflowPhase = "06 External review",

            Groups = [new TransitionGroup { Id = "group.wait", TransitionIds = ["transition.wait_resume"] }],

        };

        var review = new StateNode

        {

            Id = "state.review",

            Name = "Decision",

            WorkflowPhase = "07 Decision",

            Groups = [new TransitionGroup { Id = "group.condition", TransitionIds = ["transition.approve", "transition.rework"] }],

        };

        var rework = new StateNode

        {

            Id = "state.rework",

            Name = "Rework",

            WorkflowPhase = "08 Rework",

            Groups = [new TransitionGroup { Id = "group.rework", TransitionIds = ["transition.reset_rework"] }],

        };

        var artifact = new StateNode

        {

            Id = "state.artifact",

            Name = "Artifact",

            WorkflowPhase = "09 Delivery",

            Groups = [new TransitionGroup { Id = "group.artifact", TransitionIds = ["transition.emit_artifact"] }],

        };

        var done = new StateNode

        {

            Id = "state.done",

            Name = "Done",

            WorkflowPhase = "10 Complete",

            Groups = [],

        };



        var memoryRead = new CommandTransition

        {

            Id = "transition.memory_read",

            Name = "Read request memory",

            Description = "Read the request snapshot before preparing the workflow.",

            WorkflowPhase = "01 Intake",

            TargetNodeId = prepared.Id,

            OutputPath = "memory.snapshot",

            StepKind = WorkflowStepKind.MemoryRead,

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<object>(\"memory.snapshot\") != null"),

            Command = Invocation("memory.read", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["keys"] = new object?[] { "request" },

            }),

        };

        var stateUpdate = new CommandTransition

        {

            Id = "transition.state_update",

            Name = "Prepare plan",

            Description = "Apply preparation updates to the runtime context.",

            WorkflowPhase = "02 Preparation",

            TargetNodeId = persisted.Id,

            OutputPath = "draft.plan",

            StepKind = WorkflowStepKind.StateUpdate,

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<string>(\"draft.plan\") != null"),

            Command = Invocation("state.update", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["draft.plan"] = "prepared",

                    ["needs_rework"] = false,

                },

            }),

        };

        var memoryWrite = new CommandTransition

        {

            Id = "transition.memory_write",

            Name = "Persist preparation",

            Description = "Persist the prepared value through the memory write contract.",

            WorkflowPhase = "03 Memory",

            TargetNodeId = tool.Id,

            OutputPath = "memory.persisted",

            StepKind = WorkflowStepKind.MemoryWrite,

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<string>(\"memory.persisted\") != null"),

            Command = Invocation("memory.write", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["memory.persisted"] = "written",

                },

            }),

        };

        var toolCall = new CommandTransition

        {

            Id = "transition.tool_call",

            Name = "Call tool",

            Description = "Call a deterministic tool and project its result into named context paths.",

            WorkflowPhase = "04 Tool",

            TargetNodeId = model.Id,

            OutputPath = "tool.result",

            StepKind = WorkflowStepKind.ToolCall,

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<string>(\"tool.result\") != null"),

            PublishesOutputFamilies = ["tool_summary"],

            Command = Invocation("echo", CommandInvocationKind.Tool, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["message"] = "tool result",

                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["tool_summary"] = "$result",

                },

            }),

        };

        var modelThink = new CommandTransition

        {

            Id = "transition.model_think",

            Name = "Request model review",

            Description = "Wait for a model result and project the named resume result canonically.",

            WorkflowPhase = "05 Model",

            TargetNodeId = wait.Id,

            OutputPath = "model.result",

            StepKind = WorkflowStepKind.ModelThink,

            OwnedInputMode = "runtime",

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<string>(\"model.result\") != null"),

            BlockedRoutes = ["blocked"],

            SatisfiesGateIds = ["gate.blocked"],

            PublishesOutputFamilies = ["model_summary"],

            PublishesBlockedOutputFamilies = ["tool_summary"],

            Command = Invocation("model.review", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["requiredInputs"] = new object?[] { "tool_summary", "result" },

                ["resumeOutputKey"] = "result",

                ["projectionMode"] = "canonical",

                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["model_summary"] = "$result",

                    ["tool_summary"] = "$context:tool_summary",

                },

            }),

        };

        var waitResume = new CommandTransition

        {

            Id = "transition.wait_resume",

            Name = "Wait for external approval",

            Description = "Wait for a second external result before choosing the delivery route.",

            WorkflowPhase = "06 External review",

            TargetNodeId = review.Id,

            OutputPath = "review.result",

            StepKind = WorkflowStepKind.WaitResume,

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<string>(\"review.result\") != null"),

            BlockedRoutes = ["blocked"],

            SatisfiesGateIds = ["gate.blocked"],

            PublishesOutputFamilies = ["review_summary"],

            PublishesBlockedOutputFamilies = ["tool_summary"],

            Command = Invocation("review.wait", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["requiredInputs"] = new object?[] { "result" },

                ["resumeOutputKey"] = "result",

                ["projectionMode"] = "canonical",

                ["outputBindings"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["review_summary"] = "$result",

                    ["tool_summary"] = "$context:tool_summary",

                },

            }),

        };

        var approve = new CommandTransition

        {

            Id = "transition.approve",

            Name = "Approve delivery",

            Description = "Take the delivery branch when no rework is needed.",

            WorkflowPhase = "07 Decision",

            TargetNodeId = artifact.Id,

            StepKind = WorkflowStepKind.ConditionBranch,

            GuardExpression = Predicate("!context.Get<bool>(\"needs_rework\")"),

            SucceedExpression = Predicate("true"),

            Command = Invocation("noop", CommandInvocationKind.NativeCode),

        };

        var reworkBranch = new CommandTransition

        {

            Id = "transition.rework",

            Name = "Request rework",

            Description = "Take the rework branch when the context asks for another pass.",

            WorkflowPhase = "07 Decision",

            TargetNodeId = rework.Id,

            StepKind = WorkflowStepKind.ConditionBranch,

            GuardExpression = Predicate("context.Get<bool>(\"needs_rework\")"),

            SucceedExpression = Predicate("true"),

            Command = Invocation("noop", CommandInvocationKind.NativeCode),

        };

        var resetRework = new CommandTransition

        {

            Id = "transition.reset_rework",

            Name = "Reset rework flag",

            Description = "Record the rework pass and clear the branch flag before returning to review.",

            WorkflowPhase = "08 Rework",

            TargetNodeId = review.Id,

            OutputPath = "needs_rework",

            StepKind = WorkflowStepKind.MemoryWrite,

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<bool>(\"needs_rework\") == false"),

            Command = Invocation("memory.write.rework", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)

                {

                    ["needs_rework"] = false,

                },

            }),

        };

        var emitArtifact = new CommandTransition

        {

            Id = "transition.emit_artifact",

            Name = "Emit completion artifact",

            Description = "Write the final artifact and use its path as explicit gate evidence.",

            WorkflowPhase = "09 Delivery",

            TargetNodeId = done.Id,

            OutputPath = "completion_manifest",

            StepKind = WorkflowStepKind.ArtifactEmit,

            TerminalRoutes = ["success"],

            SatisfiesGateIds = ["gate.final"],

            PublishesOutputFamilies = ["completion_manifest"],

            GuardExpression = Predicate("true"),

            SucceedExpression = Predicate("context.Get<string>(\"completion_manifest\") != null"),

            Command = Invocation("artifact.emit", CommandInvocationKind.NativeCode, new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["path"] = "complex-demo-artifact.txt",

                ["content"] = "complex demo artifact",

            }),

        };



        var finalGate = new WorkflowValidationGate

        {

            Description = "The final artifact path must be present and non-empty.",

            PassExpression = Predicate("context.Get<string>(\"completion_manifest\") != null"),

            RequiredOutputFamilies = ["completion_manifest"],

            ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)

            {

                ["completion_manifest"] = "nonEmptyString",

            },

            InstanceBinding = "current_workflow_instance",

            FailureGuidance = new WorkflowGateFailureGuidance

            {

                Summary = "The completion artifact was not recorded.",

                NextAction = "Check the artifact path and run the delivery step again.",

                EvidenceReferences = [new WorkflowEvidenceReference { Path = "workflow.demo.json", StartLine = 1, EndLine = 1, Quote = "completion_manifest" }],

            },

        };

        var blockedGate = new WorkflowValidationGate

        {

            Description = "A blocked route must retain the earlier tool summary.",

            PassExpression = Predicate("context.Get<string>(\"tool_summary\") != null"),

            RequiredOutputFamilies = ["tool_summary"],

            ValueSemantics = new Dictionary<string, string>(StringComparer.Ordinal)

            {

                ["tool_summary"] = "nonEmptyString",

            },

            InstanceBinding = "current_workflow_instance",

            FailureGuidance = new WorkflowGateFailureGuidance

            {

                Summary = "The blocked route did not retain the available tool summary.",

                NextAction = "Keep the previous tool result in context before waiting for the external result.",

                EvidenceReferences = [new WorkflowEvidenceReference { Path = "workflow.demo.json", StartLine = 1, EndLine = 1, Quote = "tool_summary" }],

            },

        };



        return new WorkflowInstance

        {

            InstanceId = "workflow-schema-demo-complex",

            TemplateKind = "so-governed-target-skill",

            RuntimeBinding = runtimeBinding,

            StartNodeId = start.Id,

            CurrentNodeId = start.Id,

            EndNodeId = done.Id,

            Status = WorkflowStatus.ReadyToStart,

            Context = new Dictionary<string, object?>(StringComparer.Ordinal)

            {

                ["request"] = "complex demo request",

                ["needs_rework"] = false,

            },

            Validation = new WorkflowValidationContract

            {

                Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal)

                {

                    ["gate.final"] = finalGate,

                    ["gate.blocked"] = blockedGate,

                },

                Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal)

                {

                    ["success"] = new WorkflowRouteValidationProfile { RequiredTerminalGateIds = ["gate.final"] },

                    ["blocked"] = new WorkflowRouteValidationProfile { RequiredBlockedGateIds = ["gate.blocked"] },

                },

                ReservedRuntimeOwnedFields = ["workflow_file", "event_log_file"],

            },

            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)

            {

                [start.Id] = start,

                [prepared.Id] = prepared,

                [persisted.Id] = persisted,

                [tool.Id] = tool,

                [model.Id] = model,

                [wait.Id] = wait,

                [review.Id] = review,

                [rework.Id] = rework,

                [artifact.Id] = artifact,

                [done.Id] = done,

                [memoryRead.Id] = memoryRead,

                [stateUpdate.Id] = stateUpdate,

                [memoryWrite.Id] = memoryWrite,

                [toolCall.Id] = toolCall,

                [modelThink.Id] = modelThink,

                [waitResume.Id] = waitResume,

                [approve.Id] = approve,

                [reworkBranch.Id] = reworkBranch,

                [resetRework.Id] = resetRework,

                [emitArtifact.Id] = emitArtifact,

            },

        };

    }

    public static string CreateDemoBuilderScript()

    {

        return """

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

return new WorkflowInstance { InstanceId = "workflow-schema-demo-complex", TemplateKind = "so-governed-target-skill", RuntimeBinding = input.RuntimeBinding, RuntimeVersion = input.RuntimeVersion, StartNodeId = start.Id, CurrentNodeId = start.Id, EndNodeId = done.Id, Status = WorkflowStatus.ReadyToStart, Context = context, Validation = new WorkflowValidationContract { Gates = new Dictionary<string, WorkflowValidationGate>(StringComparer.Ordinal) { ["gate.final"] = finalGate, ["gate.blocked"] = blockedGate }, Routes = new Dictionary<string, WorkflowRouteValidationProfile>(StringComparer.Ordinal) { ["success"] = new WorkflowRouteValidationProfile { RequiredTerminalGateIds = ["gate.final"] }, ["blocked"] = new WorkflowRouteValidationProfile { RequiredBlockedGateIds = ["gate.blocked"] } }, ReservedRuntimeOwnedFields = ["workflow_file", "event_log_file"] }, Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal) { [start.Id] = start, [prepared.Id] = prepared, [persisted.Id] = persisted, [tool.Id] = tool, [model.Id] = model, [wait.Id] = wait, [review.Id] = review, [rework.Id] = rework, [artifact.Id] = artifact, [done.Id] = done, [memoryRead.Id] = memoryRead, [stateUpdate.Id] = stateUpdate, [memoryWrite.Id] = memoryWrite, [toolCall.Id] = toolCall, [modelThink.Id] = modelThink, [waitResume.Id] = waitResume, [approve.Id] = approve, [reworkBranch.Id] = reworkBranch, [resetRework.Id] = resetRework, [emitArtifact.Id] = emitArtifact } };

    }

}

""";

    }



    public static string CreateDemoVerifierScript()

    {

        return """

public static class WorkflowDemoVerifier

{

    public static WorkflowScriptVerificationResult Verify(

        WorkflowInstance actual,

        WorkflowInstance reference,

        WorkflowModelReference model)

    {

        var suite = new WorkflowScriptVerificationSuite();

        var transitions = actual.GetTransitionNodes().Values;

        suite.Check("demo.schema", model.SchemaId == "techne-loom.workflow-instance", "The candidate uses the current workflow schema.", "demo");

        suite.Check("demo.runtime_binding", actual.RuntimeBinding == model.RuntimeBinding, "The candidate uses the selected runtime binding.", "demo");

        suite.Check("demo.memory_read", transitions.Any(item => item.StepKind == WorkflowStepKind.MemoryRead), "The demo contains a MemoryRead step.", "demo");

        suite.Check("demo.state_update", transitions.Any(item => item.StepKind == WorkflowStepKind.StateUpdate), "The demo contains a StateUpdate step.", "demo");

        suite.Check("demo.memory_write", transitions.Any(item => item.StepKind == WorkflowStepKind.MemoryWrite), "The demo contains a MemoryWrite step.", "demo");

        suite.Check("demo.tool_projection", transitions.OfType<CommandTransition>().Any(item => item.StepKind == WorkflowStepKind.ToolCall && item.OutputPath == "tool.result"), "The tool step has an output path.", "demo");

        suite.Check("demo.canonical_resume", transitions.OfType<CommandTransition>().Where(item => item.StepKind == WorkflowStepKind.ModelThink || item.StepKind == WorkflowStepKind.WaitResume).All(item => item.Command.Parameters is not null && item.Command.Parameters.ContainsKey("resumeOutputKey") && item.Command.Parameters.ContainsKey("projectionMode")), "External steps declare canonical resume projection fields.", "demo");

        suite.Check("demo.blocked_route", transitions.Any(item => item.BlockedRoutes?.Contains("blocked") == true), "The demo includes a blocked route.", "demo");

        suite.Check("demo.rework_loop", actual.Nodes.ContainsKey("transition.rework") && actual.Nodes.ContainsKey("transition.reset_rework"), "The demo includes a rework loop.", "demo");

        suite.Check("demo.artifact", transitions.OfType<CommandTransition>().Any(item => item.StepKind == WorkflowStepKind.ArtifactEmit), "The demo emits an artifact.", "demo");

        suite.Check("demo.gates", actual.Validation?.Gates.ContainsKey("gate.final") == true && actual.Validation.Gates.ContainsKey("gate.blocked"), "The demo contains terminal and blocked gates.", "demo");

        suite.Check("demo.reference_shape", actual.Nodes.Count == reference.Nodes.Count, "The candidate and reference have the same node count.", "demo", reference.Nodes.Count.ToString(), actual.Nodes.Count.ToString());

        return suite.Complete(null, "workflow.demo.json");

    }

}

""";

    }

    private static IReadOnlyList<string> GetEnumValues<TEnum>()
        where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .Select(static value => JsonSerializer.Serialize(value, JsonOptions).Trim('"'))
            .ToArray();
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return string.Create(value.Length, value, static (buffer, source) =>
        {
            buffer[0] = char.ToLowerInvariant(source[0]);
            source.AsSpan(1).CopyTo(buffer[1..]);
        });
    }

    private static async Task WriteAtomicallyAsync(string targetPath, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"Unable to resolve output directory for '{targetPath}'.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, Utf8WithoutBom, ct).ConfigureAwait(false);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public sealed record WorkflowSchemaDemoExportResult(

    string OutputDirectory,

    string RuntimeBinding,

    string? RuntimeVersion,

    string SchemaFile,

    string DemoFile,

    string ModelFile,

    string BuilderScriptFile,

    string VerifierScriptFile,

    IReadOnlyDictionary<string, string> Sha256);

public sealed record WorkflowSchemaContract(
    string SchemaId,
    string SchemaVersion,
    string RuntimeType,
    string PropertyNamingPolicy,
    string NodeStorage,
    string NodeKindDiscriminator,
    IReadOnlyList<string> RootFields,
    IReadOnlyList<string> RequiredRootFields,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NodeFields,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredNodeFields,
    IReadOnlyList<string> ExpressionDefinitionFields,
    string ExpressionStringInputCompatibility,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedValues,
    IReadOnlyDictionary<string, string> CommandParameterContracts,
    IReadOnlyList<string> CompileRules);
