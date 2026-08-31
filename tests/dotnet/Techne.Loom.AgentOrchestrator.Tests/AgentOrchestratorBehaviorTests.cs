using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Cli;
using Techne.Loom.Common.Documentation;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AgentOrchestratorBehaviorTests
{
    [Fact]
    public async Task CliResume_CompletionFlagWithoutTerminalEvidence_RemainsBlocked()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completion-gate-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completion-gate-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Completion requires terminal evidence.");
        await File.WriteAllTextAsync(contextFile, "{}");
        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completion-gate-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(new
        {
            transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
            correlation_key = "completion-gate",
            payload = new Dictionary<string, object?>
            {
                ["mark_completed"] = true,
            },
        }));

        var resume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");
        Assert.Equal(3, resume.ExitCode);
        Assert.Contains("\"status\":\"blocked\"", resume.StdOut);
        Assert.Equal("blocked", await ReadWorkflowStatusAsync(workflowFile));
    }

    [Fact]
    public async Task CliResume_BlockerHistoryIsAppendOnlyAndRoutesReplanStrategy()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-history-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-history-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Retain blocker history before replanning.");
        await File.WriteAllTextAsync(contextFile, "{}");
        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var firstResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-history-first-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(firstResultFile, JsonSerializer.Serialize(new
        {
            transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
            correlation_key = "blocked-attempt-1",
            payload = new Dictionary<string, object?>
            {
                ["blocker_report"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reason"] = "compile-blocked",
                },
                ["attempted_action"] = "retry compile",
                ["outcome"] = "blocked",
                ["attempt_history"] = new List<object?> { "retry compile" },
                ["evidence_references"] = new List<object?> { "audit/step-1/summary.json" },
            },
        }));

        var firstResume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{firstResultFile}\"");
        Assert.Equal(3, firstResume.ExitCode);
        using var firstEnvelope = ReadFinalAoEnvelope(firstResume.StdOut);
        var firstPayload = firstEnvelope.RootElement.GetProperty("payload");
        Assert.Equal("replan_required", firstPayload.GetProperty("boundary_reason").GetString());
        Assert.Equal("state.replan_strategy", firstPayload.GetProperty("current_node_id").GetString());
        Assert.Equal(1, firstPayload.GetProperty("replan_history").GetArrayLength());
        Assert.Equal("boundary.clarification", firstPayload.GetProperty("replan_history")[0].GetProperty("current_node_id").GetString());

        var secondResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-history-second-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(secondResultFile, JsonSerializer.Serialize(new
        {
            transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
            correlation_key = "strategy-1",
            payload = new Dictionary<string, object?>
            {
                ["replan_strategy"] = "continue_from_current",
                ["replan_anchor"] = "boundary.clarification",
                ["candidate_terminal_path"] = new List<object?> { "state.replan_current", "state.done" },
                ["replan_evidence_references"] = new List<object?>                 {                     new Dictionary<string, object?>(StringComparer.Ordinal)                     {                         ["path"] = "docs/en/guides/so-guide.md",                         ["start_line"] = 1,                         ["end_line"] = 5,                         ["role"] = "replan-contract",                     },                 },
            },
        }));

        var secondResume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{secondResultFile}\"");
        Assert.Equal(3, secondResume.ExitCode);
        using var secondEnvelope = ReadFinalAoEnvelope(secondResume.StdOut);
        var secondPayload = secondEnvelope.RootElement.GetProperty("payload");
        Assert.Equal("replan_required", secondPayload.GetProperty("boundary_reason").GetString());
        Assert.Equal("state.replan_current", secondPayload.GetProperty("current_node_id").GetString());
        Assert.Equal(1, secondPayload.GetProperty("replan_history").GetArrayLength());

        var invalidResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-history-invalid-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(invalidResultFile, JsonSerializer.Serialize(new
        {
            transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
            correlation_key = "strategy-invalid-evidence",
            payload = new Dictionary<string, object?>
            {
                ["replan_strategy"] = "full_redesign",
                ["replan_anchor"] = "boundary.clarification",
                ["candidate_terminal_path"] = new List<object?> { "state.replan_full", "state.done" },
                ["replan_evidence_references"] = new List<object?> { "missing/not-a-real-file.json" },
            },
        }));

        var invalidResume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{invalidResultFile}\"");
        Assert.Equal(3, invalidResume.ExitCode);
        using var invalidEnvelope = ReadFinalAoEnvelope(invalidResume.StdOut);
        Assert.Equal("state.replan_strategy", invalidEnvelope.RootElement.GetProperty("payload").GetProperty("current_node_id").GetString());
    }


    [Fact]
    public async Task CliRunThenResume_PersistsWorkflowAndAppendsEvents()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Plan AO implementation route.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("<ao_property>", run.StdOut);
        Assert.Contains("\"type\":\"boundary\"", run.StdOut);
        Assert.Contains("\"boundary_reason\":\"clarification_required\"", run.StdOut);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var eventLogFile = GetEventLogFile(sessionDirectory, sessionId);
        Assert.True(File.Exists(workflowFile));
        Assert.True(File.Exists(eventLogFile));

        var firstTransitionId = await ReadWorkflowTransitionIdAsync(workflowFile);
        Assert.Equal("transition.clarify", firstTransitionId);

        var beforeLines = (await File.ReadAllLinesAsync(eventLogFile)).Length;
        Assert.True(beforeLines > 0);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = firstTransitionId,
                correlation_key = (string?)null,
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                    ["mark_completed"] = true,
                    ["terminal_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)                     {                         ["status"] = "verified",                         ["reference"] = "test-terminal-evidence",                     },
                },
            }));

        var resume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");
        Assert.Equal(0, resume.ExitCode);
        Assert.Contains("\"type\":\"result\"", resume.StdOut);
        Assert.Contains("\"status\":\"completed\"", resume.StdOut);

        var afterLines = (await File.ReadAllLinesAsync(eventLogFile)).Length;
        Assert.True(afterLines > beforeLines);
    }

    [Fact]
    public async Task CliResume_WithConfirmedScope_DoesNotRemainOnClarificationBoundary()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-confirmed-scope-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-confirmed-scope-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Route AO forward after scope confirmation.");
        await File.WriteAllTextAsync(
            contextFile,
            """
            {
              "plan_meta": {
                "selected_frontier_action": "continue_with_confirmed_plan"
              }
            }
            """,
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"boundary_reason\":\"clarification_required\"", run.StdOut);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-confirmed-scope-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
                correlation_key = "confirmed-scope-forward",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                    ["plan_meta"] = new Dictionary<string, object?>
                    {
                        ["selected_frontier_action"] = "continue_with_confirmed_plan",
                    },
                },
            }));

        var resume = await RunCliAsync(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");

        Assert.Equal(3, resume.ExitCode);
        Assert.DoesNotContain("\"boundary_reason\":\"clarification_required\"", resume.StdOut);
        Assert.Contains("\"boundary_reason\":\"tool_probe_required\"", resume.StdOut);
    }

    [Fact]
    public async Task CliResume_WithConfirmedScopeFalse_RemainsOnClarificationBoundary()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-confirmed-scope-false-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-confirmed-scope-false-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Stay on clarification when scope is not confirmed.");
        await File.WriteAllTextAsync(contextFile, "{}", System.Text.Encoding.UTF8);

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-confirmed-scope-false-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
                correlation_key = "confirmed-scope-false",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = false,
                },
            }));

        var resume = await RunCliAsync(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");

        Assert.Equal(3, resume.ExitCode);
        Assert.Contains("\"boundary_reason\":\"clarification_required\"", resume.StdOut);
    }

    [Fact]
    public async Task CliResume_ForcedClarificationBoundary_OverridesConfirmedScope()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-forced-clarification-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-forced-clarification-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Forced clarification should override confirmed scope.");
        await File.WriteAllTextAsync(
            contextFile,
            """
            {
              "force_boundary_reason": "clarification_required"
            }
            """,
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-forced-clarification-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
                correlation_key = "forced-clarification",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                    ["force_boundary_reason"] = "clarification_required",
                },
            }));

        var resume = await RunCliAsync(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");

        Assert.Equal(3, resume.ExitCode);
        Assert.Contains("\"boundary_reason\":\"clarification_required\"", resume.StdOut);
    }

    [Fact]
    public async Task CliRun_WeaveOutBoundary_EmitsStructuredWeaveOutRequest()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-weave-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-weave-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Compare two frontier options.");
        await File.WriteAllTextAsync(contextFile, "{\"force_boundary_reason\":\"weave_out_required\",\"confirmed_scope\":true,\"evidence_references\":[{\"path\":\"docs/en/guides/so-guide.md\",\"start_line\":12,\"end_line\":18,\"role\":\"guide-contract\"}]}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"session_id\":\"", run.StdOut);
        Assert.Contains("\"boundary_reason\":\"weave_out_required\"", run.StdOut);
        Assert.Contains("\"weave_out_request\":{", run.StdOut);
        Assert.Contains("\"objective\":\"compare candidate execution frontiers\"", run.StdOut);
        Assert.Contains("\"artifacts\":[\"frontier-a.json\",\"frontier-b.json\"]", run.StdOut);
    }

    [Fact]
    public async Task CliRun_WeaveOutBoundary_RejectsMixedEvidenceReferencesAtomically()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-weave-invalid-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-weave-invalid-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Compare two frontier options.");
        await File.WriteAllTextAsync(contextFile, "{\"force_boundary_reason\":\"weave_out_required\",\"confirmed_scope\":true,\"evidence_references\":[{\"path\":\"docs/en/guides/so-guide.md\",\"start_line\":12,\"end_line\":18,\"role\":\"guide-contract\"},{\"path\":\"C:\\\\absolute.md\",\"start_line\":1,\"end_line\":2,\"role\":\"invalid\"}]}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("evidence_references", run.StdOut);
        Assert.DoesNotContain("\\\"weave_out_request\\\":{", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_ExistingWorkflowFile_ValidatesWithoutRedrafting()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-audit-{Guid.NewGuid():N}");
        var snapshot = new AoWorkflowSnapshot(
            Objective: "Validate an existing AO workflow snapshot.",
            Context: new Dictionary<string, object?>(StringComparer.Ordinal),
            Status: "drafting",
            CurrentNodeId: "boundary.clarification",
            LastTransitionId: "transition.clarify",
            LastBoundaryReason: "clarification_required",
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: ["scope"],
            NextFrontier: ["ask_user"],
            HumanOrAgentHint: "Ask for missing scope",
            WeaveOutRequest: null,
            AuditStepSequence: 0);
        await File.WriteAllTextAsync(workflowFile, JsonSerializer.Serialize(snapshot, WorkflowJsonSerializer.CreateDefaultOptions(indented: true)));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"status\": \"drafting\"", await File.ReadAllTextAsync(workflowFile));
        Assert.Contains("Validation artifacts:", run.StdErr);
        var feedbackFile = Assert.Single(Directory.GetFiles(auditDirectory, "workflow.compile-feedback.json", SearchOption.AllDirectories));
        using var feedbackDocument = JsonDocument.Parse(await File.ReadAllTextAsync(feedbackFile));
        Assert.Equal("workflow.compile-feedback.v1", feedbackDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("succeeded", feedbackDocument.RootElement.GetProperty("status").GetString());
        Assert.True((await File.ReadAllLinesAsync(feedbackFile)).Length > 1);
        Assert.True(Path.IsPathFullyQualified(feedbackFile));
        var mermaidFile = Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single();
        AssertFileStartsWithMermaidFence(mermaidFile);
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        Assert.StartsWith($"```mermaid{Environment.NewLine}{Environment.NewLine}", mermaid);
        Assert.EndsWith($"{Environment.NewLine}{Environment.NewLine}```{Environment.NewLine}{Environment.NewLine}", mermaid);
        Assert.Contains("style state_start fill:#f8fafc,stroke:#94a3b8,stroke-width:1px", mermaid);
        Assert.Contains("fill:#fee2e2,stroke:#ea580c,stroke-width:3px", mermaid);
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
    }
    [Theory]
    [InlineData("--guide --lang zh-cn")]
    [InlineData("--guide --help")]
    [InlineData("--guide --section Overview")]
    [InlineData("--guide --export guide.md")]
    public async Task CliGuide_LegacyArguments_AreRejected(string command)
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, command);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("accepts no additional arguments", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_ReadOnlyWorkflowFile_SucceedsWithoutMutatingInput()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-readonly-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-readonly-audit-{Guid.NewGuid():N}");
        var snapshot = new AoWorkflowSnapshot(
            Objective: "Validate read-only AO workflow snapshot.",
            Context: new Dictionary<string, object?>(StringComparer.Ordinal),
            Status: "drafting",
            CurrentNodeId: "boundary.clarification",
            LastTransitionId: "transition.clarify",
            LastBoundaryReason: "clarification_required",
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: ["scope"],
            NextFrontier: ["ask_user"],
            HumanOrAgentHint: "Ask for missing scope",
            WeaveOutRequest: null,
            AuditStepSequence: 0);
        await File.WriteAllTextAsync(workflowFile, JsonSerializer.Serialize(snapshot, WorkflowJsonSerializer.CreateDefaultOptions(indented: true)));
        File.SetAttributes(workflowFile, FileAttributes.ReadOnly);

        try
        {
            var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("Validation artifacts:", run.StdErr);
        }
        finally
        {
            File.SetAttributes(workflowFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task CliCompile_WorkflowInstanceFile_SucceedsForPromptPlanAuthoringPath()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-instance-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-instance-audit-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreatePromptReplanWorkflowInstance()));
        var compileInstance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile));
        var compileRoute = Assert.IsType<ExpressionTransition>(compileInstance.Nodes["transition.route_to_review"]);
        compileInstance.Nodes[compileRoute.Id] = compileRoute with { GuardExpression = "true", SucceedExpression = "true" };
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(compileInstance));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Validation artifacts:", run.StdErr);
        Assert.Contains("\"instanceId\": \"prompt-replan-instance\"", run.StdOut);
        var mermaidFile = Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single();
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        Assert.Contains("MainTbr", mermaid);
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
    }

    [Fact]
    public async Task CliCompile_WorkflowInstanceFile_MissingWorkflowPhase_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-phase-{Guid.NewGuid():N}.json");
        var instance = CreatePromptReplanWorkflowInstance();
        var route = Assert.IsType<ExpressionTransition>(instance.Nodes["transition.route_to_review"]);
        instance.Nodes[route.Id] = route with { GuardExpression = "true", SucceedExpression = "true" };
        Assert.IsType<StateNode>(instance.Nodes["state.start"]).WorkflowPhase = null;
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("workflowPhase", run.StdOut);
        Assert.Contains("state.start", run.StdOut);
        Assert.Contains("overall workflow stage", run.StdOut);
        Assert.Contains("node belongs to", run.StdOut);
    }

    [Fact]
    public async Task CliCompile_PreexistingAuditArtifacts_FailsWithoutOverwritingFiles()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-existing-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-existing-audit-{Guid.NewGuid():N}");
        var snapshot = new AoWorkflowSnapshot(
            Objective: "Reject compile overwrite.",
            Context: new Dictionary<string, object?>(StringComparer.Ordinal),
            Status: "drafting",
            CurrentNodeId: "boundary.clarification",
            LastTransitionId: "transition.clarify",
            LastBoundaryReason: "clarification_required",
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: ["scope"],
            NextFrontier: ["ask_user"],
            HumanOrAgentHint: "Ask for missing scope",
            WeaveOutRequest: null,
            AuditStepSequence: 0);
        await File.WriteAllTextAsync(workflowFile, JsonSerializer.Serialize(snapshot, WorkflowJsonSerializer.CreateDefaultOptions(indented: true)));

        var firstRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, firstRun.ExitCode);

        var secondRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, secondRun.ExitCode);
        Assert.Contains("\"type\":\"error\"", secondRun.StdOut);
        Assert.Contains("Refusing to overwrite existing audit artifacts", secondRun.StdOut);
        Assert.Contains("workflow.mermaid.md", secondRun.StdOut);
        Assert.Contains("Choose a different audit output root", secondRun.StdOut);
    }

    [Fact]
    public async Task CliCompile_DefaultAuditRoot_DoesNotCollideAcrossCliInvocations()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-default-audit-{Guid.NewGuid():N}.json");
        var snapshot = new AoWorkflowSnapshot(
            Objective: "Validate default temporary audit root isolation.",
            Context: new Dictionary<string, object?>(StringComparer.Ordinal),
            Status: "drafting",
            CurrentNodeId: "boundary.clarification",
            LastTransitionId: "transition.clarify",
            LastBoundaryReason: "clarification_required",
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: ["scope"],
            NextFrontier: ["ask_user"],
            HumanOrAgentHint: "Ask for missing scope",
            WeaveOutRequest: null,
            AuditStepSequence: 0);
        await File.WriteAllTextAsync(workflowFile, JsonSerializer.Serialize(snapshot, WorkflowJsonSerializer.CreateDefaultOptions(indented: true)));

        var firstRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");
        var secondRun = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\"");

        Assert.Equal(0, firstRun.ExitCode);
        Assert.Equal(0, secondRun.ExitCode);
        Assert.DoesNotContain("Refusing to overwrite existing audit artifacts", secondRun.StdOut);
    }

    [Fact]
    public async Task CliCompile_AuditOutputInsideSkillFolder_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-compile-skill-audit-{Guid.NewGuid():N}.json");
        var skillRoot = CreateSkillRoot();
        var auditDirectory = Path.Combine(skillRoot, "runtime-audit");
        var snapshot = new AoWorkflowSnapshot(
            Objective: "Reject skill-owned audit output.",
            Context: new Dictionary<string, object?>(StringComparer.Ordinal),
            Status: "drafting",
            CurrentNodeId: "boundary.clarification",
            LastTransitionId: "transition.clarify",
            LastBoundaryReason: "clarification_required",
            UpdatedAt: DateTimeOffset.UtcNow,
            PendingRequirements: ["scope"],
            NextFrontier: ["ask_user"],
            HumanOrAgentHint: "Ask for missing scope",
            WeaveOutRequest: null,
            AuditStepSequence: 0);
        await File.WriteAllTextAsync(workflowFile, JsonSerializer.Serialize(snapshot, WorkflowJsonSerializer.CreateDefaultOptions(indented: true)));

        var run = await RunCliAsync(repoRoot, $"compile --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--audit-output", run.StdOut);
        Assert.False(Directory.Exists(auditDirectory));
    }

    [Fact]
    public async Task CliRun_WithAuditOutput_EmitsAuditArtifactLinks()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        await File.WriteAllTextAsync(objectiveFile, "Generate audit artifacts.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\" --workspace-root \"{workspaceRoot}\"");

        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalAoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var audit = payload.GetProperty("audit_artifacts");
        var delivery = audit.GetProperty("mermaid_delivery");
        Assert.Equal(Path.GetFullPath(auditDirectory), audit.GetProperty("output_root").GetString());
        Assert.Equal("workspace_mirror", delivery.GetProperty("status").GetString());
        Assert.Equal("fresh", delivery.GetProperty("generation_status").GetString());
        Assert.True(delivery.GetProperty("artifact_generated").GetBoolean());
        Assert.True(delivery.GetProperty("link_resolvable").GetBoolean());
        Assert.False(delivery.GetProperty("visual_preview_rendered").GetBoolean());
        Assert.False(delivery.GetProperty("card_display_available").GetBoolean());
        var mermaidFile = audit.GetProperty("mermaid_file").GetString()!;
        var htmlFile = audit.GetProperty("html_file").GetString()!;
        var workspaceMermaidFile = delivery.GetProperty("workspace_mermaid_file").GetString()!;
        var workspaceHtmlFile = delivery.GetProperty("workspace_html_file").GetString()!;
        Assert.True(File.Exists(mermaidFile));
        Assert.True(File.Exists(htmlFile));
        Assert.True(File.Exists(audit.GetProperty("workflow_backup_file").GetString()));
        Assert.True(File.Exists(workspaceMermaidFile));
        Assert.True(File.Exists(workspaceHtmlFile));
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceMermaidFile).Replace('\\', '/'), delivery.GetProperty("workspace_relative_mermaid_file").GetString());
        Assert.Equal(Path.GetRelativePath(workspaceRoot, workspaceHtmlFile).Replace('\\', '/'), delivery.GetProperty("workspace_relative_html_file").GetString());
        Assert.True((await File.ReadAllBytesAsync(mermaidFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceMermaidFile)));
        Assert.True((await File.ReadAllBytesAsync(htmlFile)).SequenceEqual(await File.ReadAllBytesAsync(workspaceHtmlFile)));
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Equal(workspaceMermaidFile, mustShowFiles[0]);
        Assert.Equal(workspaceHtmlFile, mustShowFiles[1]);
        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        Assert.StartsWith($"```mermaid{Environment.NewLine}{Environment.NewLine}", mermaid);
        Assert.Contains(Environment.NewLine + "```", mermaid);
        Assert.Contains("\"type\":\"progress\"", run.StdOut);
    }

    [Fact]
    public async Task CliRun_ProgressPayload_EmitsCurrentWorkflowRenderPaths()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-progress-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-progress-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-progress-audit-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(objectiveFile, "Render current AO workflow on progress.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        using var progressEnvelope = ReadAoEnvelope(run.StdOut);
        var payload = progressEnvelope.RootElement.GetProperty("payload");
        Assert.Equal("blocked", payload.GetProperty("status").GetString());
        Assert.Equal(Path.GetFullPath(GetRuntimeWorkflowFile(sessionDirectory, payload.GetProperty("session_id").GetString()!)), payload.GetProperty("workflow_instance_file").GetString());
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.mermaid.md", StringComparison.Ordinal));
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.html", StringComparison.Ordinal));
        Assert.Contains("AO workflow is blocked", payload.GetProperty("workflow_location_summary").GetString());
        Assert.Contains("workflow.mermaid.md", run.StdOut);
        Assert.Contains("workflow.html", run.StdOut);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Length > 0);
        Assert.True(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public async Task CliRun_SessionDirectoryInsideSkillFolder_IsRejectedWithoutCreatingSessionArtifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-skill-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-skill-context-{Guid.NewGuid():N}.json");
        var skillRoot = CreateSkillRoot();
        var sessionDirectory = Path.Combine(skillRoot, "runtime-session");

        await File.WriteAllTextAsync(objectiveFile, "Reject session output inside skill folder.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("skill-owned directory", run.StdOut);
        Assert.Contains("--session-dir", run.StdOut);
        Assert.False(Directory.Exists(sessionDirectory));
    }

    [Fact]
    public async Task CliResume_MalformedEnvelope_ReturnsStableError()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Need clarification.");
        await File.WriteAllTextAsync(contextFile, "{}");
        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        var sessionId = ReadSessionIdFromOutput(run.StdOut);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-mal-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(resultFile, "{\"correlation_key\":\"abc\",\"payload\":{\"confirmed_scope\":true}}");

        var resume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");
        Assert.Equal(2, resume.ExitCode);
        Assert.Contains("<ao_property>", resume.StdOut);
        Assert.Contains("\"type\":\"error\"", resume.StdOut);
        Assert.Contains("transition_id", resume.StdOut);
        using var errorEnvelope = ReadFinalAoEnvelope(resume.StdOut);
        var errorPayload = errorEnvelope.RootElement.GetProperty("payload");
        Assert.Equal(Path.GetFullPath(GetWorkflowFile(sessionDirectory, sessionId)), errorPayload.GetProperty("workflow_file").GetString());
        Assert.Equal(Path.GetFullPath(GetEventLogFile(sessionDirectory, sessionId)), errorPayload.GetProperty("event_log_file").GetString());
        Assert.Equal(Path.GetFullPath(resultFile), errorPayload.GetProperty("result_file").GetString());
        Assert.Contains("resume", errorPayload.GetProperty("workflow_location_summary").GetString());
        var errorMustShowFiles = errorPayload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(errorMustShowFiles, static path => path is not null && path.EndsWith("_workflow.json", StringComparison.Ordinal));
        Assert.Contains(errorMustShowFiles, static path => path is not null && path.EndsWith("_events.jsonl", StringComparison.Ordinal));
        Assert.Contains(Path.GetFullPath(resultFile), errorMustShowFiles);
    }
    [Fact]
    public async Task CliResume_MissingPayload_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-payload-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-payload-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Need clarification.");
        await File.WriteAllTextAsync(contextFile, "{}");
        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-payload-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
                correlation_key = "abc",
                payload = (Dictionary<string, object?>?)null,
            }));

        var resume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");
        Assert.Equal(2, resume.ExitCode);
        Assert.Contains("<ao_property>", resume.StdOut);
        Assert.Contains("\"type\":\"error\"", resume.StdOut);
        Assert.Contains("payload", resume.StdOut);
    }

    [Fact]
    public async Task CliResume_MissingEventLog_IsRejected()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missinglog-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missinglog-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Need event-log continuity check.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);

        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var eventLogFile = GetEventLogFile(sessionDirectory, sessionId);
        var transitionId = await ReadWorkflowTransitionIdAsync(workflowFile);

        Assert.True(File.Exists(eventLogFile));
        File.Delete(eventLogFile);

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missinglog-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = "missing-log-check",
                payload = new Dictionary<string, object?>
                {
                    ["mark_completed"] = true,
                    ["terminal_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)                     {                         ["status"] = "verified",                         ["reference"] = "test-terminal-evidence",                     },
                },
            }));

        var resume = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");
        Assert.Equal(2, resume.ExitCode);
        Assert.Contains("\"type\":\"error\"", resume.StdOut);
        Assert.Contains("missing its event log file", resume.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CliResume_ReplayedTransitionId_IsRejectedWithoutMutatingWorkflow()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replay-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replay-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Advance AO to another boundary.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var eventLogFile = GetEventLogFile(sessionDirectory, sessionId);

        var originalTransitionId = await ReadWorkflowTransitionIdAsync(workflowFile);
        Assert.Equal("transition.clarify", originalTransitionId);

        var advanceResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replay-advance-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            advanceResultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = originalTransitionId,
                correlation_key = "advance-boundary",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                    ["force_boundary_reason"] = "weave_out_required",
                },
            }));

        var advance = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{advanceResultFile}\"");
        Assert.Equal(3, advance.ExitCode);
        Assert.Contains("\"boundary_reason\":\"weave_out_required\"", advance.StdOut);

        var currentTransitionId = await ReadWorkflowTransitionIdAsync(workflowFile);
        Assert.Equal("transition.weave_out", currentTransitionId);
        Assert.NotEqual(originalTransitionId, currentTransitionId);

        var eventLinesBeforeReplay = (await File.ReadAllLinesAsync(eventLogFile)).Length;
        var replayResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replay-stale-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            replayResultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = originalTransitionId,
                correlation_key = "stale-boundary",
                payload = new Dictionary<string, object?>
                {
                    ["mark_completed"] = true,
                    ["terminal_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)                     {                         ["status"] = "verified",                         ["reference"] = "test-terminal-evidence",                     },
                },
            }));

        var replay = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{replayResultFile}\"");
        Assert.Equal(2, replay.ExitCode);
        Assert.Contains("\"type\":\"error\"", replay.StdOut);
        Assert.Contains("does not match the current workflow boundary", replay.StdOut);
        Assert.Equal(currentTransitionId, await ReadWorkflowTransitionIdAsync(workflowFile));
        Assert.Equal(eventLinesBeforeReplay, (await File.ReadAllLinesAsync(eventLogFile)).Length);
    }

    [Fact]
    public async Task CliResume_CompletedWorkflow_IsRejectedWithoutAppendingEvents()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completed-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completed-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Complete AO and reject another resume.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var eventLogFile = GetEventLogFile(sessionDirectory, sessionId);

        var transitionId = await ReadWorkflowTransitionIdAsync(workflowFile);
        var completionResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completed-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            completionResultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = "complete-run",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                    ["mark_completed"] = true,
                    ["terminal_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)                     {                         ["status"] = "verified",                         ["reference"] = "test-terminal-evidence",                     },
                },
            }));

        var completion = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{completionResultFile}\"");
        Assert.Equal(0, completion.ExitCode);
        Assert.Contains("\"status\":\"completed\"", completion.StdOut);
        Assert.Equal("completed", await ReadWorkflowStatusAsync(workflowFile));

        var eventLinesBeforeRejectedResume = (await File.ReadAllLinesAsync(eventLogFile)).Length;
        var replayResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-completed-replay-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            replayResultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = transitionId,
                correlation_key = "replay-completed",
                payload = new Dictionary<string, object?>
                {
                    ["mark_completed"] = true,
                    ["terminal_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)                     {                         ["status"] = "verified",                         ["reference"] = "test-terminal-evidence",                     },
                },
            }));

        var replay = await RunCliAsync(repoRoot, $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{replayResultFile}\"");
        Assert.Equal(2, replay.ExitCode);
        Assert.Contains("\"type\":\"error\"", replay.StdOut);
        Assert.Contains("Workflow is not in a resumable state", replay.StdOut);
        Assert.Contains("completed", replay.StdOut);
        Assert.Equal("completed", await ReadWorkflowStatusAsync(workflowFile));
        Assert.Equal(eventLinesBeforeRejectedResume, (await File.ReadAllLinesAsync(eventLogFile)).Length);
    }

    [Fact]
    public async Task CliHost_IsRejectedAsUnknownCommand()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "host");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Unknown command", run.StdOut);
        Assert.Contains("host", run.StdOut);
    }

    [Fact]
    public async Task CliPlanner_IsRejectedAsUnknownCommand()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "planner");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Unknown command", run.StdOut);
        Assert.Contains("planner", run.StdOut);
    }

    [Fact]
    public async Task CliPromptPlan_EmitsAoOwnedPlannerPrompt()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-plan-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-plan-context-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Plan AO implementation route.");
        await File.WriteAllTextAsync(contextFile, "{\"confirmed_scope\":\"ao-implementation\"}");

        var run = await RunCliAsync(repoRoot, $"prompt-plan --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadFinalAoEnvelope(run.StdOut);
        Assert.Equal("prompt", envelope.RootElement.GetProperty("type").GetString());

        var payload = envelope.RootElement.GetProperty("payload");
        Assert.Equal("prompt-plan", payload.GetProperty("command").GetString());
        Assert.Equal("plan", payload.GetProperty("prompt_kind").GetString());
        Assert.True(payload.GetProperty("requires_terminal_tbr_path").GetBoolean());
        Assert.Contains("state", payload.GetProperty("allowed_node_kinds").EnumerateArray().Select(static item => item.GetString()));
        Assert.Contains("pythonScript", payload.GetProperty("allowed_command_kinds").EnumerateArray().Select(static item => item.GetString()));
        Assert.False(payload.TryGetProperty("sections", out _));

        var outputSchemaBlock = FindPromptBlock(payload, "workflow.output-schema");
        Assert.Equal("guide-contract", outputSchemaBlock.GetProperty("block_kind").GetString());
        Assert.Equal("required", outputSchemaBlock.GetProperty("consumption_requirement").GetString());

        var commandTransitionBlock = FindPromptBlock(payload, "workflow.command-transition-example");
        Assert.Equal("guide-example", commandTransitionBlock.GetProperty("block_kind").GetString());
        Assert.Equal("optional", commandTransitionBlock.GetProperty("consumption_requirement").GetString());

        var workflowProjectionBlock = FindPromptBlock(payload, "workflow.example-projection");
        Assert.Equal("guide-example", workflowProjectionBlock.GetProperty("block_kind").GetString());

        var planningContextBlock = FindPromptBlock(payload, "prompt.plan.runtime-context");
        Assert.Equal("guide-template", planningContextBlock.GetProperty("block_kind").GetString());

        var prompt = payload.GetProperty("prompt").GetString() ?? string.Empty;
        Assert.Contains("Generate the contents of a WorkflowInstance JSON file", prompt);
        Assert.Contains("```guide-contract", prompt);
        Assert.Contains("block_id: workflow.output-schema", prompt);
        Assert.Contains("consumption_requirement: required", prompt);
        Assert.Contains("block_id: workflow.example-projection", prompt);
        Assert.Contains("Plan AO implementation route.", prompt);
    }

    [Fact]
    public async Task CliPromptPlan_PayloadMatchesSnapshot()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-plan-snapshot-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-plan-snapshot-context-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Plan AO implementation route.");
        await File.WriteAllTextAsync(contextFile, "{\"confirmed_scope\":\"ao-implementation\"}");

        var run = await RunCliAsync(repoRoot, $"prompt-plan --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\"");

        Assert.Equal(0, run.ExitCode);
        using var envelope = ReadFinalAoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");

        await AssertPromptPayloadMatchesSnapshotAsync(payload, "prompt-plan.payload.json");
    }

    [Fact]
    public async Task CliPromptReplan_EmitsNodeReplacementPrompt()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-instance-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Replan the blocked workflow instance.");
        await File.WriteAllTextAsync(
            contextFile,
            "{\"plan_meta\":{\"selected_frontier_action\":\"continue_with_confirmed_plan\"}}",
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);

        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(CreatePromptReplanWorkflowInstance()));

        var promptRun = await RunCliAsync(
            repoRoot,
            $"prompt-replan --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --instance-file \"{instanceFile}\" --tbr-id \"transition.main_tbr\"");

        Assert.Equal(0, promptRun.ExitCode);
        using var envelope = ReadFinalAoEnvelope(promptRun.StdOut);
        Assert.Equal("prompt", envelope.RootElement.GetProperty("type").GetString());

        var payload = envelope.RootElement.GetProperty("payload");
        Assert.Equal("prompt-replan", payload.GetProperty("command").GetString());
        Assert.Equal("replan", payload.GetProperty("prompt_kind").GetString());
        Assert.Equal("continue_with_confirmed_plan", payload.GetProperty("selected_frontier_action").GetString());
        Assert.Equal("transition.main_tbr", payload.GetProperty("selected_tbr_id").GetString());
        Assert.Equal("state.review", payload.GetProperty("selected_tbr_predecessor_state_ids")[0].GetString());
        Assert.Equal("state.end", payload.GetProperty("selected_tbr_target_node_id").GetString());
        Assert.Contains("tbr", payload.GetProperty("allowed_node_kinds").EnumerateArray().Select(static item => item.GetString()));
        Assert.False(payload.TryGetProperty("sections", out _));

        var blockedBoundaryBlock = FindPromptBlock(payload, "prompt.replan.blocked-boundary-context");
        Assert.Equal("guide-template", blockedBoundaryBlock.GetProperty("block_kind").GetString());
        Assert.Equal("required", blockedBoundaryBlock.GetProperty("consumption_requirement").GetString());

        var runtimeContextBlock = FindPromptBlock(payload, "prompt.replan.runtime-context");
        Assert.Equal("guide-template", runtimeContextBlock.GetProperty("block_kind").GetString());
        Assert.Equal("required", runtimeContextBlock.GetProperty("consumption_requirement").GetString());

        var currentWorkflowProjectionBlock = FindPromptBlock(payload, "prompt.replan.current-workflow-projection");
        Assert.Equal("guide-example", currentWorkflowProjectionBlock.GetProperty("block_kind").GetString());

        var selectedTbrBlock = FindPromptBlock(payload, "prompt.replan.selected-tbr-projection");
        Assert.Equal("guide-example", selectedTbrBlock.GetProperty("block_kind").GetString());

        var currentWorkflowInstanceBlock = FindPromptBlock(payload, "prompt.replan.current-workflow-instance");
        Assert.Equal("guide-example", currentWorkflowInstanceBlock.GetProperty("block_kind").GetString());

        var prompt = payload.GetProperty("prompt").GetString() ?? string.Empty;
        Assert.Contains("The most recent selected frontier action 'continue_with_confirmed_plan' did not converge.", prompt);
        Assert.Contains("expand the `tbr` node 'transition.main_tbr'", prompt);
        Assert.Contains("carry those decisions forward into the updated WorkflowInstance seam", prompt);
        Assert.Contains("block_id: prompt.replan.runtime-context", prompt);
        Assert.Contains("block_id: prompt.replan.current-workflow-projection", prompt);
        await AssertPromptPayloadMatchesSnapshotAsync(payload, "prompt-replan.payload.json");
        Assert.Contains("block_id: prompt.replan.selected-tbr-projection", prompt);
        Assert.Contains("consumption_requirement: required", prompt);
    }

    [Fact]
    public async Task CliPromptReplan_RuntimeWorkflowEditsFlowIntoNextAuditStep()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-flow-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-flow-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-flow-instance-{Guid.NewGuid():N}.json");
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-flow-audit-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(objectiveFile, "Replan the blocked workflow instance.");
        await File.WriteAllTextAsync(
            contextFile,
            "{\"plan_meta\":{\"selected_frontier_action\":\"continue_with_confirmed_plan\"}}",
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var runtimeWorkflowFile = GetRuntimeWorkflowFile(sessionDirectory, sessionId);
        var runtimeWorkflowPointerFile = GetRuntimeWorkflowPointerFile(sessionDirectory, sessionId);

        var initialInstance = CreatePromptReplanWorkflowInstance();
        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(initialInstance));

        var promptRun = await RunCliAsync(
            repoRoot,
            $"prompt-replan --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --instance-file \"{instanceFile}\" --tbr-id \"transition.main_tbr\"");

        Assert.Equal(0, promptRun.ExitCode);

        var updatedInstance = CreatePromptReplanWorkflowInstance();
        updatedInstance.Nodes.Remove("transition.main_tbr");
        updatedInstance.Nodes["transition.route_confirmed_scope"] = new ExpressionTransition
        {
            Id = "transition.route_confirmed_scope",
            Name = "RouteConfirmedScope",
            TargetNodeId = "state.end",
            GuardExpression = "True",
            SucceedExpression = "True",
            StepKind = WorkflowStepKind.ToolCall,
            Priority = 0,
        };

        var reviewState = (StateNode)updatedInstance.Nodes["state.review"];
        reviewState.Groups[0].TransitionIds[0] = "transition.route_confirmed_scope";
        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(updatedInstance));

        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-replan-flow-result-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
                correlation_key = "replan-flow",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                },
            }));

        var resume = await RunCliAsync(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(3, resume.ExitCode);
        using var envelope = ReadFinalAoEnvelope(resume.StdOut);
        var resumedPayload = envelope.RootElement.GetProperty("payload");
        Assert.Equal(Path.GetFullPath(runtimeWorkflowFile), resumedPayload.GetProperty("workflow_instance_file").GetString());
        using var pointer = JsonDocument.Parse(await File.ReadAllTextAsync(runtimeWorkflowPointerFile));
        Assert.Equal(Path.GetFullPath(runtimeWorkflowFile), pointer.RootElement.GetProperty("workflow_instance_file").GetString());
        var audit = envelope.RootElement.GetProperty("payload").GetProperty("audit_artifacts");
        var workflowBackupFile = audit.GetProperty("workflow_backup_file").GetString()!;
        var workflowJson = await File.ReadAllTextAsync(workflowBackupFile);

        using var document = JsonDocument.Parse(workflowJson);
        Assert.True(document.RootElement.TryGetProperty("nodes", out var nodes));
        Assert.True(nodes.TryGetProperty("transition.route_confirmed_scope", out _));
        Assert.False(nodes.TryGetProperty("transition.main_tbr", out _));
    }

    [Fact]
    public async Task CliRun_WithAuthoredInstanceFile_SeedsFirstRuntimeAuditFromThatGraph()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-seeded-run-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-seeded-run-context-{Guid.NewGuid():N}.json");
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-seeded-run-instance-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-seeded-run-audit-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(objectiveFile, "Execute authored workflow instance.");
        await File.WriteAllTextAsync(contextFile, "{\"confirmed_scope\":true}");

        var instance = CreatePromptReplanWorkflowInstance();
        instance.InstanceId = "workflow-instance";
        instance.CurrentNodeId = "state.review";
        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(instance));

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --instance-file \"{instanceFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalAoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.Equal(Path.GetFullPath(instanceFile), payload.GetProperty("workflow_instance_file").GetString());

        var audit = payload.GetProperty("audit_artifacts");
        var workflowBackupFile = audit.GetProperty("workflow_backup_file").GetString()!;
        var workflowJson = await File.ReadAllTextAsync(workflowBackupFile);
        using var document = JsonDocument.Parse(workflowJson);
        Assert.True(document.RootElement.TryGetProperty("nodes", out var nodes));
        Assert.True(nodes.TryGetProperty("transition.main_tbr", out _));
        Assert.True(nodes.TryGetProperty("transition.remaining_tbr", out _));

        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        Assert.True(File.Exists(GetRuntimeWorkflowFile(sessionDirectory, sessionId)));
    }

    [Fact]
    public async Task CliRun_WithoutInstanceFile_AuditArtifactsExposeBlockedMetadata()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-metadata-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-metadata-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-metadata-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(objectiveFile, "Emit richer blocked audit metadata.");
        await File.WriteAllTextAsync(contextFile, "{}", System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadFinalAoEnvelope(run.StdOut);
        var audit = envelope.RootElement.GetProperty("payload").GetProperty("audit_artifacts");
        var mermaidFile = audit.GetProperty("mermaid_file").GetString()!;
        var htmlFile = audit.GetProperty("html_file").GetString()!;

        var mermaid = await File.ReadAllTextAsync(mermaidFile);
        Assert.Contains("mode: minimal-sidecar-only", mermaid);
        Assert.Contains("boundary: clarification_required", mermaid);
        Assert.Contains("pending: confirmed_scope", mermaid);
        Assert.Contains("frontier: confirm_target_scope | continue_with_confirmed_plan", mermaid);

        var html = await File.ReadAllTextAsync(htmlFile);
        Assert.Contains("Audit Summary", html);
        Assert.Contains("minimal-sidecar-only", html);
        Assert.Contains("clarification_required", html);
        Assert.Contains("confirmed_scope", html);
        Assert.Contains("confirm_target_scope, continue_with_confirmed_plan", html);
    }

    [Fact]
    public async Task CliRun_AuditSummaryAndEventLogProvideReplayMetadata()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-summary-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-summary-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-summary-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(objectiveFile, "Emit replayable audit metadata.");
        await File.WriteAllTextAsync(contextFile, "{}", System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var eventLogFile = GetEventLogFile(sessionDirectory, sessionId);

        using var envelope = ReadFinalAoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var mustShowFiles = payload.GetProperty("must_show_to_user_files").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.mermaid.md", StringComparison.Ordinal));
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("workflow.html", StringComparison.Ordinal));
        Assert.Contains(mustShowFiles, static path => path is not null && path.EndsWith("summary.json", StringComparison.Ordinal));
        Assert.Contains("AO workflow is blocked", payload.GetProperty("workflow_location_summary").GetString());
        var audit = envelope.RootElement.GetProperty("payload").GetProperty("audit_artifacts");
        var summaryFile = audit.GetProperty("summary_file").GetString()!;
        Assert.True(File.Exists(summaryFile));

        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(summaryFile));
        Assert.Equal("blocked", summary.RootElement.GetProperty("status").GetString());
        Assert.Equal("clarification_required", summary.RootElement.GetProperty("boundary_reason").GetString());
        Assert.Equal(Path.GetFullPath(workflowFile), summary.RootElement.GetProperty("workflow_file").GetString());
        Assert.Equal("confirmed_scope", summary.RootElement.GetProperty("pending_requirements")[0].GetString());
        Assert.Equal("confirm_target_scope", summary.RootElement.GetProperty("next_frontier")[0].GetString());
        Assert.Equal(summaryFile, summary.RootElement.GetProperty("audit_artifacts").GetProperty("summary_file").GetString());

        var eventLines = await File.ReadAllLinesAsync(eventLogFile);
        using var boundaryEvent = JsonDocument.Parse(eventLines.Single(line => line.Contains("\"event_type\":\"boundary\"", StringComparison.Ordinal)));
        Assert.Equal("clarification_required", boundaryEvent.RootElement.GetProperty("boundary_reason").GetString());
        Assert.Equal(1, boundaryEvent.RootElement.GetProperty("step_sequence").GetInt32());
        Assert.Contains("step-0001-", boundaryEvent.RootElement.GetProperty("step_directory").GetString());
        Assert.Contains("blocked-clarification_required", boundaryEvent.RootElement.GetProperty("step_directory").GetString());
        Assert.Equal(summaryFile, boundaryEvent.RootElement.GetProperty("summary_file").GetString());
        Assert.Equal("confirmed_scope", boundaryEvent.RootElement.GetProperty("pending_requirements")[0].GetString());
        Assert.Equal("confirm_target_scope", boundaryEvent.RootElement.GetProperty("next_frontier")[0].GetString());
        Assert.Contains($"session_{sessionId}_runtime.workflow.json", boundaryEvent.RootElement.GetProperty("workflow_instance_file").GetString());
    }

    [Fact]
    public async Task CliResume_WithMissingExternalRuntimeWorkflowPointer_FallsBackToRuntimeSidecarPath()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-pointer-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-pointer-context-{Guid.NewGuid():N}.json");
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-pointer-instance-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-missing-pointer-result-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Resume after deleting the external runtime workflow file.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var instance = CreatePromptReplanWorkflowInstance();
        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(instance));

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --instance-file \"{instanceFile}\" --session-dir \"{sessionDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var runtimeWorkflowFile = GetRuntimeWorkflowFile(sessionDirectory, sessionId);
        var runtimeWorkflowPointerFile = GetRuntimeWorkflowPointerFile(sessionDirectory, sessionId);

        Assert.True(File.Exists(runtimeWorkflowFile));
        Assert.True(File.Exists(runtimeWorkflowPointerFile));

        File.Delete(instanceFile);

        await File.WriteAllTextAsync(
            resultFile,
            JsonSerializer.Serialize(new
            {
                transition_id = await ReadWorkflowTransitionIdAsync(workflowFile),
                correlation_key = "missing-pointer-fallback",
                payload = new Dictionary<string, object?>
                {
                    ["confirmed_scope"] = true,
                },
            }));

        var resume = await RunCliAsync(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{resultFile}\"");

        Assert.Equal(3, resume.ExitCode);
        using var envelope = ReadFinalAoEnvelope(resume.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.Equal(Path.GetFullPath(runtimeWorkflowFile), payload.GetProperty("workflow_instance_file").GetString());
    }

    [Fact]
    public async Task CliPromptReplan_RuntimeContextCarriesDurableFacts()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-runtime-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-runtime-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-runtime-instance-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Replan the blocked workflow instance.");
        await File.WriteAllTextAsync(
            contextFile,
            """
            {
              "confirmed_scope": {
                "area": "ao-runtime",
                "mode": "repo-src-debug"
              },
              "probe_report": {
                "status": "fresh",
                "summary": "repo structure inspected"
              },
              "plan_meta": {
                "selected_frontier_action": "probe_repo_structure",
                "next_step_prompt": "Carry probe facts back into the seam."
              }
            }
            """,
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);

        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(CreatePromptReplanWorkflowInstance()));

        var promptRun = await RunCliAsync(
            repoRoot,
            $"prompt-replan --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --instance-file \"{instanceFile}\" --tbr-id \"transition.main_tbr\"");

        Assert.Equal(0, promptRun.ExitCode);
        using var envelope = ReadFinalAoEnvelope(promptRun.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");

        Assert.Equal("probe_repo_structure", payload.GetProperty("selected_frontier_action").GetString());

        var runtimeContextBlock = FindPromptBlock(payload, "prompt.replan.runtime-context");
        var runtimeContextContent = runtimeContextBlock.GetProperty("content").GetString() ?? string.Empty;
        Assert.Contains("probe_report", runtimeContextContent);
        Assert.Contains("repo structure inspected", runtimeContextContent);
        Assert.Contains("next_step_prompt", runtimeContextContent);

        var prompt = payload.GetProperty("prompt").GetString() ?? string.Empty;
        Assert.Contains("block_id: prompt.replan.runtime-context", prompt);
        Assert.Contains("carry those decisions forward into the updated WorkflowInstance seam", prompt);
    }

    [Fact]
    public async Task CliPromptReplan_SelectedTbrWithoutTargetNodeId_ReturnsStableError()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-invalid-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-invalid-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-invalid-instance-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Replan the blocked workflow instance.");
        await File.WriteAllTextAsync(
            contextFile,
            "{\"plan_meta\":{\"selected_frontier_action\":\"continue_with_confirmed_plan\"}}",
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);

        var invalidInstance = CreatePromptReplanWorkflowInstance(selectedMainTbrTargetNodeId: null);
        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(invalidInstance));

        var promptRun = await RunCliAsync(
            repoRoot,
            $"prompt-replan --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --instance-file \"{instanceFile}\" --tbr-id \"transition.main_tbr\"");

        Assert.Equal(2, promptRun.ExitCode);
        Assert.Contains("<ao_property>", promptRun.StdOut);
        Assert.Contains("\"type\":\"error\"", promptRun.StdOut);
        Assert.Contains("without a targetNodeId", promptRun.StdOut);
    }

    [Fact]
    public async Task CliPromptReplan_InvalidInstance_DoesNotRegisterRuntimeWorkflowPointer()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-invalid-pointer-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-invalid-pointer-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var instanceFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-prompt-replan-invalid-pointer-instance-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Replan the blocked workflow instance.");
        await File.WriteAllTextAsync(
            contextFile,
            "{\"plan_meta\":{\"selected_frontier_action\":\"continue_with_confirmed_plan\"}}",
            System.Text.Encoding.UTF8);

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);

        var invalidInstance = CreatePromptReplanWorkflowInstance(selectedMainTbrTargetNodeId: null);
        await File.WriteAllTextAsync(instanceFile, WorkflowJsonSerializer.Serialize(invalidInstance));

        var promptRun = await RunCliAsync(
            repoRoot,
            $"prompt-replan --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --instance-file \"{instanceFile}\" --tbr-id \"transition.main_tbr\"");

        Assert.Equal(2, promptRun.ExitCode);
        Assert.False(File.Exists(GetRuntimeWorkflowPointerFile(sessionDirectory, sessionId)));
    }

    [Fact]
    public async Task CliHelp_ListsExpectedDotnetAoDllParameters()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "--help");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("dotnet ao.dll --guide", run.StdOut);
        Assert.Contains("dotnet ao.dll --help", run.StdOut);
        Assert.Contains("dotnet ao.dll --patch", run.StdOut);
        Assert.Contains("--patch-content-file <path>", run.StdOut);
        Assert.Contains("--patch-target <path>", run.StdOut);
        Assert.Contains("--from-line <n>", run.StdOut);
        Assert.Contains("--to-line <n>", run.StdOut);
        Assert.Contains("dotnet ao.dll compile", run.StdOut);
        Assert.Contains("dotnet ao.dll prompt-plan", run.StdOut);
        Assert.Contains("dotnet ao.dll prompt-replan", run.StdOut);
        Assert.Contains("dotnet ao.dll run", run.StdOut);
        Assert.Contains("--instance-file <path>", run.StdOut);
        Assert.Contains("dotnet ao.dll resume", run.StdOut);
        Assert.Contains("dotnet ao.dll inspect-workflow-fragment", run.StdOut);
        Assert.Contains("fragment is null and truncation metadata explains why", run.StdOut);
        Assert.DoesNotContain("dotnet ao.dll host", run.StdOut);
    }

    [Theory]
    [InlineData("compile", "--workflow-file")]
    [InlineData("--patch", "--patch-content-file")]
    [InlineData("prompt-plan", "--objective-file")]
    [InlineData("prompt-replan", "--session-dir")]
    [InlineData("run", "--objective-file")]
    [InlineData("resume", "--session-dir")]
    [InlineData("inspect-workflow-fragment", "--workflow-file")]
    public async Task CliRequiredDotnetAoDllParameters_MissingOptionsReturnStableError(string command, string requiredOption)
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, command);
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("<ao_property>", run.StdOut);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Missing required option", run.StdOut);
        Assert.Contains(requiredOption, run.StdOut);
    }

    [Fact]
    public async Task CliPatch_ReplacesRequestedLineRange()
    {
        var repoRoot = FindRepositoryRoot();
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-patch-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-patch-content-{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(targetFile, "line1\r\nline2\r\nline3\r\nline4\r\n");
        await File.WriteAllTextAsync(patchFile, "new2\r\nnew3\r\n");

        var run = await RunCliAsync(repoRoot, $"--patch --patch-content-file \"{patchFile}\" --patch-target \"{targetFile}\" --from-line 2 --to-line 3");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"applied_from_line\":2", run.StdOut);
        Assert.Contains("\"applied_to_line\":3", run.StdOut);
        Assert.Equal("line1\r\nnew2\r\nnew3\r\nline4\r\n", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task CliPatch_InvalidRange_ReturnsStableErrorAndDoesNotModifyFile()
    {
        var repoRoot = FindRepositoryRoot();
        var targetFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-patch-invalid-target-{Guid.NewGuid():N}.txt");
        var patchFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-patch-invalid-content-{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(targetFile, "line1\nline2\n");
        await File.WriteAllTextAsync(patchFile, "replacement\n");

        var run = await RunCliAsync(repoRoot, $"--patch --patch-content-file \"{patchFile}\" --patch-target \"{targetFile}\" --from-line 5 --to-line 9");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("exceeds the target file line count", run.StdOut);
        Assert.Equal("line1\nline2\n", await File.ReadAllTextAsync(targetFile));
    }
    [Fact]
    public async Task CliGuide_ReturnsInstalledEnglishBundlePaths()
    {
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "--guide");

        Assert.Equal(0, run.ExitCode);
        using var document = JsonDocument.Parse(run.StdOut);
        var payload = document.RootElement;
        var version = payload.GetProperty("version").GetString() ?? throw new InvalidOperationException("Guide JSON did not contain version.");
        var docsRoot = payload.GetProperty("docs_root").GetString() ?? throw new InvalidOperationException("Guide JSON did not contain docs_root.");
        var guidePath = payload.GetProperty("guide_path").GetString() ?? throw new InvalidOperationException("Guide JSON did not contain guide_path.");

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.True(Path.IsPathFullyQualified(docsRoot));
        Assert.True(Path.IsPathFullyQualified(guidePath));
        Assert.True(Directory.Exists(docsRoot));
        Assert.True(File.Exists(guidePath));
        Assert.StartsWith(Path.GetFullPath(docsRoot), Path.GetFullPath(guidePath), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(docsRoot, "zh-cn")));

        var guide = await File.ReadAllTextAsync(guidePath);
        Assert.Contains($"Version: {version}", guide);
        Assert.Contains($"Build: published package {version}", guide);
        Assert.InRange(guide.Split(["\r\n", "\n"], StringSplitOptions.None).Length, 1, 200);
        var flowPath = Path.Combine(docsRoot, "guides", "ao-guide-flow.md");
        var referencePath = Path.Combine(docsRoot, "guides", "ao-guide-reference.md");
        Assert.True(File.Exists(flowPath));
        Assert.True(File.Exists(referencePath));
        var referenceContractsPath = Path.Combine(docsRoot, "guides", "ao-guide-reference-contracts.md");
        Assert.True(File.Exists(referenceContractsPath));
        Assert.Contains("direct line-range patch path", await File.ReadAllTextAsync(referenceContractsPath));
    }

    [Fact]
    public async Task DocumentationBundleInstaller_UsesDirectPackageDocs()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(AoCommandHandlers).Assembly.Location)!;
        var incompleteBaseDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-docs-incomplete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(incompleteBaseDirectory);

        try
        {
            var result = await DocumentationBundleInstaller.InstallAsync(
                typeof(AoCommandHandlers).Assembly,
                "guides/ao-guide.md",
                new DocumentationBundleInstallOptions { BaseDirectory = incompleteBaseDirectory });

            var expectedRoot = Path.GetFullPath(Path.Combine(assemblyDirectory, "docs", "en"));
        Assert.Equal(expectedRoot, result.DocsRoot, StringComparer.OrdinalIgnoreCase);
        Assert.False(result.IsPartial);
        Assert.Empty(result.Warnings);
        Assert.True(File.Exists(result.GuidePath));
        var guide = await File.ReadAllTextAsync(result.GuidePath);
        Assert.Contains($"Version: {result.Version}", guide);
        Assert.Contains($"Build: published package {result.Version}", guide);
        Assert.DoesNotContain(
            typeof(AoCommandHandlers).Assembly.GetManifestResourceNames(),
            resourceName => resourceName.EndsWith("Techne.Loom.DocsBundle.zip", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(incompleteBaseDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DocumentationBundleInstaller_RejectsUnsafeGuidePath()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(AoCommandHandlers).Assembly.Location)!;

        await Assert.ThrowsAsync<DocumentationBundleInstallException>(() =>
            DocumentationBundleInstaller.InstallAsync(
                typeof(AoCommandHandlers).Assembly,
                "../ao-guide.md",
                new DocumentationBundleInstallOptions { BaseDirectory = assemblyDirectory }));
    }
    [Fact]
    public async Task CliResume_ConcurrentProcesses_AllowOnlyOneWinnerPerTransition()
    {
        var repoRoot = FindRepositoryRoot();
        var sessionDirectory = CreateSessionDirectory();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-concurrent-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-concurrent-context-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(objectiveFile, "Validate concurrent resume protection.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        var sessionId = ReadSessionIdFromOutput(run.StdOut);
        var workflowFile = GetWorkflowFile(sessionDirectory, sessionId);
        var eventLogFile = GetEventLogFile(sessionDirectory, sessionId);

        var transitionId = await ReadWorkflowTransitionIdAsync(workflowFile);
        var firstResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-concurrent-result-a-{Guid.NewGuid():N}.json");
        var secondResultFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-concurrent-result-b-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(firstResultFile, CreateCompletionEnvelopeJson(transitionId, "resume-a"));
        await File.WriteAllTextAsync(secondResultFile, CreateCompletionEnvelopeJson(transitionId, "resume-b"));

        using var firstProcess = Process.Start(CreateCliStartInfo(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{firstResultFile}\"")) ?? throw new InvalidOperationException("Failed to start first AO resume process.");

        using var secondProcess = Process.Start(CreateCliStartInfo(
            repoRoot,
            $"resume --session-dir \"{sessionDirectory}\" --session-id \"{sessionId}\" --result-file \"{secondResultFile}\"")) ?? throw new InvalidOperationException("Failed to start second AO resume process.");

        var outcomes = await Task.WhenAll(
            ReadProcessResultAsync(firstProcess),
            ReadProcessResultAsync(secondProcess));

        Assert.Equal(1, outcomes.Count(outcome => outcome.IsSuccess));
        Assert.Equal(1, outcomes.Count(outcome => !outcome.IsSuccess));
        Assert.Contains(outcomes, outcome => outcome.ErrorMessage?.Contains("not in a resumable state", StringComparison.Ordinal) == true);
        Assert.Equal("completed", await ReadWorkflowStatusAsync(workflowFile));

        var eventLines = await File.ReadAllLinesAsync(eventLogFile);
        Assert.Equal(1, eventLines.Count(line => line.Contains("\"event_type\":\"status_change\"", StringComparison.Ordinal)
            && line.Contains("\"to_status\":\"completed\"", StringComparison.Ordinal)));
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string repoRoot, string arguments)
    {
        using var process = Process.Start(CreateCliStartInfo(repoRoot, arguments))
            ?? throw new InvalidOperationException("Failed to start AO CLI process.");
        return await ReadCliResultAsync(process);
    }

    private static async Task<string> ReadWorkflowTransitionIdAsync(string workflowFile)
    {
        var json = await File.ReadAllTextAsync(workflowFile);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("last_transition_id").GetString()
            ?? throw new InvalidOperationException("Workflow snapshot did not contain last_transition_id.");
    }

    private static async Task<string> ReadWorkflowStatusAsync(string workflowFile)
    {
        var json = await File.ReadAllTextAsync(workflowFile);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("status").GetString()
            ?? throw new InvalidOperationException("Workflow snapshot did not contain status.");
    }

    private static ProcessStartInfo CreateCliStartInfo(string repoRoot, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{GetCliAssemblyPath()}\" {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static async Task<(bool IsSuccess, string? ErrorMessage)> ReadProcessResultAsync(Process process)
    {
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return (true, null);
        }

        var message = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        return (false, message);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> ReadCliResultAsync(Process process)
    {
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private static string CreateCompletionEnvelopeJson(string transitionId, string correlationKey)
    {
        return JsonSerializer.Serialize(new
        {
            transition_id = transitionId,
            correlation_key = correlationKey,
            payload = new Dictionary<string, object?>
            {
                ["mark_completed"] = true,
                ["terminal_evidence"] = new Dictionary<string, object?>(StringComparer.Ordinal)                 {                     ["status"] = "verified",                     ["reference"] = "test-terminal-evidence",                 },
            },
        });
    }

    private static string ReadSessionIdFromOutput(string stdout)
    {
        using var document = ReadAoEnvelope(stdout);
        return document.RootElement.GetProperty("payload").GetProperty("session_id").GetString()
            ?? throw new InvalidOperationException("AO payload did not contain session_id.");
    }

    private static JsonDocument ReadAoEnvelope(string stdout)
    {
        return ReadAoEnvelopeByType(stdout, type => string.Equals(type, "progress", StringComparison.Ordinal));
    }

    private static JsonDocument ReadFinalAoEnvelope(string stdout)
    {
        return ReadAoEnvelopeByType(stdout, type => !string.Equals(type, "progress", StringComparison.Ordinal));
    }

    private static JsonDocument ReadAoEnvelopeByType(string stdout, Func<string, bool> predicate)
    {
        const string startTag = "<ao_property>";
        const string endTag = "</ao_property>";
        var index = 0;

        while (true)
        {
            var startIndex = stdout.IndexOf(startTag, index, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                break;
            }

            var endIndex = stdout.IndexOf(endTag, startIndex, StringComparison.Ordinal);
            if (endIndex <= startIndex)
            {
                break;
            }

            var json = stdout.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length).Trim();
            var document = JsonDocument.Parse(json);
            var type = document.RootElement.GetProperty("type").GetString() ?? string.Empty;
            if (predicate(type))
            {
                return document;
            }

            document.Dispose();
            index = endIndex + endTag.Length;
        }

        throw new InvalidOperationException("AO CLI output did not contain a matching ao_property block.");
    }

    private static async Task AssertPromptPayloadMatchesSnapshotAsync(JsonElement payload, string snapshotFileName)
    {
        var snapshotPath = GetPromptSnapshotPath(snapshotFileName);
        var expectedNode = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath))?.AsObject()
            ?? throw new InvalidOperationException("Prompt snapshot could not be normalized.");
        NormalizeJsonStringLineEndings(expectedNode);
        NormalizeExpressionRootFields(expectedNode);
        var expected = expectedNode.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }).ReplaceLineEndings("\n");
        var actual = NormalizePromptPayloadSnapshot(payload).ReplaceLineEndings("\n");
        Assert.Equal(expected, actual);
    }

    private static string NormalizePromptPayloadSnapshot(JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText())?.AsObject()
            ?? throw new InvalidOperationException("Prompt payload could not be normalized.");

        ReplaceSnapshotPlaceholder(node, "objective_file", "<OBJECTIVE_FILE>");
        ReplaceSnapshotPlaceholder(node, "context_file", "<CONTEXT_FILE>");
        ReplaceSnapshotPlaceholder(node, "session_id", "<SESSION_ID>");
        ReplaceSnapshotPlaceholder(node, "workflow_file", "<WORKFLOW_FILE>");
        ReplaceSnapshotPlaceholder(node, "workflow_instance_file", "<WORKFLOW_INSTANCE_FILE>");
        NormalizeJsonStringLineEndings(node);
        NormalizeExpressionDefinitionObjects(node);

        return node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static void NormalizeJsonStringLineEndings(JsonNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(static pair => pair.Key).ToArray())
            {
                if (obj[key] is JsonValue objectValue && objectValue.TryGetValue<string>(out var objectText))
                {
                    var normalizedObjectText = objectText.ReplaceLineEndings("\n");
                    if (!string.Equals(objectText, normalizedObjectText, StringComparison.Ordinal))
                    {
                        obj[key] = normalizedObjectText;
                        continue;
                    }
                }

                NormalizeJsonStringLineEndings(obj[key]);
            }

            return;
        }

        if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is JsonValue arrayValue && arrayValue.TryGetValue<string>(out var arrayText))
                {
                    var normalizedArrayText = arrayText.ReplaceLineEndings("\n");
                    if (!string.Equals(arrayText, normalizedArrayText, StringComparison.Ordinal))
                    {
                        array[index] = normalizedArrayText;
                        continue;
                    }
                }

                NormalizeJsonStringLineEndings(array[index]);
            }

            return;
        }
    }

    private static void NormalizeExpressionRootFields(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(static pair => pair.Key).ToArray())
            {
                if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    obj[key] = text.Replace(
                        "    \"nodes\",\n    \"startNodeId\"",
                        "    \"nodes\",\n    \"runtimeBinding\",\n    \"runtimeVersion\",\n    \"expressionBinding\",\n    \"startNodeId\"",
                        StringComparison.Ordinal);
                }
                else
                {
                    NormalizeExpressionRootFields(obj[key]);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                NormalizeExpressionRootFields(item);
            }
        }
    }

    private static void NormalizeExpressionDefinitionObjects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(static pair => pair.Key).ToArray())
            {
                if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    var normalized = Regex.Replace(
                        text,
                        "\\\"(?<field>guardExpression|succeedExpression|passExpression)\\\"\\s*:\\s*\\{\\s*\\\"kind\\\"\\s*:\\s*\\\"[^\\\"]+\\\"\\s*,\\s*\\\"source\\\"\\s*:\\s*\\\"(?<source>(?:\\\\.|[^\\\"\\\\])*)\\\"(?:\\s*,\\s*\\\"entryPoint\\\"\\s*:\\s*\\\"[^\\\"]*\\\")?\\s*,\\s*\\\"resultType\\\"\\s*:\\s*\\\"[^\\\"]+\\\"\\s*\\}",
                        static match => $"\"{match.Groups["field"].Value}\": \"{match.Groups["source"].Value}\"",
                        RegexOptions.CultureInvariant);
                    normalized = Regex.Replace(
                        normalized,
                        "\\\\?\"runtimeBinding\\\\?\"\\s*:\\s*\\\\?\"[^\"]+\\\\?\"\\s*,\\s*\\\\?\"expressionBinding\\\\?\"\\s*:\\s*\\{.*?\\}\\s*,\\s*",
                        string.Empty,
                        RegexOptions.Singleline | RegexOptions.CultureInvariant);
                    normalized = Regex.Replace(
                        normalized,
                        "\\\\?\"expressionBinding\\\\?\"\\s*:\\s*\\{.*?\\}\\s*,\\s*",
                        string.Empty,
                        RegexOptions.Singleline | RegexOptions.CultureInvariant);
                    normalized = Regex.Replace(
                        normalized,
                        "\\\\?\"runtimeBinding\\\\?\"\\s*:\\s*\\\\?\"[^\"]+\\\\?\"\\s*,\\s*",
                        string.Empty,
                        RegexOptions.Singleline | RegexOptions.CultureInvariant);
                    normalized = Regex.Replace(
                        normalized,
                        "\\\\?\"runtimeVersion\\\\?\"\\s*:\\s*\\\\?\"[^\"]*\\\\?\"\\s*,\\s*\\\\?\"expressionBinding\\\\?\"\\s*:\\s*\\{.*?\\}\\s*,\\s*",
                        string.Empty,
                        RegexOptions.Singleline | RegexOptions.CultureInvariant);

                    if (!string.Equals(text, normalized, StringComparison.Ordinal))
                    {
                        obj[key] = normalized;
                        continue;
                    }
                }

                NormalizeExpressionDefinitionObjects(obj[key]);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                NormalizeExpressionDefinitionObjects(item);
            }
        }
    }

    private static void ReplaceSnapshotPlaceholder(JsonObject node, string propertyName, string placeholder)
    {
        if (node[propertyName] is not null)
        {
            node[propertyName] = placeholder;
        }
    }

    private static string GetPromptSnapshotPath(string snapshotFileName)
        => Path.Combine(FindRepositoryRoot(), "tests", "dotnet", "Techne.Loom.AgentOrchestrator.Tests", "snapshots", snapshotFileName);

    private static string CreateSessionDirectory()
        => Path.Combine(Path.GetTempPath(), $"techne-loom-ao-session-{Guid.NewGuid():N}");

    private static string CreateTempDirectoryWithName(string directoryName)
    {
        var directory = Path.Combine(Path.GetTempPath(), directoryName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static WorkflowInstance CreatePromptReplanWorkflowInstance(string? selectedMainTbrTargetNodeId = "state.end")
    {
        var startState = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Description = "Start state.",
            WorkflowPhase = "Planning",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.start",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.route_to_review"],
                },
            ],
        };

        var reviewState = new StateNode
        {
            Id = "state.review",
            Name = "Review",
            Description = "Blocked review state.",
            WorkflowPhase = "Review",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.review",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = ["transition.main_tbr", "transition.remaining_tbr"],
                },
            ],
        };

        var endState = new StateNode
        {
            Id = "state.end",
            Name = "End",
            Description = "End state.",
            WorkflowPhase = "Done",
            Groups = [],
        };

        var routeToReview = new ExpressionTransition
        {
            Id = "transition.route_to_review",
            Name = "RouteToReview",
            TargetNodeId = "state.review",
            GuardExpression = "True",
            SucceedExpression = "True",
            StepKind = WorkflowStepKind.ConditionBranch,
            Priority = 0,
        };

        var mainTbr = new ToBeRefinedTransition
        {
            Id = "transition.main_tbr",
            Name = "MainTbr",
            TargetNodeId = selectedMainTbrTargetNodeId,
            StepKind = WorkflowStepKind.ModelThink,
            DesignNotes = "Current blocked seam that now needs expansion into a viable replacement path.",
        };

        var remainingTbr = new ToBeRefinedTransition
        {
            Id = "transition.remaining_tbr",
            Name = "RemainingTbr",
            TargetNodeId = "state.end",
            StepKind = WorkflowStepKind.ModelThink,
            DesignNotes = "A separate future refinement seam that should remain in the graph.",
        };

        return new WorkflowInstance
        {
            InstanceId = "prompt-replan-instance",
            StartNodeId = "state.start",
            CurrentNodeId = "state.review",
            EndNodeId = "state.end",
            Status = WorkflowStatus.Running,
            Version = 3,
            Context = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["topic"] = "ao prompt replanning",
            },
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [startState.Id] = startState,
                [reviewState.Id] = reviewState,
                [endState.Id] = endState,
                [routeToReview.Id] = routeToReview,
                [mainTbr.Id] = mainTbr,
                [remainingTbr.Id] = remainingTbr,
            },
        };
    }

    private static string CreateSkillRoot()
    {
        var skillRoot = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-skill-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "# Temp skill\n");
        return skillRoot;
    }

    private static string GetWorkflowFile(string sessionDirectory, string sessionId)
        => Path.Combine(Path.GetFullPath(sessionDirectory), $"session_{sessionId}_workflow.json");

    private static string GetEventLogFile(string sessionDirectory, string sessionId)
        => Path.Combine(Path.GetFullPath(sessionDirectory), $"session_{sessionId}_events.jsonl");

    private static string GetRuntimeWorkflowFile(string sessionDirectory, string sessionId)
        => Path.Combine(Path.GetFullPath(sessionDirectory), $"session_{sessionId}_runtime.workflow.json");

    private static string GetRuntimeWorkflowPointerFile(string sessionDirectory, string sessionId)
        => Path.Combine(Path.GetFullPath(sessionDirectory), $"session_{sessionId}_runtime.workflow.pointer.json");

    private static void AssertFileStartsWithMermaidFence(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        Assert.True(bytes.Length >= 3, $"Expected {filePath} to contain at least three bytes.");
        Assert.Equal((byte)'`', bytes[0]);
        Assert.Equal((byte)'`', bytes[1]);
        Assert.Equal((byte)'`', bytes[2]);
    }

    private static JsonElement FindPromptBlock(JsonElement payload, string blockId)
    {
        foreach (var block in payload.GetProperty("blocks").EnumerateArray())
        {
            if (string.Equals(block.GetProperty("block_id").GetString(), blockId, StringComparison.Ordinal))
            {
                return block;
            }
        }

        throw new Xunit.Sdk.XunitException($"Prompt block '{blockId}' was not found.");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md")) && Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static string GetCliAssemblyPath()
        => typeof(Techne.Loom.AgentOrchestrator.Runtime.AoRuntimeService).Assembly.Location;
}
