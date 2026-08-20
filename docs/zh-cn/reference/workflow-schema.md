# Workflow Schema 参考

[English](../../en/reference/workflow-schema.md) | [根目录](../README.md)

canonical workflow schema 描述 SO 要执行的持久化 workflow file，以及未来 AO/SO 兼容 runtime 应能读取的结构。

像 **pattern**、**strand**、**weave out**、**weave back** 这样的 repo 级解释术语定义在 [Workflow 术语](../architecture/workflow-terminology.md) 中。

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
- transition 条目使用 `stepKind` 和 owned-input 元数据作为分析与可视化的稳定语义输入。Mermaid renderer 会从这些字段同时推导浅色节点背景与稳定 emoji 标签：`🔎` AI/model/subagent 工作用绿色，`⚙️` 代码/工具工作用蓝色，`💬` user-owned 的可选分支决策用黄色，`🚧` 必须用户输入用红色，`❓` 一般条件分支用琥珀黄/浅黄，`📜` gate/governance 状态用白色或极浅灰色。
- 每个 `state` 节点都必须声明一个非空的 `workflowPhase`。这个字段表示该节点属于整个 workflow 的哪个阶段，compile 和可视化都会用它来确定 Mermaid 泳道分组。不要把它当成可选装饰字段。
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
- `dotnet so.dll compile`、`run` 与 `resume` 会在 audit step 目录下写出 `workflow.analysis.json`。该 artifact 从 workflow file 推导，汇总所需输入、发布的输出族、branch、loop、用户 seam、运行时 seam、gate 与图灵完备控制风险。
- `dotnet so.dll compile` 会拒绝任何缺少 `workflowPhase`、或把它写成 null、空字符串、纯空白的 state 节点。错误输出应指出 state node id、`workflowPhase` 字段路径，以及修复建议，明确说明这个字段的含义是“该节点处于整个 workflow 的哪个阶段”。

## Sidecar 分层

- workflow file 不等于 CLI control payload。
- `<so_property>` 承载公开控制元数据，也是当前 weave-out surface 之一。
- `dotnet so.dll resume --result-file` 会读取一个独立的结构化 JSON envelope。这个 envelope 就是当前的 weave-back sidecar。
- workflow file 旁边的 `.events.jsonl` 保存 append-on-growth 的事件历史。

SO 的 blocking payload 与 AO 的 control payload 是建立在共享低层约定之上的独立产品契约。


## ExpressionDefinition 表达式定义

workflow 根部声明 `runtimeBinding` 与 `expressionBinding`。当前 .NET binding 是 C# + Roslyn，并使用 `compileFeedbackContract: "detailedCompileFeedbackV1"`。谓词字段使用带 `kind`、`source`、`entryPoint`、`resultType` 的结构化 `ExpressionDefinition`；只有显式 C# binding 存在时才读取字符串 shorthand，写出时始终变为对象。使用 `context.Get<T>("path")` 等同步只读 context API。legacy 非 C# 语法与异步构造非法，必须 fail closed。compile 结果必须使用结构化 `ExpressionCompileFeedback`，不能只透传 compiler 原文。

Rust+CEL 被记录为未来第四条 runtime 路线，复用同一 root binding、expression definition 与详细 feedback contract；它不是执行 Rust 表达式。Node.js 与 Python adapter 在实现同一 feedback contract 前不得标记为 supported。
