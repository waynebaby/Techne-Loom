# Basic Task Tracking Example

[中文](../../zh-cn/examples/basic-task-tracking.md) | [Root](../README.md)

This example shows the smallest public workflow shape worth keeping.

## Flow

1. A workflow instance starts at a state node.
2. A deterministic transition updates context.
3. A history entry records the move.
4. Command output is captured into workflow context and the final result payload.

## Minimal Workflow File

```json
{
  "instanceId": "sample1",
  "nodes": {
    "state.start": {
      "$kind": "state",
      "id": "state.start",
      "name": "Start",
      "workflowPhase": "01 Intake",
      "groups": [
        {
          "id": "group.main",
          "strategy": "firstSuccess",
          "transitionIds": ["transition.run"]
        }
      ],
      "waitBehavior": "blockUntilComplete"
    },
    "state.done": {
      "$kind": "state",
      "id": "state.done",
      "name": "Done",
      "workflowPhase": "03 Complete",
      "groups": [],
      "waitBehavior": "blockUntilComplete"
    },
    "transition.run": {
      "$kind": "command",
      "id": "transition.run",
      "name": "Run tool",
      "targetNodeId": "state.done",
      "outputPath": "toolResult",
      "stepKind": "toolCall",
      "guardExpression": "true",
      "succeedExpression": "true",
      "command": {
        "kind": "tool",
        "name": "echo",
        "parameters": {
          "message": "hello"
        },
        "currentRetryCount": 0
      },
      "currentRetryCount": 0,
      "maxRetry": 10
    }
  },
  "startNodeId": "state.start",
  "currentNodeId": "state.start",
  "endNodeId": "state.done",
  "status": "readyToStart",
  "context": {},
  "history": [],
  "version": 0,
  "activeWaitGroups": []
}
```

## Run Command

```powershell
.\so.exe run --workflow-file .\workflow.json
```

## What To Expect

- SO runs the deterministic `toolCall` step.
- The workflow ends at `state.done`.
- The final `<so_property>` block carries a `result` payload.


## Execution Map

```mermaid
flowchart LR
    A["🧭 state.start"] --> B["⚙️ toolCall: echo"]
    B --> C["🧾 context.toolResult"]
    C --> D["✅ state.done"]

    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef runtime fill:#dbeafe,stroke:#2563eb,color:#1e3a8a;
    classDef evidence fill:#ede9fe,stroke:#7c3aed,color:#4c1d95;
    classDef done fill:#dcfce7,stroke:#15803d,color:#14532d;
    class A intake;
    class B runtime;
    class C evidence;
    class D done;
    subgraph legend["Legend"]
        L1["🧭 intake"]
        L2["⚙️ runtime"]
        L3["🧾 evidence"]
        L4["✅ completion"]
    end
    class L1 intake;
    class L2 runtime;
    class L3 evidence;
    class L4 done;
```

## Compile Evidence

The workflow JSON example is intended for the SO runtime. Run the same-version command below against a complete external workflow copy; keep the returned Mermaid and audit files under a temporary output root. The checked-in example remains explanatory source, while the runtime-generated Mermaid is the authority for the executed copy.

```powershell
.\so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
```


## Runtime-Generated Mermaid (SO 0.3.316-beta)

Source workflow: `basic-task-tracking.md` first JSON workflow block. Command: `so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>`.

```mermaid

flowchart TD
    subgraph phase_01_intake["01 Intake"]
    state.start["⚙️ Start"]
    end
    subgraph phase_03_complete["03 Complete"]
    state.done["📜 Done"]
    end
    state.start -->|Run tool| state.done
    style state.done fill:#f8fafc,stroke:#94a3b8,stroke-width:1px
    style state.start fill:#dbeafe,stroke:#2563eb,stroke-width:1px
    style state.start stroke:#ea580c,stroke-width:3px
    subgraph legend[Legend]
        legend_ai["🔎 AI"]
    style legend_ai fill:#dcfce7,stroke:#16a34a,stroke-width:1px
        legend_tool["⚙️ Code/Tool"]
    style legend_tool fill:#dbeafe,stroke:#2563eb,stroke-width:1px
        legend_branch["❓ Conditional branch"]
    style legend_branch fill:#fef3c7,stroke:#a16207,stroke-width:1px
        legend_optional["💬 Optional user choice"]
    style legend_optional fill:#fef3c7,stroke:#d97706,stroke-width:1px
        legend_required["🚧 Required user input"]
    style legend_required fill:#fee2e2,stroke:#dc2626,stroke-width:1px
        legend_gate["📜 Gate"]
    style legend_gate fill:#f8fafc,stroke:#94a3b8,stroke-width:1px
    end

```
