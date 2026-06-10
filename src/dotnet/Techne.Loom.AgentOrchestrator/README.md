# Techne.Loom.AgentOrchestrator

## English

Exploratory orchestration host and CLI for Techne Loom AgentOrchestrator.

This package is the AO-facing runtime surface. It exposes the official `MCP/stdio` host, the guide surface, the planner/compile entrypoint, and CLI run/resume commands that emit machine-readable control payloads plus audit artifact links.

### Install

```bash
dotnet add package Techne.Loom.AgentOrchestrator --version 0.1.0
```

### Primary entrypoints

- `dotnet ao.dll --guide`
- `dotnet ao.dll --help`
- `dotnet ao.dll planner`
- `dotnet ao.dll compile`
- `dotnet ao.dll host`
- `dotnet ao.dll run`
- `dotnet ao.dll resume`

### Docs

- Product guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/ao-guide.md>
- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/cli.md>
- MCP reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/mcp.md>
- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

## 中文

Techne Loom AgentOrchestrator 的探索式编排宿主与 CLI 包。

这个包是 AO 面向外部的 runtime 表面。它暴露官方 `MCP/stdio` 宿主、guide surface、planner/compile 入口，以及会返回机器可读控制载荷和审计 artifact links 的 run/resume CLI。

### 安装

```bash
dotnet add package Techne.Loom.AgentOrchestrator --version 0.1.0
```

### 主要入口

- `dotnet ao.dll --guide`
- `dotnet ao.dll --help`
- `dotnet ao.dll planner`
- `dotnet ao.dll compile`
- `dotnet ao.dll host`
- `dotnet ao.dll run`
- `dotnet ao.dll resume`

### 文档

- 产品 guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/ao-guide.md>
- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/cli.md>
- MCP 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/mcp.md>
- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
