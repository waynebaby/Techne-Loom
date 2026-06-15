using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;
using Techne.Loom.SkillOrchestrator.Visualizer;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SkillOrchestratorBehaviorTests
{
    [Fact]
    public void WorkflowJsonSerializer_NormalizesObjectContainers()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "wf-json",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                },
                ["transition.ask"] = new CommandTransition
                {
                    Id = "transition.ask",
                    Name = "Ask",
                    StepKind = WorkflowStepKind.AskUser,
                    Command = new CommandInvocation
                    {
                        Kind = CommandInvocationKind.Tool,
                        Name = "noop",
                        Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["requiredInputs"] = new List<object?> { "filePath", "content" },
                            ["updates"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["review.summary"] = "ready",
                            },
                        },
                    },
                },
            },
        };

        var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(instance));
        var transition = Assert.IsType<CommandTransition>(roundTrip.Nodes["transition.ask"]);
        var requiredInputs = Assert.IsAssignableFrom<IEnumerable<object?>>(transition.Command.Parameters!["requiredInputs"]);
        Assert.Equal(["filePath", "content"], requiredInputs.Select(Convert.ToString));

        var updates = Assert.IsAssignableFrom<IDictionary<string, object?>>(transition.Command.Parameters["updates"]);
        Assert.Equal("ready", updates["review.summary"]);
    }

    [Fact]
    public async Task StartOrAdvanceAsync_TimeoutGroup_MovesToTimeoutTarget()
    {
        var instance = CreateImmediateTimeoutWorkflow();
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance);
        var engine = new DefaultTaskTrackingEngine(store);
        var service = new DefaultWorkflowTaskTrackingService(engine);

        var first = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.True(first.Suspended);
        Assert.Equal(WorkflowStatus.WaitingExternal, first.StatusProjection.Status);

        var second = await service.StartOrAdvanceAsync(instance.InstanceId);
        Assert.False(second.Suspended);
        Assert.False(second.Failed);
        Assert.Equal(WorkflowStatus.Succeeded, second.StatusProjection.Status);
        Assert.Equal("state.timeout", second.StatusProjection.CurrentNodeId);

        var saved = await service.GetInstanceAsync(instance.InstanceId);
        Assert.NotNull(saved);
        Assert.Empty(saved!.ActiveWaitGroups);
    }

    [Fact]
    public async Task MermaidVisualizer_UsesOwningStateForChainedTransitions()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateChainedWorkflow());

        Assert.Contains("state.start -->|First| state.mid", mermaid);
        Assert.Contains("state.mid -->|Second| state.done", mermaid);
        Assert.DoesNotContain("state.start -->|Second| state.done", mermaid);
    }

    [Fact]
    public async Task HtmlVisualizer_ShowsSourceToTargetTransitionChain()
    {
        var html = await new HtmlWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateChainedWorkflow());

        Assert.Contains("<td>Start</td><td>First</td><td>Mid</td>", html);
        Assert.Contains("<td>Mid</td><td>Second</td><td>Done</td>", html);
    }

    [Fact]
    public async Task MermaidVisualizer_DoesNotProjectUnownedTransitionsFromStart()
    {
        var mermaid = await new MermaidWorkflowInstanceVisualizer().VisualizeToStringAsync(CreateChainedWorkflow(includeUnownedTransition: true));

        Assert.DoesNotContain("state.start -->|Detached| state.done", mermaid);
        Assert.DoesNotContain("-->|Detached|", mermaid);
    }

    [Fact]
    public async Task CliRun_CommandLineWorkflow_EmitsWrappedExecAndEscapesOutput()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateEscapedCommandWorkflow()));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" run --workflow-file \"{workflowPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("<wrapped_exec>", stdout);
        Assert.Contains($"<commandline>{GetEscapedCommandPrefix()}", stdout);
        Assert.Contains("[stdout] &lt;danger&gt;", stdout);
        Assert.Contains("[stderr] error-line", stdout);
        Assert.Contains("<so_property>", stdout);
        Assert.DoesNotContain("<danger>", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr));
    }

    [Fact]
    public async Task CliRun_InvalidCommand_EmitsStableErrorProperty()
    {
        var repoRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" nope",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(2, process.ExitCode);
        Assert.Contains("<so_property>", stdout);
        Assert.Contains("\"type\":\"error\"", stdout);
        Assert.DoesNotContain("Unhandled exception", stdout);
        Assert.DoesNotContain("Stack Trace", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr));
    }

    [Fact]
    public async Task CliRun_NoProgressWorkflow_EmitsBoundaryInsteadOfResult()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-noprogress-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateNoProgressWorkflow()));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" run --workflow-file \"{workflowPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(3, process.ExitCode);
        Assert.Contains("<so_property>", stdout);
        Assert.Contains("\"type\":\"boundary\"", stdout);
        Assert.DoesNotContain("\"type\":\"result\"", stdout);
        Assert.Contains("\"status\":\"blocked\"", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr));

        var persistedWorkflow = await File.ReadAllTextAsync(workflowPath);
        Assert.Contains("\"status\": \"running\"", persistedWorkflow);

        var eventsPath = workflowPath + ".events.jsonl";
        Assert.True(File.Exists(eventsPath));
        var events = await File.ReadAllTextAsync(eventsPath);
        Assert.Contains("Start", events);
    }

    [Fact]
    public async Task CliResume_SnakeCaseEnvelope_WithNestedPayload_CompletesWorkflow()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-resume-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var firstRun = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(3, firstRun.ExitCode);
        Assert.Contains("\"type\":\"boundary\"", firstRun.StdOut);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-resume-payload-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, "{" +
            "\"transition_id\":\"transition.ask\"," +
            "\"correlation_key\":null," +
            "\"payload\":{\"review\":{\"approved\":true}}" +
            "}");

        var resumeRun = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowPath}\" --result-file \"{resultFile}\"");
        Assert.Equal(0, resumeRun.ExitCode);
        Assert.Contains("\"type\":\"result\"", resumeRun.StdOut);
        Assert.Contains("\"status\":\"completed\"", resumeRun.StdOut);
    }

    [Fact]
    public async Task CliRun_ContextFile_WithNestedObject_AllowsDottedPathEvaluation()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-context-payload-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateContextWorkflow()));
        await File.WriteAllTextAsync(contextFile, "{\"review\":{\"approved\":true}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"type\":\"result\"", run.StdOut);
        Assert.Contains("\"status\":\"completed\"", run.StdOut);
    }

    [Fact]
    public async Task CliRun_SelfLoopWorkflow_FailsInsteadOfHanging()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-self-loop-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateSelfLoopWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Execution step budget exceeded.", run.StdOut);
    }

    [Fact]
    public async Task CliPlanner_IsRejectedAsUnknownCommand()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "planner");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Unknown command", run.StdOut);
        Assert.Contains("planner", run.StdOut);
    }

    [Fact]
    public async Task CliHelp_ListsExpectedDotnetSoDllParameters()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "--help");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("dotnet so.dll --guide", run.StdOut);
        Assert.Contains("dotnet so.dll --help", run.StdOut);
        Assert.Contains("dotnet so.dll compile", run.StdOut);
        Assert.Contains("dotnet so.dll run", run.StdOut);
        Assert.Contains("dotnet so.dll resume", run.StdOut);
        Assert.Contains("dotnet so.dll status", run.StdOut);
        Assert.Contains("dotnet so.dll inspect-workflow", run.StdOut);
        Assert.Contains("dotnet so.dll inspect-events", run.StdOut);
        Assert.Contains("dotnet so.dll ls", run.StdOut);
        Assert.DoesNotContain("dotnet so.dll planner", run.StdOut);
    }

    [Theory]
    [InlineData("compile", "--workflow-file")]
    [InlineData("run", "--workflow-file")]
    [InlineData("resume", "--workflow-file")]
    [InlineData("status", "--workflow-file")]
    [InlineData("inspect-workflow", "--workflow-file")]
    [InlineData("inspect-events", "--workflow-file")]
    public async Task CliRequiredDotnetSoDllParameters_MissingOptionsReturnStableError(string command, string requiredOption)
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, command);
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("<so_property>", run.StdOut);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Missing required option", run.StdOut);
        Assert.Contains(requiredOption, run.StdOut);
    }

    [Fact]
    public async Task CliCompile_ExistingWorkflowFile_ValidatesWithoutRedrafting()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"status\": \"readyToStart\"", await File.ReadAllTextAsync(workflowFile));
        Assert.DoesNotContain("\"status\": \"drafting\"", await File.ReadAllTextAsync(workflowFile));
        Assert.Contains("Validation artifacts:", run.StdErr);
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
    }

    [Fact]
    public async Task CliGuide_ExportInsideSkillFolder_IsRejectedWithoutWritingFile()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = CreateSkillRoot();
        var exportFile = Path.Combine(skillRoot, "guide-export", "so-guide.md");

        var run = await RunCliAsync(repoRoot, $"--guide --export \"{exportFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--export", run.StdOut);
        Assert.False(File.Exists(exportFile));
    }

    [Fact]
    public async Task CliCompile_ReadOnlyWorkflowFile_SucceedsWithoutMutatingInput()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-readonly-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-readonly-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));
        File.SetAttributes(workflowFile, FileAttributes.ReadOnly);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("Validation artifacts:", run.StdErr);
        }
        finally
        {
            File.SetAttributes(workflowFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task CliCompile_PreexistingAuditArtifacts_FailsWithoutOverwritingFiles()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-existing-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-existing-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var firstRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, firstRun.ExitCode);

        var secondRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, secondRun.ExitCode);
        Assert.Contains("\"type\":\"error\"", secondRun.StdOut);
        Assert.Contains("Refusing to overwrite existing audit artifacts", secondRun.StdOut);
        Assert.Contains("workflow.html", secondRun.StdOut);
        Assert.Contains("Choose a different audit output root", secondRun.StdOut);
    }

    [Fact]
    public async Task CliCompile_DefaultAuditRoot_DoesNotCollideAcrossCliInvocations()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-default-audit-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var firstRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");
        var secondRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(0, firstRun.ExitCode);
        Assert.Equal(0, secondRun.ExitCode);
        Assert.DoesNotContain("Refusing to overwrite existing audit artifacts", secondRun.StdOut);
    }

    [Fact]
    public async Task CliCompile_AuditOutputInsideSkillFolder_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-skill-audit-{Guid.NewGuid():N}.json");
        var skillRoot = CreateSkillRoot();
        var auditDirectory = Path.Combine(skillRoot, "runtime-audit");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--audit-output", run.StdOut);
        Assert.False(Directory.Exists(auditDirectory));
    }

    [Fact]
    public async Task CliCompile_WithDescriptionFile_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var descriptionFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-description-{Guid.NewGuid():N}.md");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-compile-description-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(descriptionFile, "This should be rejected.");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --description-file \"{descriptionFile}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Option", run.StdOut);
        Assert.Contains("--description-file", run.StdOut);
        Assert.Contains("compile", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_InvalidWorkflowStructure_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-invalid-compile-{Guid.NewGuid():N}.json");
        var invalid = CreateResumeWorkflow();
        invalid.StartNodeId = "state.missing";
        invalid.CurrentNodeId = "state.missing";
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(invalid));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("startNodeId", run.StdOut);
        Assert.Contains("state.missing", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_WorkflowPayload_GeneratesConnectedMermaidGraph()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceWorkflowFile = GetWorkflowPayloadPath(repoRoot);
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-payload-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-payload-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, await File.ReadAllTextAsync(sourceWorkflowFile));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);

        var mermaidFile = Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single();
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));

        Assert.StartsWith("```mermaid", mermaid);
        Assert.Contains(Environment.NewLine + "```", mermaid);
        Assert.Contains("flowchart TD", mermaid);
        AssertMermaidStateGraphConnected(mermaid, instance);
    }

    [Fact]
    public async Task CliRun_WithAuditOutput_EmitsAuditArtifactLinks()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-audit-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalSoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var audit = payload.GetProperty("audit_artifacts");
        Assert.Equal(Path.GetFullPath(auditDirectory), audit.GetProperty("output_root").GetString());
        Assert.True(File.Exists(audit.GetProperty("mermaid_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("html_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("workflow_backup_file").GetString()));
    }

    [Fact]
    public async Task CliRun_ProgressPayload_EmitsCurrentWorkflowRenderPaths()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-progress-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-progress-context-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-so-progress-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateContextWorkflow()));
        await File.WriteAllTextAsync(contextFile, "{\"review\":{\"approved\":true}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"type\":\"progress\"", run.StdOut);
        Assert.Contains("workflow.mermaid.md", run.StdOut);
        Assert.Contains("workflow.html", run.StdOut);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Length > 0);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public async Task CliRun_WorkflowFileInsideSkillFolder_IsRejectedWithoutWritingEvents()
    {
        var repoRoot = FindRepositoryRoot();
        var skillRoot = CreateSkillRoot();
        var workflowDirectory = Path.Combine(skillRoot, "assets", "so-workflow");
        Directory.CreateDirectory(workflowDirectory);
        var workflowPath = Path.Combine(workflowDirectory, "workflow.current.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--workflow-file", run.StdOut);
        Assert.False(File.Exists(workflowPath + ".events.jsonl"));
        Assert.Contains("\"status\": \"readyToStart\"", await File.ReadAllTextAsync(workflowPath));
    }

    [Fact]
    public async Task CliRun_BoundaryWithoutMemoryHints_DoesNotLeakWholeContext()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-so-memory-{Guid.NewGuid():N}.json");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-memory-context-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(CreateResumeWorkflow()));
        await File.WriteAllTextAsync(contextFile, "{\"apiToken\":\"secret\",\"largeBlob\":\"very-large\",\"review\":{\"summary\":\"ok\"}}");

        var run = await RunCliAsync(repoRoot, $"run --workflow-file \"{workflowPath}\" --context-file \"{contextFile}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"type\":\"boundary\"", run.StdOut);
        Assert.Contains("\"memory_for_next_step\":{}", run.StdOut);
        Assert.DoesNotContain("apiToken", run.StdOut);
        Assert.DoesNotContain("largeBlob", run.StdOut);
    }

    private static WorkflowInstance CreateImmediateTimeoutWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.ask",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    GroupTimeout = TimeSpan.Zero,
                    TimeoutTargetStateId = "state.timeout",
                    TransitionIds = ["transition.ask"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var timeout = new StateNode
        {
            Id = "state.timeout",
            Name = "Timed Out",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var ask = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask user",
            Description = "Need external input",
            TargetNodeId = timeout.Id,
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = "timeout-wf",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = timeout.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [timeout.Id] = timeout,
                [ask.Id] = ask,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateChainedWorkflow(bool includeUnownedTransition = false)
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    TransitionIds = ["transition.first"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var mid = new StateNode
        {
            Id = "state.mid",
            Name = "Mid",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.mid",
                    TransitionIds = ["transition.second"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var first = new CommandTransition
        {
            Id = "transition.first",
            Name = "First",
            TargetNodeId = mid.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "sample.first",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var second = new CommandTransition
        {
            Id = "transition.second",
            Name = "Second",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.StateUpdate,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "sample.second",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [start.Id] = start,
            [mid.Id] = mid,
            [done.Id] = done,
            [first.Id] = first,
            [second.Id] = second,
        };

        if (includeUnownedTransition)
        {
            nodes["transition.detached"] = new CommandTransition
            {
                Id = "transition.detached",
                Name = "Detached",
                TargetNodeId = done.Id,
                StepKind = WorkflowStepKind.StateUpdate,
                Command = new CommandInvocation
                {
                    Kind = CommandInvocationKind.Tool,
                    Name = "sample.detached",
                    Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
                },
            };
        }

        return new WorkflowInstance
        {
            InstanceId = "chainsample",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = nodes,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateEscapedCommandWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.cmd",
                    TransitionIds = ["transition.cmd"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var end = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var command = new CommandTransition
        {
            Id = "transition.cmd",
            Name = "Danger echo",
            Description = "Emit stdout and stderr",
            TargetNodeId = end.Id,
            OutputPath = "echoOutput",
            StepKind = WorkflowStepKind.ToolCall,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.CommandLine,
                Name = GetEscapedCommandName(),
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["args"] = GetEscapedCommandArguments(),
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"cli-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = end.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [end.Id] = end,
                [command.Id] = command,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateNoProgressWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.never",
                    TransitionIds = ["transition.never"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var transition = new ExpressionTransition
        {
            Id = "transition.never",
            Name = "Never",
            GuardExpression = "false",
            SucceedExpression = "false",
            StepKind = WorkflowStepKind.ConditionBranch,
        };

        return new WorkflowInstance
        {
            InstanceId = $"no-progress-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [transition.Id] = transition,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateResumeWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.ask",
                    TransitionIds = ["transition.ask"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var review = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.review",
                    TransitionIds = ["transition.check"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var ask = new CommandTransition
        {
            Id = "transition.ask",
            Name = "Ask user",
            Description = "Need structured result",
            TargetNodeId = review.Id,
            StepKind = WorkflowStepKind.AskUser,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        var check = new ExpressionTransition
        {
            Id = "transition.check",
            Name = "Check review",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            SucceedExpression = "review.approved == true",
            GuardExpression = "true",
        };

        return new WorkflowInstance
        {
            InstanceId = $"resume-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [review.Id] = review,
                [done.Id] = done,
                [ask.Id] = ask,
                [check.Id] = check,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateContextWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.check",
                    TransitionIds = ["transition.check"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var check = new ExpressionTransition
        {
            Id = "transition.check",
            Name = "Check context",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            SucceedExpression = "review.approved == true",
            GuardExpression = "true",
        };

        return new WorkflowInstance
        {
            InstanceId = $"context-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [check.Id] = check,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static WorkflowInstance CreateSelfLoopWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.loop",
            Name = "Loop",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.loop",
                    TransitionIds = ["transition.loop"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };

        var loop = new ExpressionTransition
        {
            Id = "transition.loop",
            Name = "Loop forever",
            TargetNodeId = start.Id,
            StepKind = WorkflowStepKind.ConditionBranch,
            GuardExpression = "true",
            SucceedExpression = "true",
        };

        return new WorkflowInstance
        {
            InstanceId = $"self-loop-wf-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [loop.Id] = loop,
            },
            Context = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Techne.Loom.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static string GetWorkflowPayloadPath(string repoRoot)
    {
        return Path.Combine(repoRoot, "tests", "dotnet", "Techne.Loom.SkillOrchestrator.Tests", "workflow.payload.json");
    }

    private static string CreateSkillRoot()
    {
        var skillRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-skill-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "# Temp skill\n");
        return skillRoot;
    }

    private static string GetCliAssemblyPath()
    {
        return typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
    }

    private static string GetEscapedCommandName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "bash";

    private static string GetEscapedCommandArguments()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "/c (echo ^<danger^> & echo error-line 1>&2)"
            : "-lc \"printf '<danger>\\n'; printf 'error-line\\n' >&2\"";

    private static string GetEscapedCommandPrefix()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd /c (echo" : "bash -lc";

    private static JsonDocument ReadSoEnvelope(string stdout)
    {
        return ReadSoEnvelopeByType(stdout, null);
    }

    private static JsonDocument ReadFinalSoEnvelope(string stdout)
    {
        return ReadSoEnvelopeByType(stdout, type => !string.Equals(type, "progress", StringComparison.Ordinal));
    }

    private static JsonDocument ReadSoEnvelopeByType(string stdout, Func<string, bool>? predicate)
    {
        const string startTag = "<so_property>";
        const string endTag = "</so_property>";
        var index = 0;
        JsonDocument? fallback = null;

        while (true)
        {
            var startIndex = stdout.IndexOf(startTag, index, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                break;
            }

            var endIndex = stdout.IndexOf(endTag, startIndex, StringComparison.Ordinal);
            if (endIndex <= startIndex)
            {
                break;
            }

            var json = stdout.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length).Trim();
            var document = JsonDocument.Parse(json);
            fallback?.Dispose();
            fallback = document;

            var type = document.RootElement.GetProperty("type").GetString() ?? string.Empty;
            if (predicate is null || predicate(type))
            {
                return document;
            }

            index = endIndex + endTag.Length;
        }

        if (fallback is not null)
        {
            return fallback;
        }

        throw new InvalidOperationException("SO CLI output did not contain a so_property block.");
    }

    private static void AssertMermaidStateGraphConnected(string mermaid, WorkflowInstance instance)
    {
        var stateIds = instance.Nodes.Values
            .OfType<StateNode>()
            .Select(static state => state.Id)
            .ToArray();

        Assert.NotEmpty(stateIds);

        var adjacency = stateIds.ToDictionary(
            static stateId => stateId,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var line in mermaid.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = Regex.Match(line, @"^(?<from>\S+)\s+-->\|.*\|\s+(?<to>\S+)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var from = match.Groups["from"].Value;
            var to = match.Groups["to"].Value;
            if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
            {
                continue;
            }

            adjacency[from].Add(to);
            adjacency[to].Add(from);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(instance.StartNodeId);
        visited.Add(instance.StartNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var disconnectedStates = stateIds.Where(stateId => !visited.Contains(stateId)).ToArray();
        Assert.True(disconnectedStates.Length == 0, $"Mermaid graph contains disconnected states: {string.Join(", ", disconnectedStates)}");
    }
}