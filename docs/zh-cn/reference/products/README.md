# 产品 Guide

[English](../../../en/reference/products/README.md) | [根目录](../../README.md)

- [Loom Agent Plan-Execution Orchestrator Guide Hub](../../guides/ao-guide.md)
  - [AO Flow](../../guides/ao-guide-flow.md)
  - [AO Guide 完整参考](../../guides/ao-guide-reference.md)
- [Loom Skill Orchestrator Guide Hub](../../guides/so-guide.md)
  - [SO Flow](../../guides/so-guide-flow.md)
  - [SO Guide 完整参考](../../guides/so-guide-reference.md)

这些作者文档为单个自包含 `loom` apphost 中的 Agent 和 Skill 模块提供指南。apphost 只有一个顶层 `loom --guide` 入口，并通过公开的 `loom agent` 和 `loom skill` 路由提供模块命令。

如果是为了继续实现而做 handoff，这两页就是整个文档树里最优先的产品级契约来源。

当前实现说明：

- Loom Skill Orchestrator 文档描述由 `loom skill` 暴露的独立 Skill 模块。
- Loom Agent Plan-Execution Orchestrator 文档描述由 `loom agent` 暴露的独立 Agent 模块。
