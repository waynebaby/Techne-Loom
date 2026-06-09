# 架构

[English](../../en/architecture/README.md)

Techne Loom 是一个 package-first mono-repo，并且刻意保持产品拆分。

这一节是公开仓库的 handoff 级架构来源。另一个 agent 应该能够只依靠这些页面继续实现，而不需要依赖隐藏的会话上下文。

## 架构地图

- `package-layout.md` 说明跨生态 package 矩阵。
- `workflow-model.md` 定义共享 workflow 词汇表。
- `execution-model.md` 说明推进、等待、恢复和事件语义。
- `cli-and-hosts.md` 定义 AO 与 SO 的 host 边界。
- `json-contract.md` 概述 canonical workflow 与 control payload 方向。
- `implementation-roadmap.md` 记录已批准的多切片计划、当前仓库状态和推荐的下一步切片。

## 来源权威

- 早期私有代码库中的精选 workflow-tracking 材料可以作为抽取与对照输入。
- 但不要把这类私有来源材料视为公开产品的规范源。
- 当前公开规范源是仓库代码、测试，以及 `/docs` 下的作者文档。
- AO 与 SO 可以共享低层词汇，但不共享同一个运行时层级。

## 当前实现状态

- v1 里唯一实现的 runtime 家族是 `.NET`。
- `Abstractions`、`Common`、`SkillOrchestrator` 已经有活跃的公开代码。
- `AgentOrchestrator` 当前主要仍处于“文档已明确、代码只到 scaffold”的阶段。
- Node.js 与 Python 根目录目前仍是未来对齐 package 的保留位。

目标不是让每个产品都长得一样，而是在保留独立产品身份的同时，让共享契约保持低层、可复用。
