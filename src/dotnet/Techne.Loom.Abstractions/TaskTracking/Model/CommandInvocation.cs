namespace Techne.Loom.Abstractions.TaskTracking.Model;

public sealed class CommandInvocation : ICloneable
{
    public CommandInvocationKind Kind { get; set; } = CommandInvocationKind.NativeCode;

    public string Name { get; set; } = string.Empty;

    public string EnvironmentKey { get; set; } = string.Empty;

    public Dictionary<string, object?>? Parameters { get; set; }

    public int? RetryAndRefineTimes { get; set; }

    public int CurrentRetryCount { get; set; }

    public IReadOnlyList<CommandInvocation>? History { get; set; }

    public object Clone()
    {
        return new CommandInvocation
        {
            Kind = Kind,
            Name = Name,
            EnvironmentKey = EnvironmentKey,
            Parameters = Parameters is null ? null : Parameters.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            RetryAndRefineTimes = RetryAndRefineTimes,
            CurrentRetryCount = CurrentRetryCount,
            History = History?.Select(static item => (CommandInvocation)item.Clone()).ToList(),
        };
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            List<object?> list => list.Select(CloneValue).ToList(),
            IReadOnlyList<object?> list => list.Select(CloneValue).ToList(),
            _ => value,
        };
    }
}
