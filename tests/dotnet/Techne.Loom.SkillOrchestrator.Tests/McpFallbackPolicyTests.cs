using Techne.Loom.Common.Mcp;



namespace Techne.Loom.SkillOrchestrator.Tests;



public sealed class McpFallbackPolicyTests

{

    [Fact]

    public void CanFallback_OnlyAllowsApprovedReasonsBeforeDispatch()

    {

        Assert.All(McpCliFallbackPolicy.AllowedReasons, reason =>

            Assert.True(McpCliFallbackPolicy.CanFallback(McpDispatchStage.PreDispatch, reason)));

        Assert.False(McpCliFallbackPolicy.CanFallback(McpDispatchStage.PreDispatch, "mcp_application_failed"));

        Assert.False(McpCliFallbackPolicy.CanFallback(McpDispatchStage.Dispatched, "mcp_transport_unavailable"));

        Assert.False(McpCliFallbackPolicy.CanFallback(McpDispatchStage.Unknown, "mcp_tool_unavailable"));

    }

}