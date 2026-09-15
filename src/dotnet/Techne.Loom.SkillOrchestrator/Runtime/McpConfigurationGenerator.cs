using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using System.Text.Json.Nodes;

using Techne.Loom.Common.Runtime;



namespace Techne.Loom.SkillOrchestrator.Runtime;



public sealed record McpConfigurationGenerationOptions(

    string? OutputFile,

    string Format,

    string? ServerName,

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

    string RuntimeDescriptorCanonicalSha256,

    string PreparationId,

    string ServerName);



public static class McpConfigurationGenerator

{

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)

    {

        WriteIndented = true,

    };



    public static McpConfigurationGenerationResult Generate(McpConfigurationGenerationOptions options)

    {

        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.RuntimeDescriptorFile)) throw new ArgumentException("Runtime launch descriptor file is required.", nameof(options));



        var format = options.Format.Trim().ToLowerInvariant();

        if (format is not "vscode" and not "claude")

            throw new ArgumentException("MCP configuration format must be 'vscode' or 'claude'.", nameof(options));



        var descriptorFile = Path.GetFullPath(options.RuntimeDescriptorFile);

        var descriptorBytes = File.ReadAllBytes(descriptorFile);

        var descriptor = LoomPreparationDiagnostics.ReadFromJson(Encoding.UTF8.GetString(descriptorBytes));

        var launch = LoomRuntimeLaunch.CreateMcpCommand(descriptor);

        var expectedServerName = LoomRuntimeCatalog.GetMcpServerName(descriptor.Product, descriptor.ResolvedRuntimeVersion);

        var serverName = string.IsNullOrWhiteSpace(options.ServerName) ? expectedServerName : options.ServerName.Trim();

        if (!string.Equals(serverName, expectedServerName, StringComparison.Ordinal))

            throw new ArgumentException($"MCP server name must be '{expectedServerName}' for the selected product and exact runtime version.", nameof(options));



        var outputFile = string.IsNullOrWhiteSpace(options.OutputFile)

            ? LoomRuntimeCatalog.GetDefaultUserMcpConfigurationPath(descriptor.Product, descriptor.ResolvedRuntimeVersion, format)

            : Path.GetFullPath(options.OutputFile);



        var server = CreateServerNode(format, launch);

        var sectionName = format == "vscode" ? "servers" : "mcpServers";

        var configuration = LoadConfiguration(outputFile, sectionName);

        var servers = GetServerMap(configuration, sectionName);

        var existingServer = servers[serverName];

        var sameServer = existingServer is not null && JsonNode.DeepEquals(existingServer, server);

        var existed = File.Exists(outputFile);

        var status = options.Force

            ? existed ? "updated" : "generated"

            : sameServer ? "reused" : existed ? "updated" : "generated";



        servers[serverName] = server;

        var json = configuration.ToJsonString(JsonOptions) + Environment.NewLine;

        if (status != "reused")

        {

            var directory = Path.GetDirectoryName(outputFile);

            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var temporaryFile = outputFile + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try

            {

                File.WriteAllText(temporaryFile, json, new UTF8Encoding(false));

                File.Move(temporaryFile, outputFile, overwrite: true);

            }

            finally

            {

                if (File.Exists(temporaryFile)) File.Delete(temporaryFile);

            }

        }



        return new McpConfigurationGenerationResult(

            status,

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

            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(descriptorBytes)))).ToLowerInvariant(),

            McpRuntimeBindingPolicy.ComputeDescriptorSha256(descriptor),

            launch.PreparationId,

            serverName);

    }



    private static JsonObject CreateServerNode(string format, LoomRuntimeLaunchCommand launch)

    {

        var server = new JsonObject

        {

            ["command"] = launch.Command,

            ["args"] = JsonSerializer.SerializeToNode(launch.Arguments, JsonOptions),

        };

        if (format == "vscode")

        {

            server["type"] = "stdio";

        }



        if (launch.EnvironmentVariables is not null)
        {
            server["env"] = JsonSerializer.SerializeToNode(launch.EnvironmentVariables, JsonOptions);
        }
        return server;

    }



    private static JsonObject LoadConfiguration(string outputFile, string sectionName)

    {

        if (!File.Exists(outputFile))

        {

            return new JsonObject

            {

                [sectionName] = new JsonObject(),

            };

        }



        var json = File.ReadAllText(outputFile);

        try

        {

            return JsonNode.Parse(json) as JsonObject

                ?? throw new InvalidOperationException($"MCP configuration '{outputFile}' must contain a JSON object.");

        }

        catch (JsonException exception)

        {

            throw new InvalidOperationException($"MCP configuration '{outputFile}' is not valid JSON.", exception);

        }

    }



    private static JsonObject GetServerMap(JsonObject configuration, string sectionName)

    {

        if (configuration[sectionName] is JsonObject servers)

        {

            return servers;

        }



        if (configuration[sectionName] is not null)

        {

            throw new InvalidOperationException($"MCP configuration property '{sectionName}' must be a JSON object.");

        }



        var created = new JsonObject();

        configuration[sectionName] = created;

        return created;

    }

}