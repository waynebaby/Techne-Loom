using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class SoStandaloneAskCliTests
{
    [Fact]
    public async Task AskStartAndResult_RunWithoutWorkflowIdentity()
    {
        var repoRoot = FindRepositoryRoot();
        var storeRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-so-ask-{Guid.NewGuid():N}");
        var contractFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-ask-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(contractFile, JsonSerializer.Serialize(CreateContract(), WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));

        try
        {
            var start = await RunCliAsync(repoRoot, storeRoot, "ask", "start", "--contract-file", contractFile);
            Assert.Equal(0, start.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(start.StdErr));
            using var startDocument = ParseProperty(start.StdOut);
            var startPayload = startDocument.RootElement.GetProperty("payload");
            var askId = startPayload.GetProperty("askId").GetString();
            var endpointUrl = startPayload.GetProperty("url").GetString();
            Assert.False(string.IsNullOrWhiteSpace(askId));
            Assert.False(string.IsNullOrWhiteSpace(endpointUrl));
            Assert.DoesNotContain("workflowInstanceId", start.StdOut, StringComparison.Ordinal);

            var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = storeRoot });
            var capability = await File.ReadAllTextAsync(Path.Combine(storeRoot, askId!, "machine-capability"));
            var snapshot = await store.GetSnapshotAsync(askId!, capability);
            Assert.Equal(AskScopedConsumerKind.Standalone, snapshot.ConsumerKind);
            Assert.Null(snapshot.WorkflowInstanceId);
            await store.SubmitAsync(
                askId!,
                capability,
                snapshot.Generation,
                "so-standalone-submit",
                new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.intent"] = new() { Value = JsonSerializer.SerializeToElement("Return typed answers and a receipt.") },
                });

            var resultRun = await RunCliAsync(repoRoot, storeRoot, "ask", "result", "--ask-id", askId!);
            Assert.Equal(0, resultRun.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(resultRun.StdErr));
            using var resultDocument = ParseProperty(resultRun.StdOut);
            var resultPayload = resultDocument.RootElement.GetProperty("payload");
            Assert.Equal("result", resultPayload.GetProperty("action").GetString());
            var result = resultPayload.GetProperty("result");
            Assert.True(result.GetProperty("submitted").GetBoolean());
            Assert.Equal("Return typed answers and a receipt.", result.GetProperty("answers")[0].GetProperty("value").GetString());
            Assert.Equal((int)AskScopedConsumerKind.Standalone, result.GetProperty("consumerKind").GetInt32());
            Assert.False(result.TryGetProperty("workflowInstanceId", out _));
            Assert.False(result.GetProperty("receipt").TryGetProperty("transitionId", out _));

            await WaitForWorkerShutdownAsync(endpointUrl!);
        }
        finally
        {
            if (Directory.Exists(storeRoot))
            {
                Directory.Delete(storeRoot, recursive: true);
            }

            if (File.Exists(contractFile))
            {
                File.Delete(contractFile);
            }
        }
    }

    [Fact]
    public async Task AskStart_ClosesRedirectedStreamsWhileWorkerRemainsActive()
    {
        var repoRoot = FindRepositoryRoot();
        var root = Path.Combine(Path.GetTempPath(), $"techne-loom-so-pipe-{Guid.NewGuid():N}");
        var storeRoot = Path.Combine(root, "store");
        var contractFile = Path.Combine(root, "contract.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(contractFile, JsonSerializer.Serialize(CreateContract(), WorkflowJsonSerializer.CreateDefaultOptions(indented: false)));
        var startInfo = CreateCliStartInfo(repoRoot, storeRoot, redirectError: true, "ask", "start", "--contract-file", contractFile);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var stderr = process.StandardError.ReadToEndAsync();
        string? askId = null;
        string? endpointUrl = null;

        try
        {
            var outputLines = new List<string>();
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
            {
                outputLines.Add(line);
                if (string.Equals(line, "</so_property>", StringComparison.Ordinal))
                {
                    break;
                }
            }

            using var document = ParseProperty(string.Join(Environment.NewLine, outputLines));
            var payload = document.RootElement.GetProperty("payload");
            askId = payload.GetProperty("askId").GetString();
            endpointUrl = payload.GetProperty("url").GetString();
            Assert.False(string.IsNullOrWhiteSpace(askId));
            Assert.False(string.IsNullOrWhiteSpace(endpointUrl));

            var remainingOutput = process.StandardOutput.ReadToEndAsync();
            var streams = Task.WhenAll(remainingOutput, stderr);
            // This deadline checks the CLI boundary, not the intentionally long-lived worker.
            await Task.WhenAll(process.WaitForExitAsync(), streams).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(0, process.ExitCode);
            var remaining = await streams;
            Assert.All(remaining, value => Assert.True(string.IsNullOrWhiteSpace(value)));
            var endpoint = new Uri(endpointUrl!, UriKind.Absolute);
            using var client = new HttpClient();
            using var response = await client.GetAsync(new Uri(endpoint, "/"));
            Assert.True(response.IsSuccessStatusCode);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(askId))
            {
                var store = new AskScopedSubmissionStore(new AskScopedSubmissionStoreOptions { RootDirectory = storeRoot });
                var capability = await File.ReadAllTextAsync(Path.Combine(storeRoot, askId, "machine-capability"));
                var snapshot = await store.GetSnapshotAsync(askId, capability);
                if (snapshot.Receipt is null)
                {
                    await store.SubmitAsync(
                        askId,
                        capability,
                        snapshot.Generation,
                        "so-standalone-pipe-cleanup",
                        new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                        {
                            ["question.intent"] = new() { Value = JsonSerializer.SerializeToElement("Complete the test.") },
                        });
                }

                if (!string.IsNullOrWhiteSpace(endpointUrl))
                {
                    await WaitForWorkerShutdownAsync(endpointUrl);
                }
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static UserInputContract CreateContract()
        => new()
        {
            Version = 1,
            QuestionGroups =
            [
                new UserInputQuestionGroup
                {
                    Id = "group.standalone",
                    Title = "Standalone questions",
                    Questions =
                    [
                        new UserInputQuestion
                        {
                            Id = "question.intent",
                            Context = "The calling agent needs an answer without a workflow.",
                            Intent = "Collect one typed standalone answer.",
                            Prompt = "What should the standalone ask capability return?",
                            Type = UserInputQuestionTypes.Text,
                            Required = true,
                        },
                    ],
                },
            ],
        };


    private static async Task WaitForWorkerShutdownAsync(string endpointUrl)
    {
        var endpoint = new Uri(endpointUrl, UriKind.Absolute);
        using var shutdownDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(endpoint.Host, endpoint.Port, shutdownDeadline.Token);
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), shutdownDeadline.Token);
            }
        }
        catch (OperationCanceledException) when (shutdownDeadline.IsCancellationRequested)
        {
            throw new TimeoutException("The ask worker did not stop after its submission was persisted.");
        }
    }

    private static JsonDocument ParseProperty(string output)
    {
        var jsonLine = output.Split([ '\r', '\n' ], StringSplitOptions.RemoveEmptyEntries)
            .Single(static line => line.StartsWith('{'));
        return JsonDocument.Parse(jsonLine);
    }

    private static ProcessStartInfo CreateCliStartInfo(
        string repoRoot,
        string storeRoot,
        bool redirectError,
        params string[] arguments)
    {
        var cliAssembly = typeof(SoStandaloneAskCliTests).Assembly.Location
            .Replace("Techne.Loom.SkillOrchestrator.Tests.dll", "so.dll", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = redirectError,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["TECHNE_LOOM_ASK_STORE_ROOT"] = storeRoot;
        return startInfo;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string repoRoot,
        string storeRoot,
        params string[] arguments)
    {
        var startInfo = CreateCliStartInfo(repoRoot, storeRoot, redirectError: false, arguments);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start SO CLI process.");
        var outputLines = new List<string>();
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            outputLines.Add(line);
            if (string.Equals(line, "</so_property>", StringComparison.Ordinal))
            {
                break;
            }
        }

        await process.WaitForExitAsync();
        return (process.ExitCode, string.Join(Environment.NewLine, outputLines), string.Empty);
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
