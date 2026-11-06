using System.Text.RegularExpressions;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class WorkflowJsonToCSharpBuilderExporterTests
{
    [Fact]
    public async Task GeneratedBuilder_ExecutesThroughRoslynAndPreservesWorkflowJson()
    {
        var source = new WorkflowInstance
        {
            InstanceId = "workflow.example",
            TemplateKind = "skill",
            TaskType = "design",
            WorkflowKind = "governed",
            CaseId = "case-01",
            RunId = "run-01",
            RuntimeBinding = "dotnet-so",
            RuntimeVersion = "0.3.270",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.ReadyToStart,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = false,
                ["optional"] = null,
                ["label"] = "draft",
            },
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                    Description = "Workflow entry state.",
                    WorkflowPhase = "phase.01",
                },
            },
        };
        var expectedJson = WorkflowJsonSerializer.Serialize(source);
        var script = WorkflowJsonToCSharpBuilderExporter.GenerateBuilderScript(source);
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-workflow-builder-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(scriptFile, script);

        try
        {
            var execution = await new WorkflowScriptHost().ExecuteBuilderAsync(
                scriptFile,
                new WorkflowScriptInput
                {
                    RuntimeBinding = source.RuntimeBinding!,
                    RuntimeVersion = source.RuntimeVersion,
                });

            Assert.True(execution.IsSuccess, execution.Feedback.Error);
            Assert.NotNull(execution.Value);
            Assert.Equal(expectedJson, WorkflowJsonSerializer.Serialize(execution.Value));
            Assert.Contains("namespace Techne.Loom.WorkflowScripts;", script, StringComparison.Ordinal);
            Assert.Contains("Build(WorkflowScriptInput input)", script, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(scriptFile))
            {
                File.Delete(scriptFile);
            }
        }
    }

    [Fact]
    public void Exporter_RejectsWorkflowWithPersistedGateEvaluation()
    {
        var workflow = new WorkflowInstance
        {
            Status = WorkflowStatus.Drafting,
            LastGateEvaluation = new GateEvaluationResult { Passed = true },
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowJsonToCSharpBuilderExporter.GenerateBuilderScript(workflow));

        Assert.Contains("Only a draft or unstarted workflow template", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exporter_RejectsWorkflowThatHasStartedExecuting()
    {
        var workflow = new WorkflowInstance
        {
            InstanceId = "workflow.running",
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Status = WorkflowStatus.Running,
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowJsonToCSharpBuilderExporter.GenerateBuilderScript(workflow));

        Assert.Contains("Only a draft or unstarted workflow template", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TemplateMarkdown_ExecutesThroughRoslynWithRequiredNamespaceAndEntryPoint()
    {
        var markdown = WorkflowJsonToCSharpBuilderExporter.GenerateBuilderTemplateMarkdown().Replace("\r\n", "\n", StringComparison.Ordinal);
        const string openingFence = "```csharp\n";
        const string closingFence = "\n```";
        var codeStart = markdown.IndexOf(openingFence, StringComparison.Ordinal);
        Assert.True(codeStart >= 0);
        codeStart += openingFence.Length;
        var codeEnd = markdown.IndexOf(closingFence, codeStart, StringComparison.Ordinal);
        Assert.True(codeEnd > codeStart);
        var script = markdown[codeStart..codeEnd];
        var scriptFile = Path.Combine(Path.GetTempPath(), $"loom-workflow-template-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(scriptFile, script);

        try
        {
            var execution = await new WorkflowScriptHost().ExecuteBuilderAsync(
                scriptFile,
                new WorkflowScriptInput
                {
                    RuntimeBinding = "dotnet-so",
                    RuntimeVersion = "0.3.270",
                });

            Assert.True(execution.IsSuccess, execution.Feedback.Error);
            Assert.NotNull(execution.Value);
            Assert.Equal("workflow.example", execution.Value.InstanceId);
            Assert.Equal("dotnet-so", execution.Value.RuntimeBinding);
            Assert.Equal("0.3.270", execution.Value.RuntimeVersion);
        }
        finally
        {
            if (File.Exists(scriptFile))
            {
                File.Delete(scriptFile);
            }
        }

    }

    [Fact]
    public void ContractReferenceBundle_IncludesWorkflowContractsAndExcludesUnrelatedApis()
    {
        var contractRoot = Path.Combine(AppContext.BaseDirectory, "docs", "contracts");
        var indexPath = Path.Combine(contractRoot, "index.md");
        var abstractionsRoot = Path.Combine(contractRoot, "abstractions");
        var abstractionsIndexPath = Path.Combine(abstractionsRoot, "index.md");
        Assert.True(File.Exists(indexPath));
        Assert.True(File.Exists(abstractionsIndexPath));
        var index = File.ReadAllText(indexPath);
        var abstractionsIndex = File.ReadAllText(abstractionsIndexPath);
        var sourceManifestPath = Path.Combine(abstractionsRoot, "source-manifest.txt");
        Assert.True(File.Exists(sourceManifestPath));
        var sourceManifest = File.ReadAllText(sourceManifestPath);
        Assert.Contains("API reference version:", index, StringComparison.Ordinal);
        Assert.Contains("API reference version:", abstractionsIndex, StringComparison.Ordinal);
        Assert.Contains("abstractions/index.md", index, StringComparison.Ordinal);
        Assert.Contains("TaskTracking/", abstractionsIndex, StringComparison.Ordinal);
        Assert.Contains("SkillOrchestrator/Analysis/", abstractionsIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("Ask/", abstractionsIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("Mcp/", abstractionsIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("AgentOrchestrator/Models/", abstractionsIndex, StringComparison.Ordinal);
        var templatePath = Path.Combine(contractRoot, "workflow-instance-builder.csx.md");
        Assert.True(File.Exists(templatePath));

        AssertLinksExist(index, contractRoot);
        AssertLinksExist(abstractionsIndex, abstractionsRoot);

        var referencePages = Directory.GetFiles(abstractionsRoot, "*.cs.md", SearchOption.AllDirectories);
        Assert.NotEmpty(referencePages);
        foreach (var referencePath in referencePages)
        {
            var relativePath = Path.GetRelativePath(abstractionsRoot, referencePath).Replace('\\', '/');
            Assert.Contains($"({relativePath})", abstractionsIndex, StringComparison.Ordinal);
            Assert.Contains(relativePath, sourceManifest, StringComparison.Ordinal);
        }

        foreach (var referencePath in referencePages.Append(templatePath))
        {
            var content = File.ReadAllText(referencePath);
            var opening = Regex.Match(content, @"(?m)^(?<fence>`{3,})csharp[ \t]*\r?\n");
            Assert.True(opening.Success, $"Missing fenced C# block in '{referencePath}'.");
            var contentAfterOpening = content[(opening.Index + opening.Length)..];
            var closingPattern = @"(?m)^" + Regex.Escape(opening.Groups["fence"].Value) + @"[ \t]*\r?$";
            var closing = Regex.Match(contentAfterOpening, closingPattern);
            Assert.True(closing.Success && closing.Index > 0, $"Incomplete fenced C# block in '{referencePath}'.");
        }

        Assert.Empty(Directory.GetFiles(contractRoot, "*.cs", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(contractRoot, "*.csx", SearchOption.AllDirectories));

        static void AssertLinksExist(string document, string root)
        {
            foreach (Match match in Regex.Matches(document, @"\[[^\]]+\]\(([^)]+)\)"))
            {
                var relativePath = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                Assert.True(File.Exists(Path.Combine(root, relativePath)), $"Missing contract reference target '{relativePath}'.");
            }
        }
    }
}
