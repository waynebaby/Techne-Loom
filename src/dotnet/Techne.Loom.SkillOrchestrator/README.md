# Techne.Loom.SkillOrchestrator

## English

Deterministic workflow execution and tracking for Techne Loom skills.

This package is the SO-facing runtime surface. It exposes the guide surface, the compile/run/resume/status/inspect entrypoints, and workflow audit artifacts that capture Mermaid Markdown, HTML, and workflow JSON backups step by step. `compile` validates an existing workflow JSON directly.

### Install

```bash
dotnet add package Techne.Loom.SkillOrchestrator --version 0.1.0
```

### Primary entrypoints

- `dotnet so.dll --guide`
- `dotnet so.dll --help`
- `dotnet so.dll compile`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`

### Docs

- Product guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md>
- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/cli.md>
- Skill I/O reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/skills.md>
- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

## 中文

Techne Loom SkillOrchestrator 的确定型 workflow 执行与跟踪包。

这个包是 SO 面向外部的 runtime 表面。它暴露 guide surface、compile/run/resume/status/inspect 入口，以及按 step 保存 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact。`compile` 只负责直接校验已有 workflow JSON。

### 安装

```bash
dotnet add package Techne.Loom.SkillOrchestrator --version 0.1.0
```

### 主要入口

- `dotnet so.dll --guide`
- `dotnet so.dll --help`
- `dotnet so.dll compile`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`

### 文档

- 产品 guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/so-guide.md>
- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/cli.md>
- Skill 输入输出参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/skills.md>
- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
