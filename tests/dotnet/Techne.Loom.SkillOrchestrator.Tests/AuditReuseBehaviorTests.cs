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
        var sourceDelivery = source.MermaidDelivery ?? throw new InvalidOperationException("Mermaid source delivery evidence was not written.");
        Assert.Equal("runtime_path_only", sourceDelivery.Status);
        Assert.Equal("fresh", sourceDelivery.GenerationStatus);
        Assert.True(sourceDelivery.ArtifactGenerated);
        Assert.False(sourceDelivery.LinkResolvable);
        Assert.False(sourceDelivery.VisualPreviewRendered);
        Assert.False(sourceDelivery.CardDisplayAvailable);

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
            Assert.Equal(reused.ArtifactOrigin, manifest.GetProperty("artifact_origin").GetString());
            Assert.False(reused.OfficialExecutionEvidence.GetValueOrDefault());
            Assert.Equal(manifest.GetProperty("official_execution_evidence").GetBoolean(), reused.OfficialExecutionEvidence.GetValueOrDefault());
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
    public async Task WriteAsync_ConcurrentSameStepRejectsOneWithoutOverwrite()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-concurrent-{Guid.NewGuid():N}");
        var writes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await WorkflowAuditArtifactWriter.WriteAsync(
                    "audit-concurrent",
                    1,
                    "concurrent",
                    "{\"instanceId\":\"audit-concurrent\"}",
                    "flowchart TD\nstart --> done",
                    "<html>concurrent</html>",
                    outputRoot);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }));

        Assert.Equal(1, writes.Count(static succeeded => succeeded));
        Assert.Equal(1, writes.Count(static succeeded => !succeeded));
        var stepDirectory = Path.Combine(outputRoot, "wf-audit-concurrent", "step-0001-concurrent");
        Assert.True(File.Exists(Path.Combine(stepDirectory, "workflow.json")));
        Assert.True(File.Exists(Path.Combine(stepDirectory, "workflow.html")));
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

    [Fact]
    public async Task CopyStepAsync_RejectsCurrentWorkflowMismatchWhenExpectedSnapshotProvided()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-audit-source-{Guid.NewGuid():N}");
        var source = await WorkflowAuditArtifactWriter.WriteAsync(
            "audit-reuse-source",
            1,
            "verified",
            "{\"instanceId\":\"audit-reuse-source\"}",
            "flowchart TD\nstart --> done",
            "<html>verified</html>",
            sourceRoot);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkflowAuditArtifactWriter.CopyStepAsync(
            source.StepDirectory,
            "audit-reuse-target",
            2,
            "reused",
            Path.Combine(Path.GetTempPath(), $"techne-loom-audit-destination-{Guid.NewGuid():N}"),
                    "The current workflow snapshot changed.",
                    "test-reviewer",
                    expectedWorkflowJson: "{\"instanceId\":\"audit-reuse-current\",\"startNodeId\":\"different\"}"));

                Assert.Contains("does not match the current workflow render inputs", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_RecordsFreshDeliveryAndMirrorsToWorkspace()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"techne loom audit source-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"techne loom workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        var artifacts = await WorkflowAuditArtifactWriter.WriteAsync(
            "delivery-fresh",
            1,
            "rendered",
            "{\"instanceId\":\"delivery-fresh\"}",
            "flowchart TD\nstart --> done",
            "<html><body>fresh</body></html>",
            outputRoot,
            workspaceRoot: workspaceRoot);
        var delivery = artifacts.MermaidDelivery ?? throw new InvalidOperationException("Mermaid delivery evidence was not written.");

        Assert.Equal("workspace_mirror", delivery.Status);
        Assert.Equal("fresh", delivery.GenerationStatus);
        Assert.True(delivery.ArtifactGenerated);
        Assert.True(delivery.LinkResolvable);
        Assert.False(delivery.VisualPreviewRendered);
        Assert.False(delivery.CardDisplayAvailable);
        Assert.Equal("html_available", delivery.PreviewStatus);
        Assert.Equal(Path.GetRelativePath(workspaceRoot, delivery.WorkspaceMermaidFile!).Replace('\\', '/'), delivery.WorkspaceRelativeMermaidFile);
        Assert.Equal(Path.GetRelativePath(workspaceRoot, delivery.WorkspaceHtmlFile!).Replace('\\', '/'), delivery.WorkspaceRelativeHtmlFile);
        Assert.Equal("copied", delivery.MirrorStatus);
        Assert.True(delivery.MermaidExists);
        Assert.True(delivery.HtmlExists);
        Assert.True(delivery.MermaidReadable);
        Assert.True(delivery.HtmlReadable);
        Assert.NotNull(delivery.MermaidSha256);
        Assert.NotNull(delivery.HtmlSha256);
        Assert.True(File.Exists(delivery.WorkspaceMermaidFile));
        Assert.True(File.Exists(delivery.WorkspaceHtmlFile));
        Assert.Equal(await File.ReadAllTextAsync(artifacts.MermaidFile), await File.ReadAllTextAsync(delivery.WorkspaceMermaidFile!));
        Assert.Equal(await File.ReadAllTextAsync(artifacts.HtmlFile), await File.ReadAllTextAsync(delivery.WorkspaceHtmlFile!));
    }

    [Fact]
    public async Task CopyStepAsync_RecordsReusedDeliveryAndMirrorsToWorkspace()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"techne loom audit reuse source-{Guid.NewGuid():N}");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"techne loom audit reuse destination-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"techne loom workspace reuse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var source = await WorkflowAuditArtifactWriter.WriteAsync(
            "delivery-reuse-source",
            1,
            "rendered",
            "{\"instanceId\":\"delivery-reuse-source\"}",
            "flowchart TD\nstart --> done",
            "<html><body>reuse</body></html>",
            sourceRoot);
        var sourceDelivery = source.MermaidDelivery ?? throw new InvalidOperationException("Mermaid source delivery evidence was not written.");

        var reused = await WorkflowAuditArtifactWriter.CopyStepAsync(
            source.StepDirectory,
            "delivery-reuse-target",
            2,
            "reused",
            destinationRoot,
            "Verified reuse delivery.",
            "test-reviewer",
            workspaceRoot: workspaceRoot);
        var delivery = reused.MermaidDelivery ?? throw new InvalidOperationException("Mermaid reuse delivery evidence was not written.");

        Assert.Equal("workspace_mirror", delivery.Status);
        Assert.Equal("reused", delivery.GenerationStatus);
        Assert.True(delivery.ArtifactGenerated);
        Assert.True(delivery.LinkResolvable);
        Assert.False(delivery.VisualPreviewRendered);
        Assert.False(delivery.CardDisplayAvailable);
        Assert.Equal("html_available", delivery.PreviewStatus);
        Assert.Equal(Path.GetRelativePath(workspaceRoot, delivery.WorkspaceMermaidFile!).Replace('\\', '/'), delivery.WorkspaceRelativeMermaidFile);
        Assert.Equal(Path.GetRelativePath(workspaceRoot, delivery.WorkspaceHtmlFile!).Replace('\\', '/'), delivery.WorkspaceRelativeHtmlFile);
        Assert.Equal(source.StepDirectory, delivery.SourceStepDirectory);
        Assert.Equal("copied", delivery.MirrorStatus);
        Assert.Equal(sourceDelivery.MermaidSha256, delivery.MermaidSha256);
        Assert.Equal(sourceDelivery.HtmlSha256, delivery.HtmlSha256);
        Assert.True(File.Exists(delivery.WorkspaceMermaidFile));
        Assert.True(File.Exists(delivery.WorkspaceHtmlFile));
    }

    [Fact]
    public async Task WriteAsync_FailedDeliveryCarriesEvidenceAndCleansPartialDirectory()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"techne loom audit failed-{Guid.NewGuid():N}");

        var error = await Assert.ThrowsAsync<WorkflowAuditDeliveryException>(() => WorkflowAuditArtifactWriter.WriteAsync(
            "delivery-failed",
            1,
            "rendered",
            "{\"instanceId\":\"delivery-failed\"}",
            "flowchart TD",
            "<html>",
            outputRoot));
        var delivery = error.AuditArtifacts.MermaidDelivery ?? throw new InvalidOperationException("Failed Mermaid delivery evidence was not written.");

        Assert.Equal("delivery_failed", delivery.Status);
        Assert.Contains("truncated", delivery.Error, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(outputRoot, "wf-delivery-failed", "step-0001-rendered"), error.AuditArtifacts.StepDirectory);
        Assert.False(Directory.Exists(error.AuditArtifacts.StepDirectory));
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
        public async Task CliRun_WithAuditReuse_ExecutesWorkflowAndWritesCurrentSnapshot()
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
            Assert.Contains("\"artifact_origin\":\"fresh-runtime\"", stdout, StringComparison.Ordinal);
            Assert.Contains("Mermaid render updated in this call.", stdout, StringComparison.Ordinal);
            var eventsPath = workflowPath + ".events.jsonl";
            Assert.True(File.Exists(eventsPath));
            Assert.Contains("transition.run", await File.ReadAllTextAsync(eventsPath), StringComparison.Ordinal);
            Assert.True(Directory.GetFiles(destinationRoot, "workflow.mermaid.md", SearchOption.AllDirectories).Length >= 2);
            var reuseManifestPath = Assert.Single(Directory.GetFiles(destinationRoot, "audit-reuse.json", SearchOption.AllDirectories));
            var reusedStepDirectory = Directory.GetParent(reuseManifestPath)!.FullName;
            var reusedWorkflowPath = Path.Combine(reusedStepDirectory, "workflow.json");
            var reusedMermaidPath = Path.Combine(reusedStepDirectory, "workflow.mermaid.md");
            var reusedHtmlPath = Path.Combine(reusedStepDirectory, "workflow.html");
            var reusedInstance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(reusedWorkflowPath));
            var persistedInstance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowPath));
            Assert.Equal(workflow.InstanceId, reusedInstance.InstanceId);
            Assert.NotEqual(WorkflowStatus.ReadyToStart, reusedInstance.Status);
            Assert.Equal("state.done", reusedInstance.CurrentNodeId);
            Assert.True(reusedInstance.Version > 0);
            Assert.Equal(WorkflowStatus.Succeeded, persistedInstance.Status);
            Assert.Equal(persistedInstance.CurrentNodeId, reusedInstance.CurrentNodeId);
            Assert.Contains("state.done", await File.ReadAllTextAsync(reusedMermaidPath), StringComparison.Ordinal);
                Assert.Contains("wf-state-active", await File.ReadAllTextAsync(reusedHtmlPath), StringComparison.Ordinal);
                Assert.Contains("Done", await File.ReadAllTextAsync(reusedHtmlPath), StringComparison.Ordinal);
                using var reuseManifest = JsonDocument.Parse(await File.ReadAllTextAsync(reuseManifestPath));
                var replacedFiles = reuseManifest.RootElement.GetProperty("replaced_file_names").EnumerateArray().Select(item => item.GetString()).ToArray();
                Assert.Contains("workflow.json", replacedFiles);
                Assert.Contains("workflow.mermaid.md", replacedFiles);
                Assert.Contains("workflow.html", replacedFiles);
                Assert.Equal("fresh-runtime", reuseManifest.RootElement.GetProperty("artifact_origin").GetString());
                Assert.True(reuseManifest.RootElement.GetProperty("official_execution_evidence").GetBoolean());
                Assert.Empty(reuseManifest.RootElement.GetProperty("copied_file_names").EnumerateArray());
                var progressJson = stdout
                    .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .First(line => line.StartsWith("{\"type\":\"progress\"", StringComparison.Ordinal));
                using var progressDocument = JsonDocument.Parse(progressJson);
                var returnedArtifacts = progressDocument.RootElement.GetProperty("payload").GetProperty("audit_artifacts");
                Assert.Equal(reuseManifest.RootElement.GetProperty("artifact_origin").GetString(), returnedArtifacts.GetProperty("artifact_origin").GetString());
                Assert.Equal(reuseManifest.RootElement.GetProperty("official_execution_evidence").GetBoolean(), returnedArtifacts.GetProperty("official_execution_evidence").GetBoolean());
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
