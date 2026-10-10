using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed class AskScopedStandaloneSessionService
{
    private readonly AskScopedSubmissionStore _store;
    private readonly Func<AskScopedSubmissionStore, AskScopedLaunch, CancellationToken, Task<AskScopedWorkerEndpoint>> _startWorkerAsync;

    public AskScopedStandaloneSessionService(AskScopedSubmissionStore? store = null)
        : this(store ?? new AskScopedSubmissionStore(), AskScopedWorkerProcessLauncher.StartDetachedAsync)
    {
    }

    internal AskScopedStandaloneSessionService(
        AskScopedSubmissionStore store,
        Func<AskScopedSubmissionStore, AskScopedLaunch, CancellationToken, Task<AskScopedWorkerEndpoint>> startWorkerAsync)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(startWorkerAsync);
        _store = store;
        _startWorkerAsync = startWorkerAsync;
    }

    public async Task<AskScopedWorkerEndpoint> StartFromContractFileAsync(
        string contractFile,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractFile);
        var fullPath = Path.GetFullPath(contractFile);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The standalone ask contract file was not found.", fullPath);
        }

        var json = await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
        var contract = JsonSerializer.Deserialize<UserInputContract>(json, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? throw new JsonException("The standalone ask contract file was empty.");
        var launch = await _store.CreateStandaloneAsync(contract, ct).ConfigureAwait(false);
        try
        {
            return await _startWorkerAsync(_store, launch, ct).ConfigureAwait(false);
        }
        catch (Exception startupException)
        {
            try
            {
                await _store.RemoveUnsubmittedStandaloneAsync(launch, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "The standalone ask worker failed to start and its unsubmitted session could not be removed.",
                    startupException,
                    cleanupException);
            }

            throw;
        }
    }

    public Task<AskScopedStandaloneResult> GetResultAsync(string askId, CancellationToken ct = default)
        => _store.GetStandaloneResultAsync(askId, ct);
}
