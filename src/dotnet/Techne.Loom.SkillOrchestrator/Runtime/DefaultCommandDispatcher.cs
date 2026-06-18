using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed class DefaultCommandDispatcher : ICommandDispatcher
{
    private readonly HttpClient _httpClient;

    public DefaultCommandDispatcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<object?> ExecuteAsync(
        CommandInvocation invocation,
        IReadOnlyDictionary<string, object?> workflowContextReference,
        IProgress<object>? progress,
        CancellationToken ct)
    {
        return invocation.Kind switch
        {
            CommandInvocationKind.Tool => await ExecuteToolAsync(invocation, ct).ConfigureAwait(false),
            CommandInvocationKind.CommandLine => await ExecuteProcessAsync(invocation, progress, ct).ConfigureAwait(false),
            CommandInvocationKind.NativeCode => await ExecuteToolAsync(invocation, ct).ConfigureAwait(false),
            CommandInvocationKind.Http => await ExecuteHttpAsync(invocation, ct).ConfigureAwait(false),
            CommandInvocationKind.PythonScript => await ExecutePythonScriptAsync(invocation, ct).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Unsupported command invocation kind '{invocation.Kind}'."),
        };
    }

    private static Task<object?> ExecuteToolAsync(CommandInvocation invocation, CancellationToken ct)
    {
        var parameters = invocation.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        return invocation.Name switch
        {
            "noop" => Task.FromResult<object?>(null),
            "echo" => Task.FromResult(parameters.TryGetValue("message", out var message) ? message : null),
            "ls" => Task.FromResult<object?>(Directory.Exists(GetPath(parameters))
                ? Directory.GetFileSystemEntries(GetPath(parameters)).Select(Path.GetFileName).ToArray()
                : Array.Empty<string>()),
            "write-file" => Task.FromResult<object?>(WriteFile(parameters)),
            _ => throw new InvalidOperationException($"Unknown built-in tool '{invocation.Name}'."),
        };
    }

    private async Task<object?> ExecuteHttpAsync(CommandInvocation invocation, CancellationToken ct)
    {
        var parameters = invocation.Parameters ?? throw new InvalidOperationException("HTTP invocation requires parameters.");
        var url = parameters.TryGetValue("url", out var urlValue) ? Convert.ToString(urlValue) : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("HTTP invocation requires a 'url' parameter.");
        }

        var method = parameters.TryGetValue("method", out var methodValue)
            ? Convert.ToString(methodValue)?.ToUpperInvariant()
            : "GET";

        return method switch
        {
            "POST" => await PostAsync(url, parameters, ct).ConfigureAwait(false),
            _ => await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false),
        };
    }

    private async Task<object?> ExecuteProcessAsync(CommandInvocation invocation, IProgress<object>? progress, CancellationToken ct)
    {
        var parameters = invocation.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var arguments = parameters.TryGetValue("args", out var argsValue) ? Convert.ToString(argsValue) : string.Empty;
        var workingDirectory = parameters.TryGetValue("workingDirectory", out var directoryValue)
            ? Convert.ToString(directoryValue)
            : Environment.CurrentDirectory;
        var commandLine = string.IsNullOrWhiteSpace(arguments) ? invocation.Name : $"{invocation.Name} {arguments}";
        progress?.Report(new CommandStreamStart(commandLine));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = invocation.Name,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var stdoutTask = ConsumeStreamAsync(process.StandardOutput, commandLine, "stdout", stdout, progress, ct);
            var stderrTask = ConsumeStreamAsync(process.StandardError, commandLine, "stderr", stderr, progress, ct);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Process '{invocation.Name}' failed with exit code {process.ExitCode}: {stderr}".Trim());
            }

            return stdout.ToString().TrimEnd();
        }
        finally
        {
            progress?.Report(new CommandStreamEnd(commandLine));
        }
    }

    private Task<object?> ExecutePythonScriptAsync(CommandInvocation invocation, CancellationToken ct)
    {
        var parameters = invocation.Parameters is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(invocation.Parameters, StringComparer.Ordinal);

        parameters.TryAdd("args", parameters.TryGetValue("args", out var argsValue) ? argsValue : string.Empty);

        return ExecuteProcessAsync(new CommandInvocation
        {
            Kind = CommandInvocationKind.CommandLine,
            Name = "python",
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["args"] = $"\"{invocation.Name}\" {Convert.ToString(parameters["args"])}".Trim(),
                ["workingDirectory"] = parameters.TryGetValue("workingDirectory", out var workingDirectory) ? workingDirectory : Environment.CurrentDirectory,
            },
        }, progress: null, ct);
    }

    private async Task<object?> PostAsync(string url, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        var body = parameters.TryGetValue("body", out var bodyValue) ? bodyValue : parameters;
        var response = await _httpClient.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static string GetPath(Dictionary<string, object?> parameters)
    {
        return parameters.TryGetValue("path", out var pathValue)
            ? Convert.ToString(pathValue) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;
    }

    private static string WriteFile(Dictionary<string, object?> parameters)
    {
        var path = parameters.TryGetValue("path", out var pathValue)
            ? Convert.ToString(pathValue)
            : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("write-file requires a 'path' parameter.");
        }

        var content = parameters.TryGetValue("content", out var contentValue)
            ? Convert.ToString(contentValue) ?? string.Empty
            : string.Empty;

        path = ResolveWritePath(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
        return path;
    }

    private static string ResolveWritePath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return path;
        }

        var normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var dotTmpPrefix = $".tmp{Path.DirectorySeparatorChar}";
        if (normalizedPath.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(dotTmpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(Path.GetTempPath(), normalizedPath));
        }

        return path;
    }

    private static async Task ConsumeStreamAsync(
        StreamReader reader,
        string commandLine,
        string streamName,
        StringBuilder collector,
        IProgress<object>? progress,
        CancellationToken ct)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (collector.Length > 0)
            {
                collector.AppendLine();
            }

            collector.Append(line);
            progress?.Report(new CommandStreamChunk(commandLine, streamName, line));
        }
    }
}
