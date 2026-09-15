using System.Text.Json;

using Techne.Loom.Common.Runtime;

using Techne.Loom.Common.TaskTracking.Runtime;



namespace Techne.Loom.Common.Mcp;



public sealed record McpWorkflowOperationResult(

    McpToolResult Result,

    string Transport,

    McpDispatchStage DispatchStage,

    string? FallbackReason,

    McpServerInfo? ServerInfo);



public static class McpWorkflowOperationClient

{

    private static readonly JsonSerializerOptions ResultOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);
    private static readonly McpSessionRegistry SharedSessionRegistry = new();



    public static async Task<McpWorkflowOperationResult> CallAsync(
        LoomLaunchDescriptor descriptor,
        string toolName,
        JsonElement toolArguments,
        IReadOnlyList<string> cliFallbackArguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(cliFallbackArguments);
        if (toolArguments.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("MCP tool arguments must be an object.", nameof(toolArguments));
        }

        McpToolArguments.RequiredOperationId(toolArguments);
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "MCP operation timeout must be positive.");
        }

        var launch = LoomRuntimeLaunch.CreateMcpCommand(descriptor);
        var serverName = LoomRuntimeCatalog.GetMcpServerName(descriptor.Product, descriptor.ResolvedRuntimeVersion);
        try
        {
            var runtimeDescriptorSha256 = McpRuntimeBindingPolicy.ComputeDescriptorSha256(descriptor);
            var registration = await SharedSessionRegistry.RegisterAsync(
                serverName,
                descriptor.ResolvedRuntimeVersion,
                launch,
                toolName,
                runtimeDescriptorSha256: runtimeDescriptorSha256,
                timeout: effectiveTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var result = await SharedSessionRegistry.CallToolAsync(
                serverName,
                toolName,
                toolArguments,
                cancellationToken).ConfigureAwait(false);
            if (result.IsError)
            {
                throw new McpApplicationException(
                    $"MCP tool '{toolName}' returned an application error.",
                    McpDispatchStage.Dispatched);
            }

            return new McpWorkflowOperationResult(
                result,
                "mcp_stdio",
                McpDispatchStage.Dispatched,
                null,
                registration.Registration.ServerInfo);
        }
        catch (McpDispatchException exception) when (McpCliFallbackPolicy.CanFallback(exception.Stage, exception.Reason))
        {
            var fallback = McpCliFallbackPolicy.CreateCommand(
                descriptor,
                cliFallbackArguments,
                exception.Stage,
                exception.Reason);
            var process = await new DefaultLoomRuntimeProcessRunner().RunAsync(
                fallback.Command,
                fallback.Arguments,
                fallback.WorkingDirectory,
                effectiveTimeout,
                environmentVariables: null,
                cancellationToken).ConfigureAwait(false);
            if (!process.Started)
            {
                throw new LoomRuntimeHostStartupException($"The CLI fallback process '{fallback.Command}' could not start.");
            }

            if (process.ExitCode != 0)
            {
                throw new LoomRuntimeCommandException(
                    $"The CLI fallback failed after MCP pre-dispatch failure with exit code {process.ExitCode}: {FirstNonEmpty(process.StandardError, process.StandardOutput)}");
            }

            try
            {
                using var document = JsonDocument.Parse(process.StandardOutput);
                return new McpWorkflowOperationResult(
                    McpToolResults.Json(document.RootElement.Clone(), ResultOptions),
                    "cli",
                    McpDispatchStage.PreDispatch,
                    exception.Reason,
                    null);
            }
            catch (JsonException jsonException)
            {
                throw new LoomRuntimeCommandException("The CLI fallback returned invalid JSON.", jsonException);
            }
        }
    }    public static Task<McpWorkflowOperationResult> InspectWorkflowFragmentAsync(

        LoomLaunchDescriptor descriptor,

        string workflowFile,

        string operationId,

        string? jsonPointer = null,

        WorkflowFragmentLimits? limits = null,

        TimeSpan? timeout = null,

        CancellationToken cancellationToken = default)

    {

        ArgumentException.ThrowIfNullOrWhiteSpace(workflowFile);

        WorkflowOperationLedger.ValidateOperationId(operationId);

        var effectiveLimits = limits ?? WorkflowFragmentLimits.Default;

        effectiveLimits.Validate();

        var toolPrefix = LoomRuntimeCatalog.GetEntryPoint(descriptor.Product);

        var toolName = $"{toolPrefix}_inspect_workflow_fragment";

        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)

        {

            ["operation_id"] = operationId,

            ["workflow_file"] = Path.GetFullPath(workflowFile),


            ["max_bytes"] = effectiveLimits.MaxBytes,

            ["max_array_items"] = effectiveLimits.MaxArrayItems,

            ["max_object_properties"] = effectiveLimits.MaxObjectProperties,

            ["max_depth"] = effectiveLimits.MaxDepth,

        };

        var cliArguments = new List<string>

        {

            "inspect-workflow-fragment",

            "--workflow-file",

            Path.GetFullPath(workflowFile),

            "--operation-id",

            operationId,

            "--max-bytes",

            effectiveLimits.MaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),

            "--max-array-items",

            effectiveLimits.MaxArrayItems.ToString(System.Globalization.CultureInfo.InvariantCulture),

            "--max-object-properties",

            effectiveLimits.MaxObjectProperties.ToString(System.Globalization.CultureInfo.InvariantCulture),

            "--max-depth",

            effectiveLimits.MaxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),

        };

        if (jsonPointer is not null)

        {

            arguments.Remove("json_pointer");

            arguments["json_pointer"] = jsonPointer;

            cliArguments.Add("--json-pointer");

            cliArguments.Add(jsonPointer);

        }



        return CallAsync(

            descriptor,

            toolName,

            JsonSerializer.SerializeToElement(arguments, ResultOptions),

            cliArguments,

            timeout,

            cancellationToken);

    }



    private static string FirstNonEmpty(string? first, string? second)

        => !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;

}