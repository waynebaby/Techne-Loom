# Workflow 模型

[English](../../en/architecture/workflow-model.md)

共享 workflow 模型是语言中立的核心，AO 与 SO 可以在低层围绕它达成一致。

涉及 **weave out**、**weave back**、**strand**、**seam** 这类 repo 级解释性术语时，请先阅读 [Workflow 术语](workflow-terminology.md)。这一页保留的是中性的模型名词。

## 核心概念

- `WorkflowInstance`：一次执行的持久化状态。
- `StateNode`：拥有有序 transition group 的命名节点。
- `TransitionGroup`：按一种并发策略一起评估的一组 transition。
- `TransitionBase`：可执行、等待、分支或占位步骤的公共元数据。
- `WorkflowHistoryEntry`：记录推进、等待、输出与失败的追加式事件轨迹。

## 产品视角

- SO 执行的是完整物化后的 workflow。
- AO 允许逐步重写当前 workflow，但仍需持久化节点、产物与决策。
- 共享术语不代表共享顶层行为。
- 在当前公开 SO runtime 中，完整支持的 transition-group 策略是 `FirstSuccess`。`FirstResponse` 与 `All` 仍保留在模型表面，但一旦多 ready transition 真需要这些语义，runtime 会显式拒绝，而不是默默做错。

## 设计方向

公开模型旨在保留参考 task-tracking 设计中有价值的部分，同时不把私有产品依赖带进开源核心。
