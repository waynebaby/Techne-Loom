# 执行模型

[English](../../en/architecture/execution-model.md)

执行模型围绕显式 workflow 状态、追加式历史，以及清晰的职责边界展开。

## 共享运行时语义

- 从物化或加载 workflow instance 开始。
- 从当前节点出发，按顺序推进 transition group。
- 为状态变化、输出、等待、过期和失败记录历史事件。
- 将上下文更新持久化，而不是把状态偷偷塞进 prompt。

## AO 与 SO 的差异

- AO 是 decision-first：它应该在有意义的控制边界返回，让顶层 agent 决定下一步。
- SO 是 execution-first：它应该沿着 SO 拥有的确定性步骤持续推进，直到终态或外部参与边界。
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

- 等待是第一类状态，不是隐式重试循环。
- 恢复依赖结构化外部输入，而不是自由叙述。
- 等待过期会生成历史事件，并触发确定性的后续行为。
- 在当前公开 SO runtime 中，只要作者显式给出 timeout target，wait timeout 就可以把执行推进到该目标状态。

## 产品差异

- AO 在主要控制边界返回，让顶层 agent 决定下一步。
- SO 会一直运行到需要外部参与或到达终态为止。
- SO 自己执行 memory read/write，并直接更新 workflow context，再产出 `memory_for_next_step`。

## 当前公开 runtime 的限制

- 当前公开 SO runtime 中，完整支持的 transition-group 策略是 `FirstSuccess`。
- `FirstResponse` 与 `All` 仍然保留在模型层里，但一旦多 ready transition 真需要这些语义，当前公开 runtime 会显式失败。
- SO 的 no-progress 运行会被当成一个边界条件处理，而不是静默成功。
- 当前 `memory_for_next_step` 在没有命中 memory 相关键时，已经避免回退成 whole-context 泄露。
