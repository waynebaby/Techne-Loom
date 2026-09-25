using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.Mcp;
using Techne.Loom.Common.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
namespace Techne.Loom.AgentOrchestrator.Cli;
internal static class AoMcpOperations
{
    public static IMcpTool CreateCompileTool()
        => new DelegateMcpTool(
            "ao_compile_workflow",
            "Validate one disk-backed AO workflow and return structured compile feedback.",
            "{\"type\":\"object\",\"properties\":{\"operation_id\":{\"type\":\"string\"},\"workflow_file\":{\"type\":\"string\"},\"audit_output\":{\"type\":\"string\"}},\"required\":[\"operation_id\",\"workflow_file\"],\"additionalProperties\":false}",
            CompileAsync);
    private static async Task<McpToolResult> CompileAsync(JsonElement arguments, CancellationToken ct)
    {
        var operationId = McpToolArguments.RequiredOperationId(arguments, "operation_id");
        var workflowFile = McpToolArguments.RequiredExistingPath(arguments, "workflow_file");
        var identity = LoomRuntimeIdentity.FromCurrentProcess(
            LoomRuntimeProduct.AgentOrchestrator,
            typeof(AoMcpOperations).Assembly);
        var auditOutput = McpToolArguments.OptionalString(arguments, "audit_output");
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile, ct).ConfigureAwait(false);
        var workflowJson = await File.ReadAllTextAsync(workflowFile, ct).ConfigureAwait(false);
        var workflowHash = Hash(workflowJson);
        WorkflowCompileFeedback feedback;
        try
        {
            var instance = WorkflowJsonSerializer.Deserialize(workflowJson);
            feedback = AoWorkflowCompileValidator.ValidateWorkflowInstance(instance).ToFeedback("agent-orchestrator", "dotnet-ao", workflowFile, workflowHash);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            feedback = new WorkflowCompileFeedback
            {
                Product = "agent-orchestrator",
                Runtime = "dotnet-ao",
                Status = "failed",
                WorkflowPath = workflowFile,
                WorkflowHash = workflowHash,
                Diagnostics =
                [
                    new WorkflowCompileDiagnostic
                    {
                        RuleId = "LOOM.COMPILE.PARSE",
                        Code = "LOOM.COMPILE.PARSE",
                        Category = "syntax",
                        Severity = "error",
                        Message = $"Workflow JSON could not be parsed: {exception.Message}",
                        Location = workflowFile,
                        SuggestedFix = "Fix the workflow JSON and run compile again.",
                        Phase = "parse",
                    },
                ],
            };
        }
        feedback.CandidatePath = workflowFile;
        feedback.CandidateHash = workflowHash;
        feedback.RuntimeIdentity = "Techne.Loom.AgentOrchestrator";
        feedback.RuntimeVersion = identity.Version;
        var feedbackFile = await WriteFeedbackAsync(auditOutput, operationId, feedback, ct).ConfigureAwait(false);
        return McpToolResults.Json(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operation_id"] = operationId,
            ["status"] = feedback.Status == "succeeded" ? "completed" : "failed",
            ["workflow_file"] = workflowFile,
            ["workflow_sha256"] = workflowHash,
            ["runtime_package_id"] = identity.PackageId,
            ["runtime_rid"] = identity.RuntimeIdentifier,
            ["compile_feedback"] = feedback,
            ["artifact_manifest"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["compile_feedback_file"] = feedbackFile,
            },
        }, WorkflowJsonSerializer.CreateDefaultOptions(indented: false));
    }
    private static async Task<string?> WriteFeedbackAsync(string? auditOutput, string operationId, WorkflowCompileFeedback feedback, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(auditOutput))
        {
            return null;
        }
        var directory = Path.GetFullPath(auditOutput);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, operationId + ".compile-feedback.json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, feedback, WorkflowJsonSerializer.CreateDefaultOptions(indented: true), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        return path;
    }
    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}