using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Abstractions.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class LocalCommandDispatcher : ICommandDispatcher
{
    private readonly HttpClient _httpClient;

    public LocalCommandDispatcher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<object?> ExecuteAsync(
        CommandInvocation invocation,
        IReadOnlyDictionary<string, object?> workflowContextReference,
        IProgress<object>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return invocation.Kind switch
        {
            CommandInvocationKind.Tool or CommandInvocationKind.NativeCode => await ExecuteToolAsync(invocation, ct).ConfigureAwait(false),
            CommandInvocationKind.CommandLine => await ExecuteProcessAsync(invocation, progress, ct).ConfigureAwait(false),
            CommandInvocationKind.PythonScript => await ExecutePythonScriptAsync(invocation, progress, ct).ConfigureAwait(false),
            CommandInvocationKind.Http => await ExecuteHttpAsync(invocation, ct).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Unsupported command invocation kind '{invocation.Kind}'."),
        };
    }

    private static Task<object?> ExecuteToolAsync(CommandInvocation invocation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var parameters = invocation.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        return invocation.Name switch
        {
            "noop" => Task.FromResult<object?>(null),
            "echo" => Task.FromResult<object?>(parameters.TryGetValue("message", out var message) ? message : null),
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
        using var response = method == "POST"
            ? await _httpClient.PostAsJsonAsync(url, parameters.TryGetValue("body", out var bodyValue) ? bodyValue : parameters, ct).ConfigureAwait(false)
            : await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static async Task<object?> ExecutePythonScriptAsync(CommandInvocation invocation, IProgress<object>? progress, CancellationToken ct)
    {
        var parameters = invocation.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var scriptArguments = parameters.TryGetValue("args", out var argsValue) ? Convert.ToString(argsValue) : string.Empty;
        return await ExecuteProcessAsync(
            new CommandInvocation
            {
                Kind = CommandInvocationKind.CommandLine,
                Name = "python",
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["args"] = $"\"{invocation.Name}\" {scriptArguments}".Trim(),
                    ["workingDirectory"] = parameters.TryGetValue("workingDirectory", out var directory) ? directory : Environment.CurrentDirectory,
                },
            },
            progress,
            ct).ConfigureAwait(false);
    }

    private static async Task<object?> ExecuteProcessAsync(CommandInvocation invocation, IProgress<object>? progress, CancellationToken ct)
    {
        var parameters = invocation.Parameters ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var arguments = parameters.TryGetValue("args", out var argsValue) ? Convert.ToString(argsValue) ?? string.Empty : string.Empty;
        var workingDirectory = parameters.TryGetValue("workingDirectory", out var directoryValue)
            ? Convert.ToString(directoryValue)
            : Environment.CurrentDirectory;
        var commandLine = string.IsNullOrWhiteSpace(arguments) ? invocation.Name : $"{invocation.Name} {arguments}";
        progress?.Report(commandLine);

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
        if (!process.Start())
        {
            throw new InvalidOperationException($"Process '{invocation.Name}' could not be started.");
        }

        using var cancellationRegistration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process '{invocation.Name}' failed with exit code {process.ExitCode}: {stderr.Trim()}");
        }

        return stdout.TrimEnd();
    }


    private static string GetPath(Dictionary<string, object?> parameters)
        => parameters.TryGetValue("path", out var pathValue)
            ? Convert.ToString(pathValue) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;

    private static string WriteFile(Dictionary<string, object?> parameters)
    {
        var path = parameters.TryGetValue("path", out var pathValue) ? Convert.ToString(pathValue) : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("write-file requires a 'path' parameter.");
        }

        var content = parameters.TryGetValue("content", out var contentValue) ? Convert.ToString(contentValue) ?? string.Empty : string.Empty;
        var normalizedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileContent = string.Equals(Path.GetExtension(normalizedPath), ".json", StringComparison.OrdinalIgnoreCase)
            ? WorkflowJsonSerializer.FormatForFileOutputOrOriginal(content)
            : content;
        var temporaryPath = Path.Combine(directory ?? Environment.CurrentDirectory, $".{Path.GetFileName(normalizedPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, fileContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, normalizedPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return normalizedPath;
    }
}
