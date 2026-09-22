# Skill-Driven Workflow Example

[中文](../../zh-cn/examples/skill-driven-workflow.md) | [Root](../README.md)

This example shows how SO owns deterministic execution while the caller owns external seams surfaced through boundary payloads.

## Flow

1. The caller provides workflow input.
2. SO runs until it reaches an external seam.
3. SO returns a boundary payload that surfaces that seam with the current step kind and required inputs.
4. The caller performs the external action and sends back a structured resume envelope.
5. SO evaluates the resumed context and finishes at a deterministic done state.

## Resumable Workflow File

```json
{
  "instanceId": "ask1",
  "nodes": {
    "state.start": {
      "$kind": "state",
      "id": "state.start",
      "name": "Start",
      "workflowPhase": "01 Intake",
      "groups": [
        {
          "id": "group.ask",
          "strategy": "firstSuccess",
          "transitionIds": ["transition.ask"]
        }
      ],
      "waitBehavior": "blockUntilComplete"
    },
    "state.review": {
      "$kind": "state",
      "id": "state.review",
      "name": "Review",
      "workflowPhase": "02 Review",
      "groups": [
        {
          "id": "group.review",
          "strategy": "firstSuccess",
          "transitionIds": ["transition.check"]
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
    "transition.ask": {
      "$kind": "command",
      "id": "transition.ask",
      "name": "Ask user",
      "description": "Need structured result",
      "targetNodeId": "state.review",
      "stepKind": "askUser",
      "command": {
        "kind": "tool",
        "name": "noop",
        "environmentKey": "",
        "parameters": {
          "requiredInputs": ["filePath", "content"]
        },
        "currentRetryCount": 0
      },
      "currentRetryCount": 0,
      "maxRetry": 10
    },
    "transition.check": {
      "$kind": "expr",
      "id": "transition.check",
      "name": "Check review",
      "targetNodeId": "state.done",
      "stepKind": "conditionBranch",
      "guardExpression": "true",
      "succeedExpression": "review.approved == true",
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

## First Run

```powershell
dotnet so.dll run --workflow-file .\ask-workflow.json
```

Expected control payload excerpt:

```xml
<so_property>
{"type":"boundary","payload":{"status":"blocked","current_step_kind":"AskUser","required_inputs":["filePath","content"]}}
</so_property>
```

## Resume Envelope

```json
{
  "transition_id": "transition.ask",
  "correlation_key": null,
  "payload": {
    "review": {
      "approved": true
    }
  }
}
```

## Resume Command

```powershell
dotnet so.dll resume --workflow-file .\ask-workflow.json --result-file .\resume.json
```

Expected final control payload excerpt:

```xml
<so_property>
{"type":"result","payload":{"status":"completed","current_node_id":"state.done"}}
</so_property>
```

## Practical Reading Of The Result

- The caller owns the external action between `run` and `resume`.
- SO owns the persisted workflow state before and after the seam.
- The structured resume envelope is part of the public contract, not an implementation detail.

## Route Map

```mermaid
flowchart LR
    A["🧭 Input"] --> B["⚙️ SO run"]
    B --> C{"❓ External seam?"}
    C -- "No" --> D["✅ state.done"]
    C -- "Yes" --> E["🚧 Boundary payload"]
    E --> F["🔁 Structured resume"]
    F --> G["💬 Review resumed context"]
    G --> D

    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef runtime fill:#dbeafe,stroke:#2563eb,color:#1e3a8a;
    classDef decision fill:#fef3c7,stroke:#a16207,color:#713f12;
    classDef blocked fill:#fee2e2,stroke:#dc2626,color:#7f1d1d;
    classDef review fill:#ffedd5,stroke:#ea580c,color:#9a3412;
    classDef done fill:#dcfce7,stroke:#15803d,color:#14532d;
    class A intake;
    class B,F runtime;
    class C decision;
    class E blocked;
    class G review;
    class D done;
    subgraph legend["Legend"]
        L1["🧭 intake"]
        L2["⚙️ runtime"]
        L3["❓ decision"]
        L4["🚧 blocked"]
        L5["🔁 resume"]
        L6["✅ completion"]
    end
    class L1 intake;
    class L2 runtime;
    class L3 decision;
    class L4 blocked;
    class L5 runtime;
    class L6 done;
```

## Compile Evidence

This JSON is a public contract example. Compile an external copy with the exact SO runtime before treating its Mermaid as execution evidence:

```powershell
dotnet so.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
```


## Runtime-Generated Mermaid (SO 0.3.316-beta)

Source workflow: `skill-driven-workflow.md` first JSON workflow block. Command: `so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>`.

```mermaid

flowchart TD
    subgraph phase_01_intake["01 Intake"]
    state.start["🚧 Start"]
    end
    subgraph phase_02_review["02 Review"]
    state.review["❓ Review"]
    end
    subgraph phase_03_complete["03 Complete"]
    state.done["📜 Done"]
    end
    state.review -->|Check review| state.done
    state.start -->|Ask user| state.review
    style state.done fill:#f8fafc,stroke:#94a3b8,stroke-width:1px
    style state.review fill:#fef3c7,stroke:#a16207,stroke-width:1px
    style state.start fill:#fee2e2,stroke:#dc2626,stroke-width:1px
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
