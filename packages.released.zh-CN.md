# Released Package Index（稳定包索引）

[English](packages.released.md) | [Beta](packages.beta.zh-CN.md)

这个页面用于稳定 / release 通道的包获取。skills 和 agents 在开始配置或执行前，应先让用户选择包通道。

本地运行时 bundle 规则：不要只恢复 runtime 主包。AO runtime 获取必须同时下载 `Techne.Loom.AgentOrchestrator` + `Techne.Loom.Common` + `Techne.Loom.Abstractions`；SO runtime 获取和 target skill 日常恢复必须同时下载 `Techne.Loom.SkillOrchestrator` + `Techne.Loom.Common` + `Techne.Loom.Abstractions`，并保持三者使用同一 released 版本。

## 仓库引用

- Main 分支：`https://github.com/waynebaby/Techne-Loom/tree/main`
- Development 分支：`https://github.com/waynebaby/Techne-Loom/tree/development`

## GitHub Release Fallback

当 NuGet 源不可用时，使用下面这些链接。稳定通道 fallback release 同时保存精确版本的 `.nupkg` 与稳定的 `*.latest.nupkg` 别名。

- 最新稳定 fallback release 页面：<https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-stable-latest>

## NuGet.org 最新版本

如果你想先确认 NuGet.org 上当前发布的最新稳定版本号，再决定是否固定 `--version`，请先看对应的包页面。

- NuGet.org 的包页面顶部会显示当前最新稳定版本。
- 如果不需要固定精确版本，可以直接使用 `dotnet add package <PackageId>`，它会从 NuGet.org 解析最新稳定包。
- 如果需要固定精确版本，请先从 NuGet.org 复制最新稳定版本号，再使用 `dotnet add package <PackageId> --version <latest-stable-version>`。

| 包名 | NuGet.org | 最新稳定示例 |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator` |

## 版本形态

<!-- package-version-block:start -->
- 当前最新已发布的稳定包版本是 `0.2.25`。
- `main` 分支上的稳定发布会按当前仓库策略把 `major.minor.<distance>` 版本推到 NuGet.org。
<!-- package-version-block:end -->

## .NET

<!-- package-dotnet-block:start -->
| 角色 | 包名 | 稳定获取方式 | GitHub fallback | 示例 |
| --- | --- | --- | --- | --- |
| 抽象层 | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.2.25` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Abstractions.latest.nupkg) | `using Techne.Loom.Abstractions.TaskTracking.Model;` |
| 公共层 | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.2.25` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Common.latest.nupkg) | `var json = WorkflowJsonSerializer.Serialize(instance);` |
| 计划执行 runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.2.25`，并同时恢复 `Techne.Loom.Common` 与 `Techne.Loom.Abstractions` 的 `0.2.25` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | 使用 AO runtime bundle 运行 `dotnet ao.dll --guide` |
| skill 执行 runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.2.25`，并同时恢复 `Techne.Loom.Common` 与 `Techne.Loom.Abstractions` 的 `0.2.25` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | 使用 SO runtime bundle 运行 `dotnet so.dll --guide` |
<!-- package-dotnet-block:end -->

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
