using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal static class WorkflowFragmentEditPilot
{
    internal static async Task<WorkflowInstance> ApplyAsync(
        string baselineJson,
        WorkflowFragmentEdit edit,
        string scratchDirectory,
        WorkflowFragmentRuntime runtime,
        CancellationToken cancellationToken = default,
        ContextFieldCatalog? catalog = null,
        bool reuseOwnerValidation = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);
        RuntimeArtifactPathGuard.EnsureWorkspaceRootOutsideSkillDirectory(scratchDirectory);
        var draft = WorkflowDraftImport.Read(baselineJson);
        runtime.CheckIdentity(draft);

        var statement = edit.Stage(draft);
        var expectedJson = WorkflowJsonSerializer.Serialize(draft);
        var scriptInput = WorkflowJsonSerializer.Deserialize(baselineJson);
        var script = $$"""
            using Techne.Loom.Abstractions.TaskTracking.Model;

            namespace Techne.Loom.WorkflowScripts;

            public static class WorkflowInstanceBuilder
            {
                public static WorkflowInstance Edit(WorkflowInstance workflow, WorkflowScriptInput input)
                {
                    {{statement}}
                    return workflow;
                }
            }
            """;

        var scriptFile = Path.Combine(scratchDirectory, $"fragment-edit-{Guid.NewGuid():N}.cs");
        await using (var stream = new FileStream(scriptFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(script.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        var result = await new WorkflowScriptHost().ExecuteEditorAsync(
            scriptFile,
            scriptInput,
            new WorkflowScriptInput { RuntimeBinding = runtime.Binding, RuntimeVersion = runtime.Version },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Feedback.Error ?? "Fragment edit script failed.");
        }

        var candidateJson = WorkflowJsonSerializer.Serialize(result.Value!);
        if (!string.Equals(expectedJson, candidateJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The candidate differs from the authorized fragment edit.");
        }

        var candidate = WorkflowDraftImport.Read(candidateJson);
        if (!reuseOwnerValidation || !runtime.SupportsDescriptionEvidenceReuse
            || edit is not WorkflowStateDescriptionEdit || catalog is null)
        {
            catalog ??= (await WorkflowContextFieldCatalogLoader.ReadRequiredAsync(candidate, ct: cancellationToken).ConfigureAwait(false)).Catalog;
            runtime.Validate(candidate, catalog);
        }
        return candidate;
    }
}
