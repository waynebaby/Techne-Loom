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

    public static int GetRequiredInt32Option(IReadOnlyList<string> args, string name)
    {
        var value = GetRequiredOption(args, name);
        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"Option '{name}' must be a valid integer.");
        }

        return parsed;
    }

    public static int GetOptionalInt32Option(IReadOnlyList<string> args, string name, int defaultValue)
    {
        var value = GetOption(args, name);
        if (value is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"Option '{name}' must be a valid integer.");
        }

        return parsed;
    }
}
