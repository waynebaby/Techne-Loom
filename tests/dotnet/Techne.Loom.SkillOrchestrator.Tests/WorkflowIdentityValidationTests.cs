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
            "loom-skill-enhancement--so-template.json");
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
            Assert.Equal("loom-skill-enhancement--workflow.current.json", Path.GetFileName(runtimePath));
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

    [Fact]
    public void WorkflowIdentityContract_DoesNotTreatGenericSchemaDemosAsSkillOwned()
    {
        var workflow = new WorkflowInstance { TemplateKind = "schema-demo" };
        var workflowFile = Path.Combine(Path.GetTempPath(), "workflow.demo.json");

        Assert.False(WorkflowIdentityContract.IsSkillOwnedWorkflow(workflow));
        Assert.Null(CanonicalWorkflowFileStore.GetSkillFileNameError(workflow, workflowFile));
    }

    [Fact]
    public void SkillWorkflowFileName_RequiresDoubleDashPrefixAndJsonPurpose()
    {
        var workflow = new WorkflowInstance { OwnerSkillSlug = "sample-skill" };
        var validPath = Path.Combine(Path.GetTempPath(), "sample-skill--template.json");
        var singleDashPath = Path.Combine(Path.GetTempPath(), "sample-skill-template.json");
        var emptyPurposePath = Path.Combine(Path.GetTempPath(), "sample-skill--.json");
        var wrongExtensionPath = Path.Combine(Path.GetTempPath(), "sample-skill--template.txt");

        Assert.Null(CanonicalWorkflowFileStore.GetSkillFileNameError(workflow, validPath));
        Assert.Contains("must begin with 'sample-skill--'", CanonicalWorkflowFileStore.GetSkillFileNameError(workflow, singleDashPath));
        Assert.Contains("non-empty purpose", CanonicalWorkflowFileStore.GetSkillFileNameError(workflow, emptyPurposePath));
        Assert.Contains("must use the '.json' extension", CanonicalWorkflowFileStore.GetSkillFileNameError(workflow, wrongExtensionPath));
    }

    [Fact]
    public void SkillWorkflowPath_RequiresMatchingDeclaredOwner()
    {
        var workflowFile = Path.Combine(
            Path.GetTempPath(),
            ".agents",
            "skills",
            "sample-skill",
            "assets",
            "so-workflow",
            "sample-skill--template.json");

        Assert.Contains("must declare ownerSkillSlug", CanonicalWorkflowFileStore.GetSkillFileNameError(new WorkflowInstance(), workflowFile));
        Assert.Contains(
            "must match skill directory 'sample-skill'",
            CanonicalWorkflowFileStore.GetSkillFileNameError(new WorkflowInstance { OwnerSkillSlug = "other-skill" }, workflowFile));
        Assert.Null(CanonicalWorkflowFileStore.GetSkillFileNameError(new WorkflowInstance { OwnerSkillSlug = "sample-skill" }, workflowFile));
    }

    [Fact]
    public void OwnerSkillSlug_RoundTripsAndClones()
    {
        var workflow = new WorkflowInstance { OwnerSkillSlug = "sample-skill" };
        var roundTrip = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(workflow, indented: false));

        Assert.Equal("sample-skill", roundTrip.OwnerSkillSlug);
        Assert.Equal("sample-skill", WorkflowInstanceCloner.Clone(workflow).OwnerSkillSlug);
    }

    [Fact]
    public void ClaudeSkillWorkflowPath_RequiresMatchingDeclaredOwner()
    {
        var workflowFile = Path.Combine(
            Path.GetTempPath(),
            ".claude",
            "skills",
            "sample-skill",
            "assets",
            "so-workflow",
            "sample-skill--template.json");

        Assert.Contains("must declare ownerSkillSlug", CanonicalWorkflowFileStore.GetSkillFileNameError(new WorkflowInstance(), workflowFile));
        Assert.Contains(
            "must match skill directory 'sample-skill'",
            CanonicalWorkflowFileStore.GetSkillFileNameError(new WorkflowInstance { OwnerSkillSlug = "other-skill" }, workflowFile));
        Assert.Null(CanonicalWorkflowFileStore.GetSkillFileNameError(new WorkflowInstance { OwnerSkillSlug = "sample-skill" }, workflowFile));
    }

    [Fact]
    public async Task WorkflowIdentityAssignment_IsAllowedInWorkflowBuilderScripts()
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-script-owner-skill-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(
            scriptFile,
            "public static class S { public static WorkflowInstance Build(WorkflowScriptInput input) => new WorkflowInstance { OwnerSkillSlug = \"sample-skill\", TaskType = \"requirement_generation\", WorkflowKind = \"target_skill_business\", CaseId = \"case-601\", RunId = \"run-601\", RuntimeBinding = input.RuntimeBinding, RuntimeVersion = input.RuntimeVersion }; }");

        try
        {
            var execution = await new WorkflowScriptHost().ExecuteBuilderAsync(
                scriptFile,
                new WorkflowScriptInput { RuntimeBinding = "dotnet-so", RuntimeVersion = "0.1.0" });

            Assert.True(execution.IsSuccess, execution.Feedback.Error);
            Assert.Equal("sample-skill", execution.Value!.OwnerSkillSlug);
            Assert.Equal("requirement_generation", execution.Value.TaskType);
            Assert.Equal("target_skill_business", execution.Value.WorkflowKind);
            Assert.Equal("case-601", execution.Value.CaseId);
            Assert.Equal("run-601", execution.Value.RunId);
        }
        finally
        {
            if (File.Exists(scriptFile))
            {
                File.Delete(scriptFile);
            }
        }
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
            "loom-skill-enhancement--so-template.json");
        return WorkflowJsonSerializer.Deserialize(File.ReadAllText(path));
    }

    private static string WriteTemporaryWorkflow(WorkflowInstance workflow, string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"loom-skill-enhancement--{name}-{Guid.NewGuid():N}.json");
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