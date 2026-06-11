using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoCommandHandlers
{
<<<<<<< HEAD
    public const string UsageText = "Usage: dotnet ao.dll --guide [--lang <en|zh-cn>] [--section <name>] [--export <path>] | dotnet ao.dll --help | dotnet ao.dll planner --plan-file <path> --workflow-file <path> [--context-file <path>] [--audit-output <path>] | dotnet ao.dll compile --workflow-file <path> [--audit-output <path>] | dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>] | dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path> [--audit-output <path>]\nAO is CLI-only in this project. Planner drafts from --plan-file. Compile only validates an existing workflow-file and writes Mermaid Markdown, HTML, and workflow JSON backup validation artifacts under the selected audit output root or the default temporary audit root.";
=======
    public const string UsageText = "Usage: dotnet ao.dll --guide | dotnet ao.dll host | dotnet ao.dll planner --plan-file <path> --workflow-file <path> [--context-file <path>] | dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>] | dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path> [--audit-output <path>]";

    public static async Task<int> HandleHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services
            .AddSingleton<Runtime.AoRuntimeService>()
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync().ConfigureAwait(false);
        return 0;
    }
>>>>>>> origin/main

    public static async Task<int> HandleGuideAsync(IReadOnlyList<string> args)
    {
        var lang = AoCliOptions.GetOption(args, "--lang") ?? "en";
        var section = AoCliOptions.GetOption(args, "--section");
        var export = AoCliOptions.GetOption(args, "--export");
        var content = await AoGuideContentService.LoadGuideAsync(lang, section).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(export))
        {
            var directory = Path.GetDirectoryName(export);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(export, content).ConfigureAwait(false);
        }

        Console.Write(content);
        return 0;
    }

    public static async Task<int> HandlePlannerAsync(IReadOnlyList<string> args)
    {
        var planFile = AoCliOptions.GetRequiredOption(args, "--plan-file");
        var workflowFile = AoCliOptions.GetRequiredOption(args, "--workflow-file");
        var contextFile = AoCliOptions.GetOption(args, "--context-file");
<<<<<<< HEAD
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");
=======
>>>>>>> origin/main
        var planText = await File.ReadAllTextAsync(planFile).ConfigureAwait(false);
        var context = await LoadContextAsync(contextFile).ConfigureAwait(false);
        context["plan_text"] = planText;
        context["plan_line_count"] = CountNonEmptyLines(planText);

        var boundaryPlan = Runtime.AoBoundaryPlanner.CreatePlan(context);
        var snapshot = new AoWorkflowSnapshot(
            Objective: planText,
            Context: context,
            Status: "drafting",
            CurrentNodeId: boundaryPlan.CurrentNodeId,
            LastTransitionId: boundaryPlan.TransitionId,
            LastBoundaryReason: boundaryPlan.Reason,
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: boundaryPlan.PendingRequirements,
            NextFrontier: boundaryPlan.NextFrontier,
            HumanOrAgentHint: boundaryPlan.Hint,
            WeaveOutRequest: boundaryPlan.WeaveOutRequest,
            AuditStepSequence: 0);

        var directory = Path.GetDirectoryName(workflowFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            workflowFile,
            JsonSerializer.Serialize(snapshot, WorkflowJsonSerializer.CreateDefaultOptions(indented: true))).ConfigureAwait(false);
<<<<<<< HEAD
        var auditArtifacts = await WritePlannerValidationArtifactsAsync(snapshot, workflowFile, auditOutput).ConfigureAwait(false);
        Console.Error.WriteLine($"Validation artifacts: {auditArtifacts.StepDirectory}");
=======
>>>>>>> origin/main
        Console.Write(await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false));
        return 0;
    }

<<<<<<< HEAD
    public static async Task<int> HandleCompileAsync(IReadOnlyList<string> args)
    {
        var workflowFile = AoCliOptions.GetRequiredOption(args, "--workflow-file");
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");
        EnsureOptionAbsent(args, "--plan-file", "compile");
        EnsureOptionAbsent(args, "--context-file", "compile");

        var json = await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false);
        var snapshot = JsonSerializer.Deserialize<AoWorkflowSnapshot>(json, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? throw new InvalidOperationException("Failed to deserialize workflow snapshot.");
        ValidateWorkflowSnapshot(snapshot);

        var auditArtifacts = await WritePlannerValidationArtifactsAsync(snapshot, workflowFile, auditOutput, json).ConfigureAwait(false);
        Console.Error.WriteLine($"Validation artifacts: {auditArtifacts.StepDirectory}");
        Console.Write(json);
        return 0;
    }

