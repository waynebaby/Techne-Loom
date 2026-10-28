using Techne.Loom.Abstractions.Ask;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Tests;

public sealed class ContractAssemblyBoundaryTests
{
    [Fact]
    public void StandaloneImplementationImplementsContractInterface()
        => Assert.True(typeof(IStandaloneAskSessionService).IsAssignableFrom(typeof(AskScopedStandaloneSessionService)));

    [Fact]
    public void CommonForwardsMovedContractsWithoutDuplicatingTypes()
    {
        var common = typeof(AskScopedStandaloneSessionService).Assembly;
        var contracts = typeof(UserInputContract).Assembly;
        var forwarded = common.GetForwardedTypes();
        Assert.Contains(typeof(AskScopedSubmissionReceipt), forwarded);
        Assert.Contains(typeof(WorkflowAuditArtifacts), forwarded);
        Assert.All(forwarded, type => Assert.Same(contracts, type.Assembly));
        Assert.All(forwarded, type => Assert.Same(type,
            Type.GetType($"{type.FullName}, {common.GetName().Name}", throwOnError: true)));
        Assert.DoesNotContain(common.GetExportedTypes(), type => type == typeof(AskScopedAnswerValue));
    }
}
