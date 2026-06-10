# Released Package Index（稳定包索引）

[English](packages.released.md) | [Beta](packages.beta.zh-CN.md)

这个页面用于稳定 / release 通道的包获取。skills 和 agents 在开始配置或执行前，应先让用户选择包通道。

## 仓库引用

- Main 分支：`https://github.com/waynebaby/Techne-Loom/tree/main`
- Development 分支：`https://github.com/waynebaby/Techne-Loom/tree/development`

## .NET

| 角色 | 包名 | 稳定获取方式 | 示例 |
| --- | --- | --- | --- |
| 抽象层 | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.1.0` | `using Techne.Loom.Abstractions.TaskTracking.Model;` |
| 公共层 | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.1.0` | `var json = WorkflowJsonSerializer.Serialize(instance);` |
| 计划执行 runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.1.0` | `dotnet ao.dll --guide` |
| skill 执行 runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.1.0` | `dotnet so.dll --guide` |

## Node.js

| 角色 | 包名 | 稳定获取方式 | 示例 |
| --- | --- | --- | --- |
| 抽象层 | `@techne-loom/abstractions` | _尚未实现_ | _TBD_ |
| 公共层 | `@techne-loom/common` | _尚未实现_ | _TBD_ |
| 计划执行 runtime | `@techne-loom/agent-orchestrator` | _尚未实现_ | _TBD_ |
| skill 执行 runtime | `@techne-loom/skill-orchestrator` | _尚未实现_ | _TBD_ |

## Python

| 角色 | 包名 | 稳定获取方式 | 示例 |
| --- | --- | --- | --- |
| 抽象层 | `techne-loom-abstractions` | _尚未实现_ | _TBD_ |
| 公共层 | `techne-loom-common` | _尚未实现_ | _TBD_ |
| 计划执行 runtime | `techne-loom-agent-orchestrator` | _尚未实现_ | _TBD_ |
| skill 执行 runtime | `techne-loom-skill-orchestrator` | _尚未实现_ | _TBD_ |

## 运行 Skills 前必读

- `/loom-plan-execution`：先读 `packages.released.zh-CN.md` 或 `packages.beta.zh-CN.md`，再读 `docs/zh-cn/reference/products/ao-guide.md`
- `/loom-skill-enhancement`：先读 `packages.released.zh-CN.md` 或 `packages.beta.zh-CN.md`，再读 `docs/zh-cn/reference/products/so-guide.md`
