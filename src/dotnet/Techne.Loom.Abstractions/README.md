# Techne.Loom.Abstractions

## English

Public workflow and orchestration contracts for Techne Loom.

This assembly owns standalone Ask questions, answers, receipts, endpoints, the
`IStandaloneAskSessionService` interface, and workflow I/O contracts. It also
owns the public AO/SO CLI and analysis payloads, command-stream results, and MCP
tool, session, and configuration contracts. It depends only on .NET libraries,
not Common, Roslyn, or either executable. Existing public type namespaces are
retained for compatibility: a `Techne.Loom.Common` namespace does not imply that
the type still belongs to the Common assembly. Existing Common and SO public
contract names are forwarded to this assembly where compatibility requires it.

The contract package does not run an HTTP worker. External applications invoke
the matching AO or SO self-contained apphost (`ask start --contract-file`, then
`ask result --ask-id`) and deserialize its output with these contracts. Do not
instantiate the Common standalone service in an arbitrary host expecting it to
launch a product worker automatically. Confirm the installed apphost `--help` lists both Ask commands; support depends on its package version.

Use this package when you need the shared model types and contracts without taking a dependency on the AO or SO runtime package.

### Install

```bash
dotnet add package Techne.Loom.Abstractions --version 0.1.0
```

### Package role

- shared workflow model types
- contract-first integration surface
- reusable JSON-serializable abstractions

### Docs

- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Repository guide: <https://github.com/waynebaby/Techne-Loom/blob/main/README.md>

## 中文

Techne Loom 的公开 workflow / orchestration 契约包。

该程序集拥有独立 Ask 的问题、回答、回执、端点、`IStandaloneAskSessionService`
接口及 workflow 输入输出契约，也拥有 AO/SO CLI 与分析结果、命令流结果，以及 MCP
工具、会话和配置契约。它仅依赖 .NET 基础库，不依赖 Common、Roslyn 或可执行程序。
已有公开类型保留原命名空间以兼容调用方；命名空间带 `Techne.Loom.Common`
不代表类型仍属于 Common 程序集。为兼容旧调用方，Common 与 SO 中已有的公开契约名
会按需要转发到此程序集。

契约包不托管 HTTP worker。外部应用调用匹配的 AO 或 SO 自包含 apphost
（`ask start --contract-file`，随后 `ask result --ask-id`），使用本包反序列化输出。
不要在任意宿主中实例化 Common standalone 服务并假设它会自动启动产品 worker；使用前请检查已安装 apphost 的 `--help` 是否同时列出这两个 Ask 命令，因支持情况取决于具体发布版本。

当你只需要共享模型类型与契约，而不想直接依赖 AO / SO runtime 包时，使用这个包。

### 安装

```bash
dotnet add package Techne.Loom.Abstractions --version 0.1.0
```

### 包职责

- 共享 workflow 模型类型
- 契约优先的集成表面
- 可复用的 JSON 可序列化抽象

### 文档

- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
- 仓库总览：<https://github.com/waynebaby/Techne-Loom/blob/main/README.zh-CN.md>
