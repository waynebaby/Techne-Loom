using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record WorkflowFragmentLimits(
    int MaxBytes = 32_768,
    int MaxArrayItems = 32,
    int MaxDepth = 6)
{
    public int MaxObjectProperties { get; init; } = MaxArrayItems;

    public static WorkflowFragmentLimits Default { get; } = new();

    public void Validate()
    {
        if (MaxBytes < 128)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBytes), "MaxBytes must be at least 128.");
        }

        if (MaxArrayItems < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxArrayItems), "MaxArrayItems must be positive.");
        }

        if (MaxObjectProperties < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxObjectProperties), "MaxObjectProperties must be positive.");
        }

        if (MaxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDepth), "MaxDepth must be positive.");
        }
    }
}

public sealed record WorkflowFragmentTransitionSummary(
    string Id,
    string? Name,
    string? StepKind,
    string? TargetNodeId);

public sealed record WorkflowFragmentSummary(
    string InstanceId,
    string? TemplateKind,
    string? TaskType,
    string? WorkflowKind,
    string? CaseId,
    string? RunId,
    string? Status,
    string? StartNodeId,
    string? CurrentNodeId,
    string? EndNodeId,
    int Version,
    int NodeCount,
    int TransitionCount,
    int HistoryCount,
    int ActiveWaitGroupCount,
    DateTimeOffset? LastActivityUtc,
    IReadOnlyList<string> ContextKeys,
    bool ContextKeysTruncated,
    IReadOnlyList<WorkflowFragmentTransitionSummary> CurrentTransitions);

public sealed record WorkflowFragmentResult(
    WorkflowFragmentSummary Summary,
    string? JsonPointer,
    JsonElement? Fragment,
    int ReturnedBytes,
    bool Truncated,
    string? TruncationReason);

