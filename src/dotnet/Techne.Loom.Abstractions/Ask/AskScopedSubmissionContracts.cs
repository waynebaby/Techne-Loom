using System.Text.Json;
using System.Text.Json.Serialization;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record AskScopedResumeRequest(
    AskScopedLaunch Launch,
    string WorkflowInstanceId,
    string WaitId,
    string TransitionId,
    string? CorrelationKey,
    string OperationId,
    long ReceiptGeneration,
    Dictionary<string, object?> Payload);

public sealed record AskScopedOfflineSubmission(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("askId")] string AskId,
    [property: JsonPropertyName("expectedGeneration")] long ExpectedGeneration,
    [property: JsonPropertyName("operationId")] string OperationId,
    [property: JsonPropertyName("answers")] Dictionary<string, AskScopedAnswerValue> Answers);
