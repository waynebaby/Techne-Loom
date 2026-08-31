using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Common.Mcp;

public static class WorkflowMcpToolSet
{
    public static McpToolRegistry Create(string toolPrefix)
    {
        if (string.IsNullOrWhiteSpace(toolPrefix))
        {
            throw new ArgumentException("An MCP tool prefix is required.", nameof(toolPrefix));
        }

        var registry = new McpToolRegistry();
        registry.Register(new InspectWorkflowFragmentTool($"{toolPrefix}_inspect_workflow_fragment"));
        registry.Register(new InspectWorkflowEventsTool($"{toolPrefix}_inspect_workflow_events"));
        registry.Register(new ListWorkflowArtifactsTool($"{toolPrefix}_list_workflow_artifacts"));
        registry.Register(new RunWorkflowTool($"{toolPrefix}_run_workflow"));
        registry.Register(new ResumeWorkflowTool($"{toolPrefix}_resume_workflow"));
        registry.Register(new GetWorkflowStatusTool($"{toolPrefix}_get_workflow_status"));
        return registry;
    }

    private abstract class WorkflowToolBase : IMcpTool
    {
        protected static readonly JsonSerializerOptions OutputOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);

        protected WorkflowToolBase(string name, string description, string inputSchema)
        {
            Definition = new McpToolDefinition(name, description, ParseSchema(inputSchema));
        }

        public McpToolDefinition Definition { get; }

