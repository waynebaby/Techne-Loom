using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed class WorkflowFragmentBlueprint
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    private readonly WorkflowInstance _draft;

    private WorkflowFragmentBlueprint(WorkflowInstance draft, List<string> unresolved, int fragmentCount)
    {
        _draft = draft;
        Unresolved = unresolved;
        FragmentCount = fragmentCount;
    }

    internal IReadOnlyList<string> Unresolved { get; }

    internal int FragmentCount { get; }

    internal async Task<IReadOnlyList<string>> CheckExpressionsAsync()
    {
        var errors = new List<string>();
        var compiler = new ExpressionCompilerRouter();
        var catalog = _draft.ContractBinding is null
            ? null
            : (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(_draft).ConfigureAwait(false)).Catalog;

        void Check(ExpressionDefinition? expression, string location)
        {
            if (expression is null)
            {
                return;
            }

            var result = catalog is null
                ? compiler.Compile(_draft.ExpressionBinding, expression, location)
                : compiler.CompileWithContextFieldCatalog(_draft.ExpressionBinding, expression, catalog, location);
            if (!result.IsSuccess)
            {
                errors.Add($"{location}: {result.Feedback.Message}");
            }
        }

        foreach (var node in _draft.Nodes.Values)
        {
            if (node is StateNode state)
            {
                Check(state.StateFailedExpression, $"state {state.Id} stateFailedExpression");
                foreach (var group in state.Groups)
                {
                    Check(group.PassExpression, $"group {state.Id}/{group.Id} passExpression");
                    Check(group.TimeoutTransition?.GuardExpression, $"group {state.Id}/{group.Id} timeout guardExpression");
                    Check(group.TimeoutTransition?.PassExpression, $"group {state.Id}/{group.Id} timeout passExpression");
                }
            }
            else if (node is TransitionBase transition)
            {
                Check(transition.GuardExpression, $"transition {transition.Id} guardExpression");
                Check(transition.PassExpression, $"transition {transition.Id} passExpression");
            }
        }

        foreach (var (key, gate) in _draft.Validation!.Gates)
        {
            Check(gate.PassExpression, $"gate {key} passExpression");
        }

        return errors;
    }

    internal static async Task<WorkflowFragmentBlueprint> ReadAsync(string path, WorkflowFragmentRuntime runtime)
    {
        var manifestPath = Path.GetFullPath(path);
        var manifest = await ReadJsonAsync<BlueprintManifest>(manifestPath).ConfigureAwait(false);
        if (manifest.Schema != "loom.workflow-fragment-blueprint.v1" || string.IsNullOrWhiteSpace(manifest.InstanceId))
        {
            throw new InvalidOperationException("A versioned fragment blueprint and workflow identity are required.");
        }

        var root = Path.GetDirectoryName(manifestPath)!;
        var draft = new WorkflowInstance
        {
            InstanceId = manifest.InstanceId,
            RuntimeBinding = runtime.Binding,
            RuntimeVersion = runtime.Version,
            Status = WorkflowStatus.Drafting,
            TemplateKind = manifest.TemplateKind,
            TaskType = manifest.TaskType,
            WorkflowKind = manifest.WorkflowKind,
            CaseId = manifest.CaseId,
            RunId = manifest.RunId,
            StartNodeId = manifest.StartNodeId ?? "",
            CurrentNodeId = manifest.StartNodeId ?? "",
            EndNodeId = manifest.EndNodeId,
            ContractBinding = manifest.ContractBinding,
            Context = manifest.Context ?? new(StringComparer.Ordinal),
            ExpressionBinding = manifest.ExpressionBinding ?? new(),
            Validation = new()
            {
                DeclaredUserOwnedFields = manifest.DeclaredUserOwnedFields ?? [],
                ReservedRuntimeOwnedFields = manifest.ReservedRuntimeOwnedFields ?? [],
            },
        };

        var unresolved = new List<string>();
        if (manifest.ContractBinding is null)
        {
            unresolved.Add("The owning context catalog requires a contractBinding.");
        }
        var groups = new HashSet<(string State, string Group)>();
        foreach (var file in manifest.States)
        {
            var node = await ReadFragmentAsync<ITaskNode>(root, file).ConfigureAwait(false);
            if (node is not StateNode { EntranceTime: null, Groups.Count: 0 })
            {
                throw new InvalidOperationException($"State fragment '{file}' must declare a state without runtime fields or group links.");
            }

            Add(draft.Nodes, node.Id, node, file);
        }

        foreach (var file in manifest.Transitions)
        {
            using var document = await ReadDocumentAsync(ResolveFragment(root, file)).ConfigureAwait(false);
            var node = ReadJson<ITaskNode>(document);
            if (node is not TransitionBase transition
                || !document.RootElement.TryGetProperty("guardExpression", out var guard) || guard.ValueKind == JsonValueKind.Null
                || !document.RootElement.TryGetProperty("passExpression", out var pass) || pass.ValueKind == JsonValueKind.Null
                || WorkflowDraftImport.HasTransitionExecutionState(transition))
            {
                throw new InvalidOperationException($"Transition fragment '{file}' must explicitly declare guard and pass expressions with fresh execution state.");
            }

            Add(draft.Nodes, node.Id, node, file);
        }

        foreach (var file in manifest.Groups)
        {
            var item = await ReadFragmentAsync<BlueprintGroup>(root, file).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(item.StateId) || string.IsNullOrWhiteSpace(item.Group.Id)
                || WorkflowDraftImport.HasTransitionExecutionState(item.Group.TimeoutTransition)
                || !groups.Add((item.StateId, item.Group.Id)))
            {
                throw new InvalidOperationException($"Group fragment '{file}' has a missing or duplicate identity.");
            }

            if (draft.Nodes.TryGetValue(item.StateId, out var node) && node is StateNode state)
            {
                state.Groups.Add(item.Group);
            }
            else
            {
                unresolved.Add($"Group '{item.Group.Id}' requires owner state '{item.StateId}'.");
            }
        }

        foreach (var file in manifest.Gates)
        {
            var item = await ReadFragmentAsync<BlueprintGate>(root, file).ConfigureAwait(false);
            Add(draft.Validation.Gates, item.Id, item.Gate, file);
            if (item.Gate.PassExpression is null || string.IsNullOrWhiteSpace(item.Gate.PassExpression.Source))
            {
                unresolved.Add($"Gate '{item.Id}' requires a pass expression.");
            }
        }

        foreach (var file in manifest.Routes)
        {
            var item = await ReadFragmentAsync<BlueprintRoute>(root, file).ConfigureAwait(false);
            Add(draft.Validation.Routes, item.Id, item.Route, file);
        }

        if (!draft.Nodes.TryGetValue(draft.StartNodeId, out var start) || start is not StateNode)
        {
            unresolved.Add("A declared start state is required.");
        }

        if (draft.EndNodeId is not null && (!draft.Nodes.TryGetValue(draft.EndNodeId, out var end) || end is not StateNode))
        {
            unresolved.Add($"End state '{draft.EndNodeId}' has not been declared.");
        }

        if (WorkflowFragmentGraph.Create(draft).HasUnresolvedReferences)
        {
            unresolved.Add("One or more groups, transitions, gates, or routes reference undeclared fragments.");
        }

        return new WorkflowFragmentBlueprint(draft, unresolved,
            draft.Nodes.Count + groups.Count + draft.Validation.Gates.Count + draft.Validation.Routes.Count);
    }

    internal async Task<(string WorkflowJson, ContextFieldCatalog Catalog)> BuildAsync(string scratchDirectory, WorkflowFragmentRuntime runtime)
    {
        if (Unresolved.Count != 0)
        {
            throw new InvalidOperationException("The fragment blueprint is incomplete. Run fragment-design check.");
        }

        runtime.CheckIdentity(_draft);
        var candidate = WorkflowJsonSerializer.Deserialize(WorkflowJsonSerializer.Serialize(_draft));
        candidate.Status = WorkflowStatus.ReadyToStart;
        var expected = WorkflowJsonSerializer.Serialize(candidate);
        var scriptFile = Path.Combine(scratchDirectory, "build.cs");
        const string script = """
            using Techne.Loom.Abstractions.TaskTracking.Model;

            namespace Techne.Loom.WorkflowScripts;

            public static class WorkflowInstanceBuilder
            {
                public static WorkflowInstance Build(WorkflowScriptInput input)
                {
                    return (WorkflowInstance)input.Options["assembledWorkflow"];
                }
            }
            """;
        await File.WriteAllTextAsync(scriptFile, script).ConfigureAwait(false);
        var execution = await new WorkflowScriptHost().ExecuteBuilderAsync(scriptFile, new WorkflowScriptInput
        {
            RuntimeBinding = runtime.Binding,
            RuntimeVersion = runtime.Version,
            Options = new Dictionary<string, object?>(StringComparer.Ordinal) { ["assembledWorkflow"] = candidate },
        }).ConfigureAwait(false);
        if (!execution.IsSuccess || execution.Value is null)
        {
            throw new InvalidOperationException(execution.Feedback.Error ?? "Fragment builder failed.");
        }

        var result = WorkflowJsonSerializer.Serialize(execution.Value);
        if (result != expected)
        {
            throw new InvalidOperationException("The generated builder changed the assembled fragments.");
        }

        var catalog = await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(execution.Value).ConfigureAwait(false);
        return (result, catalog.Catalog);
    }

    private static void Add<T>(Dictionary<string, T> values, string id, T value, string file)
    {
        if (string.IsNullOrWhiteSpace(id) || !values.TryAdd(id, value))
        {
            throw new InvalidOperationException($"Fragment '{file}' has a missing or duplicate identity.");
        }
    }

    private static Task<T> ReadFragmentAsync<T>(string root, string file) where T : class
        => ReadJsonAsync<T>(ResolveFragment(root, file));

    private static string ResolveFragment(string root, string file)
    {
        if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file))
        {
            throw new InvalidOperationException("Blueprint fragments must use relative file paths.");
        }

        var target = Path.GetFullPath(file, root);
        var relative = Path.GetRelativePath(root, target);
        if (relative is "." or ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException($"Fragment path '{file}' leaves its blueprint directory.");
        }

        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException($"Fragment path '{file}' follows a link.");
            }
        }

        return target;
    }

    private static async Task<JsonDocument> ReadDocumentAsync(string path)
        => JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));

    private static async Task<T> ReadJsonAsync<T>(string path) where T : class
    {
        using var document = await ReadDocumentAsync(path).ConfigureAwait(false);
        return ReadJson<T>(document);
    }

    private static T ReadJson<T>(JsonDocument document) where T : class
    {
        var root = document.RootElement;
        WorkflowDraftImport.RejectDuplicateProperties(root, "");
        var value = root.Deserialize<T>(JsonOptions) ?? throw new JsonException("Expected a blueprint JSON object.");
        var serialized = JsonSerializer.SerializeToElement(value, JsonOptions);
        VerifyDynamicData(root, serialized, ["context"]);
        VerifyDynamicData(root, serialized, ["command", "parameters"]);
        VerifyDynamicData(root, serialized, ["group", "timeoutTransition", "command", "parameters"]);
        return value;
    }

    private static void VerifyDynamicData(JsonElement source, JsonElement serialized, string[] propertyPath)
    {
        var pointer = "/" + string.Join("/", propertyPath);
        foreach (var property in propertyPath)
        {
            if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(property, out source))
            {
                return;
            }
        }

        if (source.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        foreach (var property in propertyPath)
        {
            if (serialized.ValueKind != JsonValueKind.Object || !serialized.TryGetProperty(property, out serialized))
            {
                throw new JsonException($"Blueprint data is not preserved at '{pointer}'.");
            }
        }

        WorkflowDraftImport.VerifyEquivalent(source, serialized, pointer);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = WorkflowJsonSerializer.CreateDefaultOptions();
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.AllowOutOfOrderMetadataProperties = true;
        return options;
    }

    private sealed record BlueprintManifest(
        string Schema, string InstanceId, string[] States, string[] Transitions, string[] Groups, string[] Gates, string[] Routes,
        string? StartNodeId = null, string? EndNodeId = null, string? TemplateKind = null, string? TaskType = null,
        string? WorkflowKind = null, string? CaseId = null, string? RunId = null, ContractBinding? ContractBinding = null,
        Dictionary<string, object?>? Context = null, ExpressionBinding? ExpressionBinding = null,
        List<string>? DeclaredUserOwnedFields = null, List<string>? ReservedRuntimeOwnedFields = null);

    private sealed record BlueprintGroup(string StateId, TransitionGroup Group);
    private sealed record BlueprintGate(string Id, WorkflowValidationGate Gate);
    private sealed record BlueprintRoute(string Id, WorkflowRouteValidationProfile Route);
}
