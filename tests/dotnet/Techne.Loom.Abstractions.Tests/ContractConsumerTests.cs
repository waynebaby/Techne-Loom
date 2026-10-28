using System.Reflection;
using System.Text.Json;
using Techne.Loom.Abstractions.Ask;
using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Abstractions.Tests;

public sealed class ContractConsumerTests
{
    [Fact]
    public void ContractsHaveNoRuntimeAssemblyDependencies()
    {
        var assembly = typeof(UserInputContract).Assembly;
        Assert.All(assembly.GetReferencedAssemblies(), reference =>
            Assert.StartsWith("System.", reference.Name));
        Assert.All(new[]
        {
            typeof(AskScopedStandaloneResult), typeof(AskScopedSubmissionReceipt),
            typeof(AskScopedAnswerValue), typeof(AskScopedWorkerEndpoint),
            typeof(IStandaloneAskSessionService), typeof(WorkflowFileExecutionResult),
            typeof(WorkflowFragmentResult), typeof(WorkflowEventFragmentResult),
            typeof(WorkflowArtifactManifestResult), typeof(ContextFieldWeaveOutField),
            typeof(WorkflowSchemaDemoExportResult), typeof(ISystemClock),
        }, type => Assert.Same(assembly, type.Assembly));
    }

    [Fact]
    public void StandaloneReceiptPreservesFalseAndOmitsWorkflowIdentity()
    {
        var answer = new AskScopedNormalizedAnswer(
            "confirm", null, JsonSerializer.SerializeToElement(false), false, []);
        var receipt = new AskScopedSubmissionReceipt(
            1, "ask-test", null, null, 0, 1, "submit", "request-hash",
            DateTimeOffset.UnixEpoch, [answer], "integrity-hash", AskScopedConsumerKind.Standalone);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(receipt, options);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("workflowInstanceId", out _));
        Assert.False(document.RootElement.TryGetProperty("transitionId", out _));
        Assert.False(document.RootElement.GetProperty("answers")[0].GetProperty("value").GetBoolean());
        Assert.False(document.RootElement.GetProperty("answers")[0].TryGetProperty("contextPath", out _));
        var restored = JsonSerializer.Deserialize<AskScopedSubmissionReceipt>(json, options)!;
        Assert.Equal(AskScopedConsumerKind.Standalone, restored.ConsumerKind);
        Assert.False(Assert.Single(restored.Answers).Value!.Value.GetBoolean());
    }

    [Fact]
    public void WorkflowReceiptKeepsDefaultConsumerOmission()
    {
        var receipt = new AskScopedSubmissionReceipt(
            1, "ask-test", "workflow", "transition", 0, 1, "submit",
            "request-hash", DateTimeOffset.UnixEpoch, [], "integrity-hash");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            receipt, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.False(document.RootElement.TryGetProperty("consumerKind", out _));
        Assert.Equal("workflow", document.RootElement.GetProperty("workflowInstanceId").GetString());
    }

    [Fact]
    public async Task ExternalConsumerCanImplementAskInterfaceWithoutCommon()
    {
        IStandaloneAskSessionService service = new ConsumerAskAdapter();
        var endpoint = await service.StartFromContractFileAsync("questions.json");
        var result = await service.GetResultAsync(endpoint.AskId);
        Assert.Equal(endpoint.AskId, result.AskId);
        Assert.Equal(AskScopedConsumerKind.Standalone, result.ConsumerKind);
    }

    [Fact]
    public void AuditContractRetainsItsDirectSerializationConverter()
    {
        var file = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "contract space", "diagram.md"));
        var audit = new WorkflowAuditArtifacts("output", "workflow", 1, "compile", "step", file, null, "backup");
        var json = JsonSerializer.Serialize(audit);
        using var document = JsonDocument.Parse(json);
        var url = document.RootElement.GetProperty("mermaid_file").GetString()!;
        Assert.StartsWith("file:///", url);
        Assert.Contains("contract%20space", url);
        Assert.Equal(file, JsonSerializer.Deserialize<WorkflowAuditArtifacts>(json)!.MermaidFile);
    }

    [Fact]
    public void AgentOrchestratorContractsAreOwnedByAbstractions()
    {
        var assembly = typeof(AoControlPayload).Assembly;
        Assert.Same(typeof(UserInputContract).Assembly, assembly);
        Assert.All(new[]
        {
            typeof(AoControlPayload), typeof(AoErrorPayload), typeof(AoEventRecord),
            typeof(AoEvidenceReference), typeof(AoPromptBlock),
            typeof(AoPromptBlockConsumptionRequirement), typeof(AoPromptPayload),
            typeof(AoPropertyEnvelope), typeof(AoResumeEnvelope),
            typeof(AoWeaveOutRequest), typeof(AoWorkflowFilePayload),
            typeof(AoWorkflowSnapshot), typeof(AoProgressPayload),
        }, type => Assert.Same(assembly, type.Assembly));
    }

    [Fact]
    public void AoProgressPayloadPreservesItsSnakeCaseWireNames()
    {
        var payload = new AoProgressPayload(
            "running", "session", "workflow.json", "instance.json", "events.json",
            "node", null, null, [], null, null);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        Assert.Equal("instance.json", document.RootElement.GetProperty("workflow_instance_file").GetString());
        Assert.True(document.RootElement.TryGetProperty("must_show_to_user_files", out _));
    }

    private sealed class ConsumerAskAdapter : IStandaloneAskSessionService
    {
        public Task<AskScopedWorkerEndpoint> StartFromContractFileAsync(string contractFile, CancellationToken ct = default)
            => Task.FromResult(new AskScopedWorkerEndpoint("consumer-ask", "http://127.0.0.1/", DateTimeOffset.UnixEpoch));

        public Task<AskScopedStandaloneResult> GetResultAsync(string askId, CancellationToken ct = default)
            => Task.FromResult(new AskScopedStandaloneResult(
                askId, AskScopedConsumerKind.Standalone, false, 0, DateTimeOffset.UnixEpoch, [], null));
    }
}
