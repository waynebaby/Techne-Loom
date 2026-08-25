using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowAuditArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly Lazy<string> DefaultTemporaryOutputRoot = new(CreateDefaultTemporaryOutputRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task<WorkflowAuditArtifacts> WriteAsync(
        string workflowId,
        int sequence,
        string action,
        string workflowJson,
        string mermaidMarkdown,
        string html,
        string? outputRoot = null,
        string? analysisJson = null,
        CancellationToken ct = default)
    {
        return WriteAsync(workflowId, sequence, action, workflowJson, mermaidMarkdown, html, outputRoot, analysisJson, dataflowJson: null, ct);
    }

    public static async Task<WorkflowAuditArtifacts> WriteAsync(
        string workflowId,
        int sequence,
        string action,
        string workflowJson,
        string mermaidMarkdown,
        string html,
        string? outputRoot,
        string? analysisJson,
        string? dataflowJson,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new InvalidOperationException("A non-empty workflow identifier is required for audit output.");
        }

        var normalizedAction = SanitizeSegment(action, "action");
        var normalizedOutputRoot = ResolveOutputRoot(outputRoot);
        var stepDirectory = Path.Combine(
            normalizedOutputRoot,
            $"wf-{SanitizeSegment(workflowId, "wf")}",
            $"step-{Math.Max(sequence, 1):D4}-{normalizedAction}");

        await using var destinationLock = await WorkflowFileLock.AcquireAsync(stepDirectory, ct).ConfigureAwait(false);
        var existingEntries = Directory.Exists(stepDirectory)
            ? Directory.EnumerateFileSystemEntries(stepDirectory).ToArray()
            : Array.Empty<string>();
        if (existingEntries.Length > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite existing audit artifacts for workflow '{workflowId}' at '{stepDirectory}'. " +
                $"Existing files: {string.Join(", ", existingEntries.Select(path => $"'{path}'"))}. " +
                "Choose a different audit output root or sequence.");
        }

        Directory.CreateDirectory(stepDirectory);

        var mermaidFile = Path.Combine(stepDirectory, "workflow.mermaid.md");
        var htmlFile = Path.Combine(stepDirectory, "workflow.html");
        var workflowBackupFile = Path.Combine(stepDirectory, "workflow.json");
        var analysisFile = string.IsNullOrWhiteSpace(analysisJson)
            ? null
            : Path.Combine(stepDirectory, "workflow.analysis.json");
        var dataflowFile = string.IsNullOrWhiteSpace(dataflowJson)
            ? null
            : Path.Combine(stepDirectory, "workflow.dataflow.json");

        var existingFiles = new[] { mermaidFile, htmlFile, workflowBackupFile, analysisFile, dataflowFile }
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Where(File.Exists)
            .ToArray();

        if (existingFiles.Length > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite existing audit artifacts for workflow '{workflowId}' at '{stepDirectory}'. " +
                $"Existing files: {string.Join(", ", existingFiles.Select(path => $"'{path}'"))}. " +
                "Choose a different audit output root, clean the existing step directory, or let the runtime use a temporary output root.");
        }

        await File.WriteAllTextAsync(mermaidFile, FormatMermaidMarkdown(mermaidMarkdown), Utf8WithoutBom, ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(htmlFile, html, Utf8WithoutBom, ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(workflowBackupFile, workflowJson, Utf8WithoutBom, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(analysisFile))
        {
            await File.WriteAllTextAsync(analysisFile, analysisJson!, Utf8WithoutBom, ct).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(dataflowFile))
        {
            await File.WriteAllTextAsync(dataflowFile, dataflowJson!, Utf8WithoutBom, ct).ConfigureAwait(false);
        }

        return new WorkflowAuditArtifacts(
            normalizedOutputRoot,
            workflowId,
            Math.Max(sequence, 1),
            normalizedAction,
            stepDirectory,
            mermaidFile,
            htmlFile,
            workflowBackupFile,
            AnalysisFile: analysisFile,
                DataflowFile: dataflowFile);
    }

    public static async Task<WorkflowAuditArtifacts> CopyStepAsync(
        string sourceStepDirectory,
        string workflowId,
        int sequence,
        string action,
        string? outputRoot,
        string reason,
        string verifiedBy,
        CancellationToken ct = default,
        string? expectedWorkflowJson = null,
        string? analysisJsonOverride = null,
        string? dataflowJsonOverride = null,
        string? mermaidMarkdownOverride = null,
        string? htmlOverride = null)
    {
        if (string.IsNullOrWhiteSpace(sourceStepDirectory))
        {
            throw new InvalidOperationException("A source audit step directory is required for audit reuse.");
        }

        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new InvalidOperationException("A non-empty workflow identifier is required for audit reuse.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("An explicit audit reuse reason is required.");
        }

        if (string.IsNullOrWhiteSpace(verifiedBy))
        {
            throw new InvalidOperationException("An explicit audit reuse verifier is required.");
        }

        ct.ThrowIfCancellationRequested();
        var sourceDirectory = Path.GetFullPath(sourceStepDirectory);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException($"Source audit step directory '{sourceDirectory}' was not found.");
        }

        var requiredFileNames = new[]
        {
            "workflow.mermaid.md",
            "workflow.html",
            "workflow.json",
        };
        var missingFiles = requiredFileNames
            .Where(fileName => !File.Exists(Path.Combine(sourceDirectory, fileName)))
            .ToArray();
        if (missingFiles.Length > 0)
        {
            throw new InvalidOperationException(
                $"Source audit step '{sourceDirectory}' is incomplete. Missing required files: {string.Join(", ", missingFiles)}.");
        }

        var currentSnapshotMode = !string.IsNullOrWhiteSpace(expectedWorkflowJson);
        var optionalFileNames = new[] { "workflow.analysis.json", "workflow.dataflow.json", "summary.json" };
        var sourceFileNames = currentSnapshotMode
            ? requiredFileNames
            : requiredFileNames
                .Concat(optionalFileNames.Where(fileName => File.Exists(Path.Combine(sourceDirectory, fileName))))
                .ToArray();
        var copiedFileNames = currentSnapshotMode
            ? sourceFileNames.Where(fileName => !string.Equals(fileName, "workflow.json", StringComparison.Ordinal)).ToArray()
            : sourceFileNames;
        var replacedFileNames = currentSnapshotMode
            ? new[] { "workflow.json" }
                .Concat(string.IsNullOrWhiteSpace(analysisJsonOverride) ? Enumerable.Empty<string>() : ["workflow.analysis.json"])
                .Concat(string.IsNullOrWhiteSpace(dataflowJsonOverride) ? Enumerable.Empty<string>() : ["workflow.dataflow.json"])
                .ToArray()
            : Array.Empty<string>();
        var sourceWorkflowFile = Path.Combine(sourceDirectory, "workflow.json");
        var sourceWorkflowJson = await File.ReadAllTextAsync(sourceWorkflowFile, ct).ConfigureAwait(false);
        string sourceInstanceId;
        try
        {
            sourceInstanceId = WorkflowJsonSerializer.Deserialize(sourceWorkflowJson).InstanceId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Source audit step '{sourceDirectory}' contains an invalid workflow.json and cannot be verified.",
                ex);
        }

        if (string.IsNullOrWhiteSpace(sourceInstanceId))
        {
            throw new InvalidOperationException(
                $"Source audit step '{sourceDirectory}' workflow.json does not contain a non-empty instanceId and cannot be verified.");
        }

        if (currentSnapshotMode)
        {
            string sourceProjection;
            string expectedProjection;
            try
            {
                sourceProjection = CreateStableWorkflowProjection(sourceWorkflowJson);
                expectedProjection = CreateStableWorkflowProjection(expectedWorkflowJson!);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "The current workflow snapshot is invalid and cannot be compared for audit reuse.",
                    ex);
            }

            if (!string.Equals(sourceProjection, expectedProjection, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Source audit step '{sourceDirectory}' does not match the current workflow render inputs and cannot be reused.");
            }
        }

        var renderMatchesCurrent = true;
        if (currentSnapshotMode)
        {
            if (string.IsNullOrWhiteSpace(mermaidMarkdownOverride) || string.IsNullOrWhiteSpace(htmlOverride))
            {
                throw new InvalidOperationException(
                    "Current Mermaid and HTML renders are required when reusing an audit step for a runtime snapshot.");
            }

            var expectedMermaid = FormatMermaidMarkdown(mermaidMarkdownOverride);
            var sourceMermaid = await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "workflow.mermaid.md"), ct).ConfigureAwait(false);
            var sourceHtml = await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "workflow.html"), ct).ConfigureAwait(false);
            renderMatchesCurrent = string.Equals(sourceMermaid, expectedMermaid, StringComparison.Ordinal)
                && string.Equals(sourceHtml, htmlOverride, StringComparison.Ordinal);
            if (!renderMatchesCurrent)
            {
                copiedFileNames = [];
                replacedFileNames = new[] { "workflow.mermaid.md", "workflow.html", "workflow.json" }
                    .Concat(string.IsNullOrWhiteSpace(analysisJsonOverride) ? Enumerable.Empty<string>() : ["workflow.analysis.json"])
                    .Concat(string.IsNullOrWhiteSpace(dataflowJsonOverride) ? Enumerable.Empty<string>() : ["workflow.dataflow.json"])
                    .ToArray();
            }
        }

        var destinationFileNames = copiedFileNames
            .Concat(replacedFileNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var sourceWorkflowDirectory = Directory.GetParent(sourceDirectory)?.Name ?? sourceDirectory;
        var sourceWorkflowId = sourceWorkflowDirectory.StartsWith("wf-", StringComparison.OrdinalIgnoreCase)
            ? sourceWorkflowDirectory[3..]
            : sourceWorkflowDirectory;
        var normalizedOutputRoot = ResolveOutputRoot(outputRoot);
        var normalizedAction = SanitizeSegment(action, "reused");
        var normalizedSequence = Math.Max(sequence, 1);
        var destinationDirectory = Path.Combine(
            normalizedOutputRoot,
            $"wf-{SanitizeSegment(workflowId, "wf")}",
            $"step-{normalizedSequence:D4}-{normalizedAction}");
        if (string.Equals(sourceDirectory, Path.GetFullPath(destinationDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The source audit step and destination audit step must be different directories.");
        }

        await using var destinationLock = await WorkflowFileLock.AcquireAsync(destinationDirectory, ct).ConfigureAwait(false);
        if (Directory.Exists(destinationDirectory) && Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite reused audit artifacts at '{destinationDirectory}'. " +
                "The destination step directory already contains files or directories. Choose a different audit output root or sequence.");
        }

        Directory.CreateDirectory(destinationDirectory);
        var manifestFile = Path.Combine(destinationDirectory, "audit-reuse.json");
        var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var artifactOrigin = currentSnapshotMode && !renderMatchesCurrent ? "fresh-runtime" : "verified-copy";
        var officialExecutionEvidence = currentSnapshotMode && !renderMatchesCurrent;
        try
        {
            foreach (var fileName in sourceFileNames)
            {
                ct.ThrowIfCancellationRequested();
                var sourceFile = Path.Combine(sourceDirectory, fileName);
                var sourceHash = await ComputeSha256Async(sourceFile, ct).ConfigureAwait(false);
                sourceHashes[fileName] = Convert.ToHexString(sourceHash).ToLowerInvariant();
                if (!copiedFileNames.Contains(fileName, StringComparer.Ordinal))
                {
                    continue;
                }

                var destinationFile = Path.Combine(destinationDirectory, fileName);
                File.Copy(sourceFile, destinationFile, overwrite: false);
                var destinationHash = await ComputeSha256Async(destinationFile, ct).ConfigureAwait(false);
                if (!sourceHash.SequenceEqual(destinationHash))
                {
                    throw new InvalidOperationException($"Audit reuse verification failed for '{fileName}': source and destination SHA-256 values differ.");
                }
            }

            if (currentSnapshotMode)
            {
                if (!renderMatchesCurrent)
                {
                    await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "workflow.mermaid.md"), FormatMermaidMarkdown(mermaidMarkdownOverride!), Utf8WithoutBom, ct).ConfigureAwait(false);
                    await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "workflow.html"), htmlOverride!, Utf8WithoutBom, ct).ConfigureAwait(false);
                }

                await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "workflow.json"), expectedWorkflowJson!, Utf8WithoutBom, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(analysisJsonOverride))
                {
                    await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "workflow.analysis.json"), analysisJsonOverride, Utf8WithoutBom, ct).ConfigureAwait(false);
                }
                if (!string.IsNullOrWhiteSpace(dataflowJsonOverride))
                {
                    await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "workflow.dataflow.json"), dataflowJsonOverride, Utf8WithoutBom, ct).ConfigureAwait(false);
                }
            }

            var manifest = new WorkflowAuditReuseManifest(
                sourceDirectory,
                destinationDirectory,
                sourceWorkflowId,
                sourceInstanceId,
                workflowId,
                normalizedSequence,
                normalizedAction,
                reason,
                verifiedBy,
                DateTimeOffset.UtcNow,
                ArtifactOrigin: artifactOrigin,
                OfficialExecutionEvidence: officialExecutionEvidence,
                sourceHashes)
            {
                CopiedFileNames = copiedFileNames,
                ReplacedFileNames = replacedFileNames,
            };
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestFile, manifestJson, Utf8WithoutBom, ct).ConfigureAwait(false);
        }
        catch
        {
            foreach (var fileName in destinationFileNames.Append("audit-reuse.json"))
            {
                var filePath = Path.Combine(destinationDirectory, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            if (Directory.Exists(destinationDirectory) && !Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
            {
                Directory.Delete(destinationDirectory);
            }

            throw;
        }

        return new WorkflowAuditArtifacts(
            normalizedOutputRoot,
            workflowId,
            normalizedSequence,
            normalizedAction,
            destinationDirectory,
            Path.Combine(destinationDirectory, "workflow.mermaid.md"),
            Path.Combine(destinationDirectory, "workflow.html"),
            Path.Combine(destinationDirectory, "workflow.json"),
            SummaryFile: currentSnapshotMode || !sourceFileNames.Contains("summary.json", StringComparer.Ordinal) ? null : Path.Combine(destinationDirectory, "summary.json"),
            AnalysisFile: currentSnapshotMode
                ? string.IsNullOrWhiteSpace(analysisJsonOverride) ? null : Path.Combine(destinationDirectory, "workflow.analysis.json")
                : sourceFileNames.Contains("workflow.analysis.json", StringComparer.Ordinal) ? Path.Combine(destinationDirectory, "workflow.analysis.json") : null,
            DataflowFile: currentSnapshotMode
                ? string.IsNullOrWhiteSpace(dataflowJsonOverride) ? null : Path.Combine(destinationDirectory, "workflow.dataflow.json")
                : sourceFileNames.Contains("workflow.dataflow.json", StringComparer.Ordinal) ? Path.Combine(destinationDirectory, "workflow.dataflow.json") : null)
        with
        {
            ReuseManifestFile = manifestFile,
            ReusedFromStepDirectory = sourceDirectory,
            ReuseReason = reason,
            ReuseVerifiedBy = verifiedBy,
            ArtifactOrigin = artifactOrigin,
            OfficialExecutionEvidence = officialExecutionEvidence,
        };
    }

    private static string CreateStableWorkflowProjection(string workflowJson)
    {
        var instance = WorkflowJsonSerializer.Deserialize(workflowJson);
        instance.InstanceId = string.Empty;
        instance.CurrentNodeId = string.Empty;
        instance.Status = WorkflowStatus.ReadyToStart;
        instance.Context.Clear();
        instance.History.Clear();
        instance.Version = 0;
        instance.ActiveWaitGroups.Clear();
        instance.LastGateEvaluation = null;
        instance.LastActivityUtc = null;
        instance.LastHeartbeatUtc = null;
        instance.LeaseOwner = null;
        instance.LeaseExpiresUtc = null;
        foreach (var state in instance.GetStateNodes().Values)
        {
            state.EntranceTime = null;
        }
        foreach (var transition in instance.GetTransitionNodes().Values.OfType<CommandTransition>())
        {
            transition.CurrentRetryCount = 0;
        }

        return WorkflowJsonSerializer.Serialize(instance, indented: false);
    }

    private static async Task<byte[]> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        return await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
    }
    public static string ResolveOutputRoot(string? outputRoot)
    {
        var root = string.IsNullOrWhiteSpace(outputRoot)
            ? DefaultTemporaryOutputRoot.Value
            : outputRoot;

        var normalizedRoot = Path.GetFullPath(root);
        Directory.CreateDirectory(normalizedRoot);
        return normalizedRoot;
    }

    private static string CreateDefaultTemporaryOutputRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "techne-loom-audit",
            $"exec-{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}");
    }

    private static string FormatMermaidMarkdown(string mermaidMarkdown)
    {
        var normalized = (mermaidMarkdown ?? string.Empty).Trim();
        if (normalized.StartsWith("```mermaid", StringComparison.OrdinalIgnoreCase) &&
            normalized.EndsWith("```", StringComparison.Ordinal))
        {
            var body = normalized["```mermaid".Length..^"```".Length].Trim('\r', '\n');
            return $"```mermaid{Environment.NewLine}{Environment.NewLine}{body}{Environment.NewLine}{Environment.NewLine}```{Environment.NewLine}{Environment.NewLine}";
        }

        return $"```mermaid{Environment.NewLine}{Environment.NewLine}{normalized}{Environment.NewLine}{Environment.NewLine}```{Environment.NewLine}{Environment.NewLine}";
    }

    private static string SanitizeSegment(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
