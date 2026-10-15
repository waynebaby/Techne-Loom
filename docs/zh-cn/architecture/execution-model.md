# 执行模型

[English](../../en/architecture/execution-model.md) | [根目录](../README.md)

执行模型围绕显式 workflow 状态、追加式历史，以及清晰的所有权接缝展开。

像 **weave out**、**weave back**、**strand**、**seam** 这样的 repo 级解释术语统一定义在 [Workflow 术语](workflow-terminology.md) 中。

## 共享运行时语义

- 从物化或加载 workflow instance 开始。
- 从当前节点出发，按顺序推进 transition group。
- 为状态变化、输出、等待、过期和失败记录历史事件。
- 将上下文更新持久化，而不是把状态偷偷塞进 prompt。

## AO 与 SO 的差异

- AO 是 decision-first：它应该在有意义的控制 seam 上 weave out，让顶层 agent 决定下一步。
- SO 是 execution-first：它应该沿着 SO 拥有的确定性步骤持续推进，直到终态或外部参与 seam。
- AO 允许在不确定场景下改写当前 workflow。
- SO 只应运行在完整物化后的 workflow 状态上。

## Step 分类

公开模型里已经带有未来 AO/SO 兼容 runtime 预期理解的 step kind：

- `ModelThink`
- `ToolCall`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `ConditionBranch`
- `WaitResume`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

## Wait 与 Resume

- 等待是第一类状态，不是隐式重试循环。runtime 一旦 weave out，就会显式停在那里，直到收到合法的 weave back。
- 恢复依赖结构化外部输入，而不是自由叙述。这个结构化回返面就是 weave-back surface。
- 每个 fan-out resume（成功或失败）都必须携带持久化 workflow 的 `activeWaitGroups` 中当前尝试对应的 `wait_id`；旧 id 会被拒绝。`execution_error` 用于报告外部执行失败，且不能同时携带非空结果 payload。runtime 会按 `maxRetries` 重试该 transition；重试耗尽后，等同组其他分支结束再统一路由。
- 等待过期会生成历史事件，并触发确定性的后续行为。
- 在当前公开 SO runtime 中，只要作者显式给出 timeout target，wait timeout 就可以把执行推进到该目标状态。

## 产品差异

- AO 会在主要控制 seam 处 weave out，让顶层 agent 决定下一步；协议层再把这个 seam 表达成带显式 boundary 字段的 blocked payload。
- SO 会一直运行到需要对外 weave out，或到达终态为止。
- SO 自己执行 memory read/write，并直接更新 workflow context，再产出 `memory_for_next_step`。

## 当前公开 runtime 行为

- `firstPass` 会按优先级顺序评估 guard 命中的 transition。guard 为 false 时继续检查下一个候选；命令结果未满足 `passExpression` 会让该 transition 失败，不会继续 fall through。`firstPass` 不支持 `noPassTargetStateId`。
- `whenAnyResponse`、`whenAnyPass`、`whenAllPass` 和 `whenExpression` 只会并发启动外部步骤；compile 会拒绝 fan-out 中的同步步骤，以及声明 `contractRefs` 的分支。
- `whenAnyPass` 即使其他分支重试耗尽，也会接受首个通过的分支。`whenAllPass` 会等待所有分支结束，再优先路由已耗尽的执行错误；没有执行错误时才走 no-pass，且只有所有分支都通过才会汇合。
- `whenExpression` 的谓词基于已完成分支的输出为 true 时可以提前汇合。若分支执行重试耗尽，则等待所有预期分支结束后再路由错误；编译器会拒绝可能汇合分支之间静态声明的重叠 `outputPath`/`outputBindings`，runtime 仍会兜底拒绝冲突且不发布部分输出。
- 旧 wire values `firstSuccess`、`firstResponse` 和 `all` 必须显式迁移为 canonical 策略值；runtime 不会静默升级。
- fan-out wait 超时后，会使用 `timeoutTargetStateId` 或嵌套的 `timeoutTransition.targetNodeId`，前提是目标状态有效。
- SO 的 no-progress 运行会被当成一个显式阻塞条件处理，而不是静默成功。
- 当前 `memory_for_next_step` 在没有命中 memory 相关键时，已经避免回退成 whole-context 泄露。
