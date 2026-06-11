using System.Diagnostics;
using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AgentOrchestratorBehaviorTests
{
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
    public async Task CliRun_WeaveOutBoundary_EmitsStructuredWeaveOutRequest()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-weave-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-weave-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();

        await File.WriteAllTextAsync(objectiveFile, "Compare two frontier options.");
        await File.WriteAllTextAsync(contextFile, "{\"force_boundary_reason\":\"weave_out_required\",\"confirmed_scope\":true}");

        var run = await RunCliAsync(repoRoot, $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\"");
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("\"session_id\":\"", run.StdOut);
        Assert.Contains("\"boundary_reason\":\"weave_out_required\"", run.StdOut);
        Assert.Contains("\"weave_out_request\":{", run.StdOut);
        Assert.Contains("\"objective\":\"compare candidate execution frontiers\"", run.StdOut);
        Assert.Contains("\"artifacts\":[\"frontier-a.json\",\"frontier-b.json\"]", run.StdOut);
    }

    [Fact]
<<<<<<< HEAD
    public async Task CliPlanner_WritesDraftWorkflowJsonAndValidationArtifacts()
=======
    public async Task CliPlanner_WritesDraftWorkflowJson()
>>>>>>> origin/main
    {
        var repoRoot = FindRepositoryRoot();
        var planFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-plan-{Guid.NewGuid():N}.md");
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-plan-{Guid.NewGuid():N}.json");
<<<<<<< HEAD
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-plan-audit-{Guid.NewGuid():N}");
=======
>>>>>>> origin/main
        await File.WriteAllTextAsync(planFile, """
            Goal
            1. Inspect current code.
            2. Compare candidate paths.
            3. Return a structured frontier.
            """);

<<<<<<< HEAD
        var plan = await RunCliAsync(repoRoot, $"planner --plan-file \"{planFile}\" --workflow-file \"{workflowFile}\" --audit-output \"{auditDirectory}\"");
        Assert.Equal(0, plan.ExitCode);
        Assert.True(File.Exists(workflowFile));
        Assert.Contains("\"status\": \"drafting\"", await File.ReadAllTextAsync(workflowFile));
        Assert.Contains("Validation artifacts:", plan.StdErr);
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
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
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.mermaid.md", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.html", SearchOption.AllDirectories).Single()));
        Assert.True(File.Exists(Directory.GetFiles(auditDirectory, "workflow.json", SearchOption.AllDirectories).Single()));
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
=======
        var plan = await RunCliAsync(repoRoot, $"planner --plan-file \"{planFile}\" --workflow-file \"{workflowFile}\"");
        Assert.Equal(0, plan.ExitCode);
        Assert.True(File.Exists(workflowFile));
        Assert.Contains("\"status\": \"drafting\"", await File.ReadAllTextAsync(workflowFile));
>>>>>>> origin/main
    }

    [Fact]
    public async Task CliRun_WithAuditOutput_EmitsAuditArtifactLinks()
    {
        var repoRoot = FindRepositoryRoot();
        var objectiveFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-objective-{Guid.NewGuid():N}.md");
        var contextFile = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-context-{Guid.NewGuid():N}.json");
        var sessionDirectory = CreateSessionDirectory();
        var auditDirectory = Path.Combine(Path.GetTempPath(), $"techne-loom-ao-audit-{Guid.NewGuid():N}");

        await File.WriteAllTextAsync(objectiveFile, "Generate audit artifacts.");
        await File.WriteAllTextAsync(contextFile, "{}");

        var run = await RunCliAsync(
            repoRoot,
            $"run --objective-file \"{objectiveFile}\" --context-file \"{contextFile}\" --session-dir \"{sessionDirectory}\" --audit-output \"{auditDirectory}\"");

        Assert.Equal(3, run.ExitCode);
        using var envelope = ReadAoEnvelope(run.StdOut);
        var payload = envelope.RootElement.GetProperty("payload");
        var audit = payload.GetProperty("audit_artifacts");
        Assert.Equal(Path.GetFullPath(auditDirectory), audit.GetProperty("output_root").GetString());
        Assert.True(File.Exists(audit.GetProperty("mermaid_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("html_file").GetString()));
        Assert.True(File.Exists(audit.GetProperty("workflow_backup_file").GetString()));
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
<<<<<<< HEAD
        var repoRoot = FindRepositoryRoot();
        var run = await RunCliAsync(repoRoot, "host");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("\"type\":\"error\"", run.StdOut);
        Assert.Contains("Unknown command", run.StdOut);
        Assert.Contains("host", run.StdOut);
=======
        var aoRun = typeof(AoMcpTools).GetMethod(nameof(AoMcpTools.AoRun))
            ?? throw new InvalidOperationException("AoRun method was not found.");
        var aoResume = typeof(AoMcpTools).GetMethod(nameof(AoMcpTools.AoResume))
            ?? throw new InvalidOperationException("AoResume method was not found.");

        var runInvocationContext = aoRun.GetParameters().Single(parameter => parameter.Name == "invocation_context");
        Assert.Equal(typeof(AoInvocationContext), runInvocationContext.ParameterType);
        Assert.True(runInvocationContext.HasDefaultValue);
        Assert.Null(runInvocationContext.DefaultValue);

        var resumeInvocationContext = aoResume.GetParameters().Single(parameter => parameter.Name == "invocation_context");
        Assert.Equal(typeof(AoInvocationContext), resumeInvocationContext.ParameterType);
        Assert.True(resumeInvocationContext.HasDefaultValue);
        Assert.Null(resumeInvocationContext.DefaultValue);

        var runAuditOutput = aoRun.GetParameters().Single(parameter => parameter.Name == "audit_output");
        Assert.Equal(typeof(string), runAuditOutput.ParameterType);
        Assert.True(runAuditOutput.HasDefaultValue);
        Assert.Null(runAuditOutput.DefaultValue);
    }

    [Fact]
    public async Task AoRuntimeService_InvalidWeaveOutInvocationContext_IsRejected()
    {
        var runtime = new Runtime.AoRuntimeService();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.RunAsync(
                "Validate weave-out metadata.",
                new Dictionary<string, object?>(),
                CreateSessionDirectory(),
                new AoInvocationContext(new AoWeaveOutContext("   "))));

        Assert.Contains("weave_out.route", error.Message);
>>>>>>> origin/main
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
        const string startTag = "<ao_property>";
        const string endTag = "</ao_property>";
        var startIndex = stdout.IndexOf(startTag, StringComparison.Ordinal);
        var endIndex = stdout.IndexOf(endTag, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
        {
            throw new InvalidOperationException("AO CLI output did not contain an ao_property block.");
        }

        var json = stdout.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length).Trim();
        return JsonDocument.Parse(json);
    }

    private static string CreateSessionDirectory()
        => Path.Combine(Path.GetTempPath(), $"techne-loom-ao-session-{Guid.NewGuid():N}");

    private static string GetWorkflowFile(string sessionDirectory, string sessionId)
        => Path.Combine(Path.GetFullPath(sessionDirectory), $"session_{sessionId}_workflow.json");

    private static string GetEventLogFile(string sessionDirectory, string sessionId)
        => Path.Combine(Path.GetFullPath(sessionDirectory), $"session_{sessionId}_events.jsonl");

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
