# Workflow instance builder template

This C# example is text inside Markdown. It is not a standalone `.cs` or `.csx` package file. Use `Build(WorkflowScriptInput input)` for a new workflow or `Edit(WorkflowInstance workflow, WorkflowScriptInput input)` to revise an external draft through the selected AO or SO runtime's Roslyn workflow-script host. The host reads and deserializes the external JSON; do not embed JSON in the script or perform file I/O.

```csharp
using Techne.Loom.Abstractions.TaskTracking.Model;

namespace Techne.Loom.WorkflowScripts;

public static class WorkflowInstanceBuilder
{
    public static WorkflowInstance Build(WorkflowScriptInput input)
    {
        return new WorkflowInstance
        {
            InstanceId = "workflow.example",
            RuntimeBinding = input.RuntimeBinding,
            RuntimeVersion = input.RuntimeVersion,
            StartNodeId = "state.start",
            CurrentNodeId = "state.start",
            Nodes = new Dictionary<string, ITaskNode>(StringComparer.Ordinal)
            {
                ["state.start"] = new StateNode
                {
                    Id = "state.start",
                    Name = "Start",
                    Description = "Workflow entry state.",
                    WorkflowPhase = "phase.01",
                },
            },
        };
    }

    public static WorkflowInstance Edit(WorkflowInstance workflow, WorkflowScriptInput input)
    {
        workflow.RuntimeBinding = input.RuntimeBinding;
        workflow.RuntimeVersion = input.RuntimeVersion;
        return workflow;
    }
}
```
