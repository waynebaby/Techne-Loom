using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var tokens = args.ToList();
            if (tokens.Count == 0)
            {
                Console.Error.WriteLine(AoCommandHandlers.UsageText);
                return 1;
            }

            if (tokens.Contains("--help", StringComparer.Ordinal) || tokens.Contains("-h", StringComparer.Ordinal))
            {
                Console.WriteLine(AoCommandHandlers.UsageText);
                return 0;
            }

            if (tokens[0] == "--guide")
            {
                return await AoCommandHandlers.HandleGuideAsync(tokens.Skip(1).ToList()).ConfigureAwait(false);
            }

            return tokens[0] switch
            {
                "host" => await AoCommandHandlers.HandleHostAsync().ConfigureAwait(false),
                "planner" => await AoCommandHandlers.HandlePlannerAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "compile" => await AoCommandHandlers.HandlePlannerAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "run" => await AoCommandHandlers.HandleRunAsync(tokens.Skip(1).ToList(), new AoRuntimeService(), new AoPropertyWriter(Console.Out)).ConfigureAwait(false),
                "resume" => await AoCommandHandlers.HandleResumeAsync(tokens.Skip(1).ToList(), new AoRuntimeService(), new AoPropertyWriter(Console.Out)).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown command '{tokens[0]}'."),
            };
        }
        catch (Exception ex)
        {
            var writer = new AoPropertyWriter(Console.Out);
            writer.WriteAoProperty(new AoPropertyEnvelope(
                "error",
                DateTimeOffset.UtcNow,
                new AoErrorPayload(null, string.Empty, string.Empty, "failed", ex.Message, string.Empty)));
            return 2;
        }
    }
}
