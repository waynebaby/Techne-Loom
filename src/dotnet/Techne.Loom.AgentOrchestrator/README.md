# Techne.Loom.AgentOrchestrator

## English

Exploratory orchestration CLI for Loom Agent Execution Orchestrator.

This package is the Loom Agent Execution Orchestrator runtime surface. It exposes the version-matched offline docs bundle, the compile entrypoint, Loom Agent Execution Orchestrator-owned prompt-plan/prompt-replan support surfaces, and CLI run/resume commands that emit machine-readable control payloads plus audit artifact links. Loom Agent Execution Orchestrator is CLI-only in this project.

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

### Guide output

Run the bare `dotnet ao.dll --guide` command. It reads the English `docs/en` tree shipped beside the executable in a complete runtime package and emits one JSON object with `version`, `docs_root`, and `guide_path`. The executable does not contain guide pages; a missing package docs tree is an error. Read `guide_path` first as the version truth; inspect `docs_root` only when the guide leaves a question unresolved. `--lang`, `--section`, and `--export` are no longer supported.

### Docs

- Loom Agent Execution Orchestrator guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/ao-guide.md>
- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/cli.md>
- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

## 中文

Loom Agent Execution Orchestrator 的探索式编排 CLI 包。

这个包是 Loom Agent Execution Orchestrator 的 runtime 表面。它暴露与版本匹配的离线英文文档包、compile 入口、Loom Agent Execution Orchestrator 自有的 prompt-plan / prompt-replan 支持表面，以及会返回机器可读控制载荷和审计 artifact links 的 run/resume CLI。Loom Agent Execution Orchestrator 在本项目里是 CLI-only。

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

### Guide 输出

运行不带额外参数的 `dotnet ao.dll --guide`。它会读取与可执行文件放在同一个完整 runtime package 中的英文 `docs/en` 文档树，并输出包含 `version`、`docs_root`、`guide_path` 的 JSON 对象。可执行文件本身不包含 guide 页面；如果 package docs 缺失，命令会报错。先读取 `guide_path` 作为当前版本真相；只有 guide 无法消除疑问时，才查看 `docs_root`。`--lang`、`--section` 与 `--export` 已不再支持。

### 文档

- Loom Agent Execution Orchestrator guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/ao-guide.md>
- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/cli.md>
- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
