# 架构

[English](../../en/architecture/README.md) | [根目录](../README.md)

Techne Loom 是一个 package-first mono-repo，并且刻意保持产品拆分。

这一节是公开仓库的 handoff 级架构来源。另一个 agent 应该能够只依靠这些页面继续实现，而不需要依赖隐藏的会话上下文。

## 架构地图

- `workflow-terminology.md` 定义整个 repo 级的 workflow 术语根文档。
- `package-layout.md` 说明跨生态 package 矩阵。
- `workflow-model.md` 定义共享的中性 workflow 模型术语与 schema 语义。
- `execution-model.md` 说明推进、等待、恢复和事件语义。
- `skill-interoperability.md` 说明 Agent Skills 互操作层、当前证据和产品边界。
- `cli-and-hosts.md` 定义 AO 与 SO 的 host surface。
- `json-contract.md` 概述 canonical workflow 与 control payload 方向。
- `contract-context-reference.zh-CN.md` 定义 B+ contract binding、bounded fragment 注入、runtime metadata 与手动修改行为。
- `implementation-roadmap.md` 记录当前基础与下一阶段互操作切片。

## 来源权威

- 历史 workflow-tracking 材料可以作为抽取与对照输入，但公开规范源仍是仓库代码、测试与已编写文档。
- 不要把任何非公开来源的实现或文档复制到本仓库。
- 当前公开规范源是仓库代码、测试，以及 `/docs` 下的作者文档。
- AO 与 SO 可以共享低层词汇，但不共享同一个运行时层级。

## 当前实现状态

- v1 里唯一实现的 runtime 家族是 `.NET`。
- `Abstractions`、`Common`、`SkillOrchestrator` 已经有活跃的公开代码。
- `AgentOrchestrator` 现在也已经有可运行的公开 `.NET` runtime 切片。
- Node.js 与 Python 根目录目前仍是未来对齐 package 的保留位。

目标不是让每个产品都长得一样，而是在保留独立产品身份的同时，让共享契约保持低层、可复用。
