# Workflow Schema Reference

[中文](../../zh-cn/reference/workflow-schema.md) | [Root](../README.md)

The canonical workflow schema describes the persisted workflow file that SO executes and that future AO/SO-compatible runtimes should be able to read.

Repo-wide explanatory terms such as **pattern**, **strand**, **weave out**, and **weave back** are defined in [Workflow Terminology](../architecture/workflow-terminology.md).

## Core Elements

- instance identifiers and version
- node collection and start node
- current node and status
- context map
- history entries
- wait groups and expiration metadata
- artifact references

## Current Workflow File Shape

- The current workflow file uses camelCase property names.
- Task nodes are stored in a node map keyed by node id.
- Polymorphic entries use `$kind` such as `state`, `command`, `expr`, and `tbr`.
- Transition entries use `stepKind` plus owned-input metadata as the stable semantic input for analysis and visualization. Mermaid renderers derive both light node colors and stable emoji labels from those fields: `🔎` AI/model/subagent work green, `⚙️` code/tool work blue, `💬` user-owned optional branch choice yellow, `🚧` required user input red, `❓` generic conditional branch amber/yellow, and `📜` gate/governance states white or very light gray.
- Every `state` node must declare a non-empty `workflowPhase`. This field tells compile and visualization which overall workflow stage the node belongs to and is used to group Mermaid swimlanes. Treat it as required authoring data, not optional decoration.
- `context` is free-form and may carry nested objects and arrays.
- `activeWaitGroups` is part of persisted runtime state, not hidden process memory.

## Minimal Example

```json
{
  "instanceId": "sample1",
  "nodes": {
    "state.start": {
      "$kind": "state",
      "id": "state.start",
      "name": "Start",
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

## Runtime Notes

- The public model is broader than the current public SO runtime implementation.
- `firstSuccess` is the fully supported group strategy in the current reviewed slice.
- Unsupported multi-transition strategy cases fail explicitly instead of silently degrading.
- `dotnet so.dll compile`, `run`, and `resume` write `workflow.analysis.json` under the audit step directory. That artifact is derived from the workflow file and summarizes requested inputs, published output families, branches, loops, user seams, runtime seams, gates, and Turing-complete control risk.
- `dotnet so.dll copy-audit-step` copies only explicitly verified unchanged audit artifacts, records source hashes in `audit-reuse.json`, and never advances workflow state or creates official runtime evidence.
- `dotnet so.dll compile` rejects any workflow file whose state node omits `workflowPhase` or sets it to null, empty, or whitespace. The error should identify the state node id, the `workflowPhase` field path, and a corrective suggestion that explains the field means “which stage of the overall workflow this node belongs to.”

## Sidecar Separation

- The workflow file is not the same thing as the CLI control payload.
- `<so_property>` carries public control metadata and is one of the current weave-out surfaces.
- `dotnet so.dll resume --result-file` consumes a separate structured JSON envelope. That envelope is the current weave-back sidecar.
- `.events.jsonl` beside the workflow file carries append-on-growth event history.
- `.events.jsonl.meta.json` beside it records the workflow `instance_id`; a missing, malformed, or mismatched lineage record causes the event sidecar to be rewritten before new history is appended.

SO blocking payloads and AO control payloads remain separate product contracts built on top of shared low-level conventions.


## Expression Definitions

The workflow root declares `runtimeBinding` and `expressionBinding`. The current .NET binding is C# with Roslyn and `compileFeedbackContract: "detailedCompileFeedbackV1"`. Predicate fields use structured `ExpressionDefinition` objects with `kind`, `source`, `entryPoint`, and `resultType`; string shorthand is read only when an explicit C# binding is present and is always serialized back as an object. Use synchronous read-only context access such as `context.Get<T>("path")`. Legacy non-C# expression syntax and asynchronous constructs are invalid and must fail closed. Compile results use structured `ExpressionCompileFeedback`, not raw compiler text.

Rust+CEL is documented as a future fourth runtime route using the same root bindings, expression definitions, and detailed feedback contract. It is not Rust expression execution. Node.js and Python adapters must implement the same feedback contract before being marked supported.
