using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoCommandHandlers
{
    public const string UsageText = "Usage: dotnet ao.dll --guide [--lang <en|zh-cn>] [--section <name>] [--export <path>] | dotnet ao.dll --help | dotnet ao.dll compile --workflow-file <path> [--audit-output <path>] | dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>] | dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id> | dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--instance-file <path>] [--audit-output <path>] | dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path> [--audit-output <path>]\nAO is CLI-only in this project. Compile validates an existing workflow-file and writes Mermaid Markdown, HTML, and workflow JSON backup validation artifacts under the selected audit output root or the default temporary audit root. Prompt-plan and prompt-replan generate AO-owned planner/replanner prompt text through ao_property output, while official AO execution still means explicit run and resume only. Keep checked-in plans and snapshots immutable, and do not place AO session directories, runtime workflow instances, or audit outputs inside a skill folder; use a runtime temp or execution-output root instead.";

    public static async Task<int> HandleGuideAsync(IReadOnlyList<string> args)
    {
        var lang = AoCliOptions.GetOption(args, "--lang") ?? "en";
        var section = AoCliOptions.GetOption(args, "--section");
        var export = AoCliOptions.GetOption(args, "--export");
        var content = await AoGuideContentService.LoadGuideAsync(lang, section).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(export))
        {
            RuntimeArtifactPathGuard.EnsureOutputFileOutsideSkillDirectory(export, "--export");
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

    public static async Task<int> HandleCompileAsync(IReadOnlyList<string> args)
    {
        var workflowFile = AoCliOptions.GetRequiredOption(args, "--workflow-file");
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        EnsureOptionAbsent(args, "--plan-file", "compile");
        EnsureOptionAbsent(args, "--context-file", "compile");

        var json = await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false);
        var auditArtifacts = await WriteCompileValidationArtifactsAsync(workflowFile, auditOutput, json).ConfigureAwait(false);
        Console.Error.WriteLine($"Validation artifacts: {auditArtifacts.StepDirectory}");
        Console.Write(json);
        return 0;
    }

    public static async Task<int> HandlePromptPlanAsync(IReadOnlyList<string> args, AoPropertyWriter writer)
    {
        var objectiveFile = AoCliOptions.GetRequiredOption(args, "--objective-file");
        var contextFile = AoCliOptions.GetOption(args, "--context-file");
        var objective = await File.ReadAllTextAsync(objectiveFile).ConfigureAwait(false);
        var context = await LoadContextAsync(contextFile).ConfigureAwait(false);
        var promptArtifacts = AoPromptBuilder.BuildPlanPromptArtifacts(objective, context);

        WritePromptPayload(
            writer,
            new AoPromptPayload(
                Command: "prompt-plan",
                PromptKind: "plan",
                PromptTemplateVersion: AoPromptBuilder.PromptTemplateVersion,
                Prompt: promptArtifacts.Prompt,
                Blocks: promptArtifacts.Blocks,
                AllowedNodeKinds: promptArtifacts.AllowedNodeKinds,
                AllowedCommandKinds: promptArtifacts.AllowedCommandKinds,
                ObjectiveFile: Path.GetFullPath(objectiveFile),
                ContextFile: string.IsNullOrWhiteSpace(contextFile) ? null : Path.GetFullPath(contextFile),
                RequiresTerminalTbrPath: true));
        return 0;
    }

    public static async Task<int> HandlePromptReplanAsync(IReadOnlyList<string> args, AoPropertyWriter writer)
    {
        var sessionDirectory = AoCliOptions.GetRequiredOption(args, "--session-dir");
        var sessionId = AoCliOptions.GetRequiredOption(args, "--session-id");
        var instanceFile = AoCliOptions.GetRequiredOption(args, "--instance-file");
        var tbrId = AoCliOptions.GetRequiredOption(args, "--tbr-id");
        RuntimeArtifactPathGuard.EnsureSessionDirectoryOutsideSkillDirectory(sessionDirectory);
        RuntimeArtifactPathGuard.EnsureRuntimeWorkflowFileOutsideSkillDirectory(instanceFile, "--instance-file");

        var artifacts = AoSessionArtifactPaths.ResolveExisting(sessionDirectory, sessionId);
        var snapshot = await new AoWorkflowStore().LoadAsync(artifacts.WorkflowFile).ConfigureAwait(false);
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(instanceFile).ConfigureAwait(false));
        if (!instance.Nodes.TryGetValue(tbrId, out var selectedNode) || selectedNode is not ToBeRefinedTransition selectedTbr)
        {
            throw new InvalidOperationException($"Workflow instance '{Path.GetFullPath(instanceFile)}' does not contain a tbr node with id '{tbrId}'.");
        }

        if (string.IsNullOrWhiteSpace(selectedTbr.TargetNodeId))
        {
            throw new InvalidOperationException($"Workflow instance '{Path.GetFullPath(instanceFile)}' contains tbr node '{tbrId}' without a targetNodeId, so AO cannot emit a valid replan contract.");
        }

        var predecessorStateIds = FindPredecessorStateIds(instance, tbrId);
        if (predecessorStateIds.Count == 0)
        {
            throw new InvalidOperationException($"Unable to resolve predecessor state ids for tbr node '{tbrId}'.");
        }

        await RegisterRuntimeWorkflowPointerAsync(artifacts.RuntimeWorkflowPointerFile, instanceFile).ConfigureAwait(false);

        var selectedFrontierAction = TryGetNestedString(snapshot.Context, "plan_meta", "selected_frontier_action")
            ?? snapshot.NextFrontier?.FirstOrDefault()
            ?? "unspecified-frontier-action";
        var remainingTbrIds = instance.GetTransitionNodes().Values
            .OfType<ToBeRefinedTransition>()
            .Select(static transition => transition.Id)
            .Where(id => !string.Equals(id, tbrId, StringComparison.Ordinal))
            .ToArray();
        var promptArtifacts = AoPromptBuilder.BuildReplanPromptArtifacts(
            snapshot.Objective,
            snapshot,
            instance,
            selectedTbr,
            predecessorStateIds,
            selectedFrontierAction,
            remainingTbrIds);

        WritePromptPayload(
            writer,
            new AoPromptPayload(
                Command: "prompt-replan",
                PromptKind: "replan",
                PromptTemplateVersion: AoPromptBuilder.PromptTemplateVersion,
                Prompt: promptArtifacts.Prompt,
                Blocks: promptArtifacts.Blocks,
                AllowedNodeKinds: promptArtifacts.AllowedNodeKinds,
                AllowedCommandKinds: promptArtifacts.AllowedCommandKinds,
                SessionId: artifacts.SessionId,
                WorkflowFile: artifacts.WorkflowFile,
                WorkflowInstanceFile: Path.GetFullPath(instanceFile),
                BoundaryReason: snapshot.LastBoundaryReason,
                PendingRequirements: snapshot.PendingRequirements,
                NextFrontier: snapshot.NextFrontier,
                HumanOrAgentHint: snapshot.HumanOrAgentHint,
                LastTransitionId: snapshot.LastTransitionId,
                SelectedFrontierAction: selectedFrontierAction,
                SelectedTbrId: selectedTbr.Id,
                SelectedTbrPredecessorStateIds: predecessorStateIds,
                SelectedTbrTargetNodeId: selectedTbr.TargetNodeId,
                SelectedTbrDesignNotes: selectedTbr.DesignNotes,
                RemainingTbrIds: remainingTbrIds));
        return 0;
    }

    public static async Task<int> HandleRunAsync(IReadOnlyList<string> args, Runtime.AoRuntimeService runtime, AoPropertyWriter writer)
    {
        var objectiveFile = AoCliOptions.GetRequiredOption(args, "--objective-file");
        var contextFile = AoCliOptions.GetOption(args, "--context-file");
        var instanceFile = AoCliOptions.GetOption(args, "--instance-file");
        var sessionDirectory = AoCliOptions.GetRequiredOption(args, "--session-dir");
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");
        RuntimeArtifactPathGuard.EnsureSessionDirectoryOutsideSkillDirectory(sessionDirectory);
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        if (!string.IsNullOrWhiteSpace(instanceFile))
        {
            RuntimeArtifactPathGuard.EnsureRuntimeWorkflowFileOutsideSkillDirectory(instanceFile, "--instance-file");
        }

        var objective = await File.ReadAllTextAsync(objectiveFile).ConfigureAwait(false);
        var context = await LoadContextAsync(contextFile).ConfigureAwait(false);
        var payload = await runtime.RunAsync(
            objective,
            context,
            sessionDirectory,
            initialInstanceFile: string.IsNullOrWhiteSpace(instanceFile) ? null : Path.GetFullPath(instanceFile),
            auditOutputRoot: auditOutput).ConfigureAwait(false);

        WriteRunPayload(writer, payload);
        return AoExitCodeMapper.Map(payload.Status);
    }

    public static async Task<int> HandleResumeAsync(IReadOnlyList<string> args, Runtime.AoRuntimeService runtime, AoPropertyWriter writer)
    {
        var sessionDirectory = AoCliOptions.GetRequiredOption(args, "--session-dir");
        var sessionId = AoCliOptions.GetRequiredOption(args, "--session-id");
        var resultFile = AoCliOptions.GetRequiredOption(args, "--result-file");
        var auditOutput = AoCliOptions.GetOption(args, "--audit-output");
        RuntimeArtifactPathGuard.EnsureSessionDirectoryOutsideSkillDirectory(sessionDirectory);
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);

        var envelope = await LoadResumeEnvelopeAsync(resultFile).ConfigureAwait(false);
        var payload = await runtime.ResumeAsync(sessionDirectory, sessionId, envelope, auditOutputRoot: auditOutput).ConfigureAwait(false);

        WriteRunPayload(writer, payload);
        return AoExitCodeMapper.Map(payload.Status);
    }

    private static void WriteRunPayload(AoPropertyWriter writer, AoControlPayload payload)
    {
        writer.WriteAoProperty(new AoPropertyEnvelope(
            "progress",
            DateTimeOffset.UtcNow,
            new AoProgressPayload(
                payload.Status,
                payload.SessionId,
                payload.WorkflowFile,
                payload.WorkflowInstanceFile,
                payload.EventLogFile,
                payload.CurrentNodeId,
                payload.BoundaryReason,
                payload.HumanOrAgentHint,
                payload.AuditArtifacts)));

        if (payload.Status == "failed")
        {
            writer.WriteAoProperty(new AoPropertyEnvelope(
                "error",
                DateTimeOffset.UtcNow,
                new AoErrorPayload(
                    payload.SessionId,
                    payload.WorkflowFile,
                    payload.WorkflowInstanceFile,
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

    private static void WritePromptPayload(AoPropertyWriter writer, AoPromptPayload payload)
    {
        writer.WriteAoProperty(new AoPropertyEnvelope(
            "prompt",
            DateTimeOffset.UtcNow,
            payload));
    }

    internal static string RenderWorkflowInstanceMermaidForRuntime(WorkflowInstance instance)
        => RenderWorkflowInstanceMermaid(instance);

    internal static string RenderWorkflowInstanceHtmlForRuntime(WorkflowInstance instance)
        => RenderWorkflowInstanceHtml(instance);

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

    private static async Task RegisterRuntimeWorkflowPointerAsync(string pointerFile, string instanceFile)
    {
        var payload = JsonSerializer.Serialize(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["workflow_instance_file"] = Path.GetFullPath(instanceFile),
                ["updated_at"] = DateTimeOffset.UtcNow,
            },
            WorkflowJsonSerializer.CreateDefaultOptions(indented: true));

        var directory = Path.GetDirectoryName(pointerFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(pointerFile, payload).ConfigureAwait(false);
    }

    private static List<string> FindPredecessorStateIds(WorkflowInstance instance, string transitionId)
    {
        return instance.GetStateNodes().Values
            .Where(state => state.Groups.Any(group => group.TransitionIds.Any(id => string.Equals(id, transitionId, StringComparison.Ordinal))))
            .Select(static state => state.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? TryGetNestedString(IReadOnlyDictionary<string, object?> source, string parentKey, string childKey)
    {
        if (!source.TryGetValue(parentKey, out var parentValue) || parentValue is null)
        {
            return null;
        }

        return parentValue switch
        {
            IDictionary<string, object?> dictionary when dictionary.TryGetValue(childKey, out var childValue) => childValue?.ToString(),
            IReadOnlyDictionary<string, object?> dictionary when dictionary.TryGetValue(childKey, out var childValue) => childValue?.ToString(),
            _ => null,
        };
    }

    private sealed record AoProgressPayload(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("workflow_file")] string WorkflowFile,
        [property: JsonPropertyName("workflow_instance_file")] string WorkflowInstanceFile,
        [property: JsonPropertyName("event_log_file")] string EventLogFile,
        [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
        [property: JsonPropertyName("boundary_reason")] string? BoundaryReason,
        [property: JsonPropertyName("human_or_agent_hint")] string? HumanOrAgentHint,
        [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts);
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

    private static async Task<WorkflowAuditArtifacts> WriteCompileValidationArtifactsAsync(
        string workflowFile,
        string? auditOutputRoot,
        string workflowJson)
    {
        using var document = JsonDocument.Parse(workflowJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Workflow file must contain a single JSON object.");
        }

        if (document.RootElement.TryGetProperty("nodes", out _))
        {
            var instance = WorkflowJsonSerializer.Deserialize(workflowJson);
            ValidateWorkflowInstance(instance);
            return await WriteWorkflowInstanceValidationArtifactsAsync(instance, workflowFile, auditOutputRoot, workflowJson).ConfigureAwait(false);
        }

        var snapshot = JsonSerializer.Deserialize<AoWorkflowSnapshot>(workflowJson, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? throw new InvalidOperationException("Failed to deserialize workflow snapshot.");
        ValidateWorkflowSnapshot(snapshot);
        return await WritePlannerValidationArtifactsAsync(snapshot, workflowFile, auditOutputRoot, workflowJson).ConfigureAwait(false);
    }

    private static async Task<WorkflowAuditArtifacts> WriteWorkflowInstanceValidationArtifactsAsync(
        WorkflowInstance instance,
        string workflowFile,
        string? auditOutputRoot,
        string? workflowJsonOverride = null)
    {
        var workflowJson = workflowJsonOverride ?? WorkflowJsonSerializer.Serialize(instance);
        var mermaid = RenderWorkflowInstanceMermaid(instance);
        var html = RenderWorkflowInstanceHtml(instance);
        var workflowId = Path.GetFileNameWithoutExtension(workflowFile);
        var sequence = Math.Max(1, Math.Max(instance.Version, instance.History.Count));
        return await WorkflowAuditArtifactWriter.WriteAsync(
            string.IsNullOrWhiteSpace(workflowId) ? "ao-compile" : workflowId,
            sequence,
            "compiled",
            workflowJson,
            mermaid,
            html,
            auditOutputRoot).ConfigureAwait(false);
    }

    private static void ValidateWorkflowInstance(WorkflowInstance instance)
    {
        var states = instance.GetStateNodes();
        var transitions = instance.GetTransitionNodes();

        ValidateStateReference(instance.StartNodeId, "startNodeId", states);
        ValidateStateReference(instance.CurrentNodeId, "currentNodeId", states);

        if (!string.IsNullOrWhiteSpace(instance.EndNodeId))
        {
            ValidateStateReference(instance.EndNodeId, "endNodeId", states);
        }

        foreach (var state in states.Values)
        {
            if (string.IsNullOrWhiteSpace(state.WorkflowPhase))
            {
                throw new InvalidOperationException(
                    $"Workflow state '{state.Id}' must declare a non-empty workflowPhase so compile can place the node into the correct workflow swimlane/stage. Set workflowPhase to the overall workflow stage this node belongs to.");
            }

            foreach (var group in state.Groups)
            {
                foreach (var transitionId in group.TransitionIds)
                {
                    if (!transitions.TryGetValue(transitionId, out var transition))
                    {
                        throw new InvalidOperationException($"State '{state.Id}' references missing transition '{transitionId}'.");
                    }

                    if (!string.IsNullOrWhiteSpace(transition.TargetNodeId))
                    {
                        ValidateStateReference(transition.TargetNodeId, $"transition '{transition.Id}' targetNodeId", states);
                    }
                }
            }
        }
    }

    private static void ValidateStateReference(string? stateId, string fieldName, IReadOnlyDictionary<string, StateNode> states)
    {
        if (string.IsNullOrWhiteSpace(stateId))
        {
            throw new InvalidOperationException($"Workflow {fieldName} is required.");
        }

        if (!states.ContainsKey(stateId))
        {
            throw new InvalidOperationException($"Workflow {fieldName} '{stateId}' does not reference an existing state node.");
        }
    }

    private static string RenderWorkflowInstanceMermaid(WorkflowInstance instance)
    {
        var builder = new StringBuilder();
        builder.AppendLine("```mermaid");
        builder.AppendLine();
        builder.AppendLine("flowchart TD");

        var boundaryReason = TryGetMetadataString(instance.Context, "ao_runtime.last_boundary_reason");
        var pendingRequirements = TryGetMetadataStringList(instance.Context, "ao_runtime.pending_requirements");
        var nextFrontier = TryGetMetadataStringList(instance.Context, "ao_runtime.next_frontier");
        var humanHint = TryGetMetadataString(instance.Context, "ao_runtime.human_or_agent_hint");
        var workflowMode = InferAuditWorkflowMode(instance);

        builder.AppendLine($"    wf_meta[\"mode: {EscapeMermaidLabel(workflowMode)}\"]");
        if (!string.IsNullOrWhiteSpace(boundaryReason))
        {
            builder.AppendLine($"    wf_boundary[\"boundary: {EscapeMermaidLabel(boundaryReason)}\"]");
            builder.AppendLine("    wf_meta --- wf_boundary");
        }

        if (pendingRequirements.Count > 0)
        {
            builder.AppendLine($"    wf_requirements[\"pending: {EscapeMermaidLabel(string.Join(", ", pendingRequirements))}\"]");
            builder.AppendLine("    wf_meta --- wf_requirements");
        }

        if (nextFrontier.Count > 0)
        {
            builder.AppendLine($"    wf_frontier[\"frontier: {EscapeMermaidLabel(string.Join(" | ", nextFrontier))}\"]");
            builder.AppendLine("    wf_meta --- wf_frontier");
        }

        if (!string.IsNullOrWhiteSpace(humanHint))
        {
            builder.AppendLine($"    wf_hint[\"hint: {EscapeMermaidLabel(humanHint)}\"]");
            builder.AppendLine("    wf_meta --- wf_hint");
        }

        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static state => state.Id, StringComparer.Ordinal).ToList();
        var transitions = instance.Nodes.Values
            .OfType<TransitionBase>()
            .ToDictionary(static transition => transition.Id, StringComparer.Ordinal);

        foreach (var state in states)
        {
            builder.AppendLine($"    {state.Id}[\"{state.Name}\"]");
        }

        foreach (var state in states)
        {
            foreach (var group in state.Groups)
            {
                foreach (var transitionId in group.TransitionIds)
                {
                    if (!transitions.TryGetValue(transitionId, out var transition) || string.IsNullOrWhiteSpace(transition.TargetNodeId))
                    {
                        continue;
                    }

                    builder.AppendLine($"    {state.Id} -->|{transition.Name}| {transition.TargetNodeId}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(instance.CurrentNodeId))
        {
            builder.AppendLine($"    style {instance.CurrentNodeId} fill:#fff7ed,stroke:#ea580c,stroke-width:3px");
        }

        builder.AppendLine("    style wf_meta fill:#eff6ff,stroke:#2563eb,stroke-width:2px");
        if (!string.IsNullOrWhiteSpace(boundaryReason))
        {
            builder.AppendLine("    style wf_boundary fill:#fef3c7,stroke:#d97706,stroke-width:1px");
        }
        if (pendingRequirements.Count > 0)
        {
            builder.AppendLine("    style wf_requirements fill:#ecfccb,stroke:#65a30d,stroke-width:1px");
        }
        if (nextFrontier.Count > 0)
        {
            builder.AppendLine("    style wf_frontier fill:#ede9fe,stroke:#7c3aed,stroke-width:1px");
        }
        if (!string.IsNullOrWhiteSpace(humanHint))
        {
            builder.AppendLine("    style wf_hint fill:#f3f4f6,stroke:#6b7280,stroke-width:1px");
        }

        builder.AppendLine();
        builder.AppendLine("```");
        return builder.ToString();
    }

    private static string RenderWorkflowInstanceHtml(WorkflowInstance instance)
    {
        var states = instance.Nodes.Values.OfType<StateNode>().OrderBy(static node => node.Id, StringComparer.Ordinal).ToList();
        var transitions = instance.Nodes.Values.OfType<TransitionBase>().OrderBy(static transition => transition.Id, StringComparer.Ordinal).ToList();
        var boundaryReason = TryGetMetadataString(instance.Context, "ao_runtime.last_boundary_reason");
        var pendingRequirements = TryGetMetadataStringList(instance.Context, "ao_runtime.pending_requirements");
        var nextFrontier = TryGetMetadataStringList(instance.Context, "ao_runtime.next_frontier");
        var humanHint = TryGetMetadataString(instance.Context, "ao_runtime.human_or_agent_hint");
        var workflowMode = InferAuditWorkflowMode(instance);
        var builder = new StringBuilder();
        builder.AppendLine("<html><body>");
        builder.AppendLine($"<h1>Workflow {WebUtility.HtmlEncode(instance.InstanceId)}</h1>");
        builder.AppendLine("<div class=\"wf-legend\">Legend</div>");
        builder.AppendLine("<h2>Audit Summary</h2>");
        builder.AppendLine("<table class=\"wf-summary\"><tbody>");
        builder.AppendLine($"<tr><th>Mode</th><td>{WebUtility.HtmlEncode(workflowMode)}</td></tr>");
        builder.AppendLine($"<tr><th>Current Node</th><td>{WebUtility.HtmlEncode(instance.CurrentNodeId)}</td></tr>");
        builder.AppendLine($"<tr><th>Boundary Reason</th><td>{WebUtility.HtmlEncode(boundaryReason ?? string.Empty)}</td></tr>");
        builder.AppendLine($"<tr><th>Pending Requirements</th><td>{WebUtility.HtmlEncode(string.Join(", ", pendingRequirements))}</td></tr>");
        builder.AppendLine($"<tr><th>Next Frontier</th><td>{WebUtility.HtmlEncode(string.Join(", ", nextFrontier))}</td></tr>");
        builder.AppendLine($"<tr><th>Hint</th><td>{WebUtility.HtmlEncode(humanHint ?? string.Empty)}</td></tr>");
        builder.AppendLine("</tbody></table>");

        foreach (var state in states)
        {
            var isActive = string.Equals(state.Id, instance.CurrentNodeId, StringComparison.Ordinal);
            var activeClass = isActive ? " wf-state-active" : string.Empty;
            var icon = isActive ? "&#128293; " : string.Empty;
            builder.AppendLine($"<section class=\"wf-state{activeClass}\">{icon}{WebUtility.HtmlEncode(state.Name)}</section>");
            builder.AppendLine($"<div>Wait={WebUtility.HtmlEncode(state.WaitBehavior.ToString())}</div>");
            foreach (var group in state.Groups)
            {
                builder.AppendLine($"<div>Group {WebUtility.HtmlEncode(group.Id)}</div>");
            }
        }

        builder.AppendLine("<h2>Transitions</h2>");
        builder.AppendLine("<table class=\"wf-transitions\"><thead><tr><th>Source</th><th>Transition</th><th>Target</th><th>Step kind</th><th>Guard</th></tr></thead><tbody>");
        foreach (var transition in transitions)
        {
            var sourceNames = states
                .Where(state => state.Groups.Any(group => group.TransitionIds.Any(id => string.Equals(id, transition.Id, StringComparison.Ordinal))))
                .Select(static state => state.Name)
                .OrderBy(static name => name, StringComparer.Ordinal);
            builder.AppendLine(
                $"<tr><td>{WebUtility.HtmlEncode(string.Join(", ", sourceNames))}</td><td>{WebUtility.HtmlEncode(transition.Name)}</td><td>{WebUtility.HtmlEncode(transition.TargetNodeId ?? string.Empty)}</td><td>{WebUtility.HtmlEncode(transition.StepKind.ToString())}</td><td>{WebUtility.HtmlEncode(transition.GuardExpression ?? string.Empty)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Context Keys</h2>");
        foreach (var key in instance.Context.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            builder.AppendLine($"<div>{WebUtility.HtmlEncode(key)}</div>");
        }

        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static string? TryGetMetadataString(IReadOnlyDictionary<string, object?> context, string key)
    {
        if (!context.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } jsonElement => jsonElement.GetString(),
            _ => value.ToString(),
        };
    }

    private static IReadOnlyList<string> TryGetMetadataStringList(IReadOnlyDictionary<string, object?> context, string key)
    {
        if (!context.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return value switch
        {
            IReadOnlyList<string> strings => strings.ToArray(),
            IEnumerable<object?> objects => objects.Select(static item => item?.ToString()).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray(),
            JsonElement { ValueKind: JsonValueKind.Array } jsonElement => jsonElement.EnumerateArray().Select(static item => item.ToString()).Where(static item => !string.IsNullOrWhiteSpace(item)).ToArray(),
            _ => [],
        };
    }

    private static string InferAuditWorkflowMode(WorkflowInstance instance)
    {
        var transitionCount = instance.Nodes.Values.OfType<TransitionBase>().Count();
        var toBeRefinedCount = instance.Nodes.Values.OfType<ToBeRefinedTransition>().Count();
        var hasRichGraph = transitionCount > 1 || toBeRefinedCount > 1;
        return hasRichGraph ? "caller-authored-or-merged-runtime" : "minimal-sidecar-only";
    }

    private static string EscapeMermaidLabel(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "'", StringComparison.Ordinal);

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
}
