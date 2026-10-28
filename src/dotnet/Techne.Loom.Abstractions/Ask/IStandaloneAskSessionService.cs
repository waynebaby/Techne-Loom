using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Abstractions.Ask;

public interface IStandaloneAskSessionService
{
    Task<AskScopedWorkerEndpoint> StartFromContractFileAsync(
        string contractFile,
        CancellationToken ct = default);

    Task<AskScopedStandaloneResult> GetResultAsync(string askId, CancellationToken ct = default);
}
