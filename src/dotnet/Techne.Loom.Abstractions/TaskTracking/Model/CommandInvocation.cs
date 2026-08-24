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
            Parameters = Parameters is null ? null : Parameters.ToDictionary(static pair => pair.Key, static pair => DeepValueCloner.Clone(pair.Value), StringComparer.Ordinal),
            RetryAndRefineTimes = RetryAndRefineTimes,
            CurrentRetryCount = CurrentRetryCount,
            History = History?.Select(static item => (CommandInvocation)item.Clone()).ToList(),
        };
    }
}
