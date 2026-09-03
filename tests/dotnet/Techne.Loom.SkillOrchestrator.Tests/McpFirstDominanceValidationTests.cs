using System.Diagnostics;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpFirstDominanceValidationTests
{
    [Fact]
    public async Task Compile_RejectsExternalBranchThatBypassesGovernanceEntryTransports()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceWorkflowFile = Path.Combine(repoRoot, ".agents", "skills", "loom-skill-enhancement", "assets", "so-workflow", "so-template.json");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-first-bypass-{Guid.NewGuid():N}.json");
        var workflow = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(sourceWorkflowFile));
        var decision = Assert.IsType<StateNode>(workflow.Nodes["state.channel_decision"]);
        decision.Groups[0].TransitionIds.Add("transition.bypass_mcp");
        var bypassState = new StateNode
        {
            Id = "state.bypass_mcp",
            Name = "Bypass",
            WorkflowPhase = "03 Runtime Proof",
            Groups = [],
        };
        var bypass = new CommandTransition
        {
            Id = "transition.bypass_mcp",
            Name = "Bypass MCP",
            TargetNodeId = bypassState.Id,
            OutputPath = "bypass.result",
            StepKind = WorkflowStepKind.WaitResume,
            GuardExpression = "true",
            SucceedExpression = "context.Get<string>(\"bypass.result\") != null",
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.Tool,
                Name = "bypass",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["projectionMode"] = "canonical",
                    ["resumeOutputKey"] = "result",
                    ["requiredInputs"] = new object?[] { "result" },
                },
            },
        };
        workflow.Nodes[bypassState.Id] = bypassState;
        workflow.Nodes[bypass.Id] = bypass;
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(workflow));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repoRoot,
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
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("can bypass both transports", stdout + stderr, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(workflowFile))
            {
                File.Delete(workflowFile);
            }
        }
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
}
