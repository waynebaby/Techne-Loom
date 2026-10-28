using System;
using System.IO;

namespace Techne.Loom.Common.Runtime;

public enum LoomRuntimeProduct
{
    AgentOrchestrator,
    SkillOrchestrator,
}

public sealed record McpRuntimeBinding(
    string Product,
    string RuntimeVersion,
    string Rid,
    string LaunchCommand,
    string LaunchFile,
    string LaunchArgumentsSha256,
    string ExecutableSha256)
{
    public bool Matches(McpRuntimeBinding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Product, other.Product, StringComparison.Ordinal)
            && string.Equals(RuntimeVersion, other.RuntimeVersion, StringComparison.Ordinal)
            && string.Equals(Rid, other.Rid, StringComparison.Ordinal)
            && string.Equals(LaunchCommand, other.LaunchCommand, StringComparison.Ordinal)
            && PathEquals(LaunchFile, other.LaunchFile)
            && string.Equals(LaunchArgumentsSha256, other.LaunchArgumentsSha256, StringComparison.Ordinal)
            && string.Equals(ExecutableSha256, other.ExecutableSha256, StringComparison.Ordinal);
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
