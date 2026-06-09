# Skill 驱动 Workflow 示例

[English](../../en/examples/skill-driven-workflow.md)

这个示例说明 SO 如何拥有确定性执行，而调用方如何拥有外部边界。

## 流程

1. 调用方提供 workflow 输入。
2. SO 运行到外部 boundary。
3. SO 返回包含当前 step kind 和 required inputs 的 boundary payload。
4. 调用方执行外部动作，并送回结构化的 resume envelope。
5. SO 用恢复后的 context 做判断，并确定性地结束在 done state。

## 可恢复的 Workflow File

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

## 第一次运行

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- run --workflow-file .\ask-workflow.json
```

预期控制载荷摘录：

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

## Resume 命令

```powershell
dotnet run --project .\src\dotnet\Techne.Loom.SkillOrchestrator\Techne.Loom.SkillOrchestrator.csproj -- resume --workflow-file .\ask-workflow.json --result-file .\resume.json
```

预期最终控制载荷摘录：

```xml
<so_property>
{"type":"result","payload":{"status":"completed","current_node_id":"state.done"}}
</so_property>
```

## 如何理解这个结果

- `run` 和 `resume` 之间的外部动作由调用方拥有。
- 边界前后的持久化 workflow state 由 SO 拥有。
- 结构化 resume envelope 是公开契约的一部分，不是内部细节。
- resume payload 之所以有意义，是因为 workflow 同时定义了 boundary 之后的 review 与 done 路径。
