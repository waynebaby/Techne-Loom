using System.Reflection;
using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Cli;
using Techne.Loom.Common.Documentation;
using Techne.Loom.Common.TaskTracking.Runtime;

if (args.Length == 1 && string.Equals(args[0], "--ask-user-worker", StringComparison.Ordinal))
{
    return await AskScopedWorkerCommand.RunFromStandardInputAsync().ConfigureAwait(false);
}

return await LoomCli.RunAsync(args).ConfigureAwait(false);

public static class LoomCli
{
    private const string UsageText = """
        Usage: loom --guide | --help | ask <start|result|wait> [options] | progress append [options]
               loom agent <Agent Orchestrator command> [options]
               loom skill <Skill Orchestrator command> [options]

        Ask is the single workflow-independent question entry point. Agent and skill
        workflow commands are available under their respective subcommands.
        """;

    private const string AskUsageText = """
        Usage: loom ask <start|result|wait> [options]
          start --contract-file <path>
          result --ask-id <id>
          wait --ask-id <id> [--wait-timeout-seconds <seconds>]
        """;

    private const string ProgressUsageText = """
        Usage: loom progress append --entry-file <path> --output-root <path>
        Appends a validated UTF-8 entry to <output-root>/progress-report.md.
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            Console.WriteLine(UsageText);
            return 0;
        }

        if (string.Equals(args[0], "--guide", StringComparison.Ordinal))
        {
            return await HandleGuideAsync(args.Skip(1).ToArray()).ConfigureAwait(false);
        }

        if (string.Equals(args[0], "ask", StringComparison.Ordinal))
        {
            return await HandleAskAsync(args.Skip(1).ToArray()).ConfigureAwait(false);
        }

        if (string.Equals(args[0], "progress", StringComparison.Ordinal))
        {
            return await HandleProgressAsync(args.Skip(1).ToArray()).ConfigureAwait(false);
        }

        if (string.Equals(args[0], "agent", StringComparison.Ordinal))
        {
            return await RunModuleAsync("agent", args.Skip(1).ToArray(), AoCli.RunAsync).ConfigureAwait(false);
        }

        if (string.Equals(args[0], "skill", StringComparison.Ordinal))
        {
            return await RunModuleAsync("skill", args.Skip(1).ToArray(), SkillCli.RunAsync).ConfigureAwait(false);
        }

        await Console.Error.WriteLineAsync($"Unknown command '{args[0]}'.{Environment.NewLine}{UsageText}").ConfigureAwait(false);
        return 2;
    }

    private static async Task<int> HandleAskAsync(string[] args)
    {
        if (args.Length == 0
            || args[0] == "help"
            || args.Any(argument => argument is "--help" or "-h"))
        {
            Console.WriteLine(AskUsageText);
            return 0;
        }

        if (args[0] is not ("start" or "result" or "wait"))
        {
            await Console.Error.WriteLineAsync($"Unknown ask command '{args[0]}'.{Environment.NewLine}{AskUsageText}").ConfigureAwait(false);
            return 2;
        }

        return await AoCli.RunAsync(["ask", .. args]).ConfigureAwait(false);
    }

    private static Task<int> HandleProgressAsync(string[] args)
    {
        if (args.Length == 0
            || args[0] == "help"
            || args.Any(argument => argument is "--help" or "-h"))
        {
            Console.WriteLine(ProgressUsageText);
            return Task.FromResult(0);
        }

        if (args[0] != "append")
        {
            Console.Error.WriteLine($"Unknown progress command '{args[0]}'.{Environment.NewLine}{ProgressUsageText}");
            return Task.FromResult(2);
        }

        return Task.FromResult(RunProgressAppend(args.Skip(1).ToArray()));
    }

    private static int RunProgressAppend(string[] args)
    {
        string? entryArgument = null;
        string? outputRootArgument = null;
        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            if (option is not ("--entry-file" or "--output-root"))
            {
                return ProgressError($"Unknown option '{option}'.", 2);
            }
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return ProgressError($"Missing value for '{option}'.", 2);
            }

            var value = args[++index];
            if (option == "--entry-file")
            {
                if (entryArgument is not null)
                {
                    return ProgressError("Duplicate option '--entry-file'.", 2);
                }
                entryArgument = value;
            }
            else
            {
                if (outputRootArgument is not null)
                {
                    return ProgressError("Duplicate option '--output-root'.", 2);
                }
                outputRootArgument = value;
            }
        }

        if (entryArgument is null || outputRootArgument is null)
        {
            return ProgressError("Both '--entry-file' and '--output-root' are required.", 2);
        }

        try
        {
            var outputRoot = Path.GetFullPath(outputRootArgument);
            if (!Directory.Exists(outputRoot))
            {
                return ProgressError("Output root must be an existing directory.");
            }
            var outputAttributes = File.GetAttributes(outputRoot);
            if ((outputAttributes & FileAttributes.Directory) == 0 || (outputAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return ProgressError("Output root must be a regular directory, not a symbolic link or reparse point.");
            }

            var entryPath = Path.GetFullPath(entryArgument);
            var targetPath = Path.Combine(outputRoot, "progress-report.md");
            var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            if (pathComparer.Equals(entryPath, targetPath))
            {
                return ProgressError("Entry file and progress report target must be different.");
            }

            var entryAttributes = File.GetAttributes(entryPath);
            if ((entryAttributes & FileAttributes.Directory) != 0)
            {
                return ProgressError("Entry file must be a regular file.");
            }
            if ((entryAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return ProgressError("Entry file cannot be a symbolic link or reparse point.");
            }

            var entryBytes = File.ReadAllBytes(entryPath);
            if (entryBytes.Length == 0)
            {
                return ProgressError("Entry file is empty.");
            }
            _ = new System.Text.UTF8Encoding(false, true).GetString(entryBytes);
            if (entryBytes[^1] != 0x0a)
            {
                return ProgressError("Entry file must end with a line feed.");
            }

            var targetInfo = new FileInfo(targetPath);
            if (targetInfo.LinkTarget is not null)
            {
                return ProgressError("Target cannot be a symbolic link or reparse point.");
            }
            if (Directory.Exists(targetPath))
            {
                return ProgressError("Target must be a regular file.");
            }
            if (File.Exists(targetPath) && (File.GetAttributes(targetPath) & FileAttributes.ReparsePoint) != 0)
            {
                return ProgressError("Target cannot be a symbolic link or reparse point.");
            }

            var appendBytes = new byte[entryBytes.Length + 1];
            appendBytes[0] = 0x0a;
            Buffer.BlockCopy(entryBytes, 0, appendBytes, 1, entryBytes.Length);
            using (var stream = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                stream.Write(appendBytes);
                stream.Flush(flushToDisk: true);
            }

            Console.WriteLine(new Uri(targetPath).AbsoluteUri);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return ProgressError(exception.Message);
        }
    }

    private static int ProgressError(string message, int exitCode = 1)
    {
        Console.Error.WriteLine($"loom progress append: {message}");
        return exitCode;
    }

    private static async Task<int> RunModuleAsync(
        string module,
        string[] args,
        Func<string[], Task<int>> runModule)
    {
        if (args.Length > 0 && string.Equals(args[0], "ask", StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync("Standalone questions are available only through the top-level 'loom ask' command.").ConfigureAwait(false);
            return 2;
        }

        if (args.Length > 0 && string.Equals(args[0], "--guide", StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync("Use the unified top-level 'loom --guide' command.").ConfigureAwait(false);
            return 2;
        }

        if (args.Length == 0 || args.Any(static argument => argument is "--help" or "-h"))
        {
            var originalOutput = Console.Out;
            using var capturedOutput = new StringWriter();
            try
            {
                var moduleArguments = args.Length == 0 ? new[] { "--help" } : args;
                Console.SetOut(capturedOutput);
                await runModule(moduleArguments).ConfigureAwait(false);
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            var oldUsage = module == "agent" ? "Usage: ao[.exe]" : "Usage: so[.exe]";
            var newUsage = $"Usage: loom {module}";
            var output = capturedOutput.ToString()
                .Replace(oldUsage, newUsage, StringComparison.Ordinal)
                .Replace($"{newUsage} --guide | --help", $"{newUsage} --help", StringComparison.Ordinal)
                .Replace(" | ask start --contract-file <path> | ask result --ask-id <id> | ask wait --ask-id <id> [--wait-timeout-seconds <seconds>] | ", " | ", StringComparison.Ordinal)
                .Replace(" | ask start --contract-file <path> | ask result --ask-id <id> | ", " | ", StringComparison.Ordinal);
            var outputLines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("Standalone workflow-independent ask:", StringComparison.Ordinal));
            await Console.Out.WriteLineAsync(string.Join(Environment.NewLine, outputLines)).ConfigureAwait(false);
            return 0;
        }

        return await runModule(args).ConfigureAwait(false);
    }

    private static async Task<int> HandleGuideAsync(IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            await Console.Error.WriteLineAsync("The --guide command accepts no additional arguments.").ConfigureAwait(false);
            return 2;
        }

        var result = await DocumentationBundleInstaller.InstallAsync(
            Assembly.GetExecutingAssembly(),
            "guides/loom-guide.md").ConfigureAwait(false);
        foreach (var warning in result.Warnings)
        {
            await Console.Error.WriteLineAsync($"Warning: {warning}").ConfigureAwait(false);
        }

        Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = result.Version,
            ["docs_root"] = result.DocsRoot,
            ["guide_path"] = result.GuidePath,
            ["contract_docs_root"] = result.ContractsRoot,
            ["contract_index_path"] = result.ContractsIndexPath,
            ["builder_template_path"] = result.BuilderTemplatePath,
        }));
        return 0;
    }
}
