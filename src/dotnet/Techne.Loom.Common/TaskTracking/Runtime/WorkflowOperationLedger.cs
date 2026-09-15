using System.Text;

using System.Text.Json;

using System.Text.Json.Serialization;

using Techne.Loom.Abstractions.TaskTracking.Runtime;



namespace Techne.Loom.Common.TaskTracking.Runtime;



public sealed record WorkflowOperationLedgerRecord(

    [property: JsonPropertyName("operation_id")] string OperationId,

    [property: JsonPropertyName("operation_kind")] string OperationKind,

    [property: JsonPropertyName("request_hash")] string RequestHash,

    [property: JsonPropertyName("status")] string Status,

    [property: JsonPropertyName("recorded_at_utc")] DateTimeOffset RecordedAtUtc,

    [property: JsonPropertyName("result_json")] string? ResultJson = null);



public sealed class WorkflowOperationInDoubtException : InvalidOperationException

{

    public WorkflowOperationInDoubtException(string operationId)

        : base($"Workflow operation '{operationId}' has an in-doubt result and must not be replayed.")

    {

    }

}



public static class WorkflowOperationLedger

{

    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: false);



    public static string GetPath(string workflowFile)

        => CanonicalWorkflowFileStore.NormalizePath(workflowFile) + ".operations.jsonl";



    public static string ComputeRequestHash(object value)
    {
        var serialized = JsonSerializer.SerializeToElement(value, JsonOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, serialized);
            writer.Flush();
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
    public static bool IsValidOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 128)
        {
            return false;
        }

        foreach (var character in operationId)
        {
            if (!((character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '.' || character == '_' || character == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = new System.Collections.Generic.List<JsonProperty>();
                foreach (var property in value.EnumerateObject())
                {
                    properties.Add(property);
                }
                properties.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
                foreach (var property in properties)
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                return;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                return;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                return;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                return;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                return;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                return;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{value.ValueKind}'.");
        }
    }
    public static void ValidateOperationId(string operationId)
    {
        if (!IsValidOperationId(operationId))
        {
            throw new ArgumentException(
                "Operation IDs must be 1-128 ASCII characters using only letters, digits, '.', '_', or '-'.",
                nameof(operationId));
        }
    }
    public static async Task<WorkflowFileExecutionResult?> BeginAsync(

        string workflowFile,

        string operationId,

        string operationKind,

        string requestHash,

        CancellationToken ct = default)

    {

        ValidateOperationId(operationId);

        ArgumentException.ThrowIfNullOrWhiteSpace(operationKind);

        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        await using var ledgerLock = await WorkflowFileLock.AcquireAsync(GetPath(workflowFile), ct).ConfigureAwait(false);

        var existing = await FindLatestAsync(workflowFile, operationId, ct).ConfigureAwait(false);

        if (existing is not null)

        {

            if (!string.Equals(existing.OperationKind, operationKind, StringComparison.Ordinal)

                || !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))

            {

                throw new InvalidOperationException($"Workflow operation '{operationId}' was already used for a different request.");

            }



            if (string.Equals(existing.Status, "started", StringComparison.Ordinal))

            {

                throw new WorkflowOperationInDoubtException(operationId);

            }



            if (!string.Equals(existing.Status, "completed", StringComparison.Ordinal)

                || string.IsNullOrWhiteSpace(existing.ResultJson))

            {

                throw new InvalidOperationException($"Workflow operation '{operationId}' has an invalid persisted state '{existing.Status}'.");

            }



            return JsonSerializer.Deserialize<WorkflowFileExecutionResult>(existing.ResultJson, JsonOptions)

                ?? throw new InvalidOperationException($"Workflow operation '{operationId}' has an empty persisted result.");

        }



        await AppendAsync(

            workflowFile,

            new WorkflowOperationLedgerRecord(operationId, operationKind, requestHash, "started", DateTimeOffset.UtcNow),

            ct).ConfigureAwait(false);

        return null;

    }



    public static async Task CompleteAsync(
        string workflowFile,
        string operationId,
        string operationKind,
        string requestHash,
        WorkflowFileExecutionResult result,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        await using var ledgerLock = await WorkflowFileLock.AcquireAsync(GetPath(workflowFile), ct).ConfigureAwait(false);
        await EnsureStartedAsync(workflowFile, operationId, operationKind, requestHash, ct).ConfigureAwait(false);
        await AppendAsync(
            workflowFile,
            new WorkflowOperationLedgerRecord(
                operationId,
                operationKind,
                requestHash,
                "completed",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(result, JsonOptions)),
            ct).ConfigureAwait(false);
    }
    public static async Task<string?> BeginRawAsync(
        string workflowFile,
        string operationId,
        string operationKind,
        string requestHash,
        CancellationToken ct = default)
    {
        ValidateOperationId(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        await using var ledgerLock = await WorkflowFileLock.AcquireAsync(GetPath(workflowFile), ct).ConfigureAwait(false);
        var existing = await FindLatestAsync(workflowFile, operationId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals(existing.OperationKind, operationKind, StringComparison.Ordinal)
                || !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Workflow operation '{operationId}' was already used for a different request.");
            }
            if (string.Equals(existing.Status, "started", StringComparison.Ordinal))
            {
                throw new WorkflowOperationInDoubtException(operationId);
            }
            if (!string.Equals(existing.Status, "completed", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(existing.ResultJson))
            {
                throw new InvalidOperationException($"Workflow operation '{operationId}' has an invalid persisted state '{existing.Status}'.");
            }
            return existing.ResultJson;
        }
        await AppendAsync(
            workflowFile,
            new WorkflowOperationLedgerRecord(operationId, operationKind, requestHash, "started", DateTimeOffset.UtcNow),
            ct).ConfigureAwait(false);
        return null;
    }

    public static async Task CompleteRawAsync(
        string workflowFile,
        string operationId,
        string operationKind,
        string requestHash,
        string resultJson,
        CancellationToken ct = default)
    {
        ValidateResultJson(resultJson);
        await using var ledgerLock = await WorkflowFileLock.AcquireAsync(GetPath(workflowFile), ct).ConfigureAwait(false);
        await EnsureStartedAsync(workflowFile, operationId, operationKind, requestHash, ct).ConfigureAwait(false);
        await AppendAsync(
            workflowFile,
            new WorkflowOperationLedgerRecord(operationId, operationKind, requestHash, "completed", DateTimeOffset.UtcNow, resultJson),
            ct).ConfigureAwait(false);
    }

    private static async Task EnsureStartedAsync(
        string workflowFile,
        string operationId,
        string operationKind,
        string requestHash,
        CancellationToken ct)
    {
        ValidateOperationId(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        var existing = await FindLatestAsync(workflowFile, operationId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            throw new InvalidOperationException($"Workflow operation '{operationId}' has no started record to complete.");
        }

        if (!string.Equals(existing.OperationKind, operationKind, StringComparison.Ordinal)
            || !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Workflow operation '{operationId}' completion does not match its started request.");
        }

        if (string.Equals(existing.Status, "completed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Workflow operation '{operationId}' is already completed.");
        }

        if (!string.Equals(existing.Status, "started", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Workflow operation '{operationId}' has an invalid persisted state '{existing.Status}'.");
        }
    }

    private static void ValidateResultJson(string resultJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The persisted workflow operation result must be a JSON object.", nameof(resultJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The persisted workflow operation result must be valid JSON.", nameof(resultJson), exception);
        }
    }
    private static async Task<WorkflowOperationLedgerRecord?> FindLatestAsync(

        string workflowFile,

        string operationId,

        CancellationToken ct)

    {

        var path = GetPath(workflowFile);

        if (!File.Exists(path)) return null;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        WorkflowOperationLedgerRecord? latest = null;

        while (true)

        {

            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

            if (line is null) break;

            if (string.IsNullOrWhiteSpace(line)) continue;

            WorkflowOperationLedgerRecord? record;

            try

            {

                record = JsonSerializer.Deserialize<WorkflowOperationLedgerRecord>(line, JsonOptions);

            }

            catch (JsonException exception)

            {

                throw new InvalidOperationException($"Workflow operation ledger '{path}' contains invalid JSON.", exception);

            }

            if (record is not null && string.Equals(record.OperationId, operationId, StringComparison.Ordinal)) latest = record;

        }

        return latest;

    }



    private static async Task AppendAsync(string workflowFile, WorkflowOperationLedgerRecord record, CancellationToken ct)

    {

        var path = GetPath(workflowFile);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);

        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync(JsonSerializer.Serialize(record, JsonOptions)).ConfigureAwait(false);

        await writer.FlushAsync(ct).ConfigureAwait(false);

    }

}