using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.AgentOrchestrator.Runtime;

internal static class AoRuntimeWorkflowBridge
{
    private const string MetadataPrefix = "ao_runtime.";
    private const string ObjectiveKey = MetadataPrefix + "objective";
    private const string StatusKey = MetadataPrefix + "status";
    private const string LastTransitionIdKey = MetadataPrefix + "last_transition_id";
    private const string LastBoundaryReasonKey = MetadataPrefix + "last_boundary_reason";
    private const string PendingRequirementsKey = MetadataPrefix + "pending_requirements";
    private const string NextFrontierKey = MetadataPrefix + "next_frontier";
    private const string HumanOrAgentHintKey = MetadataPrefix + "human_or_agent_hint";
    private const string WeaveOutRequestKey = MetadataPrefix + "weave_out_request";
    private const string UpdatedAtKey = MetadataPrefix + "updated_at";
    private const string AuditStepSequenceKey = MetadataPrefix + "audit_step_sequence";

    public static WorkflowInstance CreateInitialRuntimeWorkflow(string sessionId, AoWorkflowSnapshot snapshot)
    {
        var blockedState = new StateNode
        {
            Id = snapshot.CurrentNodeId,
            Name = snapshot.CurrentNodeId,
            Description = snapshot.HumanOrAgentHint,
            Groups =
            [
                new TransitionGroup
                {
                    Id = $"group.{NormalizeSegment(snapshot.CurrentNodeId)}",
                    Strategy = ConcurrencyStrategy.FirstSuccess,
                    TransitionIds = string.IsNullOrWhiteSpace(snapshot.LastTransitionId)
                        ? []
                        : [snapshot.LastTransitionId],
                },
            ],
        };

        var nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
        {
            [blockedState.Id] = blockedState,
        };

        if (!string.IsNullOrWhiteSpace(snapshot.LastTransitionId))
        {
            nodes[snapshot.LastTransitionId] = new ToBeRefinedTransition
            {
                Id = snapshot.LastTransitionId,
                Name = snapshot.LastTransitionId,
                TargetNodeId = snapshot.Status == "completed" ? "state.completed" : snapshot.CurrentNodeId,
                StepKind = WorkflowStepKind.WaitResume,
                DesignNotes = snapshot.LastBoundaryReason,
            };
        }

        var runtimeContext = CopyContext(snapshot.Context);
        return new WorkflowInstance
        {
            InstanceId = sessionId,
            StartNodeId = blockedState.Id,
            CurrentNodeId = blockedState.Id,
            EndNodeId = snapshot.Status == "completed" ? "state.completed" : blockedState.Id,
            Status = ToWorkflowStatus(snapshot.Status),
            Version = Math.Max(snapshot.AuditStepSequence, 1),
            LastActivityUtc = snapshot.UpdatedAt,
            Context = ApplyMetadata(runtimeContext, snapshot),
            Nodes = nodes,
        };
    }

    public static WorkflowInstance MergeExternalRuntimeWorkflow(
        WorkflowInstance currentRuntime,
        WorkflowInstance externalRuntime,
        AoWorkflowSnapshot snapshot)
    {
        var merged = WorkflowInstanceCloner.Clone(externalRuntime);
        merged.InstanceId = currentRuntime.InstanceId;
        merged.Version = Math.Max(currentRuntime.Version, merged.Version);
        merged.Status = currentRuntime.Status;
        merged.LastActivityUtc = currentRuntime.LastActivityUtc;

        var mergedContext = CopyContext(externalRuntime.Context);
        foreach (var pair in currentRuntime.Context)
        {
            if (pair.Key.StartsWith(MetadataPrefix, StringComparison.Ordinal))
            {
                mergedContext[pair.Key] = CloneValue(pair.Value);
            }
        }

        merged.Context = ApplyMetadata(mergedContext, snapshot);
        return merged;
    }

