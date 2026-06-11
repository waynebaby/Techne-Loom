namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoCliOptions
{
    public static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    public static string GetRequiredOption(IReadOnlyList<string> args, string name)
    {
        return GetOption(args, name)
            ?? throw new InvalidOperationException($"Missing required option '{name}'.");
    }
}
