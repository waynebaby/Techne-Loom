# Workflow Schema 参考

[English](../../en/reference/workflow-schema.md)

canonical workflow schema 描述 SO 要执行的持久化 workflow file，以及未来 AO/SO 兼容 runtime 应能读取的结构。

## 核心元素

- instance 标识与版本
- node 集合与起始节点
- 当前节点与状态
- context map
- 历史记录条目
- wait group 与过期元数据
- artifact 引用

## 当前 Workflow File 形状

- 当前 workflow file 使用 camelCase 属性名。
- task node 存在一个以 node id 为 key 的 map 中。
- 多态条目通过 `$kind` 区分，例如 `state`、`command`、`expr`、`tbr`。
- `context` 是自由形状的，并且允许嵌套对象和数组。
- `activeWaitGroups` 是持久化 runtime state 的一部分，不是隐藏的进程内临时内存。

## 最小示例

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

## Runtime 说明

- 公开模型比当前公开 SO runtime 的真实实现更宽。
- 当前已 review 切片完整支持的 group strategy 是 `firstSuccess`。
- 对不支持的 multi-transition 策略场景，runtime 会显式失败，而不是静默降级。

## Sidecar 分层

- workflow file 不等于 CLI control payload。
- `<so_property>` 承载公开控制元数据。
- `so resume --result-file` 会读取一个独立的结构化 JSON envelope。
- workflow file 旁边的 `.events.jsonl` 保存 append-on-growth 的事件历史。

SO 的 blocking payload 与 AO 的 control payload 是建立在共享低层约定之上的独立产品契约。
