# Workflow instance builder template

This C# example is text inside Markdown. It is not a standalone `.cs` or `.csx` package file. Keep the public namespace and `Build(WorkflowScriptInput input)` signature so the selected AO or SO runtime can execute it through its Roslyn workflow-script host.

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
}
```
