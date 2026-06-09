# First Workflow

[中文](../../zh-cn/getting-started/first-workflow.md)

The shortest useful first workflow is an SO-owned deterministic flow.

## Path 1: Fastest Runnable Path

Use the built-in shorthand first:

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- ls .
```

What it does:

1. Compiles the shorthand into a workflow.
2. Runs a wrapped command-line directory listing.
3. Writes the completed workflow state to a temp workflow file.
4. Emits a final `<so_property>` result block.

## Path 2: Authored Workflow File

Use this when you want to see the current public workflow contract directly.

Minimal workflow file:

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

Run it with:

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- run --workflow-file .\workflow.json
```

## Path 3: Blocked Workflow Plus Resume

A resumable boundary workflow example:

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

First run:

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- run --workflow-file .\ask-workflow.json
```

Expected SO property shape on the first run:

```xml
<so_property>
{"type":"boundary","payload":{"status":"blocked","current_step_kind":"AskUser","required_inputs":["filePath","content"]}}
</so_property>
```

Resume sidecar:

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

Resume command:

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- resume --workflow-file .\ask-workflow.json --result-file .\resume.json
```

Expected SO property shape after resume:

```xml
<so_property>
{"type":"result","payload":{"status":"completed","current_node_id":"state.done"}}
</so_property>
```

## Why These Three Paths Matter

1. Compile a shorthand request or write a small workflow JSON.
2. Run SO until blocked or finished.
3. If SO blocks, execute the requested external step and resume with a structured result envelope.

## Minimal Direction

- Start with a local deterministic tool step.
- Add one explicit state update.
- End with one captured result payload. Add a separate `ArtifactEmit` step only when your workflow truly needs one.

The examples section shows a narrative version of this same flow.
