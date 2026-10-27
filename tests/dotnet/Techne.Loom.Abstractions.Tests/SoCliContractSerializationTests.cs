using System.Text.Json;
using Techne.Loom.SkillOrchestrator.Models;

namespace Techne.Loom.Abstractions.Tests;

public sealed class SoCliContractSerializationTests
{
    [Fact]
    public void PropertyEnvelopeKeepsWebWireNames()
    {
        var envelope = new SoPropertyEnvelope("result", DateTimeOffset.UnixEpoch, new { action = "status" });
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal("result", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(DateTimeOffset.UnixEpoch, document.RootElement.GetProperty("timestampUtc").GetDateTimeOffset());
        Assert.Equal("status", document.RootElement.GetProperty("payload").GetProperty("action").GetString());
    }

    [Fact]
    public void StatusPayloadPreservesSnakeCaseAndExplicitFalseValues()
    {
        var payload = new SkillStatusPayload(
            "workflow.json", "instance-1", "running", null, null, "events.json", [], "at node",
            CanResume: false, FreshInstanceRequired: false);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal("workflow.json", document.RootElement.GetProperty("workflow_file").GetString());
        Assert.False(document.RootElement.GetProperty("can_resume").GetBoolean());
        Assert.False(document.RootElement.GetProperty("fresh_instance_required").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("workflowFile", out _));
    }
}
