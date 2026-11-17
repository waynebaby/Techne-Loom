using Microsoft.CodeAnalysis.CSharp;
using Techne.Loom.Abstractions.TaskTracking.Model;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.SkillOrchestrator.Validation;

internal sealed record WorkflowStateDescriptionEdit(string StateId, string? Description);

internal static class WorkflowFragmentEditPilot
{
    internal static async Task<WorkflowInstance> ApplyAsync(
        string baselineJson,
        WorkflowStateDescriptionEdit edit,
        string scratchDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);
        RuntimeArtifactPathGuard.EnsureWorkspaceRootOutsideSkillDirectory(scratchDirectory);
        var draft = WorkflowDraftImport.Read(baselineJson);
        var runtimeVersion = SkillCli.GetRuntimeVersion();
        if (draft.RuntimeBinding != "dotnet-so" || draft.RuntimeVersion != runtimeVersion)
        {
            throw new InvalidOperationException($"Fragment edits require a template bound to SO version '{runtimeVersion}'.");
        }

        if (!draft.Nodes.TryGetValue(edit.StateId, out var node) || node is not StateNode expectedState)
        {
            throw new InvalidOperationException($"The requested state '{edit.StateId}' does not exist.");
        }

        expectedState.Description = edit.Description;
        var expectedJson = WorkflowJsonSerializer.Serialize(draft);
        var scriptInput = WorkflowJsonSerializer.Deserialize(baselineJson);
        var state = SymbolDisplay.FormatLiteral(edit.StateId, quote: true);
        var description = edit.Description is null ? "null" : SymbolDisplay.FormatLiteral(edit.Description, quote: true);
        var script = $$"""
            using Techne.Loom.Abstractions.TaskTracking.Model;

            namespace Techne.Loom.WorkflowScripts;

            public static class WorkflowInstanceBuilder
            {
                public static WorkflowInstance Edit(WorkflowInstance workflow, WorkflowScriptInput input)
                {
                    ((StateNode)workflow.Nodes[{{state}}]).Description = {{description}};
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
            new WorkflowScriptInput { RuntimeBinding = "dotnet-so", RuntimeVersion = runtimeVersion },
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
        (await WorkflowValidator.ValidateWithContextFieldCatalogAsync(candidate, cancellationToken).ConfigureAwait(false)).ThrowIfInvalid();
        return candidate;
    }
}
