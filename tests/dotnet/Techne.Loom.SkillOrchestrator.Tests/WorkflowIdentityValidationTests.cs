using System.Diagnostics;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowIdentityValidationTests
{
    [Fact]
    public void WorkflowJsonSerializer_RoundTripsWorkflowIdentity()
    {
        var instance = new WorkflowInstance
        {
            InstanceId = "workflow-identity",
            TemplateKind = "so-governed-target-skill",
            TaskType = "requirement_generation",
            WorkflowKind = "target_skill_business",
            CaseId = "case-601",
            RunId = "run-601",
        };

        var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(instance, indented: false));

        Assert.Equal("requirement_generation", roundTrip.TaskType);
        Assert.Equal("target_skill_business", roundTrip.WorkflowKind);
        Assert.Equal("case-601", roundTrip.CaseId);
        Assert.Equal("run-601", roundTrip.RunId);
    }

    [Fact]
    public async Task Compile_RejectsBusinessTaskBoundToEnhancementWorkflow()
    {
        var workflow = LoadSelfBootstrapTemplate();
        workflow.TaskType = "requirement_generation";
        workflow.WorkflowKind = WorkflowIdentityContract.TargetSkillEnhancementWorkflowKind;
        workflow.CaseId = "case-es601";
        workflow.RunId = "run-es601";
        var workflowFile = WriteTemporaryWorkflow(workflow, "identity-mismatched");

        try
        {
            var result = await RunCompileAsync(workflowFile);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("skill_enhancement", result.Output, StringComparison.Ordinal);
            Assert.Contains("requirement_generation", result.Output, StringComparison.Ordinal);
            Assert.Contains("taskType/workflowKind", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(workflowFile);
        }
    }

    [Fact]
    public async Task Compile_RejectsTargetBusinessWorkflowWithEnhancementSteps()
    {
        var workflow = LoadSelfBootstrapTemplate();
        workflow.TaskType = "model_generation";
        workflow.WorkflowKind = WorkflowIdentityContract.TargetSkillBusinessWorkflowKind;
        workflow.CaseId = "case-es601";
        workflow.RunId = "run-es601";
        var workflowFile = WriteTemporaryWorkflow(workflow, "identity-business-with-enhancement-steps");

        try
        {
            var result = await RunCompileAsync(workflowFile);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("must not publish SO enhancement output families", result.Output, StringComparison.Ordinal);
            Assert.Contains("shared_review_context", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(workflowFile);
        }
    }

    [Fact]
    public async Task MaterializeRuntimeCopy_ReplacesTemplateRunId()
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            ".agents",
            "skills",
            "loom-skill-enhancement",
            "assets",
            "so-workflow",
            "so-template.json");
        var source = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(sourcePath));
        var dispatcher = new DefaultCommandDispatcher();
        var runtimePath = Assert.IsType<string>(await dispatcher.ExecuteAsync(
            new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "workflow.materializeRuntimeCopy",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["sourceTemplatePath"] = sourcePath,
                },
            },
            new Dictionary<string, object?>(StringComparer.Ordinal),
            progress: null,
            ct: default));

        try
        {
            var runtime = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(runtimePath));

            Assert.Equal(source.CaseId, runtime.CaseId);
            Assert.NotEqual(source.RunId, runtime.RunId);
            Assert.StartsWith("run-", runtime.RunId);
        }
        finally
        {
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (!string.IsNullOrWhiteSpace(runtimeDirectory) && Directory.Exists(runtimeDirectory))
            {
                Directory.Delete(runtimeDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SelfBootstrapTemplate_UsesEnhancementIdentity()
    {
        var workflow = LoadSelfBootstrapTemplate();

        Assert.Equal(WorkflowIdentityContract.SkillEnhancementTaskType, workflow.TaskType);
        Assert.Equal(WorkflowIdentityContract.SoSelfBootstrapWorkflowKind, workflow.WorkflowKind);
        Assert.False(string.IsNullOrWhiteSpace(workflow.CaseId));
        Assert.False(string.IsNullOrWhiteSpace(workflow.RunId));
    }

    private static WorkflowInstance LoadSelfBootstrapTemplate()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            ".agents",
            "skills",
            "loom-skill-enhancement",
            "assets",
            "so-workflow",
            "so-template.json");
        return WorkflowJsonSerializer.Deserialize(File.ReadAllText(path));
    }

    private static string WriteTemporaryWorkflow(WorkflowInstance workflow, string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"techne-loom-{name}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, WorkflowJsonSerializer.Serialize(workflow));
        return path;
    }

    private static async Task<(int ExitCode, string Output)> RunCompileAsync(string workflowFile)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(typeof(DefaultWorkflowTaskTrackingService).Assembly.Location);
        startInfo.ArgumentList.Add("compile");
        startInfo.ArgumentList.Add("--workflow-file");
        startInfo.ArgumentList.Add(workflowFile);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO compile process.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, standardOutput + Environment.NewLine + standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Techne.Loom.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}