using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

internal static class StandaloneAskCli
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new InvalidOperationException("Usage: so ask start --contract-file <path> | so ask result --ask-id <id>.");
        }

        var service = new AskScopedStandaloneSessionService();
        object payload = args[0] switch
        {
            "start" => await StartAsync(args.Skip(1).ToArray(), service).ConfigureAwait(false),
            "result" => await GetResultAsync(args.Skip(1).ToArray(), service).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown ask command '{args[0]}'."),
        };

        WriteProperty(new SoAskPropertyEnvelope("result", DateTimeOffset.UtcNow, payload));
        return 0;
    }

    private static async Task<object> StartAsync(IReadOnlyList<string> args, AskScopedStandaloneSessionService service)
    {
        var contractFile = GetRequiredOption(args, "--contract-file");
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
        var askId = GetRequiredOption(args, "--ask-id");
        var result = await service.GetResultAsync(askId).ConfigureAwait(false);
        return new
        {
            action = "result",
            result,
        };
    }

    private static string GetRequiredOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        throw new InvalidOperationException($"Missing required option '{name}'.");
    }

    private static void WriteProperty(SoAskPropertyEnvelope envelope)
    {
        Console.WriteLine("<so_property>");
        Console.WriteLine(JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Console.WriteLine("</so_property>");
    }

    private sealed record SoAskPropertyEnvelope(string Type, DateTimeOffset TimestampUtc, object Payload);
}
