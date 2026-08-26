using System.Reflection;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowSchemaDemoExporter
{
    public const string SchemaFileName = "workflow.schema.json";
    public const string DemoFileName = "workflow.demo.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions();

    public static async Task<WorkflowSchemaDemoExportResult> WriteAsync(
        string outputDirectory,
        string runtimeBinding,
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

        var schemaFile = Path.Combine(normalizedOutputDirectory, SchemaFileName);
        var demoFile = Path.Combine(normalizedOutputDirectory, DemoFileName);
        var schemaJson = JsonSerializer.Serialize(CreateSchemaContract(), JsonOptions);
        var demoJson = WorkflowJsonSerializer.Serialize(CreateDemoWorkflow(runtimeBinding));

        await WriteAtomicallyAsync(schemaFile, schemaJson, ct).ConfigureAwait(false);
        await WriteAtomicallyAsync(demoFile, demoJson, ct).ConfigureAwait(false);

        return new WorkflowSchemaDemoExportResult(normalizedOutputDirectory, schemaFile, demoFile);
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
            CompileRules:
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
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.main",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.echo"],
                },
            ],
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "02 Complete",
            Groups = [],
        };
        var echo = new CommandTransition
        {
            Id = "transition.echo",
            Name = "Echo message",
            Description = "Run one built-in tool and store its result.",
            WorkflowPhase = "01 Start",
            TargetNodeId = done.Id,
            OutputPath = "toolResult",
            StepKind = WorkflowStepKind.ToolCall,
            GuardExpression = new ExpressionDefinition
            {
                Kind = "predicate",
                Source = "true",
                ResultType = "bool",
            },
            SucceedExpression = new ExpressionDefinition
            {
                Kind = "predicate",
                Source = "true",
                ResultType = "bool",
            },
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "echo",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["message"] = "hello",
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "workflow-schema-demo",
            RuntimeBinding = runtimeBinding,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [echo.Id] = echo,
            },
        };
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
    string SchemaFile,
    string DemoFile);

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
    IReadOnlyList<string> CompileRules);
