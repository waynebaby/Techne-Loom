using Techne.Loom.AgentOrchestrator.Models;
using Techne.Loom.AgentOrchestrator.Runtime;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal static class AoCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var tokens = args.ToList();

        try
        {
            if (tokens.Count == 0)
            {
                Console.Error.WriteLine(AoCommandHandlers.UsageText);
                return 1;
            }

            if (tokens[0] == "--guide")
            {
                return await AoCommandHandlers.HandleGuideAsync(tokens.Skip(1).ToList()).ConfigureAwait(false);
            }

            if (tokens.Contains("--help", StringComparer.Ordinal) || tokens.Contains("-h", StringComparer.Ordinal))
            {
                Console.WriteLine(AoCommandHandlers.UsageText);
                return 0;
            }

            if (tokens[0] == "--patch")
            {
                return await AoCommandHandlers.HandlePatchAsync(tokens.Skip(1).ToList()).ConfigureAwait(false);
            }

            if (tokens[0] == "--schema-demo-output")
            {
                return await AoCommandHandlers.HandleSchemaDemoOutputAsync(tokens).ConfigureAwait(false);
            }

            return tokens[0] switch
            {
                "compile" => await AoCommandHandlers.HandleCompileAsync(tokens.Skip(1).ToList()).ConfigureAwait(false),
                "prompt-plan" => await AoCommandHandlers.HandlePromptPlanAsync(tokens.Skip(1).ToList(), new AoPropertyWriter(Console.Out)).ConfigureAwait(false),
                "prompt-replan" => await AoCommandHandlers.HandlePromptReplanAsync(tokens.Skip(1).ToList(), new AoPropertyWriter(Console.Out)).ConfigureAwait(false),
                "run" => await AoCommandHandlers.HandleRunAsync(tokens.Skip(1).ToList(), new AoRuntimeService(), new AoPropertyWriter(Console.Out)).ConfigureAwait(false),
                "resume" => await AoCommandHandlers.HandleResumeAsync(tokens.Skip(1).ToList(), new AoRuntimeService(), new AoPropertyWriter(Console.Out)).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown command '{tokens[0]}'."), 
            };
        }
        catch (Exception ex)
        {
            var writer = new AoPropertyWriter(Console.Out);
            writer.WriteAoProperty(new AoPropertyEnvelope(
                "error",
                DateTimeOffset.UtcNow,
                BuildTopLevelErrorPayload(ex, tokens)));
            return 2;
        }
    }

    private static AoErrorPayload BuildTopLevelErrorPayload(Exception ex, IReadOnlyList<string> tokens)
    {
        var command = tokens.FirstOrDefault() ?? "unknown";
        var commandArgs = tokens.Count > 1 ? tokens.Skip(1).ToList() : [];
        var sessionId = AoCliOptions.GetOption(commandArgs, "--session-id");
        var sessionDirectory = AoCliOptions.GetOption(commandArgs, "--session-dir");
        var workflowFile = AoCliOptions.GetOption(commandArgs, "--workflow-file");
        var workflowInstanceFile = AoCliOptions.GetOption(commandArgs, "--instance-file");
        var eventLogFile = string.Empty;
        var resultFile = AoCliOptions.GetOption(commandArgs, "--result-file") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(sessionDirectory) && !string.IsNullOrWhiteSpace(sessionId))
        {
            var artifacts = AoSessionArtifactPaths.Resolve(sessionDirectory, sessionId);
            workflowFile ??= artifacts.WorkflowFile;
            workflowInstanceFile ??= artifacts.RuntimeWorkflowFile;
            eventLogFile = artifacts.EventLogFile;
        }

        workflowFile = NormalizePathOrEmpty(workflowFile);
        workflowInstanceFile = NormalizePathOrNull(workflowInstanceFile);
        eventLogFile = NormalizePathOrEmpty(eventLogFile);
        resultFile = NormalizePathOrEmpty(resultFile);

        return new AoErrorPayload(
            sessionId,
            workflowFile,
            workflowInstanceFile,
            eventLogFile,
            "failed",
            ex.Message,
            resultFile,
            BuildTopLevelMustShowToUserFiles(workflowFile, workflowInstanceFile, eventLogFile, resultFile),
            BuildTopLevelWorkflowLocationSummary(command, sessionId, workflowFile, workflowInstanceFile));
    }

    private static IReadOnlyList<string> BuildTopLevelMustShowToUserFiles(params string?[] candidates)
    {
        return candidates
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(static candidate => candidate!)
            .Where(File.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildTopLevelWorkflowLocationSummary(string command, string? sessionId, string workflowFile, string? workflowInstanceFile)
    {
        if (!string.IsNullOrWhiteSpace(workflowFile))
        {
            var sessionSummary = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : $" for session '{sessionId}'";
            var instanceSummary = string.IsNullOrWhiteSpace(workflowInstanceFile) ? string.Empty : $", runtime workflow '{workflowInstanceFile}'";
            return $"AO CLI failed during '{command}'{sessionSummary} while working from workflow '{workflowFile}'{instanceSummary}.";
        }

        return "AO CLI failed before a workflow render context was available.";
    }

    private static string NormalizePathOrEmpty(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);

    private static string? NormalizePathOrNull(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
}