        public abstract Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default);

        protected static string RequiredPath(JsonElement arguments, string name)
        {
            var value = OptionalString(arguments, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new McpToolInputException($"The '{name}' file path is required.");
            }

            ValidatePathOnly(value, name);
            return value;
        }

        protected static string? OptionalString(JsonElement arguments, string name)
        {
            if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new McpToolInputException($"The '{name}' argument must be a string.");
            }

            return value.GetString();
        }

        protected static int? OptionalInt(JsonElement arguments, string name)
        {
            if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            {
                throw new McpToolInputException($"The '{name}' argument must be a 32-bit integer.");
            }

            return number;
        }

        protected static void ValidatePathOnly(string value, string name)
        {
            var trimmed = value.TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                throw new McpToolInputException($"The '{name}' argument accepts a file path only; inline JSON is not supported.");
            }
        }

        protected static async Task<Dictionary<string, object?>?> LoadContextAsync(
            JsonElement arguments,
            CancellationToken ct)
        {
            var contextFile = OptionalString(arguments, "context_file");
            if (string.IsNullOrWhiteSpace(contextFile))
            {
                return null;
            }

            ValidatePathOnly(contextFile, "context_file");
            var normalizedPath = Path.GetFullPath(contextFile);
            if (!File.Exists(normalizedPath))
            {
                throw new McpToolInputException("The requested input file was not found.");
            }

            var json = await File.ReadAllTextAsync(normalizedPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, OutputOptions)
                ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        protected static async Task<ResumeEnvelope> LoadResumeEnvelopeAsync(
            JsonElement arguments,
            CancellationToken ct)
        {
            var resultFile = RequiredPath(arguments, "result_file");
            ValidatePathOnly(resultFile, "result_file");
            var normalizedPath = Path.GetFullPath(resultFile);
            if (!File.Exists(normalizedPath))
            {
                throw new McpToolInputException("The requested input file was not found.");
            }

            var json = await File.ReadAllTextAsync(normalizedPath, ct).ConfigureAwait(false);
            ResumeEnvelope envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ResumeEnvelope>(json, OutputOptions)
                    ?? throw new McpToolInputException("The resume result envelope is empty.");
            }
            catch (JsonException)
            {
                throw new McpToolInputException("The resume result envelope is not valid JSON.");
            }

            if (string.IsNullOrWhiteSpace(envelope.TransitionId))
            {
                throw new McpToolInputException("The resume result envelope requires a non-empty transition_id.");
            }

            return envelope;
        }

        protected static McpToolResult ExecutionResult(WorkflowFileExecutionResult result)
        {
            var status = result.Status.Status switch
            {
                WorkflowStatus.Succeeded => "completed",
                WorkflowStatus.Failed => "failed",
                WorkflowStatus.WaitingExternal => "blocked",
                _ => "running",
            };
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = status,
                ["workflow_file"] = result.WorkflowFile,
                ["event_log_file"] = result.EventLogFile,
                ["instance_id"] = result.Status.InstanceId,
                ["current_node_id"] = result.Status.CurrentNodeId,
                ["pending_step_kind"] = result.PendingStepKind?.ToString(),
                ["pending_transition_id"] = result.PendingTransitionId,
                ["result_file"] = result.ResultFile,
                ["required_inputs"] = result.RequiredInputs,
                ["next_node_id"] = result.Outcome.NextNodeId,
                ["error_message"] = result.Outcome.ErrorMessage,
            };
            return McpToolResults.Json(payload, OutputOptions);
        }

        private static JsonElement ParseSchema(string schema)
        {
            using var document = JsonDocument.Parse(schema);
            return document.RootElement.Clone();
        }
    }

    private sealed class InspectWorkflowFragmentTool : WorkflowToolBase
    {
        public InspectWorkflowFragmentTool(string name)
            : base(
                name,
                "Return a bounded workflow summary or JSON Pointer fragment. Full workflow values are never returned by default.",
                """
                {
                  "type": "object",
                  "properties": {
                    "workflow_file": { "type": "string", "description": "Existing workflow instance file path." },
                    "json_pointer": { "type": "string", "description": "Optional RFC 6901 JSON Pointer." },
                    "max_bytes": { "type": "integer", "minimum": 128 },
                    "max_array_items": { "type": "integer", "minimum": 1 },
                    "max_object_properties": { "type": "integer", "minimum": 1 },
                    "max_depth": { "type": "integer", "minimum": 1 }
                  },
                  "required": ["workflow_file"],
                  "additionalProperties": false
                }
                """)
        {
        }

        public override async Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var workflowFile = RequiredPath(arguments, "workflow_file");
            var defaults = WorkflowFragmentLimits.Default;
            var limits = new WorkflowFragmentLimits(
                OptionalInt(arguments, "max_bytes") ?? defaults.MaxBytes,
                OptionalInt(arguments, "max_array_items") ?? defaults.MaxArrayItems,
                OptionalInt(arguments, "max_depth") ?? defaults.MaxDepth)
            {
                MaxObjectProperties = OptionalInt(arguments, "max_object_properties") ?? defaults.MaxObjectProperties,
            };
            var result = await WorkflowFragmentReader.ReadAsync(
                workflowFile,
                OptionalString(arguments, "json_pointer"),
                limits,
                ct).ConfigureAwait(false);
            return McpToolResults.Json(result, OutputOptions);
        }
    }

    private sealed class InspectWorkflowEventsTool : WorkflowToolBase
    {
        public InspectWorkflowEventsTool(string name)
            : base(
                name,
                "Return a bounded tail of the workflow event sidecar without returning the full event log.",
                """
                {
                  "type": "object",
                  "properties": {
                    "workflow_file": { "type": "string", "description": "Existing workflow instance file path." },
                    "max_events": { "type": "integer", "minimum": 1 },
                    "max_bytes": { "type": "integer", "minimum": 128 }
                  },
                  "required": ["workflow_file"],
                  "additionalProperties": false
                }
                """)
        {
        }

        public override async Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var workflowFile = RequiredPath(arguments, "workflow_file");
            var defaults = new WorkflowEventFragmentLimits();
            var limits = new WorkflowEventFragmentLimits(
                OptionalInt(arguments, "max_bytes") ?? defaults.MaxBytes,
                OptionalInt(arguments, "max_events") ?? defaults.MaxEvents);
            var result = await WorkflowEventFragmentReader.ReadAsync(workflowFile, limits, ct).ConfigureAwait(false);
            return McpToolResults.Json(result, OutputOptions);
        }
    }

    private sealed class ListWorkflowArtifactsTool : WorkflowToolBase
    {
        public ListWorkflowArtifactsTool(string name)
            : base(
                name,
                "Return the canonical workflow and known event-sidecar artifact manifest without reading full workflow values.",
                """
                {
                  "type": "object",
                  "properties": {
                    "workflow_file": { "type": "string", "description": "Existing workflow instance file path." }
                  },
                  "required": ["workflow_file"],
                  "additionalProperties": false
                }
                """)
        {
        }

        public override Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var workflowFile = RequiredPath(arguments, "workflow_file");
            return Task.FromResult(McpToolResults.Json(WorkflowArtifactManifestReader.Read(workflowFile), OutputOptions));
        }
    }
    private sealed class RunWorkflowTool : WorkflowToolBase
    {
        public RunWorkflowTool(string name)
            : base(
                name,
                "Run a disk-backed workflow until it completes or reaches a required external result boundary.",
                """
                {
                  "type": "object",
                  "properties": {
                    "workflow_file": { "type": "string", "description": "Existing workflow instance file path." },
                    "context_file": { "type": "string", "description": "Optional existing JSON context file path." }
                  },
                  "required": ["workflow_file"],
                  "additionalProperties": false
                }
                """)
        {
        }

        public override async Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var workflowFile = RequiredPath(arguments, "workflow_file");
            var context = await LoadContextAsync(arguments, ct).ConfigureAwait(false);
            var result = await new WorkflowFileExecutionService().RunAsync(workflowFile, context, ct).ConfigureAwait(false);
            return ExecutionResult(result);
        }
    }

    private sealed class ResumeWorkflowTool : WorkflowToolBase
    {
        public ResumeWorkflowTool(string name)
            : base(
                name,
                "Apply one immutable, disk-backed external result to a waiting workflow. The result file is path-only and must include result_id for Plan steps.",
                """
                {
                  "type": "object",
                  "properties": {
                    "workflow_file": { "type": "string", "description": "Existing workflow instance file path." },
                    "result_file": { "type": "string", "description": "Existing structured result envelope file path." }
                  },
                  "required": ["workflow_file", "result_file"],
                  "additionalProperties": false
                }
                """)
        {
        }

        public override async Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var workflowFile = RequiredPath(arguments, "workflow_file");
            var envelope = await LoadResumeEnvelopeAsync(arguments, ct).ConfigureAwait(false);
            var result = await new WorkflowFileExecutionService().ResumeAsync(
                workflowFile,
                envelope.TransitionId,
                envelope.CorrelationKey,
                envelope.Payload,
                envelope.ResultId,
                ct).ConfigureAwait(false);
            return ExecutionResult(result);
        }
    }

    private sealed class GetWorkflowStatusTool : WorkflowToolBase
    {
        public GetWorkflowStatusTool(string name)
            : base(
                name,
                "Return the current status projection for a disk-backed workflow without reading or returning its full contents.",
                """
                {
                  "type": "object",
                  "properties": {
                    "workflow_file": { "type": "string", "description": "Existing workflow instance file path." }
                  },
                  "required": ["workflow_file"],
                  "additionalProperties": false
                }
                """)
        {
        }

        public override async Task<McpToolResult> InvokeAsync(JsonElement arguments, CancellationToken ct = default)
        {
            var workflowFile = RequiredPath(arguments, "workflow_file");
            var result = await new WorkflowFileExecutionService().GetStatusAsync(workflowFile, ct).ConfigureAwait(false);
            return ExecutionResult(result);
        }
    }

    private sealed record ResumeEnvelope(
        [property: JsonPropertyName("transition_id")] string TransitionId,
        [property: JsonPropertyName("correlation_key")] string? CorrelationKey,
        [property: JsonPropertyName("payload")] Dictionary<string, object?>? Payload,
        [property: JsonPropertyName("result_id")] string? ResultId = null);
}
