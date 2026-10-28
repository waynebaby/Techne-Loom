namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed record McpAppHostConfigurationGenerationResult(
    string Status,
    string Format,
    string OutputFile,
    string PackageId,
    string RuntimeVersion,
    string Rid,
    string RuntimeRoot,
    string LaunchFile,
    string Command,
    IReadOnlyList<string> Arguments,
    string AppHostSha256,
    string ConfigSha256,
    string ServerName);
