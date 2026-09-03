using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Common.Runtime;

namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed record McpConfigurationGenerationOptions(
    string OutputFile,
    string Format,
    string ServerName,
    bool Force,
    string RuntimeDescriptorFile);

public sealed record McpConfigurationGenerationResult(
    string Status,
    string Format,
    string OutputFile,
    string RuntimeMode,
    string RuntimeVersion,
    string Rid,
    string RuntimeRoot,
    string LaunchFile,
    string Command,
    IReadOnlyList<string> Arguments,
    string ConfigSha256,
    string RuntimeDescriptorFile,
    string RuntimeDescriptorSha256,
    string PreparationId);

public static class McpConfigurationGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static McpConfigurationGenerationResult Generate(McpConfigurationGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.OutputFile)) throw new ArgumentException("MCP configuration output file is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ServerName)) throw new ArgumentException("MCP server name is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.RuntimeDescriptorFile)) throw new ArgumentException("Runtime launch descriptor file is required.", nameof(options));

        var format = options.Format.Trim().ToLowerInvariant();
        if (format is not "vscode" and not "claude")
            throw new ArgumentException("MCP configuration format must be 'vscode' or 'claude'.", nameof(options));

        var descriptorFile = Path.GetFullPath(options.RuntimeDescriptorFile);
        var descriptorBytes = File.ReadAllBytes(descriptorFile);
        var descriptor = LoomPreparationDiagnostics.ReadFromJson(Encoding.UTF8.GetString(descriptorBytes));
        var launch = LoomRuntimeLaunch.CreateMcpCommand(descriptor);
        var outputFile = Path.GetFullPath(options.OutputFile);
        if (File.Exists(outputFile) && !options.Force)
            throw new IOException($"MCP configuration '{outputFile}' already exists. Pass --force to replace it.");

        object server = format == "vscode"
            ? new { type = "stdio", command = launch.Command, args = launch.Arguments }
            : new { command = launch.Command, args = launch.Arguments };
        object configuration = format == "vscode"
            ? new { servers = new Dictionary<string, object>(StringComparer.Ordinal) { [options.ServerName] = server } }
            : new { mcpServers = new Dictionary<string, object>(StringComparer.Ordinal) { [options.ServerName] = server } };

        var json = JsonSerializer.Serialize(configuration, JsonOptions) + Environment.NewLine;
        var directory = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryFile = outputFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryFile, json, new UTF8Encoding(false));
            File.Move(temporaryFile, outputFile, overwrite: options.Force);
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }

        return new McpConfigurationGenerationResult(
            "generated",
            format,
            outputFile,
            launch.RuntimeMode,
            launch.RuntimeVersion,
            launch.Rid,
            launch.WorkingDirectory,
            launch.LaunchFile,
            launch.Command,
            launch.Arguments,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(),
            descriptorFile,
            Convert.ToHexString(SHA256.HashData(descriptorBytes)).ToLowerInvariant(),
            launch.PreparationId);
    }
}
