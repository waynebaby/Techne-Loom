# Techne Loom 文档

[English](../en/README.md)

这是公开文档集的中文入口。

## 从这里开始

- [快速开始](getting-started/README.md)
- [架构](architecture/README.md)
- [指南](guides/README.md)
- [参考](reference/README.md)
- [示例](examples/README.md)

## 如果要继续实现

- 优先阅读 [实现路线图](architecture/implementation-roadmap.md)。
- 把 [AgentOrchestrator Guide 源文档](reference/products/ao-guide.md) 和 [SkillOrchestrator Guide 源文档](reference/products/so-guide.md) 视为当前产品契约的 handoff 文档。
- 当前公开边界以 [架构](architecture/README.md) 和 [参考](reference/README.md) 为准。

## 当前基线

- 仓库中已经有 `Techne.Loom.Abstractions`、`Techne.Loom.Common` 与 `Techne.Loom.SkillOrchestrator` 的公开 `.NET` 切片。
- `Techne.Loom.AgentOrchestrator` 仍处于“文档已明确、代码仅 scaffold”的阶段。
- Node.js 与 Python 根目录目前仍只是后续对齐移植的保留位。

## 产品 Guide 源文档

- [AgentOrchestrator Guide 源文档](reference/products/ao-guide.md)
- [SkillOrchestrator Guide 源文档](reference/products/so-guide.md)

## 文档规则

- 每个成对页面都要在顶部链接到另一种语言版本。
- 英文和中文目录树保持镜像相对路径。
- 根治理文件保留在仓库根目录，长文档统一放在 `/docs`。
