using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoAskCommand
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new InvalidOperationException("Usage: ao ask start --contract-file <path> | ao ask result --ask-id <id>.");
        }

        var service = new AskScopedStandaloneSessionService();
        object payload = args[0] switch
        {
            "start" => await StartAsync(args.Skip(1).ToArray(), service).ConfigureAwait(false),
            "result" => await GetResultAsync(args.Skip(1).ToArray(), service).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown ask command '{args[0]}'."),
        };

        new AoPropertyWriter(Console.Out).WriteAoProperty(new AoPropertyEnvelope("result", DateTimeOffset.UtcNow, payload));
        return 0;
    }

    private static async Task<object> StartAsync(IReadOnlyList<string> args, AskScopedStandaloneSessionService service)
    {
        var contractFile = AoCliOptions.GetRequiredOption(args, "--contract-file");
        var validatedPath = CliFileInputGuard.RequireExistingFile(contractFile, "--contract-file");
        var endpoint = await service.StartFromContractFileAsync(validatedPath).ConfigureAwait(false);
        return new
        {
            action = "start",
            endpoint.AskId,
            endpoint.Url,
            endpoint.ExpiresAtUtc,
        };
    }

    private static async Task<object> GetResultAsync(IReadOnlyList<string> args, AskScopedStandaloneSessionService service)
    {
        var askId = AoCliOptions.GetRequiredOption(args, "--ask-id");
        var result = await service.GetResultAsync(askId).ConfigureAwait(false);
        return new
        {
            action = "result",
            result,
        };
    }
}
