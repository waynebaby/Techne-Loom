using System.Text.Json.Serialization;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowArtifactManifestReader
{
    public static WorkflowArtifactManifestResult Read(string workflowFile)
    {
        var normalizedWorkflowFile = CanonicalWorkflowFileStore.NormalizePath(workflowFile);
        if (!File.Exists(normalizedWorkflowFile))
        {
            throw new FileNotFoundException("The workflow file was not found.", normalizedWorkflowFile);
        }

        var candidates = new[]
        {
            new WorkflowArtifactManifestEntry("workflow", normalizedWorkflowFile, false, null),
            new WorkflowArtifactManifestEntry("events", CanonicalWorkflowFileStore.GetEventLogPath(normalizedWorkflowFile), false, null),
            new WorkflowArtifactManifestEntry("operations", WorkflowOperationLedger.GetPath(normalizedWorkflowFile), false, null),
        };
        var artifacts = candidates
            .Select(static candidate => candidate with
            {
                Exists = File.Exists(candidate.Path),
                SizeBytes = File.Exists(candidate.Path) ? new FileInfo(candidate.Path).Length : null,
            })
            .ToArray();
        return new WorkflowArtifactManifestResult(normalizedWorkflowFile, artifacts);
    }
}
