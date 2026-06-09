# Skill-Driven Workflow Example

[中文](../../zh-cn/examples/skill-driven-workflow.md)

This example shows how SO owns deterministic execution while the caller owns external boundaries.

## Flow

1. The caller provides workflow input.
2. SO runs until it reaches an external boundary.
3. SO returns a boundary payload with the current step kind and required inputs.
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
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- run --workflow-file .\ask-workflow.json
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
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- resume --workflow-file .\ask-workflow.json --result-file .\resume.json
```

Expected final control payload excerpt:

```xml
<so_property>
{"type":"result","payload":{"status":"completed","current_node_id":"state.done"}}
</so_property>
```

## Practical Reading Of The Result

- The caller owns the external action between `run` and `resume`.
- SO owns the persisted workflow state before and after the boundary.
- The structured resume envelope is part of the public contract, not an implementation detail.
- The resume payload is only useful because the workflow also defines the post-boundary review and done path.
