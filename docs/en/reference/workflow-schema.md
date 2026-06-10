# Workflow Schema Reference

[中文](../../zh-cn/reference/workflow-schema.md)

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

## Sidecar Separation

- The workflow file is not the same thing as the CLI control payload.
- `<so_property>` carries public control metadata and is one of the current weave-out surfaces.
- `dotnet so.dll resume --result-file` consumes a separate structured JSON envelope. That envelope is the current weave-back sidecar.
- `.events.jsonl` beside the workflow file carries append-on-growth event history.

SO blocking payloads and AO control payloads remain separate product contracts built on top of shared low-level conventions.
