using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed record WorkflowFragmentRuntime(
    string Binding,
    string Version,
    Action<WorkflowInstance, ContextFieldCatalog> Validate,
    bool SupportsDescriptionEvidenceReuse = false)
{
    internal string? ValidationIdentity => CSharpExpressionCompiler.ReferenceIdentity is { } references
        ? string.Join("/", Binding, Version, Validate.Method.Module.ModuleVersionId, typeof(WorkflowInstance).Module.ModuleVersionId,
            typeof(WorkflowFragmentRuntime).Module.ModuleVersionId, references,
            typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Module.ModuleVersionId, Environment.Version)
        : null;

    internal void CheckIdentity(WorkflowInstance workflow)
    {
        if (workflow.RuntimeBinding != Binding || workflow.RuntimeVersion != Version)
        {
            throw new InvalidOperationException($"The workflow design session requires '{Binding}' version '{Version}'.");
        }
    }
}
