using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpWorkflowToolBoundaryTests
{
    [Fact]
    public async Task Server_RejectsInvalidInitializeAndNotificationIds()
    {
        var registry = WorkflowMcpToolSet.Create("so");
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"notifications/cancelled\"}"));
        using var output = new StringWriter();

        await new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        using var initializeError = JsonDocument.Parse(lines[0]);
        Assert.Equal(-32602, initializeError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        using var listError = JsonDocument.Parse(lines[1]);
        Assert.Equal(-32002, listError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        using var cancelledError = JsonDocument.Parse(lines[2]);
        Assert.Equal(-32600, cancelledError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Server_RejectsInlineJsonWithoutReturningTheSubmittedValue()
    {
        var registry = WorkflowMcpToolSet.Create("so");
        using var input = new StringReader(string.Join(
            Environment.NewLine,
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-client\",\"version\":\"1.0.0\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"so_get_workflow_status\",\"arguments\":{\"operation_id\":\"op-boundary-1\",\"workflow_file\":\"{\\\"secret\\\":\\\"value\\\"}\"}}}"));
        using var output = new StringWriter();

        await new McpStdioServer(
            registry,
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync();

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var response = JsonDocument.Parse(lines[1]);
        var result = response.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("file path only", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("value", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectWorkflowTool_ReturnsSummaryWithoutContextValuesByDefault()
    {
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-fragment-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(
                workflowFile,
                "{\"instanceId\":\"fragment-test\",\"status\":\"readyToStart\",\"version\":1,\"nodes\":{},\"context\":{\"secret\":\"large-context-value\"}}");
            var registry = WorkflowMcpToolSet.Create("so");
            Assert.True(registry.TryGet("so_inspect_workflow_fragment", out var tool));
            var escapedPath = workflowFile.Replace("\\", "\\\\", StringComparison.Ordinal);
            using var argumentsDocument = JsonDocument.Parse($"{{\"operation_id\":\"op-fragment-1\",\"workflow_file\":\"{escapedPath}\"}}");

            var result = await tool!.InvokeAsync(argumentsDocument.RootElement.Clone());

            var text = result.Content[0].Text;
            Assert.False(result.IsError);
            Assert.Contains("fragment-test", text, StringComparison.Ordinal);
            Assert.Contains("secret", text, StringComparison.Ordinal);
            Assert.DoesNotContain("large-context-value", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:/outside")]
    [InlineData("operation/id")]
    [InlineData("/absolute/path")]
    [InlineData("operation\nid")]
    public void OperationId_RejectsPathTraversalAndUnsafeCharacters(string operationId)
    {
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new { operation_id = operationId }));

        var exception = Assert.Throws<McpToolInputException>(() =>
            McpToolArguments.RequiredOperationId(arguments.RootElement));

        Assert.Contains("ASCII", exception.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void OperationId_RejectsValuesLongerThan128Characters()
    {
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new { operation_id = new string('x', 129) }));

        Assert.Throws<McpToolInputException>(() =>
            McpToolArguments.RequiredOperationId(arguments.RootElement));
    }

    [Fact]
    public async Task Server_StopsCleanlyWhenCancellationIsRequested()
    {
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await new McpStdioServer(
            new McpToolRegistry(),
            new McpStdioServerOptions("test-server", "1.0.0"),
            input,
            output).RunAsync(cancellation.Token);

        Assert.Empty(output.ToString());
    }
    [Fact]
    public void ResumeWorkflowTool_DeclaresThreeExternalResultSources()
    {
        var registry = WorkflowMcpToolSet.Create("so");
        Assert.True(registry.TryGet("so_resume_workflow", out var tool));

        var schema = tool!.Definition.InputSchema;
        Assert.Equal(3, schema.GetProperty("oneOf").GetArrayLength());
        var sourceNames = schema.GetProperty("oneOf")
            .EnumerateArray()
            .SelectMany(static choice => choice.GetProperty("required").EnumerateArray())
            .Select(static name => name.GetString())
            .Where(static name => name is not null)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("result_file", sourceNames);
        Assert.Contains("ask_id", sourceNames);
        Assert.Contains("offline_submission_file", sourceNames);
    }

    [Fact]
    public async Task ResumeWorkflowTool_RejectsMultipleSuppliedSourcesEvenWhenOneIsEmpty()
    {
        var registry = WorkflowMcpToolSet.Create("so", static () => new AskScopedSubmissionStore());
        Assert.True(registry.TryGet("so_resume_workflow", out var tool));
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            operation_id = "mcp-resume-source-check",
            workflow_file = Path.Combine(Path.GetTempPath(), "missing-workflow.json"),
            result_file = string.Empty,
            ask_id = new string('a', 32),
        }));

        var exception = await Assert.ThrowsAsync<McpToolInputException>(() =>
            tool!.InvokeAsync(arguments.RootElement.Clone()));

        Assert.Contains("exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeWorkflowTool_ResumesFromAskIdAndOfflineSubmissionFile()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-ask-resume-{Guid.NewGuid():N}");
        var askStoreRoot = Path.Combine(testRoot, "asks");
        Directory.CreateDirectory(testRoot);
        try
        {
            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = askStoreRoot });
            var registry = WorkflowMcpToolSet.Create(
                "so",
                () => new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = askStoreRoot }));
            Assert.True(registry.TryGet("so_resume_workflow", out var tool));

            var askFixture = await CreateWaitingAskWorkflowAsync(testRoot, "mcp-ask-id");
            var askLaunch = await store.GetOrCreateForWaitAsync(askFixture.Instance, askFixture.WaitGroup);
            await store.SubmitAsync(
                askLaunch.AskId,
                askLaunch.MachineCapability,
                askLaunch.Generation,
                "mcp-ask-id-submit",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.displayName"] = new AskScopedAnswerValue
                    {
                        Value = JsonSerializer.SerializeToElement("Ada"),
                    },
                });
            using var askArguments = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                operation_id = "mcp-resume-ask-id",
                workflow_file = askFixture.WorkflowFile,
                ask_id = askLaunch.AskId,
            }));

            var askResult = await tool!.InvokeAsync(askArguments.RootElement.Clone());

            Assert.False(askResult.IsError);
            using (var askPayload = JsonDocument.Parse(askResult.Content[0].Text))
            {
                Assert.Equal("completed", askPayload.RootElement.GetProperty("status").GetString());
            }
            var askInstance = await CanonicalWorkflowFileStore.LoadAsync(askFixture.WorkflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, askInstance.Status);
            Assert.Equal("Ada", Assert.IsType<string>(PathValueAccessor.GetValue(askInstance.Context, "answers.displayName")));
            Assert.NotNull((await store.GetSnapshotAsync(askLaunch.AskId, askLaunch.MachineCapability)).AppliedAtUtc);

            var offlineFixture = await CreateWaitingAskWorkflowAsync(testRoot, "mcp-offline-ask");
            var offlineLaunch = await store.GetOrCreateForWaitAsync(offlineFixture.Instance, offlineFixture.WaitGroup);
            var submission = new AskScopedOfflineSubmission(
                SchemaVersion: 1,
                AskId: offlineLaunch.AskId,
                ExpectedGeneration: offlineLaunch.Generation,
                OperationId: "mcp-offline-submit",
                Answers: new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.displayName"] = new AskScopedAnswerValue
                    {
                        Value = JsonSerializer.SerializeToElement("Grace"),
                    },
                });
            var submissionFile = Path.Combine(testRoot, "offline-submission.json");
            await File.WriteAllTextAsync(
                submissionFile,
                JsonSerializer.Serialize(submission, WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));
            using var offlineArguments = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                operation_id = "mcp-resume-offline",
                workflow_file = offlineFixture.WorkflowFile,
                offline_submission_file = submissionFile,
            }));

            var offlineResult = await tool.InvokeAsync(offlineArguments.RootElement.Clone());

            Assert.False(offlineResult.IsError);
            using (var offlinePayload = JsonDocument.Parse(offlineResult.Content[0].Text))
            {
                Assert.Equal("completed", offlinePayload.RootElement.GetProperty("status").GetString());
            }
            var offlineInstance = await CanonicalWorkflowFileStore.LoadAsync(offlineFixture.WorkflowFile);
            Assert.Equal(WorkflowStatus.Succeeded, offlineInstance.Status);
            Assert.Equal("Grace", Assert.IsType<string>(PathValueAccessor.GetValue(offlineInstance.Context, "answers.displayName")));
            Assert.NotNull((await store.GetSnapshotAsync(offlineLaunch.AskId, offlineLaunch.MachineCapability)).AppliedAtUtc);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static async Task<(string WorkflowFile, WorkflowInstance Instance, PendingWaitGroup WaitGroup)> CreateWaitingAskWorkflowAsync(
        string testRoot,
        string instanceId)
    {
        var transition = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask for display name",
            TargetNodeId = "state.done",
            StepKind = WorkflowStepKind.AskUser,
            GuardExpression = "true",
            SucceedExpression = "true",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "ask_user",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["requiredInputs"] = new[] { "answers.displayName" },
                },
            },
            UserInput = new UserInputContract
            {
                Version = 1,
                QuestionGroups =
                [
                    new UserInputQuestionGroup
                    {
                        Id = "group.identity",
                        Title = "Identity",
                        Questions =
                        [
                            new UserInputQuestion
                            {
                                Id = "question.displayName",
                                Context = "Collect the user's preferred display name.",
                                Intent = "Use the name in the workflow context.",
                                Prompt = "What name should we use?",
                                ContextPath = "answers.displayName",
                                Type = UserInputQuestionTypes.Text,
                                Required = true,
                                Constraints = new UserInputQuestionConstraints { MinLength = 1 },
                            },
                        ],
                    },
                ],
            },
        };
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "01 Start",
            Groups = [new TransitionGroup { Id = "group.start", TransitionIds = [transition.Id] }],
        };
        var done = new StateNode { Id = "state.done", Name = "Done", WorkflowPhase = "02 Done", Groups = [] };
        var waitGroup = new PendingWaitGroup
        {
            InstanceId = instanceId,
            TransitionId = transition.Id,
            CorrelationKey = instanceId + "-correlation",
            TargetStateId = done.Id,
        };
        waitGroup.AddEntry(expireAt: null);
        var instance = new WorkflowInstance
        {
            InstanceId = instanceId,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.WaitingExternal,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [transition.Id] = transition,
            },
            ActiveWaitGroups = [waitGroup],
        };
        var workflowFile = Path.Combine(testRoot, instanceId + ".json");
        await CanonicalWorkflowFileStore.SaveAsync(workflowFile, instance);
        return (workflowFile, instance, waitGroup);
    }
}
