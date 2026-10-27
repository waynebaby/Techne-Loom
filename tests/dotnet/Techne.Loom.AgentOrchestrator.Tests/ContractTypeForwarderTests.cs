using System.Reflection;
using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Tests;

public sealed class AgentOrchestratorContractForwarderTests
{
    [Fact]
    public void ExistingAoModelTypesForwardToAbstractions()
    {
        var product = Assembly.Load("ao");
        var abstractions = typeof(AoControlPayload).Assembly;
        var forwarded = product.GetForwardedTypes();
        var contracts = new[]
        {
            typeof(AoControlPayload),
            typeof(AoErrorPayload),
            typeof(AoEventRecord),
            typeof(AoEvidenceReference),
            typeof(AoPromptBlock),
            typeof(AoPromptBlockConsumptionRequirement),
            typeof(AoPromptPayload),
            typeof(AoPropertyEnvelope),
            typeof(AoResumeEnvelope),
            typeof(AoWeaveOutRequest),
            typeof(AoWorkflowFilePayload),
            typeof(AoWorkflowSnapshot),
            typeof(AoProgressPayload),
        };

        Assert.All(contracts, type => Assert.Same(abstractions, type.Assembly));
        Assert.All(contracts, type => Assert.Contains(type, forwarded));
    }
}