    public static WorkflowInstance SeedRuntimeWorkflow(
        WorkflowInstance authoredRuntime,
        string sessionId,
        AoWorkflowSnapshot snapshot)
    {
        var seeded = WorkflowInstanceCloner.Clone(authoredRuntime);
        seeded.InstanceId = string.IsNullOrWhiteSpace(seeded.InstanceId) ? sessionId : seeded.InstanceId;
        seeded.Status = ToWorkflowStatus(snapshot.Status);
        seeded.Version = Math.Max(seeded.Version, snapshot.AuditStepSequence);
        seeded.LastActivityUtc = snapshot.UpdatedAt;
        seeded.Context = ApplyMetadata(CopyContext(seeded.Context), snapshot);
        EnsureStateNode(seeded, snapshot.CurrentNodeId, snapshot.HumanOrAgentHint, snapshot.LastTransitionId);
        seeded.CurrentNodeId = snapshot.CurrentNodeId;
        seeded.StartNodeId = string.IsNullOrWhiteSpace(seeded.StartNodeId) ? snapshot.CurrentNodeId : seeded.StartNodeId;
        seeded.EndNodeId ??= snapshot.CurrentNodeId;
        return seeded;
    }

    public static WorkflowInstance UpdateRuntimeWorkflow(WorkflowInstance runtimeWorkflow, AoWorkflowSnapshot snapshot)
    {
        var updated = WorkflowInstanceCloner.Clone(runtimeWorkflow);
        EnsureStateNode(updated, snapshot.CurrentNodeId, snapshot.HumanOrAgentHint, snapshot.LastTransitionId);

        if (string.Equals(snapshot.Status, "completed", StringComparison.Ordinal))
        {
            EnsureStateNode(updated, "state.completed", "AO completed.", null);
            updated.CurrentNodeId = "state.completed";
            updated.EndNodeId = "state.completed";
        }
        else
        {
            updated.CurrentNodeId = snapshot.CurrentNodeId;
            updated.EndNodeId ??= snapshot.CurrentNodeId;
        }

        updated.Status = ToWorkflowStatus(snapshot.Status);
        updated.Version = Math.Max(updated.Version + 1, snapshot.AuditStepSequence);
        updated.LastActivityUtc = snapshot.UpdatedAt;
        updated.Context = ApplyMetadata(CopyContext(updated.Context), snapshot);
        return updated;
    }

    public static string? TryGetLastTransitionId(WorkflowInstance instance)
        => TryGetString(instance.Context, LastTransitionIdKey);

    public static string? TryGetStatus(WorkflowInstance instance)
        => TryGetString(instance.Context, StatusKey);

    public static AoWorkflowSnapshot ToSnapshot(WorkflowInstance instance)
    {
        var context = CopyContext(instance.Context);
        context = RemoveMetadata(context);
        return new AoWorkflowSnapshot(
            Objective: TryGetString(instance.Context, ObjectiveKey) ?? string.Empty,
            Context: context,
            Status: TryGetString(instance.Context, StatusKey) ?? "blocked",
            CurrentNodeId: instance.CurrentNodeId,
            LastTransitionId: TryGetString(instance.Context, LastTransitionIdKey),
            LastBoundaryReason: TryGetString(instance.Context, LastBoundaryReasonKey),
            UpdatedAt: TryGetDateTimeOffset(instance.Context, UpdatedAtKey) ?? instance.LastActivityUtc ?? DateTimeOffset.UtcNow,
            PendingRequirements: TryGetStringList(instance.Context, PendingRequirementsKey),
            NextFrontier: TryGetStringList(instance.Context, NextFrontierKey),
            HumanOrAgentHint: TryGetString(instance.Context, HumanOrAgentHintKey),
            WeaveOutRequest: TryGetWeaveOutRequest(instance.Context, WeaveOutRequestKey),
            AuditStepSequence: TryGetInt(instance.Context, AuditStepSequenceKey) ?? Math.Max(instance.Version, 1));
    }

