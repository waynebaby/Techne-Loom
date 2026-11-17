using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

internal sealed record WorkflowFragmentRuntime(
    string Binding,
    string Version,
    Action<WorkflowInstance, ContextFieldCatalog> Validate)
{
    internal void CheckIdentity(WorkflowInstance workflow)
    {
        if (workflow.RuntimeBinding != Binding || workflow.RuntimeVersion != Version)
        {
            throw new InvalidOperationException($"The workflow design session requires '{Binding}' version '{Version}'.");
        }
    }
}
