namespace Techne.Loom.AgentOrchestrator.Runtime;

internal sealed record AoSessionArtifacts(
    string SessionId,
    string SessionDirectory,
    string WorkflowFile,
    string EventLogFile);

internal static class AoSessionArtifactPaths
{
    public static AoSessionArtifacts CreateNew(string sessionDirectory)
    {
        var normalizedDirectory = NormalizeSessionDirectory(sessionDirectory);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var sessionId = CreateSessionId();
            var artifacts = Resolve(normalizedDirectory, sessionId);
            if (!File.Exists(artifacts.WorkflowFile) && !File.Exists(artifacts.EventLogFile))
            {
                return artifacts;
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique AO session identifier.");
    }

    public static AoSessionArtifacts ResolveExisting(string sessionDirectory, string sessionId)
    {
        var artifacts = Resolve(sessionDirectory, sessionId);
        if (!File.Exists(artifacts.WorkflowFile))
        {
            throw new InvalidOperationException(
                $"AO session '{artifacts.SessionId}' was not found in '{artifacts.SessionDirectory}'.");
        }

        if (!File.Exists(artifacts.EventLogFile))
        {
            throw new InvalidOperationException(
                $"AO session '{artifacts.SessionId}' is missing its event log file '{artifacts.EventLogFile}'.");
        }

        return artifacts;
    }

    public static AoSessionArtifacts Resolve(string sessionDirectory, string sessionId)
    {
        var normalizedDirectory = NormalizeSessionDirectory(sessionDirectory);
        var normalizedSessionId = NormalizeSessionId(sessionId);

        return new AoSessionArtifacts(
            normalizedSessionId,
            normalizedDirectory,
            Path.Combine(normalizedDirectory, $"session_{normalizedSessionId}_workflow.json"),
            Path.Combine(normalizedDirectory, $"session_{normalizedSessionId}_events.jsonl"));
    }

    private static string CreateSessionId()
        => $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

    private static string NormalizeSessionDirectory(string sessionDirectory)
    {
        if (string.IsNullOrWhiteSpace(sessionDirectory))
        {
            throw new InvalidOperationException("A non-empty session directory is required.");
        }

        var normalizedDirectory = Path.GetFullPath(sessionDirectory);
        Directory.CreateDirectory(normalizedDirectory);
        return normalizedDirectory;
    }

    private static string NormalizeSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("A non-empty session_id is required.");
        }

        foreach (var character in sessionId)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                continue;
            }

            throw new InvalidOperationException(
                "Invalid session_id. Only letters, digits, '-' and '_' are allowed.");
        }

        return sessionId;
    }
}