    private static Dictionary<string, object?> ApplyMetadata(Dictionary<string, object?> context, AoWorkflowSnapshot snapshot)
    {
        context[ObjectiveKey] = snapshot.Objective;
        context[StatusKey] = snapshot.Status;
        context[LastTransitionIdKey] = snapshot.LastTransitionId;
        context[LastBoundaryReasonKey] = snapshot.LastBoundaryReason;
        context[PendingRequirementsKey] = snapshot.PendingRequirements is null ? null : snapshot.PendingRequirements.ToList();
        context[NextFrontierKey] = snapshot.NextFrontier is null ? null : snapshot.NextFrontier.ToList();
        context[HumanOrAgentHintKey] = snapshot.HumanOrAgentHint;
        context[WeaveOutRequestKey] = snapshot.WeaveOutRequest is null
            ? null
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["objective"] = snapshot.WeaveOutRequest.Objective,
                ["artifacts"] = snapshot.WeaveOutRequest.Artifacts.ToList(),
                ["evidence_references"] = snapshot.WeaveOutRequest.EvidenceReferences?.Select(reference => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = reference.Path,
                    ["start_line"] = reference.StartLine,
                    ["end_line"] = reference.EndLine,
                    ["role"] = reference.Role,
                }).ToList(),
            };
        context[UpdatedAtKey] = snapshot.UpdatedAt;
        context[AuditStepSequenceKey] = snapshot.AuditStepSequence;
        return context;
    }

    private static Dictionary<string, object?> RemoveMetadata(Dictionary<string, object?> context)
    {
        foreach (var key in context.Keys.Where(static key => key.StartsWith(MetadataPrefix, StringComparison.Ordinal)).ToArray())
        {
            context.Remove(key);
        }

        return context;
    }

    private static void EnsureStateNode(WorkflowInstance instance, string stateId, string? description, string? transitionId)
    {
        if (!instance.Nodes.TryGetValue(stateId, out var existingState) || existingState is not StateNode stateNode)
        {
            stateNode = new StateNode
            {
                Id = stateId,
                Name = stateId,
                Description = description,
                Groups = [],
            };
            instance.Nodes[stateId] = stateNode;
        }
        else if (!string.IsNullOrWhiteSpace(description))
        {
            stateNode.Description = description;
        }

        if (string.IsNullOrWhiteSpace(transitionId))
        {
            return;
        }

        if (!instance.Nodes.ContainsKey(transitionId))
        {
            instance.Nodes[transitionId] = new ToBeRefinedTransition
            {
                Id = transitionId,
                Name = transitionId,
                TargetNodeId = stateId,
                StepKind = WorkflowStepKind.WaitResume,
                DesignNotes = description,
            };
        }

        var group = stateNode.Groups.FirstOrDefault(static item => string.Equals(item.Id, "group.ao-runtime", StringComparison.Ordinal));
        if (group is null)
        {
            group = new TransitionGroup
            {
                Id = "group.ao-runtime",
                Strategy = ConcurrencyStrategy.FirstSuccess,
                TransitionIds = [],
            };
            stateNode.Groups.Add(group);
        }

        if (!group.TransitionIds.Contains(transitionId, StringComparer.Ordinal))
        {
            group.TransitionIds.Add(transitionId);
        }
    }

    private static WorkflowStatus ToWorkflowStatus(string status)
        => string.Equals(status, "completed", StringComparison.Ordinal)
            ? WorkflowStatus.Succeeded
            : WorkflowStatus.WaitingExternal;

    private static Dictionary<string, object?> CopyContext(IReadOnlyDictionary<string, object?> source)
        => source.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal);

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IReadOnlyDictionary<string, object?> dictionary => dictionary.ToDictionary(static pair => pair.Key, static pair => CloneValue(pair.Value), StringComparer.Ordinal),
            IReadOnlyList<string> list => list.Cast<object?>().ToList(),
            List<object?> list => list.Select(CloneValue).ToList(),
            IReadOnlyList<object?> list => list.Select(CloneValue).ToList(),
            _ => value,
        };
    }

    private static string? TryGetString(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static IReadOnlyList<string>? TryGetStringList(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            IReadOnlyList<string> strings => strings.ToArray(),
            IEnumerable<object?> objects => objects.Select(static item => item?.ToString()).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray(),
            _ => null,
        };
    }

    private static int? TryGetInt(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            JsonElement { ValueKind: JsonValueKind.Number } jsonElement when jsonElement.TryGetInt32(out var jsonInt) => jsonInt,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? TryGetDateTimeOffset(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => dto,
            JsonElement { ValueKind: JsonValueKind.String } jsonElement when jsonElement.TryGetDateTimeOffset(out var parsedDto) => parsedDto,
            string text when DateTimeOffset.TryParse(text, out var parsed) => parsed,
            _ => null,
        };
    }

    private static AoWeaveOutRequest? TryGetWeaveOutRequest(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return CreateWeaveOutRequest(dictionary, TryGetEvidenceRoot(source));
        }

        if (value is IDictionary<string, object?> mutableDictionary)
        {
            return CreateWeaveOutRequest(new Dictionary<string, object?>(mutableDictionary, StringComparer.Ordinal), TryGetEvidenceRoot(source));
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            var objective = element.TryGetProperty("objective", out var objectiveProperty)
                ? objectiveProperty.GetString() ?? string.Empty
                : string.Empty;
            var artifacts = element.TryGetProperty("artifacts", out var artifactProperty)
                ? artifactProperty.EnumerateArray().Select(static item => item.GetString()).Where(static item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray()
                : [];
            var evidenceReferences = element.TryGetProperty("evidence_references", out var evidenceProperty)
                ? ParseEvidenceReferences(evidenceProperty, TryGetEvidenceRoot(source))
                : null;
            return new AoWeaveOutRequest(objective, artifacts, evidenceReferences);
        }

        return null;
    }

    private static AoWeaveOutRequest? CreateWeaveOutRequest(IReadOnlyDictionary<string, object?> source, string? evidenceRoot)
    {
        var objective = TryGetString(source, "objective") ?? string.Empty;
        var artifacts = TryGetStringList(source, "artifacts") ?? [];
        var evidenceReferences = TryGetEvidenceReferences(source, evidenceRoot);
        return new AoWeaveOutRequest(objective, artifacts, evidenceReferences);
    }

    private static IReadOnlyList<AoEvidenceReference>? TryGetEvidenceReferences(IReadOnlyDictionary<string, object?> source, string? evidenceRoot = null)
    {
        if (!source.TryGetValue("evidence_references", out var value) || value is null)
        {
            return null;
        }

        evidenceRoot ??= TryGetEvidenceRoot(source);
        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => ParseEvidenceReferences(element, evidenceRoot),
            IEnumerable<object?> items => ParseEvidenceReferenceItems(items, evidenceRoot),
            _ => null,
        };
    }

    private static IReadOnlyList<AoEvidenceReference> ParseEvidenceReferenceItems(IEnumerable<object?> items, string? evidenceRoot)
    {
        var parsed = items.Select(ParseEvidenceReference).ToArray();
        return AoEvidenceReferenceValidator.Validate(parsed, evidenceRoot);
    }

    private static string? TryGetEvidenceRoot(IReadOnlyDictionary<string, object?> source)
    {
        foreach (var key in new[] { "evidence_root", "workspace_root", "runtime_output_root" })
        {
            if (source.TryGetValue(key, out var value) && value is not null) return Convert.ToString(value);
        }

        return Directory.GetCurrentDirectory();
    }

    private static IReadOnlyList<AoEvidenceReference> ParseEvidenceReferences(JsonElement element, string? evidenceRoot)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = element.EnumerateArray().Select(static item => item.ValueKind == JsonValueKind.Object ? ParseEvidenceReference(item) : null).ToArray();
        return AoEvidenceReferenceValidator.Validate(parsed, evidenceRoot);
    }

    private static AoEvidenceReference? ParseEvidenceReference(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("path", out var pathProperty)
            || !element.TryGetProperty("start_line", out var startLineProperty)
            || !element.TryGetProperty("end_line", out var endLineProperty)
            || !element.TryGetProperty("role", out var roleProperty)
            || pathProperty.ValueKind != JsonValueKind.String
            || roleProperty.ValueKind != JsonValueKind.String
            || !startLineProperty.TryGetInt32(out var startLine)
            || !endLineProperty.TryGetInt32(out var endLine))
        {
            return null;
        }

        return CreateEvidenceReference(pathProperty.GetString(), startLine, endLine, roleProperty.GetString());
    }

    private static AoEvidenceReference? ParseEvidenceReference(object? value)
    {
        return value switch
        {
            JsonElement element => ParseEvidenceReference(element),
            IReadOnlyDictionary<string, object?> dictionary => CreateEvidenceReference(
                TryGetString(dictionary, "path"),
                TryGetInt(dictionary, "start_line") ?? 0,
                TryGetInt(dictionary, "end_line") ?? 0,
                TryGetString(dictionary, "role")),
            IDictionary<string, object?> dictionary => ParseEvidenceReference(new Dictionary<string, object?>(dictionary, StringComparer.Ordinal)),
            _ => null,
        };
    }

    private static AoEvidenceReference? CreateEvidenceReference(string? path, int startLine, int endLine, string? role)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathFullyQualified(path)
            || startLine < 1
            || endLine < startLine
            || string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return new AoEvidenceReference(path, startLine, endLine, role);
    }

    private static string NormalizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "ao-runtime";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.ToString().Trim('-');
    }
}