public static class WorkflowFragmentReader
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false,
    };

    public static async Task<WorkflowFragmentResult> ReadAsync(
        string workflowFile,
        string? jsonPointer = null,
        WorkflowFragmentLimits? limits = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowFile))
        {
            throw new ArgumentException("A workflow file is required.", nameof(workflowFile));
        }

        var normalizedWorkflowFile = Path.GetFullPath(workflowFile);
        if (!File.Exists(normalizedWorkflowFile))
        {
            throw new FileNotFoundException("The workflow file was not found.", normalizedWorkflowFile);
        }

        var effectiveLimits = limits ?? WorkflowFragmentLimits.Default;
        effectiveLimits.Validate();

        await using var workflowLock = await WorkflowFileLock.AcquireAsync(normalizedWorkflowFile, ct).ConfigureAwait(false);
        await using var stream = new FileStream(
            normalizedWorkflowFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var summary = BuildSummary(document.RootElement, effectiveLimits);
        if (jsonPointer is null)
        {
            return new WorkflowFragmentResult(summary, null, null, 0, false, null);
        }

        var target = ResolveJsonPointer(document.RootElement, jsonPointer);
        var truncated = false;
        string? truncationReason = null;
        var projected = Project(target, depth: 0, effectiveLimits, ref truncated, ref truncationReason);
        var serialized = projected is null ? "null" : projected.ToJsonString(CompactJsonOptions);
        var returnedBytes = Encoding.UTF8.GetByteCount(serialized);
        if (returnedBytes > effectiveLimits.MaxBytes)
        {
            return new WorkflowFragmentResult(summary, jsonPointer, null, 0, true, "max_bytes");
        }

        using var fragmentDocument = JsonDocument.Parse(serialized);
        return new WorkflowFragmentResult(
            summary,
            jsonPointer,
            fragmentDocument.RootElement.Clone(),
            returnedBytes,
            truncated,
            truncationReason);
    }

    private static WorkflowFragmentSummary BuildSummary(
        JsonElement root,
        WorkflowFragmentLimits limits)
    {
        var nodeCount = 0;
        var transitionCount = 0;
        var currentTransitions = new List<WorkflowFragmentTransitionSummary>();
        var currentNodeId = ReadString(root, "currentNodeId", "current_node_id");
        if (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Object)
        {
            foreach (var node in nodes.EnumerateObject())
            {
                nodeCount++;
                var kind = ReadString(node.Value, "$kind");
                if (!string.Equals(kind, "state", StringComparison.Ordinal))
                {
                    transitionCount++;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentNodeId)
                && nodes.TryGetProperty(currentNodeId, out var currentNode)
                && currentNode.ValueKind == JsonValueKind.Object
                && currentNode.TryGetProperty("groups", out var groups)
                && groups.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in groups.EnumerateArray())
                {
                    if (currentTransitions.Count == limits.MaxArrayItems)
                    {
                        break;
                    }

                    if (!group.TryGetProperty("transitionIds", out var transitionIds) || transitionIds.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var transitionIdElement in transitionIds.EnumerateArray().Take(limits.MaxArrayItems))
                    {
                        if (currentTransitions.Count == limits.MaxArrayItems)
                        {
                            break;
                        }

                        if (transitionIdElement.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var transitionId = transitionIdElement.GetString();
                        if (string.IsNullOrWhiteSpace(transitionId)
                            || !nodes.TryGetProperty(transitionId, out var transition)
                            || transition.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        currentTransitions.Add(new WorkflowFragmentTransitionSummary(
                            transitionId,
                            ReadString(transition, "name"),
                            ReadString(transition, "stepKind"),
                            ReadString(transition, "targetNodeId")));
                    }
                }
            }
        }

        var contextKeys = new List<string>();
        var contextKeysTruncated = false;
        if (root.TryGetProperty("context", out var context) && context.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in context.EnumerateObject())
            {
                if (contextKeys.Count == limits.MaxArrayItems)
                {
                    contextKeysTruncated = true;
                    break;
                }

                contextKeys.Add(property.Name);
            }
        }

        return new WorkflowFragmentSummary(
            ReadString(root, "instanceId", "instance_id") ?? string.Empty,
            ReadString(root, "templateKind"),
            ReadString(root, "taskType"),
            ReadString(root, "workflowKind"),
            ReadString(root, "caseId"),
            ReadString(root, "runId"),
            ReadString(root, "status"),
            ReadString(root, "startNodeId", "start_node_id"),
            currentNodeId,
            ReadString(root, "endNodeId", "end_node_id"),
            ReadInt(root, "version", "audit_step_sequence"),
            nodeCount,
            transitionCount,
            ReadArrayLength(root, "history"),
            ReadArrayLength(root, "activeWaitGroups"),
            ReadDateTimeOffset(root, "lastActivityUtc", "updated_at"),
            contextKeys,
            contextKeysTruncated,
            currentTransitions);
    }

    private static JsonNode? Project(
        JsonElement element,
        int depth,
        WorkflowFragmentLimits limits,
        ref bool truncated,
        ref string? truncationReason)
    {
        if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array && depth >= limits.MaxDepth)
        {
            truncated = true;
            truncationReason ??= "max_depth";
            return new JsonObject
            {
                ["$truncated"] = true,
                ["reason"] = "max_depth",
            };
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var result = new JsonObject();
                var count = 0;
                foreach (var property in element.EnumerateObject())
                {
                    if (count == limits.MaxObjectProperties)
                    {
                        truncated = true;
                        truncationReason ??= "max_properties";
                        break;
                    }

                    result[property.Name] = Project(property.Value, depth + 1, limits, ref truncated, ref truncationReason);
                    count++;
                }

                return result;
            }
            case JsonValueKind.Array:
            {
                var result = new JsonArray();
                var count = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (count == limits.MaxArrayItems)
                    {
                        truncated = true;
                        truncationReason ??= "max_array_items";
                        break;
                    }

                    result.Add(Project(item, depth + 1, limits, ref truncated, ref truncationReason));
                    count++;
                }

                return result;
            }
            case JsonValueKind.String:
                return JsonValue.Create(element.GetString());
            case JsonValueKind.Number:
                return JsonNode.Parse(element.GetRawText());
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Null:
                return null;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static JsonElement ResolveJsonPointer(JsonElement root, string jsonPointer)
    {
        if (jsonPointer.Length == 0)
        {
            return root;
        }

        if (!jsonPointer.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("A JSON Pointer must be empty or start with '/'.", nameof(jsonPointer));
        }

        var current = root;
        foreach (var rawSegment in jsonPointer[1..].Split('/', StringSplitOptions.None))
        {
            var segment = DecodePointerSegment(rawSegment, jsonPointer);
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    throw new InvalidOperationException($"JSON Pointer segment '{segment}' was not found in '{DescribePointer(jsonPointer)}'.");
                }

                continue;
            }

            if (current.ValueKind == JsonValueKind.Array)
            {
                if (segment == "-")
                {
                    throw new InvalidOperationException("JSON Pointer '-' is not valid for reading an array item.");
                }

                if ((segment.Length > 1 && segment[0] == '0')
                    || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    || index < 0
                    || index >= current.GetArrayLength())
                {
                    throw new InvalidOperationException($"JSON Pointer array index '{segment}' is invalid in '{DescribePointer(jsonPointer)}'.");
                }

                current = current[index];
                continue;
            }

            throw new InvalidOperationException($"JSON Pointer cannot descend through '{current.ValueKind}' at '{DescribePointer(jsonPointer)}'.");
        }

        return current;
    }

    private static string DescribePointer(string jsonPointer)
        => jsonPointer.Length <= 160 ? jsonPointer : jsonPointer[..157] + "...";

    private static string DecodePointerSegment(string segment, string jsonPointer)
    {
        var builder = new StringBuilder(segment.Length);
        for (var index = 0; index < segment.Length; index++)
        {
            if (segment[index] != '~')
            {
                builder.Append(segment[index]);
                continue;
            }

            if (index + 1 >= segment.Length || segment[index + 1] is not ('0' or '1'))
            {
                throw new ArgumentException($"JSON Pointer contains an invalid escape in '{DescribePointer(jsonPointer)}'.", nameof(jsonPointer));
            }

            builder.Append(segment[++index] == '0' ? '~' : '/');
        }

        return builder.ToString();
    }

    private static string? ReadString(JsonElement element, string propertyName, string? alternatePropertyName = null)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return alternatePropertyName is not null
            && element.TryGetProperty(alternatePropertyName, out var alternateValue)
            && alternateValue.ValueKind == JsonValueKind.String
                ? alternateValue.GetString()
                : null;
    }

    private static int ReadInt(JsonElement element, string propertyName, string? alternatePropertyName = null)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
        {
            return result;
        }

        return alternatePropertyName is not null
            && element.TryGetProperty(alternatePropertyName, out var alternateValue)
            && alternateValue.ValueKind == JsonValueKind.Number
            && alternateValue.TryGetInt32(out var alternateResult)
                ? alternateResult
                : 0;
    }

    private static int ReadArrayLength(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName, string? alternatePropertyName = null)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.TryGetDateTimeOffset(out var result))
        {
            return result;
        }

        return alternatePropertyName is not null
            && element.TryGetProperty(alternatePropertyName, out var alternateValue)
            && alternateValue.ValueKind == JsonValueKind.String
            && alternateValue.TryGetDateTimeOffset(out var alternateResult)
                ? alternateResult
                : null;
    }
}