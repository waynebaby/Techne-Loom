using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.Tests.Shared;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoWorkflowPhaseCliTests
{
    [Fact]
    public async Task PhaseCompile_WritesParseableStaticOnlyResult()
    {
        var repositoryRoot = FindRepositoryRoot();
        var root = Path.Combine(Path.GetTempPath(), $"loom-ao-phase-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var contractFile = Path.Combine(root, "contract.json");
        var workflowFile = Path.Combine(root, "workflow.json");
        var skeletonFile = Path.Combine(root, "skeleton.json");
        var fragmentFile = Path.Combine(root, "fragment.json");
        var resultFile = Path.Combine(root, "result.json");

        try
        {
            await File.WriteAllTextAsync(contractFile, """
                {
                  "name": "phase-cli",
                  "inputs": {},
                  "outputs": {},
                  "default_assumptions": {},
                  "context_fields": {
                    "schemaVersion": "loom.context-fields.v1",
                    "fields": [
                      {"path":"p00_input.approved","type":"boolean","nullable":false,"owner":"agent","description":"Approval decision."}
                    ]
                  }
                }
                """);
            var template = new WorkflowInstance
            {
                InstanceId = "phase-cli",
                StartNodeId = "p00_input.start",
                CurrentNodeId = "p00_input.start",
                EndNodeId = "p00_input.end",
                ContractBinding = new ContractBinding { Path = "contract.json", AssetRootPath = root },
            };
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(template));
            await File.WriteAllTextAsync(skeletonFile, """
                {
                  "schemaVersion": "loom.workflow-skeleton.v1",
                  "workflowId": "phase-cli",
                  "phases": [
                    {
                      "id": "p00_input", "prefix": "p00_input.", "mode": "phaseSliced",
                      "entries": ["p00_input.start"], "exits": ["p00_input.done"],
                      "imports": [], "exports": ["p00_input.approved"],
                      "dependencies": [], "gateIds": [], "terminalRoutes": []
                    }
                  ],
                  "routes": [],
                  "backedges": []
                }
                """);
            var fragment = WorkflowPhaseFragment.Create(
                "phase-cli",
                "p00_input",
                new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
                {
                    ["p00_input.start"] = new StateNode
                    {
                        Id = "p00_input.start",
                        Name = "Input",
                        WorkflowPhase = "01 Input",
                        Groups =
                        [
                            new TransitionGroup
                            {
                                Id = "input",
                                Strategy = ConcurrencyStrategy.FirstPass,
                                TransitionIds = ["p00_input.done"],
                            },
                        ],
                    },
                    ["p00_input.done"] = new ExpressionTransition
                    {
                        Id = "p00_input.done",
                        Name = "Continue",
                        WorkflowPhase = "01 Input",
                        TargetNodeId = "p00_input.end",
                        StepKind = WorkflowStepKind.ConditionBranch,
                        GuardExpression = "true",
                        PassExpression = "true",
                    },
                    ["p00_input.end"] = new StateNode
                    {
                        Id = "p00_input.end",
                        Name = "Complete",
                        WorkflowPhase = "02 Complete",
                        Groups = [],
                    },
                });
            await File.WriteAllTextAsync(fragmentFile, fragment.ToJson());

            var skeletonCheckRun = await RunCliAsync(
                repositoryRoot,
                $"skeleton-check --workflow-file \"{workflowFile}\" --skeleton-file \"{skeletonFile}\"");
            Assert.Equal(0, skeletonCheckRun.ExitCode);

            var run = await RunCliAsync(
                repositoryRoot,
                $"phase-compile --workflow-file \"{workflowFile}\" --skeleton-file \"{skeletonFile}\" --fragment-file \"{fragmentFile}\" --phase-id p00_input --output-file \"{resultFile}\"");

            Assert.Equal(0, run.ExitCode);
            Assert.True(File.Exists(resultFile));
            var result = WorkflowPhaseCompileResult.Parse(await File.ReadAllTextAsync(resultFile), resultFile);
            Assert.Equal("passed", result.Status);
            Assert.True(result.StaticOnly);
            Assert.NotNull(result.Fragment);

            var candidateFile = Path.Combine(root, "candidate.json");
            var mergeRun = await RunCliAsync(
                repositoryRoot,
                $"merge --workflow-file \"{workflowFile}\" --skeleton-file \"{skeletonFile}\" --result-file \"{resultFile}\" --output-file \"{candidateFile}\"");
            Assert.Equal(0, mergeRun.ExitCode);
            using var mergeOutput = JsonDocument.Parse(mergeRun.StdOut);
            Assert.Equal("passed", mergeOutput.RootElement.GetProperty("compile_status").GetString());
            var candidate = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(candidateFile));
            Assert.Equal("p00_input.start", candidate.CurrentNodeId);
            Assert.Equal(3, candidate.Nodes.Count);

            var impactFile = Path.Combine(root, "impact.json");
            var impactRun = await RunCliAsync(
                repositoryRoot,
                $"impact --previous-workflow-file \"{workflowFile}\" --previous-skeleton-file \"{skeletonFile}\" --previous-result-file \"{resultFile}\" --candidate-workflow-file \"{workflowFile}\" --candidate-skeleton-file \"{skeletonFile}\" --changed-phase-id p00_input --output-file \"{impactFile}\"");
            Assert.Equal(0, impactRun.ExitCode);
            var impact = WorkflowPhaseImpactAnalysis.Parse(await File.ReadAllTextAsync(impactFile), impactFile);
            Assert.Equal(["p00_input"], impact.RevalidatePhaseIds);
            Assert.True(impact.FullWorkflowCompileRequired);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TwoHundredStatePhaseWorkflowCompilesThroughAoCli()
    {
        const int authoredStateCount = 200;
        var fixture = WorkflowPhaseAcceptanceFixture.Create(authoredStateCount);
        var repositoryRoot = FindRepositoryRoot();
        var root = Path.Combine(Path.GetTempPath(), $"loom-ao-phase-acceptance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var contractFile = Path.Combine(root, "contract.json");
        var workflowFile = Path.Combine(root, "workflow.json");
        var skeletonFile = Path.Combine(root, "skeleton.json");
        var candidateFile = Path.Combine(root, "candidate.json");

        try
        {
            await File.WriteAllTextAsync(contractFile, fixture.ContractJson);
            await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(fixture.CreateTemplate(root)));
            await File.WriteAllTextAsync(skeletonFile, fixture.SkeletonJson);

            var skeletonCheckRun = await RunCliAsync(
                repositoryRoot,
                $"skeleton-check --workflow-file \"{workflowFile}\" --skeleton-file \"{skeletonFile}\"");
            Assert.True(skeletonCheckRun.ExitCode == 0, skeletonCheckRun.StdErr);

            var resultFiles = new List<string>(fixture.Fragments.Count);
            foreach (var fragment in fixture.Fragments)
            {
                var fragmentFile = Path.Combine(root, $"{fragment.PhaseId}.fragment.json");
                var resultFile = Path.Combine(root, $"{fragment.PhaseId}.result.json");
                await File.WriteAllTextAsync(fragmentFile, fragment.ToJson());
                var phaseRun = await RunCliAsync(
                    repositoryRoot,
                    $"phase-compile --workflow-file \"{workflowFile}\" --skeleton-file \"{skeletonFile}\" --fragment-file \"{fragmentFile}\" --phase-id {fragment.PhaseId} --output-file \"{resultFile}\"");

                Assert.True(phaseRun.ExitCode == 0, $"Phase {fragment.PhaseId} failed: {phaseRun.StdErr}");
                var result = WorkflowPhaseCompileResult.Parse(await File.ReadAllTextAsync(resultFile), resultFile);
                Assert.Equal("passed", result.Status);
                Assert.True(result.StaticOnly);
                Assert.NotNull(result.Fragment);
                resultFiles.Add(resultFile);
            }

            var resultArguments = string.Join(" ", resultFiles.Select(static path => $"--result-file \"{path}\""));
            var mergeRun = await RunCliAsync(
                repositoryRoot,
                $"merge --workflow-file \"{workflowFile}\" --skeleton-file \"{skeletonFile}\" {resultArguments} --output-file \"{candidateFile}\"");

            Assert.True(mergeRun.ExitCode == 0, mergeRun.StdErr);
            using var mergeOutput = JsonDocument.Parse(mergeRun.StdOut);
            Assert.Equal("passed", mergeOutput.RootElement.GetProperty("compile_status").GetString());
            var candidate = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(candidateFile));
            Assert.Equal(authoredStateCount, candidate.Nodes.Values.OfType<StateNode>().Count());
            Assert.Equal(authoredStateCount * 2 - 1, candidate.Nodes.Count);
            Assert.Equal(fixture.EndNodeId, candidate.EndNodeId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Merge_MissingInputsDoesNotCreateCandidate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var root = Path.Combine(Path.GetTempPath(), $"loom-ao-phase-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var missingFile = Path.Combine(root, "missing.json");
        var outputFile = Path.Combine(root, "candidate.json");

        try
        {
            var run = await RunCliAsync(
                repositoryRoot,
                $"merge --workflow-file \"{missingFile}\" --skeleton-file \"{missingFile}\" --result-file \"{missingFile}\" --output-file \"{outputFile}\"");

            Assert.Equal(2, run.ExitCode);
            Assert.False(File.Exists(outputFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repositoryRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{typeof(AoRuntimeService).Assembly.Location}\" {arguments}",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO CLI process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
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