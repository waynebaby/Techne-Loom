using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Common.Runtime;
namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed class WorkflowFragmentDesignCli(
    string route,
    WorkflowFragmentRuntime runtime,
    Func<string, string?, ContextFieldCatalog, Task<string>> compile)
{
    internal string Usage =>
        $"""
        loom {route} fragment-design start --workflow-file <draft> --scope-file <json>
        loom {route} fragment-design status --session-dir <path>
        loom {route} fragment-design edit --session-dir <path> --edit-file <json>
        loom {route} fragment-design compose --session-dir <path> [--audit-output <path>]
        loom {route} fragment-design check --design-file <blueprint.json> [--cache-dir <path>]
        loom {route} fragment-design build --design-file <blueprint.json> [--audit-output <path>]
        loom {route} fragment-design prompt --objective-file <path> [--session-dir <path>]
        """;

    private static readonly JsonSerializerOptions FragmentDesignOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        AllowOutOfOrderMetadataProperties = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<WorkflowFragmentKind>(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    internal async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        var options = args.Skip(1).ToArray();
        switch (args[0])
        {
            case "start":
                CheckFragmentDesignOptions(options, "--workflow-file", "--scope-file");
                var workflow = GetRequiredOption(options, "--workflow-file");
                var scopeFile = GetRequiredOption(options, "--scope-file");
                CliFileInputGuard.RequireExistingFiles(("--workflow-file", workflow), ("--scope-file", scopeFile));
                var scope = await ReadFragmentDesignInputAsync<WorkflowFragmentId[]>(scopeFile).ConfigureAwait(false);
                var (started, initial) = await WorkflowFragmentSessionStore.CreateAsync(workflow, scope, runtime).ConfigureAwait(false);
                WriteFragmentDesignStatus(started, initial);
                return 0;

            case "status":
                CheckFragmentDesignOptions(options, "--session-dir");
                var existing = WorkflowFragmentSessionStore.Open(GetRequiredOption(options, "--session-dir"), runtime);
                WriteFragmentDesignStatus(existing, await existing.ReadAsync().ConfigureAwait(false));
                return 0;

            case "edit":
                CheckFragmentDesignOptions(options, "--session-dir", "--edit-file");
                var editFile = CliFileInputGuard.RequireExistingFile(GetRequiredOption(options, "--edit-file"), "--edit-file");
                var patch = await ReadFragmentDesignInputAsync<FragmentDesignPatch>(editFile).ConfigureAwait(false);
                var editable = WorkflowFragmentSessionStore.Open(GetRequiredOption(options, "--session-dir"), runtime);
                var changed = await editable.ApplyAsync(patch.ExpectedGeneration, patch.Edit, patch.Reason).ConfigureAwait(false);
                WriteFragmentDesignStatus(editable, changed);
                return 0;

            case "compose":
                CheckFragmentDesignOptions(options, "--session-dir", "--audit-output");
                return await ComposeFragmentDesignAsync(
                    WorkflowFragmentSessionStore.Open(GetRequiredOption(options, "--session-dir"), runtime),
                    GetOption(options, "--audit-output")).ConfigureAwait(false);

            case "check":
                CheckFragmentDesignOptions(options, "--design-file", "--cache-dir");
                var design = await ReadBlueprintAsync(options).ConfigureAwait(false);
                var cache = WorkflowFragmentCheckCache.Open(GetOption(options, "--cache-dir"));
                var (errors, checkedCount, reusedCount) = await design.CheckExpressionsAsync(cache).ConfigureAwait(false);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = errors.Count != 0 ? "invalid" : design.Unresolved.Count == 0 ? "ready_for_compile" : "incomplete",
                    fragment_count = design.FragmentCount,
                    unresolved_count = design.Unresolved.Count,
                    unresolved = design.Unresolved.Take(20),
                    error_count = errors.Count,
                    errors = errors.Take(20),
                    cache_dir = cache.Root,
                    checked_expression_count = checkedCount,
                    reused_expression_count = reusedCount,
                }, FragmentDesignOptions));
                return 0;

            case "build":
                CheckFragmentDesignOptions(options, "--design-file", "--audit-output");
                return await BuildFragmentDesignAsync(
                    await ReadBlueprintAsync(options).ConfigureAwait(false), GetOption(options, "--audit-output")).ConfigureAwait(false);

            case "prompt":
                CheckFragmentDesignOptions(options, "--objective-file", "--session-dir");
                return await WriteFragmentDesignPromptAsync(options).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Unknown fragment-design action '{args[0]}'.");
        }
    }

    private async Task<int> WriteFragmentDesignPromptAsync(string[] options)
    {
        var objective = CliFileInputGuard.RequireExistingFile(GetRequiredOption(options, "--objective-file"), "--objective-file");
        var sessionPath = GetOption(options, "--session-dir");
        var session = sessionPath is null ? null : WorkflowFragmentSessionStore.Open(sessionPath, runtime);
        (long Generation, string BaselinePath, string HandoffPath, IReadOnlyList<WorkflowFragmentId> Scope)? handoff =
            session is null ? null : await session.WritePromptHandoffAsync().ConfigureAwait(false);
        var scope = handoff?.Scope ?? Array.Empty<WorkflowFragmentId>();
        var common = new[]
        {
            "Use the objective file and recover the original SKILL and its business documents, references, history and verifiable outputs. Preserve its goals, required evidence, and human approvals.",
            "Ask a business expert through the user only when the authoritative material cannot be recovered and the resulting gap materially affects business goals, required outputs or acceptance.",
            "Map each requested change to its owning fragment. Keep work outside the change scope frozen and keep source and audit files outside the SKILL source directory.",
            "Use compile feedback as design evidence. Execute business actions only through the authorized owning workflow and its original gates.",
        };
        string[] steps = sessionPath is null
            ?
            [
                "Write a loom.workflow-fragment-blueprint.v1 manifest and separate typed state, transition, group, gate and route files.",
                $"Use loom {route} fragment-design check after each stage. Reuse its cache_dir and resolve missing references and invalid expressions before building.",
                $"Use loom {route} fragment-design build only with the real gates and evidence contracts in place. Return the candidate and compile audit paths.",
            ]
            :
            [
                "Read the current workflow and full authorization from handoff_file. Use baseline_file for provenance. Inspect only the relevant authorized fragments and dependencies. Use the current expected generation and generate one typed edit file containing expectedGeneration, reason and edit.",
                $"Use loom {route} fragment-design edit. Request a fresh handoff on a generation conflict and retain unaffected frozen fragments.",
                $"Use loom {route} fragment-design compose for the final owning compile. Return the new generation and candidate and audit paths.",
            ];
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schema = "loom.fragment-design-prompt.v1",
            mode = sessionPath is null ? "new" : "edit",
            runtime_binding = runtime.Binding,
            runtime_version = runtime.Version,
            objective_file = objective,
            session_dir = session?.Root,
            baseline_file = handoff?.BaselinePath,
            handoff_file = handoff?.HandoffPath,
            expected_generation = handoff?.Generation,
            authorized_fragment_count = scope.Count,
            authorized_fragments = scope.Take(50),
            instructions = common.Concat(steps),
        }, FragmentDesignOptions));
        return 0;
    }

    private Task<WorkflowFragmentBlueprint> ReadBlueprintAsync(string[] options)
        => WorkflowFragmentBlueprint.ReadAsync(
            CliFileInputGuard.RequireExistingFile(GetRequiredOption(options, "--design-file"), "--design-file"), runtime);

    private async Task<int> BuildFragmentDesignAsync(WorkflowFragmentBlueprint design, string? auditOutput)
    {
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        var scratch = Directory.CreateTempSubdirectory("loom-fragment-build-");
        var staging = Path.Combine(scratch.FullName, "candidate.staging");
        try
        {
            var (workflowJson, catalog) = await design.BuildAsync(scratch.FullName, runtime).ConfigureAwait(false);
            var prefix = GetCandidatePrefix(workflowJson);
            if (prefix.Length > 0)
            {
                staging = Path.Combine(scratch.FullName, $"{prefix}candidate.staging.json");
            }
            await File.WriteAllTextAsync(staging, workflowJson).ConfigureAwait(false);
            var auditPath = await compile(staging, auditOutput, catalog).ConfigureAwait(false);
            var candidate = Path.Combine(scratch.FullName, $"{prefix}candidate.json");
            File.Move(staging, candidate);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status = "succeeded",
                candidate_file = candidate,
                compile_audit_path = auditPath,
            }, FragmentDesignOptions));
            return 0;
        }
        finally
        {
            File.Delete(staging);
            File.Delete(staging + ".lock");
            File.Delete(Path.Combine(scratch.FullName, "build.cs"));
            if (!Directory.EnumerateFileSystemEntries(scratch.FullName).Any())
            {
                scratch.Delete();
            }
        }
    }

    private async Task<int> ComposeFragmentDesignAsync(WorkflowFragmentSessionStore store, string? auditOutput)
    {
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        var (snapshot, catalog) = await store.ReadForCompositionAsync().ConfigureAwait(false);
        var prefix = GetCandidatePrefix(snapshot.WorkflowJson);
        var path = Path.Combine(store.Root, $"{prefix}candidate-{snapshot.Generation}-{Guid.NewGuid():N}.json");
        var staging = prefix.Length == 0 ? path + ".staging" : path[..^".json".Length] + ".staging.json";
        try
        {
            await using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(snapshot.WorkflowJson).ConfigureAwait(false);
            }

            var auditPath = await compile(staging, auditOutput, catalog).ConfigureAwait(false);
            await store.PublishCandidateAsync(staging, path, snapshot.Generation).ConfigureAwait(false);
            WriteFragmentDesignStatus(store, snapshot, path, auditPath);
            return 0;
        }
        finally
        {
            File.Delete(staging);
            File.Delete(staging + ".lock");
        }
    }

    private static string GetCandidatePrefix(string workflowJson)
    {
        var workflow = WorkflowJsonSerializer.Deserialize(workflowJson);
        var error = CanonicalWorkflowFileStore.GetSkillFileNameError(workflow);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        return string.IsNullOrWhiteSpace(workflow.OwnerSkillSlug) ? "" : $"{workflow.OwnerSkillSlug}--";
    }

    private static string? GetOption(string[] args, string option)
    {
        for (var index = 0; index < args.Length; index += 2)
        {
            if (args[index] == option)
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string GetRequiredOption(string[] args, string option)
        => GetOption(args, option) ?? throw new InvalidOperationException($"Required option '{option}' is missing.");

    private static async Task<T> ReadFragmentDesignInputAsync<T>(string path) where T : class
    {
        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        using var document = JsonDocument.Parse(text);
        WorkflowDraftImport.RejectDuplicateProperties(document.RootElement, "");
        return document.RootElement.Deserialize<T>(FragmentDesignOptions)
            ?? throw new JsonException("Expected a non-null fragment design input.");
    }

    private static void CheckFragmentDesignOptions(string[] args, params string[] allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            var option = args[index];
            if (!allowed.Contains(option, StringComparer.Ordinal) || !seen.Add(option)
                || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected, repeated, or incomplete fragment-design option '{option}'.");
            }
        }
    }

    private static void WriteFragmentDesignStatus(
        WorkflowFragmentSessionStore store, WorkflowFragmentSnapshot snapshot, string? candidate = null, string? auditPath = null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = "succeeded",
            session_dir = store.Root,
            generation = snapshot.Generation,
            fragment_count = snapshot.Fragments.Count,
            lock_event_count = snapshot.LockEvents.Length,
            candidate_file = candidate,
            compile_audit_path = auditPath,
        }, FragmentDesignOptions));
    }

    private sealed record FragmentDesignPatch(long ExpectedGeneration, string Reason, WorkflowFragmentEdit Edit);
}