=======
>>>>>>> origin/main
    public static async Task<int> HandleRunAsync(IReadOnlyList<string> args, Runtime.AoRuntimeService runtime, AoPropertyWriter writer)
    {
        var objectiveFile = AoCliOptions.GetRequiredOption(args, "--objective-file");
        var contextFile = AoCliOptions.GetOption(args, "--context-file");
        var sessionDirectory = AoCliOptions.GetRequiredOption(args, "--session-dir");
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");

        var objective = await File.ReadAllTextAsync(objectiveFile).ConfigureAwait(false);
        var context = await LoadContextAsync(contextFile).ConfigureAwait(false);
        var payload = await runtime.RunAsync(objective, context, sessionDirectory, auditOutputRoot: auditOutput).ConfigureAwait(false);

        WriteRunPayload(writer, payload);
        return AoExitCodeMapper.Map(payload.Status);
    }

    public static async Task<int> HandleResumeAsync(IReadOnlyList<string> args, Runtime.AoRuntimeService runtime, AoPropertyWriter writer)
    {
        var sessionDirectory = AoCliOptions.GetRequiredOption(args, "--session-dir");
        var sessionId = AoCliOptions.GetRequiredOption(args, "--session-id");
        var resultFile = AoCliOptions.GetRequiredOption(args, "--result-file");
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");

        var envelope = await LoadResumeEnvelopeAsync(resultFile).ConfigureAwait(false);
        var payload = await runtime.ResumeAsync(sessionDirectory, sessionId, envelope, auditOutputRoot: auditOutput).ConfigureAwait(false);

        WriteRunPayload(writer, payload);
        return AoExitCodeMapper.Map(payload.Status);
    }

    private static void WriteRunPayload(AoPropertyWriter writer, AoControlPayload payload)
    {
        if (payload.Status == "failed")
        {
            writer.WriteAoProperty(new AoPropertyEnvelope(
                "error",
                DateTimeOffset.UtcNow,
                new AoErrorPayload(
                    payload.SessionId,
                    payload.WorkflowFile,
                    payload.EventLogFile,
                    payload.Status,
                    payload.HumanOrAgentHint ?? "AO runtime failed.",
                    payload.ResultFile ?? string.Empty,
                    payload.AuditArtifacts)));
            return;
        }

        writer.WriteAoProperty(new AoPropertyEnvelope(
            payload.Status == "completed" ? "result" : "boundary",
            DateTimeOffset.UtcNow,
            payload));
    }

    private static async Task<Dictionary<string, object?>> LoadContextAsync(string? contextFile)
    {
        if (string.IsNullOrWhiteSpace(contextFile))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var json = await File.ReadAllTextAsync(contextFile).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static async Task<AoResumeEnvelope> LoadResumeEnvelopeAsync(string resultFile)
    {
        var json = await File.ReadAllTextAsync(resultFile).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AoResumeEnvelope>(json, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? throw new InvalidOperationException("Failed to deserialize resume envelope.");
    }

    private static int CountNonEmptyLines(string text)
    {
        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }
<<<<<<< HEAD

    private static void EnsureOptionAbsent(IReadOnlyList<string> args, string name, string commandName)
    {
        if (!string.IsNullOrWhiteSpace(AoCliOptions.GetOption(args, name)))
        {
            throw new InvalidOperationException($"Option '{name}' is not supported for '{commandName}'.");
        }
    }

    private static async Task<WorkflowAuditArtifacts> WritePlannerValidationArtifactsAsync(
        AoWorkflowSnapshot snapshot,
        string workflowFile,
        string? auditOutputRoot,
        string? workflowJsonOverride = null)
    {
        var workflowJson = workflowJsonOverride ?? await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false);
        var mermaid = AoWorkflowSnapshotVisualizer.RenderMermaid(snapshot);
        var html = AoWorkflowSnapshotVisualizer.RenderHtml(snapshot);
        var workflowId = Path.GetFileNameWithoutExtension(workflowFile);
        return await WorkflowAuditArtifactWriter.WriteAsync(
            string.IsNullOrWhiteSpace(workflowId) ? "ao-compile" : workflowId,
            Math.Max(snapshot.AuditStepSequence, 1),
            "compiled",
            workflowJson,
            mermaid,
            html,
            auditOutputRoot).ConfigureAwait(false);
    }

    private static void ValidateWorkflowSnapshot(AoWorkflowSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Objective))
        {
            throw new InvalidOperationException("Workflow snapshot objective is required.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Status))
        {
            throw new InvalidOperationException("Workflow snapshot status is required.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.CurrentNodeId))
        {
            throw new InvalidOperationException("Workflow snapshot current_node_id is required.");
        }
    }
=======
>>>>>>> origin/main
}
