using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;
using Techne.Loom.SkillOrchestrator.Validation;

internal static class SoPhaseCommandHandlers
{
    private static readonly JsonSerializerOptions JsonOptions = WorkflowJsonSerializer.CreateDefaultOptions(indented: true);

    public static async Task<int> HandleSkeletonCheckAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        var skeletonFile = GetRequiredOption(args, "--skeleton-file");
        var files = CliFileInputGuard.RequireExistingFiles(
            ("--workflow-file", workflowFile),
            ("--skeleton-file", skeletonFile));
        var (_, catalog) = await ReadWorkflowAndCatalogAsync(files["--workflow-file"]).ConfigureAwait(false);
        var skeleton = await ReadSkeletonAsync(files["--skeleton-file"]).ConfigureAwait(false);
        var result = WorkflowPhaseSkeletonValidator.Validate(skeleton, catalog, files["--skeleton-file"]);
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return result.IsValid ? 0 : 1;
    }

    public static async Task<int> HandlePhaseCompileAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        var skeletonFile = GetRequiredOption(args, "--skeleton-file");
        var fragmentFile = GetRequiredOption(args, "--fragment-file");
        var phaseId = GetRequiredOption(args, "--phase-id");
        var outputFile = GetRequiredOption(args, "--output-file");
        var files = CliFileInputGuard.RequireExistingFiles(
            ("--workflow-file", workflowFile),
            ("--skeleton-file", skeletonFile),
            ("--fragment-file", fragmentFile));
        var fullOutputFile = GetNewOutputFilePath(outputFile);
        var (template, catalog) = await ReadWorkflowAndCatalogAsync(files["--workflow-file"]).ConfigureAwait(false);
        var skeleton = await ReadSkeletonAsync(files["--skeleton-file"]).ConfigureAwait(false);
        var fragment = WorkflowPhaseFragment.Parse(
            await File.ReadAllTextAsync(files["--fragment-file"]).ConfigureAwait(false),
            files["--fragment-file"]);
        var result = WorkflowPhaseCompiler.Compile(
            skeleton,
            phaseId,
            fragment,
            template.ExpressionBinding,
            catalog,
            template.Validation,
            files["--fragment-file"]);
        await WriteNewFileAsync(fullOutputFile, result.ToJson()).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = result.Status,
            phase_id = result.PhaseId,
            static_only = result.StaticOnly,
            result_file = fullOutputFile,
        }, JsonOptions));
        return result.Status == "passed" ? 0 : 1;
    }

    public static async Task<int> HandleMergeAsync(IReadOnlyList<string> args)
    {
        var workflowFile = GetRequiredOption(args, "--workflow-file");
        var skeletonFile = GetRequiredOption(args, "--skeleton-file");
        var resultFiles = GetOptionValues(args, "--result-file", required: true);
        var outputFile = GetRequiredOption(args, "--output-file");
        var requirements = new List<(string OptionName, string? Path)>
        {
            ("--workflow-file", workflowFile),
            ("--skeleton-file", skeletonFile),
        };
        AddFileRequirements(requirements, "--result-file", resultFiles);
        var files = CliFileInputGuard.RequireExistingFiles(requirements.ToArray());
        var fullOutputFile = GetNewOutputFilePath(outputFile);
        var (template, catalog) = await ReadWorkflowAndCatalogAsync(files["--workflow-file"]).ConfigureAwait(false);
        var skeleton = await ReadSkeletonAsync(files["--skeleton-file"]).ConfigureAwait(false);
        var results = await ReadPhaseResultsAsync(files, "--result-file", resultFiles.Count).ConfigureAwait(false);
        var candidate = WorkflowPhaseMerger.Merge(template, skeleton, catalog, results);
        var compileValidation = await WorkflowValidator.ValidateWithContextFieldCatalogAsync(candidate).ConfigureAwait(false);
        if (compileValidation.HasErrors)
        {
            throw new InvalidOperationException(
                "Merged candidate failed the complete SO compile: " + compileValidation.ToDisplayString());
        }

        await WriteNewFileAsync(fullOutputFile, WorkflowJsonSerializer.Serialize(candidate)).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = "succeeded",
            compile_status = "passed",
            workflow_file = fullOutputFile,
        }, JsonOptions));
        return 0;
    }

    public static async Task<int> HandleImpactAsync(IReadOnlyList<string> args)
    {
        var previousWorkflowFile = GetRequiredOption(args, "--previous-workflow-file");
        var previousSkeletonFile = GetRequiredOption(args, "--previous-skeleton-file");
        var candidateWorkflowFile = GetRequiredOption(args, "--candidate-workflow-file");
        var candidateSkeletonFile = GetRequiredOption(args, "--candidate-skeleton-file");
        var previousResultFiles = GetOptionValues(args, "--previous-result-file", required: false);
        var candidateResultFiles = GetOptionValues(args, "--candidate-result-file", required: false);
        var changedPhaseIds = GetOptionValues(args, "--changed-phase-id", required: false);
        var outputFile = GetRequiredOption(args, "--output-file");
        var requirements = new List<(string OptionName, string? Path)>
        {
            ("--previous-workflow-file", previousWorkflowFile),
            ("--previous-skeleton-file", previousSkeletonFile),
            ("--candidate-workflow-file", candidateWorkflowFile),
            ("--candidate-skeleton-file", candidateSkeletonFile),
        };
        AddFileRequirements(requirements, "--previous-result-file", previousResultFiles);
        AddFileRequirements(requirements, "--candidate-result-file", candidateResultFiles);
        var files = CliFileInputGuard.RequireExistingFiles(requirements.ToArray());
        var fullOutputFile = GetNewOutputFilePath(outputFile);
        var (previousTemplate, previousCatalog) = await ReadWorkflowAndCatalogAsync(files["--previous-workflow-file"]).ConfigureAwait(false);
        var (candidateTemplate, candidateCatalog) = await ReadWorkflowAndCatalogAsync(files["--candidate-workflow-file"]).ConfigureAwait(false);
        var previousSkeleton = await ReadSkeletonAsync(files["--previous-skeleton-file"]).ConfigureAwait(false);
        var candidateSkeleton = await ReadSkeletonAsync(files["--candidate-skeleton-file"]).ConfigureAwait(false);
        var previousResults = await ReadPhaseResultsAsync(files, "--previous-result-file", previousResultFiles.Count).ConfigureAwait(false);
        var candidateResults = await ReadPhaseResultsAsync(files, "--candidate-result-file", candidateResultFiles.Count).ConfigureAwait(false);
        var analysis = WorkflowPhaseImpactAnalyzer.Analyze(
            new WorkflowPhaseImpactSnapshot(
                previousSkeleton,
                previousCatalog,
                previousTemplate.ExpressionBinding,
                previousTemplate.Validation,
                previousResults),
            new WorkflowPhaseImpactSnapshot(
                candidateSkeleton,
                candidateCatalog,
                candidateTemplate.ExpressionBinding,
                candidateTemplate.Validation,
                candidateResults),
            changedPhaseIds);
        await WriteNewFileAsync(fullOutputFile, analysis.ToJson()).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = analysis.Status,
            full_revalidation_required = analysis.FullRevalidationRequired,
            revalidate_phase_ids = analysis.RevalidatePhaseIds,
            reusable_phase_ids = analysis.ReusablePhaseIds,
            analysis_file = fullOutputFile,
        }, JsonOptions));
        return 0;
    }

    private static async Task<(WorkflowInstance Template, ContextFieldCatalog Catalog)> ReadWorkflowAndCatalogAsync(string workflowFile)
    {
        var template = WorkflowJsonSerializer.Deserialize(await File.ReadAllTextAsync(workflowFile).ConfigureAwait(false));
        var catalog = (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(template).ConfigureAwait(false)).Catalog;
        return (template, catalog);
    }

    private static async Task<WorkflowPhaseSkeleton> ReadSkeletonAsync(string skeletonFile)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(skeletonFile).ConfigureAwait(false));
        return WorkflowPhaseSkeleton.Parse(document.RootElement, skeletonFile);
    }

    private static async Task<IReadOnlyList<WorkflowPhaseCompileResult>> ReadPhaseResultsAsync(
        IReadOnlyDictionary<string, string> files,
        string optionName,
        int count)
    {
        var results = new List<WorkflowPhaseCompileResult>(count);
        for (var index = 0; index < count; index++)
        {
            var file = files[$"{optionName}[{index}]"];
            results.Add(WorkflowPhaseCompileResult.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false), file));
        }

        return results;
    }

    private static string GetRequiredOption(IReadOnlyList<string> args, string optionName)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        throw new InvalidOperationException($"Missing required option '{optionName}'.");
    }

    private static IReadOnlyList<string> GetOptionValues(IReadOnlyList<string> args, string optionName, bool required)
    {
        var values = new List<string>();
        for (var index = 0; index < args.Count; index++)
        {
            if (!string.Equals(args[index], optionName, StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Option '{optionName}' requires a value.");
            }

            values.Add(args[++index]);
        }

        if (required && values.Count == 0)
        {
            throw new InvalidOperationException($"At least one '{optionName}' option is required.");
        }

        return values;
    }

    private static void AddFileRequirements(
        ICollection<(string OptionName, string? Path)> requirements,
        string optionName,
        IReadOnlyList<string> paths)
    {
        for (var index = 0; index < paths.Count; index++)
        {
            requirements.Add(($"{optionName}[{index}]", paths[index]));
        }
    }

    private static string GetNewOutputFilePath(string outputFile)
    {
        var fullPath = Path.GetFullPath(outputFile);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Output path '{fullPath}' already exists; choose a new path.");
        }

        return fullPath;
    }

    private static async Task WriteNewFileAsync(string outputFile, string content)
    {
        var directory = Path.GetDirectoryName(outputFile)
            ?? throw new InvalidOperationException($"Output file '{outputFile}' must have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryFile = Path.Combine(directory, $".{Path.GetFileName(outputFile)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryFile, content, new UTF8Encoding(false)).ConfigureAwait(false);
            File.Move(temporaryFile, outputFile);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}