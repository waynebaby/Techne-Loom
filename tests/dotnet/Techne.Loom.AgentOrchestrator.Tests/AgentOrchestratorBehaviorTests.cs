using System.Diagnostics;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AgentOrchestratorBehaviorTests
{
    [Fact]
    public async Task CliRunThenResume_PersistsWorkflowAndAppendsEvents()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-context-{Guid.NewGuid():N}.json");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-workflow-{Guid.NewGuid():N}.json");
        var eventLogFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-events-{Guid.NewGuid():N}.jsonl");

        await File.WriteAllTextAsync(objectiveFile, "Plan AO implementation route.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --workflow-file \"{workflowFile}\" --event-log-file \"{eventLogFile}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("<ao_property>", run.StdOut);
        Assert.Contains("\"type\":\"boundary\"", run.StdOut);
        Assert.Contains("\"boundary_reason\":\"clarification_required\"", run.StdOut);
        Assert.True(File.Exists(workflowFile));
        Assert.True(File.Exists(eventLogFile));

        var beforeLines = (await File.ReadAllLinesAsync(eventLogFile)).Length;
        Assert.True(beforeLines > 0);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, "{" +
            "\"transition_id\":\"transition.clarify\"," +
            "\"correlation_key\":null," +
            "\"payload\":{\"confirmed_scope\":true,\"mark_completed\":true}" +
            "}");

        var resume = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --event-log-file \"{eventLogFile}\" --result-file \"{resultFile}\"");
        Assert.Equal(0, resume.ExitCode);
        Assert.Contains("\"type\":\"result\"", resume.StdOut);
        Assert.Contains("\"status\":\"completed\"", resume.StdOut);

        var afterLines = (await File.ReadAllLinesAsync(eventLogFile)).Length;
        Assert.True(afterLines > beforeLines);
    }

    [Fact]
    public async Task CliRun_SamplingBoundary_EmitsStructuredSamplingRequest()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sampling-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sampling-context-{Guid.NewGuid():N}.json");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sampling-workflow-{Guid.NewGuid():N}.json");
        var eventLogFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-sampling-events-{Guid.NewGuid():N}.jsonl");

        await File.WriteAllTextAsync(objectiveFile, "Compare two frontier options.");
        await File.WriteAllTextAsync(contextFile, "{\"force_boundary_reason\":\"sampling_required\",\"confirmed_scope\":true}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --workflow-file \"{workflowFile}\" --event-log-file \"{eventLogFile}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"boundary_reason\":\"sampling_required\"", run.StdOut);
        Assert.Contains("\"sampling_request\":{", run.StdOut);
        Assert.Contains("\"objective\":\"compare candidate execution frontiers\"", run.StdOut);
        Assert.Contains("\"artifacts\":[\"frontier-a.json\",\"frontier-b.json\"]", run.StdOut);
    }

    [Fact]
    public async Task CliResume_MalformedEnvelope_ReturnsStableError()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-context-{Guid.NewGuid():N}.json");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-workflow-{Guid.NewGuid():N}.json");
        var eventLogFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-events-{Guid.NewGuid():N}.jsonl");

        await File.WriteAllTextAsync(objectiveFile, "Need clarification.");
        await File.WriteAllTextAsync(contextFile, "{}");
        _ = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --workflow-file \"{workflowFile}\" --event-log-file \"{eventLogFile}\"");

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, "{\"correlation_key\":\"abc\",\"payload\":{\"confirmed_scope\":true}}");

        var resume = await RunCliAsync(repoRoot, $"resume --workflow-file \"{workflowFile}\" --event-log-file \"{eventLogFile}\" --result-file \"{resultFile}\"");
        Assert.Equal(2, resume.ExitCode);
        Assert.Contains("<ao_property>", resume.StdOut);
        Assert.Contains("\"type\":\"error\"", resume.StdOut);
        Assert.Contains("transition_id", resume.StdOut);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "src", "dotnet", "Techne.Loom.AgentOrchestrator", "Techne.Loom.AgentOrchestrator.csproj") }\" -- {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO CLI process.");
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
            if (File.Exists(Path.Combine(current.FullName, "README.md")) && Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
