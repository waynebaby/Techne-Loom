using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AoStandaloneAskCliTests
{
    [Fact]
    public async Task AskStartSubmitResult_DoesNotRequireWorkflowAndReturnsTypedReceipt()
    {
        var repoRoot = FindRepositoryRoot();
        var storeRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-ask-{Guid.NewGuid():N}");
        var contractFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-ask-{Guid.NewGuid():N}.json");
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

            await SubmitAnswerAsync(endpointUrl!, "ao-standalone-submit", "Return typed answers and a receipt.");

            var resultRun = await RunCliAsync(repoRoot, storeRoot, "ask", "result", "--ask-id", askId!);
            Assert.Equal(0, resultRun.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(resultRun.StdErr));
            using var resultDocument = ParseProperty(resultRun.StdOut);
            var resultPayload = resultDocument.RootElement.GetProperty("payload");
            Assert.Equal("result", resultPayload.GetProperty("action").GetString());
            var result = resultPayload.GetProperty("result");
            Assert.True(result.GetProperty("submitted").GetBoolean());
            Assert.Equal("Return typed answers and a receipt.", result.GetProperty("answers")[0].GetProperty("value").GetString());
            Assert.Equal(1, result.GetProperty("consumerKind").GetInt32());
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

    private static async Task SubmitAnswerAsync(string endpointUrl, string operationId, string answer)
    {
        var endpoint = new Uri(endpointUrl, UriKind.Absolute);
        var origin = endpoint.GetLeftPart(UriPartial.Authority);
        var pairingCode = Uri.UnescapeDataString(endpoint.Fragment["#pair=".Length..]);
        using var client = new HttpClient();
        using var exchangeRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/session"))
        {
            Content = JsonContent.Create(new { pairingCode }),
        };
        exchangeRequest.Headers.Add("Origin", origin);
        using var exchangeResponse = await client.SendAsync(exchangeRequest);
        Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
        var session = await exchangeResponse.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.False(string.IsNullOrWhiteSpace(session?.Token));

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/submit"))
        {
            Content = JsonContent.Create(new
            {
                expectedGeneration = 0,
                operationId,
                answers = new Dictionary<string, AskScopedAnswerValue>(StringComparer.Ordinal)
                {
                    ["question.intent"] = new() { Value = JsonSerializer.SerializeToElement(answer) },
                },
            }),
        };
        submitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        submitRequest.Headers.Add("Origin", origin);
        using var submitResponse = await client.SendAsync(submitRequest);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
    }

    private static async Task WaitForWorkerShutdownAsync(string endpointUrl)
    {
        var endpoint = new Uri(endpointUrl, UriKind.Absolute);
        var root = new UriBuilder(endpoint) { Fragment = string.Empty }.Uri;
        using var client = new HttpClient();
        using var shutdownDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                try
                {
                    using var response = await client.GetAsync(root, shutdownDeadline.Token);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
                catch (HttpRequestException)
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string repoRoot,
        string storeRoot,
        params string[] arguments)
    {
        var cliAssembly = typeof(AoStandaloneAskCliTests).Assembly.Location
            .Replace("Techne.Loom.AgentOrchestrator.Tests.dll", "ao.dll", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["TECHNE_LOOM_ASK_STORE_ROOT"] = storeRoot;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start AO CLI process.");
        var outputLines = new List<string>();
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            outputLines.Add(line);
            if (string.Equals(line, "</ao_property>", StringComparison.Ordinal))
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

    private sealed record SessionResponse(string Token);
}
