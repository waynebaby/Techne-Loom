using System.Runtime.CompilerServices;
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public static class WorkflowRuntimeEvidenceRegistry
{
    private sealed class EvidenceMarker
    {
    }

    private static readonly ConditionalWeakTable<WorkflowInstance, EvidenceMarker> ObservedInstances = new();

    public static void MarkObserved(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ObservedInstances.GetValue(instance, static _ => new EvidenceMarker());
    }

    public static void RemoveObserved(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ObservedInstances.Remove(instance);
    }

    public static bool IsObserved(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return ObservedInstances.TryGetValue(instance, out _);
    }
}
