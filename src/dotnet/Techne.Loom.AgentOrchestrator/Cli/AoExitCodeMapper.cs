namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoExitCodeMapper
{
    public static int Map(string status)
    {
        return status switch
        {
            "completed" => 0,
            "failed" => 2,
            _ => 3,
        };
    }
}
