using System.ComponentModel;
using System.Diagnostics;

namespace Techne.Loom.Common.Runtime;

internal sealed class DefaultLoomRuntimeProcessRunner : ILoomRuntimeProcessRunner
{
    public async Task<LoomProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Runtime process timeout must be positive.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (environmentVariables is not null)
        {
            foreach (var environmentVariable in environmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                return new LoomProcessResult(false, -1, string.Empty, string.Empty);
            }
        }
        catch (Win32Exception)
        {
            return new LoomProcessResult(false, -1, string.Empty, string.Empty);
        }
        catch (IOException)
        {
            return new LoomProcessResult(false, -1, string.Empty, string.Empty);
        }
        catch (InvalidOperationException)
        {
            return new LoomProcessResult(false, -1, string.Empty, string.Empty);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var callerCancelled = cancellationToken.IsCancellationRequested;
            var timedOut = !callerCancelled;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
            catch (Win32Exception) when (process.HasExited)
            {
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);

            if (callerCancelled)
            {
                throw;
            }

            if (timedOut)
            {
                throw new LoomRuntimeCommandException($"Runtime process '{fileName}' exceeded the timeout of {timeout}.");
            }
        }
        return new LoomProcessResult(
            true,
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }
}
