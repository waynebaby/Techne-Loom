using System.Text;
using System.Text.Json;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class McpStdioClientTests
{
    [Fact]
    public async Task Client_SkipsServerNotificationsWhileAwaitingResponses()
    {
        var fixture = CreateScriptedLaunch();
        try
        {
            await using var client = await McpStdioClient.StartAsync(fixture.Launch, TimeSpan.FromSeconds(5));

            var serverInfo = await client.InitializeAsync();
            var tools = await client.ListToolsAsync();
            using var arguments = JsonDocument.Parse("{\"operation_id\":\"op-client-test\"}");
            var result = await client.CallToolAsync("test_echo", arguments.RootElement.Clone());

            Assert.Equal("test", serverInfo.Name);
            Assert.Equal("1.0.0", serverInfo.Version);
            Assert.Contains("test_echo", tools);
            Assert.False(result.IsError);
            Assert.Equal("ok", result.Content[0].Text);
            Assert.Equal(McpDispatchStage.Dispatched, client.DispatchStage);
        }
        finally
        {
            if (File.Exists(fixture.ScriptPath))
            {
                File.Delete(fixture.ScriptPath);
            }
        }
    }

    private static (LoomRuntimeLaunchCommand Launch, string ScriptPath) CreateScriptedLaunch()
    {
        var responses = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2025-06-18\",\"serverInfo\":{\"name\":\"test\",\"version\":\"1.0.0\"},\"capabilities\":{}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"tools\":[{\"name\":\"test_echo\"}]}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}],\"isError\":false}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}",
        };

        var scriptPath = Path.Combine(Path.GetTempPath(), $"techne-loom-mcp-client-{Guid.NewGuid():N}{(OperatingSystem.IsWindows() ? ".cmd" : ".sh")}");
        string command;
        IReadOnlyList<string> arguments;
        string script;
        if (OperatingSystem.IsWindows())
        {
            command = "cmd.exe";
            script = string.Join(Environment.NewLine, new[]
            {
                "@echo off",
                "set /p line=",
                "echo " + responses[0],
                "echo " + responses[1],
                "echo " + responses[2],
                "set /p line=",
                "echo " + responses[3],
                "echo " + responses[4],
                "echo " + responses[5],
                "set /p line=",
                "echo " + responses[6],
                "echo " + responses[7],
                "echo " + responses[8],
            });
            arguments = ["/d", "/c", scriptPath];
        }
        else
        {
            command = "/bin/sh";
            script = string.Join("\n", new[]
            {
                "#!/bin/sh",
                "IFS= read -r line",
                "printf '%s\\n' '" + responses[0] + "'",
                "printf '%s\\n' '" + responses[1] + "'",
                "printf '%s\\n' '" + responses[2] + "'",
                "IFS= read -r line",
                "printf '%s\\n' '" + responses[3] + "'",
                "printf '%s\\n' '" + responses[4] + "'",
                "printf '%s\\n' '" + responses[5] + "'",
                "IFS= read -r line",
                "printf '%s\\n' '" + responses[6] + "'",
                "printf '%s\\n' '" + responses[7] + "'",
                "printf '%s\\n' '" + responses[8] + "'",
            });
            arguments = [scriptPath];
        }

        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
        return (new LoomRuntimeLaunchCommand(
            "framework-dependent",
            command,
            arguments,
            AppContext.BaseDirectory,
            "mcp-client-test",
            "1.0.0",
            OperatingSystem.IsWindows() ? "win-x64" : "linux-x64",
            "mcp-client-test"), scriptPath);
    }
}