using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Common.Runtime;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

internal static partial class SkillCli
{
    private const string FragmentDesignUsage =
        """
        Experimental SO fragment design:
        loom skill fragment-design start --workflow-file <draft> --scope-file <json>
        loom skill fragment-design status --session-dir <path>
        loom skill fragment-design edit --session-dir <path> --edit-file <json>
        loom skill fragment-design compose --session-dir <path> [--audit-output <path>]
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

    private static async Task<int> HandleFragmentDesignAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine(FragmentDesignUsage);
            return 1;
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
                var (started, initial) = await WorkflowFragmentSessionStore.CreateAsync(workflow, scope).ConfigureAwait(false);
                WriteFragmentDesignStatus(started, initial);
                return 0;

            case "status":
                CheckFragmentDesignOptions(options, "--session-dir");
                var existing = WorkflowFragmentSessionStore.Open(GetRequiredOption(options, "--session-dir"));
                WriteFragmentDesignStatus(existing, await existing.ReadAsync().ConfigureAwait(false));
                return 0;

            case "edit":
                CheckFragmentDesignOptions(options, "--session-dir", "--edit-file");
                var editFile = CliFileInputGuard.RequireExistingFile(GetRequiredOption(options, "--edit-file"), "--edit-file");
                var patch = await ReadFragmentDesignInputAsync<FragmentDesignPatch>(editFile).ConfigureAwait(false);
                var editable = WorkflowFragmentSessionStore.Open(GetRequiredOption(options, "--session-dir"));
                var changed = await editable.ApplyAsync(patch.ExpectedGeneration, patch.Edit, patch.Reason).ConfigureAwait(false);
                WriteFragmentDesignStatus(editable, changed);
                return 0;

            case "compose":
                CheckFragmentDesignOptions(options, "--session-dir", "--audit-output");
                return await ComposeFragmentDesignAsync(
                    WorkflowFragmentSessionStore.Open(GetRequiredOption(options, "--session-dir")),
                    GetOption(options, "--audit-output")).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Unknown fragment-design action '{args[0]}'.");
        }
    }

    private static async Task<int> ComposeFragmentDesignAsync(WorkflowFragmentSessionStore store, string? auditOutput)
    {
        RuntimeArtifactPathGuard.EnsureAuditOutputOutsideSkillDirectory(auditOutput);
        var (snapshot, catalog) = await store.ReadForCompositionAsync().ConfigureAwait(false);
        var path = Path.Combine(store.Root, $"candidate-{snapshot.Generation}-{Guid.NewGuid():N}.json");
        var staging = path + ".staging";
        try
        {
            await using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(snapshot.WorkflowJson).ConfigureAwait(false);
            }

            var options = new List<string> { "--workflow-file", staging };
            if (auditOutput is not null)
            {
                options.AddRange(["--audit-output", auditOutput]);
            }

            var (_, auditPath) = await CompileWorkflowAsync(options, catalog).ConfigureAwait(false);
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
        }, JsonOptions));
    }

    private sealed record FragmentDesignPatch(long ExpectedGeneration, string Reason, WorkflowFragmentEdit Edit);
}
