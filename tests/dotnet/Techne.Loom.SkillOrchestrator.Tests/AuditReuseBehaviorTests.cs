using System.Diagnostics;
using Techne.Loom.Abstractions.TaskTracking.Model;
using System.Text.Json;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class AuditReuseBehaviorTests
{
    [Fact]
    public async Task CopyStepAsync_CopiesVerifiedArtifactsAndWritesReuseManifest()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-source-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-destination-{Guid.NewGuid():N}");
        var source = await WorkflowAuditArtifactWriter.WriteAsync(
            "audit-reuse-source",
            1,
            "verified",
            "{\"instanceId\":\"audit-reuse-source\"}",
            "flowchart TD\nstart --> done",
            "<html>verified</html>",
            sourceRoot,
            "{\"analysis\":true}",
            CancellationToken.None);

        var reused = await WorkflowAuditArtifactWriter.CopyStepAsync(
            source.StepDirectory,
            "audit-reuse-target",
            4,
            "reused-verified",
            destinationRoot,
            "The source step was verified against the unchanged workflow and guide contract.",
            "test-reviewer");

        Assert.Equal(destinationRoot, reused.OutputRoot);
        Assert.Equal("audit-reuse-target", reused.WorkflowId);
        Assert.NotNull(reused.ReuseManifestFile);
        Assert.Equal(source.StepDirectory, reused.ReusedFromStepDirectory);
        Assert.Equal("test-reviewer", reused.ReuseVerifiedBy);
        Assert.True(File.Exists(reused.MermaidFile));
        Assert.True(File.Exists(reused.HtmlFile));
        Assert.True(File.Exists(reused.WorkflowBackupFile));
        Assert.True(File.Exists(reused.AnalysisFile));
        Assert.True(File.Exists(reused.ReuseManifestFile));

        using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(reused.ReuseManifestFile!));
        var manifest = manifestDocument.RootElement;
        Assert.Equal(source.StepDirectory, manifest.GetProperty("source_step_directory").GetString());
        Assert.Equal(reused.StepDirectory, manifest.GetProperty("destination_step_directory").GetString());
        Assert.Equal("audit-reuse-source", manifest.GetProperty("source_workflow_id").GetString());
        Assert.Equal("test-reviewer", manifest.GetProperty("verified_by").GetString());
        Assert.Equal("verified-copy", manifest.GetProperty("artifact_origin").GetString());
        Assert.False(manifest.GetProperty("official_execution_evidence").GetBoolean());
        Assert.Equal(4, manifest.GetProperty("source_file_sha256").EnumerateObject().Count());
    }

    [Fact]
    public async Task CopyStepAsync_RejectsIncompleteSourceStep()
    {
        var sourceDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-incomplete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "workflow.mermaid.md"), "flowchart TD");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowAuditArtifactWriter.CopyStepAsync(
            sourceDirectory,
            "audit-reuse-target",
            1,
            "reused",
            Path.Combine(Path.GetTempPath(), $"techne-loom-audit-destination-{Guid.NewGuid():N}"),
            "Incomplete source must fail closed.",
            "test-reviewer"));

        Assert.Contains("Missing required files", error.Message, StringComparison.Ordinal);
        Assert.Contains("workflow.html", error.Message, StringComparison.Ordinal);
        Assert.Contains("workflow.json", error.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CopyStepAsync_RejectsDestinationCollision()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-source-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-destination-{Guid.NewGuid():N}");
        var source = await WorkflowAuditArtifactWriter.WriteAsync(
            "audit-reuse-source",
            1,
            "verified",
            "{\"instanceId\":\"audit-reuse-source\"}",
            "flowchart TD\nstart --> done",
            "<html>verified</html>",
            sourceRoot);

        await WorkflowAuditArtifactWriter.CopyStepAsync(
            source.StepDirectory,
            "audit-reuse-target",
            4,
            "reused-verified",
            destinationRoot,
            "First verified copy.",
            "test-reviewer");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowAuditArtifactWriter.CopyStepAsync(
            source.StepDirectory,
            "audit-reuse-target",
            4,
            "reused-verified",
            destinationRoot,
            "Second copy must fail closed.",
            "test-reviewer"));

        Assert.Contains("already contains files or directories", error.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CliCopyAuditStep_ReturnsReuseProvenance()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-source-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-destination-{Guid.NewGuid():N}");
        var source = await WorkflowAuditArtifactWriter.WriteAsync(
            "audit-reuse-source",
            1,
            "verified",
            "{\"instanceId\":\"audit-reuse-source\"}",
            "flowchart TD\nstart --> done",
            "<html>verified</html>",
            sourceRoot);
        var cliAssembly = typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
        var arguments = $"\"{cliAssembly}\" copy-audit-step --source-step \"{source.StepDirectory}\" --workflow-id audit-reuse-cli --sequence 2 --action copied --audit-output \"{destinationRoot}\" --reason \"CLI verified copy\" --verified-by reviewer";
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = FindRepositoryRoot(),
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
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
        using var payload = JsonDocument.Parse(stdout);
        Assert.Equal("verified-copy", payload.RootElement.GetProperty("artifact_origin").GetString());
        Assert.False(payload.RootElement.GetProperty("official_execution_evidence").GetBoolean());
        Assert.True(File.Exists(payload.RootElement.GetProperty("reuse_manifest_file").GetString()));
    }

    [Fact]
    public async Task CopyStepAsync_RejectsMalformedWorkflowBackup()
    {
        var sourceDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-malformed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "workflow.mermaid.md"), "flowchart TD");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "workflow.html"), "<html></html>");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "workflow.json"), "{invalid-json");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowAuditArtifactWriter.CopyStepAsync(
            sourceDirectory,
            "audit-reuse-target",
            1,
            "reused",
            Path.Combine(Path.GetTempPath(), $"techne-loom-audit-destination-{Guid.NewGuid():N}"),
            "Malformed source must fail closed.",
            "test-reviewer"));

        Assert.Contains("invalid workflow.json", error.Message, StringComparison.Ordinal);
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
    [Fact]
    public async Task CliRun_WithAuditReuse_ExecutesWorkflowAndReusesOnlyRender()
    {
        var workflow = CreateRunnableWorkflow();
        var workflowPath = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-reuse-run-{Guid.NewGuid():N}.json");
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-reuse-source-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-reuse-run-output-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowPath, WorkflowJsonSerializer.Serialize(workflow));
        var source = await WorkflowAuditArtifactWriter.WriteAsync(
            "verified-source",
            1,
            "verified",
            WorkflowJsonSerializer.Serialize(workflow),
            "flowchart TD\nstart --> done",
            "<html>verified</html>",
            sourceRoot);
        var cliAssembly = typeof(DefaultWorkflowTaskTrackingService).Assembly.Location;
        var arguments = $"\"{cliAssembly}\" run --workflow-file \"{workflowPath}\" --audit-output \"{destinationRoot}\" --reuse-audit-step \"{source.StepDirectory}\" --reuse-audit-reason \"Workflow and render inputs were verified unchanged\" --reuse-audit-verified-by reviewer";
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = FindRepositoryRoot(),
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
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
        Assert.Contains("audit-reuse.json", stdout, StringComparison.Ordinal);
        Assert.Contains("Mermaid render unchanged in this call.", stdout, StringComparison.Ordinal);
        var eventsPath = workflowPath + ".events.jsonl";
        Assert.True(File.Exists(eventsPath));
        Assert.Contains("transition.run", await File.ReadAllTextAsync(eventsPath), StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(destinationRoot, "audit-reuse.json", SearchOption.AllDirectories));
        Assert.True(Directory.GetFiles(destinationRoot, "workflow.mermaid.md", SearchOption.AllDirectories).Length >= 2);
    }

    private static WorkflowInstance CreateRunnableWorkflow()
    {
        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            WorkflowPhase = "Run",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.run",
                    TransitionIds = ["transition.run"],
                },
            ],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var done = new StateNode
        {
            Id = "state.done",
            Name = "Done",
            WorkflowPhase = "Done",
            Groups = [],
            WaitBehavior = WaitBehavior.BlockUntilComplete,
        };
        var transition = new CommandTransition
        {
            Id = "transition.run",
            Name = "Run tool",
            TargetNodeId = done.Id,
            StepKind = WorkflowStepKind.ToolCall,
            WorkflowPhase = "Run",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "noop",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal),
            },
        };

        return new WorkflowInstance
        {
            InstanceId = $"audit-reuse-run-{Guid.NewGuid():N}",
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = done.Id,
            Status = WorkflowStatus.ReadyToStart,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [done.Id] = done,
                [transition.Id] = transition,
            },
        };
    }
}
