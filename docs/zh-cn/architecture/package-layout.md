# 包布局

[English](../../en/architecture/package-layout.md) | [根目录](../README.md)

Techne Loom 保持实现模块相互独立，同时发布统一的 runtime package 系列。

## 当前 .NET 发布集合

当前 .NET 发布集合包含八个自包含的 `Techne.Loom.Runtime.<rid>` package，每个受支持 RID 一个；另有一个无 RID 的 `Techne.Loom.Abstractions` 契约 SDK。九个 package 使用完全相同的版本和发布渠道。`Techne.Loom.AgentOrchestrator` 与 `Techne.Loom.SkillOrchestrator` 仍是独立、可构建的实现模块，但不再是单独的 runtime package 系列。

| 已发布 package | 范围 |
| --- | --- |
| `Techne.Loom.Runtime.<rid>` | 统一的 `loom` apphost、Agent 和 Skill 模块命令、共享运行时及 RID 专属自包含依赖 |
| `Techne.Loom.Abstractions` | 公开 workflow/task 契约及版本匹配的 SDK |

支持的 RID 为 `win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`linux-musl-x64`、`linux-musl-arm64`、`osx-x64` 和 `osx-arm64`。

## 实现模块

| 模块 | 职责 | 公开入口 |
| --- | --- | --- |
| `Techne.Loom.AgentOrchestrator` | Agent 计划、重规划、workflow 执行与继续 | `loom agent ...` |
| `Techne.Loom.SkillOrchestrator` | Skill 治理下的 workflow 编写、验证、执行与继续 | `loom skill ...` |

Agent 与 Skill 模块保留独立的 workflow 语义，不会互相调用，也不构成父子 runtime。它们共享已发布的 `loom` apphost 与 package 发布边界。

## 共享运行时能力

| 能力 | 职责 | 公开入口 |
| --- | --- | --- |
| 共享 XO Ask | 与 workflow 无关的结构化提问 | `loom ask ...` |

Shared XO Ask 是 `loom` runtime 提供的共享能力，不是第三个实现模块，也不形成独立 package 系列。

## 未来生态 package

`src/nodejs` 和 `src/python` 仍为未来生态工作预留。其 package 名称和调用方式目前只是规划，不属于当前 .NET 发布集合。

## 依赖与边界规则

- `Techne.Loom.Abstractions` 保持协议中立。
- `Techne.Loom.Common` 承载可复用运行时辅助，不是单独发布的 apphost 系列。
- 当协议适配器具有产品或传输特性时，应将其放在公开核心之外。
- 当前发布版本和资产以 package 索引与精确版本 runtime lock 为准。
