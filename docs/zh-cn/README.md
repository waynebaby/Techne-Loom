# Techne Loom 文档

[English](../en/README.md) | [根目录](README.md)

这是公开文档集的中文入口。

## 从这里开始

- [快速开始](getting-started/README.md)
- [架构](architecture/README.md)
- [Skill 互操作层](architecture/skill-interoperability.md)
- [Workflow 术语](../en/architecture/workflow-terminology.md)
- [指南](guides/README.md)
- [参考](reference/README.md)
- [示例](examples/README.md)
- [文章与博客](articles/README.md)

## 如果要继续实现

- 优先阅读 [实现路线图](architecture/implementation-roadmap.md)。
- 在重写 workflow 解释类文档前，先阅读 [Workflow 术语](../en/architecture/workflow-terminology.md)。
- 将 [Loom Agent Plan-Execution Orchestrator Guide](guides/ao-guide.md) 和 [Loom Skill Orchestrator Guide](guides/so-guide.md) 作为统一 `loom` runtime 的模块契约参考。
- 当前公开契约面以 [架构](architecture/README.md) 和 [参考](reference/README.md) 为准。

## 当前基线

- 当前 .NET 发布集合包含八个自包含的 `Techne.Loom.Runtime.<rid>` package，以及一个使用相同精确版本和渠道的无 RID `Techne.Loom.Abstractions` SDK。
- `Techne.Loom.AgentOrchestrator` 与 `Techne.Loom.SkillOrchestrator` 仍是独立实现模块，分别通过 `loom agent` 和 `loom skill` 暴露。
- Node.js 与 Python 根目录目前仍只是后续对齐移植的保留位。

## 产品 Guide 源文档

- [使用 Techne Loom Skills](guides/skill-usage.md)
- [Loom Agent Plan-Execution Orchestrator Guide 源文档](guides/ao-guide.md)
- [Loom Skill Orchestrator Guide](guides/so-guide.md)
- [精选示例：受 Loom Skill Orchestrator 治理的 Skill 运行](examples/so-enhanced-skill-run.md)

## 文档规则

- 每个成对页面都要在顶部链接到另一种语言版本。
- 英文和中文目录树保持镜像相对路径。
- 根治理文件保留在仓库根目录，长文档统一放在 `/docs`。
