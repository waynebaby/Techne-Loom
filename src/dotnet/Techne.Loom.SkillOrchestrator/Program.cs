using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.Documentation;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Analysis;
using Techne.Loom.SkillOrchestrator.Runtime;
using Techne.Loom.SkillOrchestrator.TaskTracking;
using Techne.Loom.SkillOrchestrator.Validation;

return await SkillCli.RunAsync(args).ConfigureAwait(false);

internal static class SkillCli
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private const int MaxCliTicksPerInvocation = 64;
    private const string UsageText = "Usage: dotnet so.dll --guide | dotnet so.dll --help | dotnet so.dll --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n> | dotnet so.dll compile --workflow-file <path> [--audit-output <path>] | dotnet so.dll copy-audit-step --source-step <path> --workflow-id <id> --sequence <n> --action <action> --audit-output <path> --reason <text> --verified-by <id> | dotnet so.dll run --workflow-file <path> [--context-file <path>] [--audit-output <path>] [--reuse-audit-step <path> --reuse-audit-reason <text> --reuse-audit-verified-by <id>] | dotnet so.dll resume --workflow-file <path> --result-file <path> [--audit-output <path>] [--reuse-audit-step <path> --reuse-audit-reason <text> --reuse-audit-verified-by <id>] | dotnet so.dll status --workflow-file <path> | dotnet so.dll inspect-workflow --workflow-file <path> | dotnet so.dll inspect-events --workflow-file <path> | dotnet so.dll ls <path>\n--guide installs the version-matched English docs bundle and emits JSON with version, docs_root, and guide_path. It accepts no additional arguments. Compile validates an existing workflow-file and writes Mermaid Markdown, HTML, workflow JSON backup, and workflow analysis validation artifacts under the selected audit output root or the default temporary audit root. For Loom-governanced target-skill templates, compile and workflow load also enforce the governed-template validation contract, route-aware business-output gates, seam ownership, blocked strongest-earned outputs, and done reachability. Copy checked-in templates to a runtime temp or execution-output folder before run/resume, and do not place runtime workflow files, event sidecars, or audit outputs inside a skill folder. `copy-audit-step` and the optional run/resume reuse parameters copy only verified audit artifacts; they never advance workflow state or replace official runtime evidence.";

    public static async Task<int> RunAsync(string[] args)
    {
        var tokens = args.ToList();

        try
        {
            if (tokens.Count == 0)
            {
                Console.Error.WriteLine(UsageText);
                return 1;
            }

            if (tokens[0] == "--guide")
            {
                return await HandleGuideAsync(tokens.Skip(1).ToList()).ConfigureAwait(false);
            }

            if (tokens.Contains("--help", StringComparer.Ordinal) || tokens.Contains("-h", StringComparer.Ordinal))
            {
                Console.WriteLine(UsageText);
                return 0;
            }

            if (tokens[0] == "--patch")
            {
                return await HandlePatchAsync(tokens.Skip(1).ToList()).ConfigureAwait(false);
            }

            return tokens[0] switch
            {
                "compile" => await HandleCompileAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "copy-audit-step" => await HandleCopyAuditStepAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "run" => await HandleRunAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "resume" => await HandleResumeAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "status" => await HandleStatusAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "inspect-workflow" => await HandleInspectWorkflowAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "inspect-events" => await HandleInspectEventsAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "ls" => await HandleLsAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown command '{tokens[0]}'."),
            };
        }
        catch (Exception ex)
        {
            var writer = new XmlFragmentWriter(Console.Out);
            writer.WriteSoProperty(new SoPropertyEnvelope(
                "error",
                DateTimeOffset.UtcNow,
                await BuildTopLevelErrorPayloadAsync(ex, tokens).ConfigureAwait(false)));
            return 2;
        }
    }

    private static async Task<SkillErrorPayload> BuildTopLevelErrorPayloadAsync(Exception ex, IReadOnlyList<string> tokens)
    {
        var command = tokens.FirstOrDefault() ?? "unknown";
        var commandArgs = tokens.Count > 1 ? tokens.Skip(1).ToList() : [];
        var workflowFile = NormalizePathOrEmpty(GetOption(commandArgs, "--workflow-file"));
        var resultFile = NormalizePathOrEmpty(GetOption(commandArgs, "--result-file"));
        var eventLogFile = string.IsNullOrWhiteSpace(workflowFile) ? string.Empty : NormalizePathOrEmpty(GetEventsFile(workflowFile));
        var instanceId = string.Empty;
        string? currentNodeId = null;
        var canResume = false;
        var freshInstanceRequired = false;
        if (File.Exists(workflowFile))
        {
            try
            {
                await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
                var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false));
                instanceId = instance.InstanceId;
                currentNodeId = instance.CurrentNodeId;
                canResume = WorkflowResumePolicy.CanResume(instance);
                freshInstanceRequired = WorkflowResumePolicy.RequiresFreshInstance(instance);
            }
            catch (Exception)
            {
            }
        }

        return new SkillErrorPayload(
            workflowFile,
            instanceId,
            "failed",
            ex.Message,
            eventLogFile,
            BuildTopLevelMustShowToUserFiles(workflowFile, eventLogFile, resultFile),
            BuildTopLevelWorkflowLocationSummary(command, workflowFile),
            null,
            canResume,
            freshInstanceRequired,
            null,
            null,
            currentNodeId);
    }
    private static IReadOnlyList<string> BuildTopLevelMustShowToUserFiles(params string?[] candidates)
    {
        return candidates
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(static candidate => candidate!)
            .Where(File.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildTopLevelWorkflowLocationSummary(string command, string workflowFile)
    {
        if (!string.IsNullOrWhiteSpace(workflowFile))
        {
            return $"SO CLI failed during '{command}' while working from workflow '{workflowFile}'.";
        }

        return "SO CLI failed before a workflow render context was available.";
    }

    private static string NormalizePathOrEmpty(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);

    private static async Task<int> HandleGuideAsync(IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            throw new ArgumentException("The --guide command accepts no additional arguments.");
        }

        var result = await DocumentationBundleInstaller.InstallAsync(
            typeof(SkillCli).Assembly,
            "reference/products/so-guide.md").ConfigureAwait(false);
        foreach (var warning in result.Warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }

        Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = result.Version,
            ["docs_root"] = result.DocsRoot,
            ["guide_path"] = result.GuidePath,
        }));
        return 0;
    }

    private static async Task<int> HandlePatchAsync(IReadOnlyList<string> args)
    {
        var request = new TextFilePatchRequest(
            GetRequiredOption(args, "--patch-content-file"),
            GetRequiredOption(args, "--patch-target"),
            GetRequiredInt32Option(args, "--from-line"),
            GetRequiredInt32Option(args, "--to-line"));

        var result = await TextFilePatchService.ApplyAsync(request).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["patch_target"] = result.PatchTarget,
            ["applied_from_line"] = result.AppliedFromLine,
            ["applied_to_line"] = result.AppliedToLine,
            ["patch_line_count"] = result.PatchLineCount,
            ["original_line_count"] = result.OriginalLineCount,
            ["updated_line_count"] = result.UpdatedLineCount,
        }));
        return 0;
    }

    private static async Task<int> HandleRunAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        var contextFile = GetOption(args, "--context-file");
        var auditOutput = GetOption(args, "--audit-output");
        RuntimeArtifactPathGuard.EnsureRuntimeWorkflowFileOutsideSkillDirectory(workflowFile);
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
        var writer = new XmlFragmentWriter(Console.Out);
        var session = await LoadSessionAsync(workflowFile, writer).ConfigureAwait(false);
        var contextDelta = await LoadContextDeltaAsync(contextFile).ConfigureAwait(false);
        var auditReuseRequest = CreateAuditReuseRequest(args);
        var lastTick = await RunUntilBoundaryAsync(session.Service, session.InstanceId, workflowFile, writer, auditOutput, contextDelta, auditReuseRequest).ConfigureAwait(false);
        await PersistSessionAsync(workflowFile, session.Service, session.InstanceId).ConfigureAwait(false);
        return MapExitCode(lastTick.StatusProjection.Status, lastTick.Suspended, lastTick.Failed);
    }

    private static async Task<int> HandleCompileAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        var auditOutput = GetOption(args, "--audit-output");
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
        EnsureOptionAbsent(args, "--description-file", "compile");
        EnsureOptionAbsent(args, "--context-file", "compile");

        var workflowJson = await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false);
        var instance = WorkflowJsonSerializer.Deserialize(workflowJson);
        WorkflowValidator.Validate(instance).ThrowIfInvalid();
        var service = await CreateServiceForVisualizationAsync(instance).ConfigureAwait(false);
        var auditArtifacts = await WriteAuditArtifactsAsync(service, instance, workflowFile, auditOutput, "compiled", workflowJson).ConfigureAwait(false);
        Console.Error.WriteLine($"Validation artifacts: {auditArtifacts.StepDirectory}");
        Console.Write(workflowJson);
        return 0;
    }

    private static async Task<int> HandleCopyAuditStepAsync(IReadOnlyList<string> args)
    {
        var sourceStep = GetRequiredOption(args, "--source-step");
        var workflowId = GetRequiredOption(args, "--workflow-id");
        var sequence = GetRequiredInt32Option(args, "--sequence");
        var action = GetRequiredOption(args, "--action");
        var auditOutput = GetRequiredOption(args, "--audit-output");
        var reason = GetRequiredOption(args, "--reason");
        var verifiedBy = GetRequiredOption(args, "--verified-by");
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(sourceStep, "--source-step");
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);

        var artifacts = await WorkflowAuditArtifactWriter.CopyStepAsync(
            sourceStep,
            workflowId,
            sequence,
            action,
            auditOutput,
            reason,
            verifiedBy).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(artifacts, JsonOptions));
        return 0;
    }
    private static async Task<int> HandleResumeAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        var resultFile = GetRequiredOption(args, "--result-file");
        var auditOutput = GetOption(args, "--audit-output");
        RuntimeArtifactPathGuard.EnsureRuntimeWorkflowFileOutsideSkillDirectory(workflowFile);
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
        var writer = new XmlFragmentWriter(Console.Out);
        var session = await LoadSessionAsync(workflowFile, writer).ConfigureAwait(false);
        var envelope = await LoadResumeEnvelopeAsync(resultFile).ConfigureAwait(false);
        await session.Service.ResumeAsync(session.InstanceId, envelope.TransitionId, envelope.CorrelationKey, envelope.Payload).ConfigureAwait(false);
        var auditReuseRequest = CreateAuditReuseRequest(args);
        var lastTick = await RunUntilBoundaryAsync(session.Service, session.InstanceId, workflowFile, writer, auditOutput, auditReuseRequest: auditReuseRequest).ConfigureAwait(false);
        await PersistSessionAsync(workflowFile, session.Service, session.InstanceId).ConfigureAwait(false);
        return MapExitCode(lastTick.StatusProjection.Status, lastTick.Suspended, lastTick.Failed);
    }

    private static async Task<int> HandleStatusAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
        var writer = new XmlFragmentWriter(Console.Out);
        var session = await LoadSessionAsync(workflowFile, writer).ConfigureAwait(false);
        var status = await session.Service.GetStatusAsync(session.InstanceId).ConfigureAwait(false);
        var statusInstance = await session.Service.GetInstanceAsync(session.InstanceId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow instance '{session.InstanceId}' was not found.");
        writer.WriteSoProperty(new SoPropertyEnvelope(
            "status",
            DateTimeOffset.UtcNow,
            new SkillStatusPayload(workflowFile, status.InstanceId, MapPublicStatus(status.Status), status.CurrentNodeId, null, GetEventsFile(workflowFile), Array.Empty<string>(), BuildWorkflowLocationSummary(MapPublicStatus(status.Status), status.CurrentNodeId, null, null, renderChanged: false), CanResume: WorkflowResumePolicy.CanResume(statusInstance), FreshInstanceRequired: WorkflowResumePolicy.RequiresFreshInstance(statusInstance))));
        return 0;
    }

    private static async Task<int> HandleInspectWorkflowAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
        Console.Write(await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false));
        return 0;
    }

    private static async Task<int> HandleInspectEventsAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        await using var workflowLock = await WorkflowFileLock.AcquireAsync(workflowFile).ConfigureAwait(false);
        var eventsFile = GetEventsFile(workflowFile);
        if (File.Exists(eventsFile))
        {
            Console.Write(await File.ReadAllTextAsync(eventsFile).ConfigureAwait(false));
        }

        return 0;
    }

    private static async Task<int> HandleLsAsync(IReadOnlyList<string> args)
    {
        var path = args.Count > 0 ? args[0] : Directory.GetCurrentDirectory();
        var workflowFile = Path.Combine(Path.GetTempPath(), $"techne-loom-so-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(workflowFile, WorkflowJsonSerializer.Serialize(CreateLsWorkflow(path))).ConfigureAwait(false);
        var writer = new XmlFragmentWriter(Console.Out);
        var session = await LoadSessionAsync(workflowFile, writer).ConfigureAwait(false);
        var lastTick = await RunUntilBoundaryAsync(session.Service, session.InstanceId, workflowFile, writer, auditOutputRoot: null).ConfigureAwait(false);
        await PersistSessionAsync(workflowFile, session.Service, session.InstanceId).ConfigureAwait(false);
        return MapExitCode(lastTick.StatusProjection.Status, lastTick.Suspended, lastTick.Failed);
    }

    private static async Task<(DefaultWorkflowTaskTrackingService Service, string InstanceId)> LoadSessionAsync(string workflowFile, XmlFragmentWriter writer)
    {
        var instance = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false));
        WorkflowValidator.Validate(instance).ThrowIfInvalid();
        var service = await CreateServiceForVisualizationAsync(instance, writer).ConfigureAwait(false);
        return (service, instance.InstanceId);
    }

    private static async Task<DefaultWorkflowTaskTrackingService> CreateServiceForVisualizationAsync(WorkflowInstance instance, XmlFragmentWriter? writer = null)
    {
        var store = new InMemoryInstanceStore();
        await store.SaveNewAsync(instance).ConfigureAwait(false);

        var progress = new Progress<object>(payload =>
        {
            if (writer is null)
            {
                return;
            }

            switch (payload)
            {
                case CommandStreamStart streamStart:
                    writer.BeginWrappedExec(streamStart.CommandLine);
                    break;
                case CommandStreamChunk streamChunk:
                    writer.WriteExecutionStreamLine(streamChunk.Stream, streamChunk.Chunk);
                    break;
                case CommandStreamEnd:
                    writer.EndWrappedExec();
                    break;
            }
        });

        var engine = new DefaultTaskTrackingEngine(store, commandProgress: progress);
        return new DefaultWorkflowTaskTrackingService(engine);
    }

    private static async Task<WorkflowTickResult> RunUntilBoundaryAsync(DefaultWorkflowTaskTrackingService service, string instanceId, string workflowFile, XmlFragmentWriter writer, string? auditOutputRoot, Dictionary<string, object?>? initialContextDelta = null, AuditReuseRequest? auditReuseRequest = null)
    {
        WorkflowTickResult tick;
        var contextDelta = initialContextDelta;
        var ticks = 0;
        do
        {
            tick = await service.StartOrAdvanceAsync(instanceId, contextDelta).ConfigureAwait(false);
            contextDelta = null;
            ticks++;

            if (ticks > MaxCliTicksPerInvocation)
            {
                var status = await service.CancelAsync(instanceId, "Execution step budget exceeded.").ConfigureAwait(false);
                var failedTick = new WorkflowTickResult(
                    instanceId,
                    Progressed: false,
                    Moved: false,
                    Suspended: false,
                    Failed: true,
                    NextNodeId: status.CurrentNodeId,
                    Version: status.Version,
                    Backoff: null,
                    ErrorMessage: "Execution step budget exceeded.",
                    StatusProjection: status);

                tick = failedTick;
                break;
            }

            if (tick.Progressed || tick.Moved)
            {
                var currentInstance = await service.GetInstanceAsync(instanceId).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Workflow instance '{instanceId}' was not found during progress rendering.");
                var progressAuditArtifacts = await WriteAuditArtifactsAsync(service, currentInstance, workflowFile, auditOutputRoot, "progress", auditReuseRequest: auditReuseRequest).ConfigureAwait(false);
                writer.WriteSoProperty(new SoPropertyEnvelope(
                    "progress",
                    DateTimeOffset.UtcNow,
                    new SkillProgressPayload(
                        workflowFile,
                        currentInstance.InstanceId,
                        MapPublicStatus(tick.StatusProjection.Status),
                        currentInstance.CurrentNodeId,
                        tick.NextNodeId,
                        GetEventsFile(workflowFile),
                        BuildMustShowToUserFiles(progressAuditArtifacts),
                        BuildWorkflowLocationSummary(MapPublicStatus(tick.StatusProjection.Status), currentInstance.CurrentNodeId, tick.NextNodeId, null, renderChanged: !string.Equals(progressAuditArtifacts.ArtifactOrigin, "verified-copy", StringComparison.Ordinal)),
                        progressAuditArtifacts)));
            }
        }
        while (tick.Progressed && !tick.Suspended && !tick.Failed && tick.StatusProjection.Status != WorkflowStatus.Succeeded);

        var instance = await service.GetInstanceAsync(instanceId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow instance '{instanceId}' was not found after execution.");

        if (tick.Failed || tick.StatusProjection.Status == WorkflowStatus.Failed)
        {
            var errorAuditArtifacts = await WriteAuditArtifactsAsync(service, instance, workflowFile, auditOutputRoot, "failed", auditReuseRequest: auditReuseRequest).ConfigureAwait(false);
            writer.WriteSoProperty(new SoPropertyEnvelope(
                "error",
                DateTimeOffset.UtcNow,
                new SkillErrorPayload(
                    workflowFile,
                    instance.InstanceId,
                    "failed",
                    tick.ErrorMessage ?? "Workflow execution failed.",
                    GetEventsFile(workflowFile),
                    BuildMustShowToUserFiles(errorAuditArtifacts),
                    BuildWorkflowLocationSummary("failed", instance.CurrentNodeId, null, null, renderChanged: !string.Equals(errorAuditArtifacts.ArtifactOrigin, "verified-copy", StringComparison.Ordinal)),
                    errorAuditArtifacts,
                    CanResume: WorkflowResumePolicy.CanResume(instance),
                    FreshInstanceRequired: WorkflowResumePolicy.RequiresFreshInstance(instance),
                    GateEvaluation: instance.LastGateEvaluation,
                    ProjectionContract: BuildProjectionContract(GetActiveTransition(instance)),
                    CurrentNodeId: instance.CurrentNodeId)));
        }
        else if (tick.Suspended || tick.StatusProjection.Status != WorkflowStatus.Succeeded)
        {
            var boundaryPayload = CreateBoundaryPayload(workflowFile, instance, GetEventsFile(workflowFile), tick.Suspended ? null : "No progress was possible for the current workflow state.");
            var boundaryAuditArtifacts = await WriteAuditArtifactsAsync(service, instance, workflowFile, auditOutputRoot, $"blocked-{boundaryPayload.CurrentStepKind ?? "boundary"}", auditReuseRequest: auditReuseRequest).ConfigureAwait(false);
            writer.WriteSoProperty(new SoPropertyEnvelope(
                "boundary",
                DateTimeOffset.UtcNow,
                boundaryPayload with
                {
                    MustShowToUserFiles = BuildMustShowToUserFiles(boundaryAuditArtifacts),
                    WorkflowLocationSummary = BuildWorkflowLocationSummary("blocked", boundaryPayload.CurrentNodeId, null, boundaryPayload.CurrentStepKind, renderChanged: !string.Equals(boundaryAuditArtifacts.ArtifactOrigin, "verified-copy", StringComparison.Ordinal)),
                    AuditArtifacts = boundaryAuditArtifacts,
                }));
        }
        else
        {
            var resultAuditArtifacts = await WriteAuditArtifactsAsync(service, instance, workflowFile, auditOutputRoot, "completed", auditReuseRequest: auditReuseRequest).ConfigureAwait(false);
            writer.WriteSoProperty(new SoPropertyEnvelope(
                "result",
                DateTimeOffset.UtcNow,
                new SkillResultPayload(
                    workflowFile,
                    instance.InstanceId,
                    MapPublicStatus(tick.StatusProjection.Status),
                    instance.CurrentNodeId,
                    instance.Context,
                    GetEventsFile(workflowFile),
                    BuildMustShowToUserFiles(resultAuditArtifacts),
                    BuildWorkflowLocationSummary(MapPublicStatus(tick.StatusProjection.Status), instance.CurrentNodeId, null, null, renderChanged: !string.Equals(resultAuditArtifacts.ArtifactOrigin, "verified-copy", StringComparison.Ordinal)),
                    resultAuditArtifacts,
                    CanResume: WorkflowResumePolicy.CanResume(instance),
                    FreshInstanceRequired: WorkflowResumePolicy.RequiresFreshInstance(instance))));
        }

        return tick;
    }

    private static SkillBoundaryPayload CreateBoundaryPayload(string workflowFile, WorkflowInstance instance, string eventsFile, string? overrideSkillHint = null)
    {
        var waitGroup = instance.ActiveWaitGroups.FirstOrDefault();
        var transition = waitGroup is not null && instance.Nodes.TryGetValue(waitGroup.TransitionId, out var node)
            ? node as TransitionBase
            : null;

        return new SkillBoundaryPayload(
            workflowFile,
            instance.InstanceId,
            "blocked",
            instance.CurrentNodeId,
            transition?.StepKind.ToString(),
            overrideSkillHint ?? transition?.Description ?? transition?.Name,
            ExtractMemoryForNextStep(instance.Context),
            ExtractRequiredInputs(transition),
            eventsFile,
            Array.Empty<string>(),
            BuildWorkflowLocationSummary("blocked", instance.CurrentNodeId, null, transition?.StepKind.ToString(), renderChanged: false),
            null,
            CanResume: instance.Status == WorkflowStatus.WaitingExternal && instance.ActiveWaitGroups.Count > 0,
            FreshInstanceRequired: false,
            GateEvaluation: instance.LastGateEvaluation,
            ProjectionContract: BuildProjectionContract(transition));
    }

    private static async Task<WorkflowAuditArtifacts> WriteAuditArtifactsAsync(
        DefaultWorkflowTaskTrackingService service,
        WorkflowInstance instance,
        string workflowFile,
        string? auditOutputRoot,
        string action,
        string? workflowJsonOverride = null,
        AuditReuseRequest? auditReuseRequest = null)
    {
            var workflowJson = workflowJsonOverride ?? WorkflowJsonSerializer.Serialize(instance);
            var sequence = Math.Max(1, Math.Max(instance.Version, instance.History.Count));
            var mermaid = await service.GetVisualAsync(instance.InstanceId, WorkflowInstanceVisualizerType.Mermaid).ConfigureAwait(false);
            var html = await service.GetVisualAsync(instance.InstanceId, WorkflowInstanceVisualizerType.Html).ConfigureAwait(false);
            var analysis = new SkillWorkflowAnalyzer().Analyze(instance);
            var analysisJson = JsonSerializer.Serialize(analysis, JsonOptions);
            var dataflowJson = JsonSerializer.Serialize(analysis.Dataflow, JsonOptions);
            if (auditReuseRequest is not null && !auditReuseRequest.Consumed)
            {
                var reused = await WorkflowAuditArtifactWriter.CopyStepAsync(
                    auditReuseRequest.SourceStepDirectory,
                    instance.InstanceId,
                    sequence,
                    action,
                    auditOutputRoot,
                    auditReuseRequest.Reason,
                    auditReuseRequest.VerifiedBy,
                    expectedWorkflowJson: workflowJson,
                    analysisJsonOverride: analysisJson,
                    dataflowJsonOverride: dataflowJson,
                    mermaidMarkdownOverride: mermaid,
                    htmlOverride: html).ConfigureAwait(false);
                auditReuseRequest.Consumed = true;
                return reused;
            }

            return await WorkflowAuditArtifactWriter.WriteAsync(
                instance.InstanceId,
                sequence,
                action,
                workflowJson,
                mermaid,
                html,
                auditOutputRoot,
                analysisJson,
                dataflowJson: dataflowJson).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ExtractRequiredInputs(TransitionBase? transition)
    {
        if (transition is not CommandTransition commandTransition || commandTransition.Command.Parameters?.TryGetValue("requiredInputs", out var value) != true || value is not IEnumerable<object?> items)
        {
            return Array.Empty<string>();
        }

        return items.Select(Convert.ToString).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray();
    }

    private static TransitionBase? GetActiveTransition(WorkflowInstance instance)
    {
        var waitGroup = instance.ActiveWaitGroups.FirstOrDefault();
        return waitGroup is not null && instance.Nodes.TryGetValue(waitGroup.TransitionId, out var node)
            ? node as TransitionBase
            : null;
    }

    private static IReadOnlyDictionary<string, object?> BuildProjectionContract(TransitionBase? transition)
    {
        var parameters = transition is CommandTransition commandTransition
            ? commandTransition.Command.Parameters
            : null;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["required_inputs"] = ExtractRequiredInputs(transition),
            ["resume_output_key"] = GetParameterString(parameters, "resumeOutputKey"),
            ["output_path"] = transition?.OutputPath,
            ["projection_mode"] = GetParameterString(parameters, "projectionMode"),
        };
    }

    private static string? GetParameterString(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        return parameters?.TryGetValue(key, out var value) == true && value is not null
            ? Convert.ToString(value)
            : null;
    }
    private static object ExtractMemoryForNextStep(IReadOnlyDictionary<string, object?> context)
    {
        var selected = context
            .Where(pair => pair.Key.Contains("memory", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("summary", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("note", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return selected.Count > 0 ? selected : new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> BuildMustShowToUserFiles(WorkflowAuditArtifacts? auditArtifacts)
    {
        if (auditArtifacts is null)
        {
            return Array.Empty<string>();
        }

        var files = new List<string>
        {
            auditArtifacts.MermaidFile,
            auditArtifacts.HtmlFile,
        };

        if (!string.IsNullOrWhiteSpace(auditArtifacts.AnalysisFile))
        {
            files.Add(auditArtifacts.AnalysisFile);
        }
        if (!string.IsNullOrWhiteSpace(auditArtifacts.DataflowFile))
        {
            files.Add(auditArtifacts.DataflowFile);
        }
        if (!string.IsNullOrWhiteSpace(auditArtifacts.ReuseManifestFile))
        {
            files.Add(auditArtifacts.ReuseManifestFile);
        }

        return files;
    }

    private static string BuildWorkflowLocationSummary(string status, string? currentNodeId, string? nextNodeId, string? currentStepKind, bool renderChanged)
    {
        var current = string.IsNullOrWhiteSpace(currentNodeId) ? "unknown node" : currentNodeId;
        var next = string.IsNullOrWhiteSpace(nextNodeId) ? null : nextNodeId;
        var step = string.IsNullOrWhiteSpace(currentStepKind) ? null : currentStepKind;
        var renderSummary = renderChanged ? "Mermaid render updated in this call." : "Mermaid render unchanged in this call.";

        if (!string.IsNullOrWhiteSpace(step) && !string.IsNullOrWhiteSpace(next))
        {
            return $"SO workflow is {status} at '{current}' with step kind '{step}', next node '{next}'. {renderSummary}";
        }

        if (!string.IsNullOrWhiteSpace(step))
        {
            return $"SO workflow is {status} at '{current}' with step kind '{step}'. {renderSummary}";
        }

        if (!string.IsNullOrWhiteSpace(next))
        {
            return $"SO workflow is {status} at '{current}', next node '{next}'. {renderSummary}";
        }

        return $"SO workflow is {status} at '{current}'. {renderSummary}";
    }

    private static async Task WriteTextAtomicallyAsync(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Workflow target file '{targetPath}' must have a parent directory.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new System.Text.UTF8Encoding(false)).ConfigureAwait(false);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task PersistSessionAsync(string workflowFile, DefaultWorkflowTaskTrackingService service, string instanceId)
    {
        var instance = await service.GetInstanceAsync(instanceId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow instance '{instanceId}' was not found after execution.");

        await WriteTextAtomicallyAsync(workflowFile, WorkflowJsonSerializer.Serialize(instance)).ConfigureAwait(false);
        var eventsFile = GetEventsFile(workflowFile);
        var metadataFile = GetEventsMetadataFile(workflowFile);
        var serializedHistory = instance.History
            .Select(entry => JsonSerializer.Serialize(entry, JsonOptions))
            .ToArray();
        if (!File.Exists(eventsFile) || !await EventSidecarLineageMatchesAsync(metadataFile, instance.InstanceId).ConfigureAwait(false))
        {
            await RewriteEventSidecarAsync(eventsFile, metadataFile, instance.InstanceId, serializedHistory).ConfigureAwait(false);
            return;
        }

        var existingLines = await File.ReadAllLinesAsync(eventsFile).ConfigureAwait(false);
        var existingCount = existingLines.Length;
        if (existingCount > instance.History.Count || !HistoryPrefixMatches(existingLines, instance.History))
        {
            await RewriteEventSidecarAsync(eventsFile, metadataFile, instance.InstanceId, serializedHistory).ConfigureAwait(false);
            return;
        }

        var newLines = serializedHistory.Skip(existingCount).ToArray();
        if (newLines.Length > 0)
        {
            await File.AppendAllLinesAsync(eventsFile, newLines).ConfigureAwait(false);
        }
    }

    private static async Task<bool> EventSidecarLineageMatchesAsync(string metadataFile, string instanceId)
    {
        if (!File.Exists(metadataFile))
        {
            return false;
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<EventSidecarMetadata>(
                await File.ReadAllTextAsync(metadataFile).ConfigureAwait(false),
                JsonOptions);
            return metadata is not null
                && string.Equals(metadata.InstanceId, instanceId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task RewriteEventSidecarAsync(
        string eventsFile,
        string metadataFile,
        string instanceId,
        IReadOnlyList<string> serializedHistory)
    {
        var directory = Path.GetDirectoryName(eventsFile) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var suffix = $".{Guid.NewGuid():N}.tmp";
        var temporaryEventsFile = eventsFile + suffix;
        var temporaryMetadataFile = metadataFile + suffix;
        try
        {
            await File.WriteAllLinesAsync(temporaryEventsFile, serializedHistory).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                temporaryMetadataFile,
                JsonSerializer.Serialize(new EventSidecarMetadata(instanceId), JsonOptions)).ConfigureAwait(false);
            File.Move(temporaryEventsFile, eventsFile, overwrite: true);
            File.Move(temporaryMetadataFile, metadataFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryEventsFile))
            {
                File.Delete(temporaryEventsFile);
            }

            if (File.Exists(temporaryMetadataFile))
            {
                File.Delete(temporaryMetadataFile);
            }
        }
    }

    private static bool HistoryPrefixMatches(IReadOnlyList<string> existingLines, IReadOnlyList<WorkflowHistoryEntry> history)
    {
        if (existingLines.Count > history.Count)
        {
            return false;
        }

        for (var index = 0; index < existingLines.Count; index++)
        {
            try
            {
                var existing = JsonSerializer.Deserialize<WorkflowHistoryEntry>(existingLines[index], JsonOptions);
                var expected = history[index];
                if (existing is null
                    || existing.Timestamp != expected.Timestamp
                    || !string.Equals(existing.NodeId, expected.NodeId, StringComparison.Ordinal)
                    || existing.NodeType != expected.NodeType
                    || existing.Status != expected.Status
                    || !string.Equals(existing.Message, expected.Message, StringComparison.Ordinal)
                    || !string.Equals(
                        JsonSerializer.Serialize(existing.ContextChanges, JsonOptions),
                        JsonSerializer.Serialize(expected.ContextChanges, JsonOptions),
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        return true;
    }
    private static async Task<Dictionary<string, object?>?> LoadContextDeltaAsync(string? contextFile)
    {
        if (string.IsNullOrWhiteSpace(contextFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(contextFile).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static async Task<ResumeEnvelope> LoadResumeEnvelopeAsync(string resultFile)
    {
        var json = await File.ReadAllTextAsync(resultFile).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ResumeEnvelope>(json, WorkflowJsonSerializer.CreateDefaultOptions(indented: false))
            ?? throw new InvalidOperationException("Failed to deserialize resume envelope.");
    }

    private static WorkflowInstance CreateLsWorkflow(string path)
    {
        var (commandName, commandArgs) = OperatingSystem.IsWindows()
            ? ("cmd", $"/c dir /b \"{path}\"")
            : ("ls", $"-1 \"{path}\"");

        var start = new StateNode
        {
            Id = "state.start",
            Name = "Start",
            Groups =
            [
                new TransitionGroup
                {
                    Id = "group.list",
                    TransitionIds = ["transition.list"],
                },
            ],
        };

        var end = new StateNode
        {
            Id = "state.done",
            Name = "Done",
        };

        var transition = new CommandTransition
        {
            Id = "transition.list",
            Name = "List directory",
            Description = "List directory contents through a wrapped command-line execution",
            TargetNodeId = end.Id,
            OutputPath = "listing",
            StepKind = WorkflowStepKind.ToolCall,
            Command = new CommandInvocation
            {
                Kind = CommandInvocationKind.CommandLine,
                Name = commandName,
                Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["args"] = commandArgs,
                },
            },
        };

        return new WorkflowInstance
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            Status = WorkflowStatus.ReadyToStart,
            StartNodeId = start.Id,
            CurrentNodeId = start.Id,
            EndNodeId = end.Id,
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                [start.Id] = start,
                [end.Id] = end,
                [transition.Id] = transition,
            },
        };
    }

    private static AuditReuseRequest? CreateAuditReuseRequest(IReadOnlyList<string> args)
    {
        var sourceStep = GetOption(args, "--reuse-audit-step");
        var reason = GetOption(args, "--reuse-audit-reason");
        var verifiedBy = GetOption(args, "--reuse-audit-verified-by");
        if (sourceStep is null && reason is null && verifiedBy is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(sourceStep) || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(verifiedBy))
        {
            throw new InvalidOperationException("--reuse-audit-step, --reuse-audit-reason, and --reuse-audit-verified-by must be supplied together.");
        }

        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(sourceStep, "--reuse-audit-step");
        return new AuditReuseRequest(sourceStep, reason, verifiedBy);
    }
    private static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string GetRequiredOption(IReadOnlyList<string> args, string name)
    {
        return GetOption(args, name)
            ?? throw new InvalidOperationException($"Missing required option '{name}'.");
    }

    private static int GetRequiredInt32Option(IReadOnlyList<string> args, string name)
    {
        var value = GetRequiredOption(args, name);
        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"Option '{name}' must be a valid integer.");
        }

        return parsed;
    }

    private static void EnsureOptionAbsent(IReadOnlyList<string> args, string name, string commandName)
    {
        if (!string.IsNullOrWhiteSpace(GetOption(args, name)))
        {
            throw new InvalidOperationException($"Option '{name}' is not supported for '{commandName}'.");
        }
    }

    private static string GetEventsFile(string workflowFile)
    {
        return workflowFile + ".events.jsonl";
    }

    private static string GetEventsMetadataFile(string workflowFile)
    {
        return GetEventsFile(workflowFile) + ".meta.json";
    }

    private static int MapExitCode(WorkflowStatus status, bool suspended, bool failed)
    {
        if (failed || status == WorkflowStatus.Failed)
        {
            return 2;
        }

        if (status == WorkflowStatus.Succeeded)
        {
            return 0;
        }

        if (suspended || status == WorkflowStatus.WaitingExternal || status == WorkflowStatus.Running || status == WorkflowStatus.ReadyToStart || status == WorkflowStatus.Drafting)
        {
            return 3;
        }

        return 2;
    }


    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class AuditReuseRequest(string sourceStepDirectory, string reason, string verifiedBy)
    {
        public string SourceStepDirectory { get; } = sourceStepDirectory;
        public string Reason { get; } = reason;
        public string VerifiedBy { get; } = verifiedBy;
        public bool Consumed { get; set; }
    }
    private sealed record EventSidecarMetadata(
        [property: JsonPropertyName("instance_id")] string InstanceId);
    private sealed record ResumeEnvelope(
        [property: JsonPropertyName("transition_id")] string TransitionId,
        [property: JsonPropertyName("correlation_key")] string? CorrelationKey,
        [property: JsonPropertyName("payload")] Dictionary<string, object?>? Payload);
    private sealed record SoPropertyEnvelope(string Type, DateTimeOffset TimestampUtc, object Payload);
    private sealed record SkillStatusPayload(
        [property: JsonPropertyName("workflow_file")] string WorkflowFile,
        [property: JsonPropertyName("instance_id")] string InstanceId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("current_node_id")] string? CurrentNodeId,
        [property: JsonPropertyName("next_node_id")] string? NextNodeId,
        [property: JsonPropertyName("event_log_file")] string EventLogFile,
        [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
        [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
        [property: JsonPropertyName("can_resume")] bool CanResume = false,
        [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false);
    private sealed record SkillProgressPayload(
        [property: JsonPropertyName("workflow_file")] string WorkflowFile,
        [property: JsonPropertyName("instance_id")] string InstanceId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("current_node_id")] string? CurrentNodeId,
        [property: JsonPropertyName("next_node_id")] string? NextNodeId,
        [property: JsonPropertyName("event_log_file")] string EventLogFile,
        [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
        [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
        [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts AuditArtifacts,
        [property: JsonPropertyName("can_resume")] bool CanResume = false,
        [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false);
    private sealed record SkillBoundaryPayload(
        [property: JsonPropertyName("workflow_file")] string WorkflowFile,
        [property: JsonPropertyName("instance_id")] string InstanceId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
        [property: JsonPropertyName("current_step_kind")] string? CurrentStepKind,
        [property: JsonPropertyName("skill_hint")] string? SkillHint,
        [property: JsonPropertyName("memory_for_next_step")] object MemoryForNextStep,
        [property: JsonPropertyName("required_inputs")] IReadOnlyList<string> RequiredInputs,
        [property: JsonPropertyName("event_log_file")] string EventLogFile,
        [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
        [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
        [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts,
        [property: JsonPropertyName("can_resume")] bool CanResume = true,
        [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
        [property: JsonPropertyName("gate_evaluation")] GateEvaluationResult? GateEvaluation = null,
        [property: JsonPropertyName("projection_contract")] IReadOnlyDictionary<string, object?>? ProjectionContract = null);
    private sealed record SkillResultPayload(
        [property: JsonPropertyName("workflow_file")] string WorkflowFile,
        [property: JsonPropertyName("instance_id")] string InstanceId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("current_node_id")] string CurrentNodeId,
        [property: JsonPropertyName("context")] IReadOnlyDictionary<string, object?> Context,
        [property: JsonPropertyName("event_log_file")] string EventLogFile,
        [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
        [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
        [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts,
        [property: JsonPropertyName("can_resume")] bool CanResume = false,
        [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false);
    private sealed record SkillErrorPayload(
        [property: JsonPropertyName("workflow_file")] string WorkflowFile,
        [property: JsonPropertyName("instance_id")] string InstanceId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("event_log_file")] string EventLogFile,
        [property: JsonPropertyName("must_show_to_user_files")] IReadOnlyList<string> MustShowToUserFiles,
        [property: JsonPropertyName("workflow_location_summary")] string WorkflowLocationSummary,
        [property: JsonPropertyName("audit_artifacts")] WorkflowAuditArtifacts? AuditArtifacts,
        [property: JsonPropertyName("can_resume")] bool CanResume = false,
        [property: JsonPropertyName("fresh_instance_required")] bool FreshInstanceRequired = false,
        [property: JsonPropertyName("gate_evaluation")] GateEvaluationResult? GateEvaluation = null,
        [property: JsonPropertyName("projection_contract")] IReadOnlyDictionary<string, object?>? ProjectionContract = null,
        [property: JsonPropertyName("current_node_id")] string? CurrentNodeId = null);

    private sealed class XmlFragmentWriter
    {
        private readonly TextWriter _writer;
        private readonly object _gate = new();
        private bool _wrappedExecOpen;

        public XmlFragmentWriter(TextWriter writer)
        {
            _writer = writer;
        }

        public void BeginWrappedExec(string commandLine)
        {
            lock (_gate)
            {
                if (_wrappedExecOpen)
                {
                    EndWrappedExecCore();
                }

                _writer.WriteLine("<wrapped_exec>");
                _writer.WriteLine($"<commandline>{EscapeXml(commandLine)}</commandline>");
                _writer.WriteLine("<exectionstream>");
                _writer.Flush();
                _wrappedExecOpen = true;
            }
        }

        public void WriteExecutionStreamLine(string stream, string line)
        {
            lock (_gate)
            {
                if (!_wrappedExecOpen)
                {
                    BeginWrappedExec("unknown");
                }

                _writer.WriteLine(EscapeXml($"[{stream}] {line}"));
                _writer.Flush();
            }
        }

        public void EndWrappedExec()
        {
            lock (_gate)
            {
                EndWrappedExecCore();
            }
        }

        public void WriteSoProperty(SoPropertyEnvelope envelope)
        {
            lock (_gate)
            {
                if (_wrappedExecOpen)
                {
                    EndWrappedExecCore();
                }

                _writer.WriteLine("<so_property>");
                _writer.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
                _writer.WriteLine("</so_property>");
                _writer.Flush();
            }
        }

        private void EndWrappedExecCore()
        {
            if (!_wrappedExecOpen)
            {
                return;
            }

            _writer.WriteLine("</exectionstream>");
            _writer.WriteLine("</wrapped_exec>");
            _writer.Flush();
            _wrappedExecOpen = false;
        }

        private static string EscapeXml(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }
    }

    private static string MapPublicStatus(WorkflowStatus workflowStatus)
    {
        return workflowStatus switch
        {
            WorkflowStatus.WaitingExternal => "blocked",
            WorkflowStatus.Succeeded => "completed",
            WorkflowStatus.Failed => "failed",
            _ => "active",
        };
    }
}
