# Techne.Loom.AgentOrchestrator

## English

Exploratory orchestration CLI for Loom Agent Execution Orchestrator.

This package is the Loom Agent Execution Orchestrator runtime surface. It exposes the guide surface, the compile entrypoint, Loom Agent Execution Orchestrator-owned prompt-plan/prompt-replan support surfaces, and CLI run/resume commands that emit machine-readable control payloads plus audit artifact links. Loom Agent Execution Orchestrator is CLI-only in this project.

### Install

```bash
dotnet add package Techne.Loom.AgentOrchestrator --version 0.1.0
```

### Primary entrypoints

- `dotnet ao.dll --guide`
- `dotnet ao.dll --help`
- `dotnet ao.dll compile`
- `dotnet ao.dll prompt-plan`
- `dotnet ao.dll prompt-replan`
- `dotnet ao.dll run`
- `dotnet ao.dll resume`

### Docs

- Loom Agent Execution Orchestrator guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/ao-guide.md>
- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/cli.md>
- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

## 中文

Loom Agent Execution Orchestrator 的探索式编排 CLI 包。

这个包是 Loom Agent Execution Orchestrator 的 runtime 表面。它暴露 guide surface、compile 入口、Loom Agent Execution Orchestrator 自有的 prompt-plan / prompt-replan 支持表面，以及会返回机器可读控制载荷和审计 artifact links 的 run/resume CLI。Loom Agent Execution Orchestrator 在本项目里是 CLI-only。

### 安装

```bash
dotnet add package Techne.Loom.AgentOrchestrator --version 0.1.0
```

### 主要入口

- `dotnet ao.dll --guide`
- `dotnet ao.dll --help`
- `dotnet ao.dll compile`
- `dotnet ao.dll prompt-plan`
- `dotnet ao.dll prompt-replan`
- `dotnet ao.dll run`
- `dotnet ao.dll resume`

### 文档

- Loom Agent Execution Orchestrator guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/ao-guide.md>
- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/cli.md>
- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
