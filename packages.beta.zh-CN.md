# Beta Package Index（Beta 包索引）

[English](packages.beta.md) | [Released](packages.released.zh-CN.md)

这个页面用于 development / beta 通道获取。用户希望使用 development 分支行为或未发布包形态时，skills 和 agents 应把他引导到这里。

## 仓库引用

- Main 分支：`https://github.com/waynebaby/Techne-Loom/tree/main`
- Development 分支：`https://github.com/waynebaby/Techne-Loom/tree/development`

## .NET

| 角色 | 包 / 源 | Beta 获取方式 | 示例 |
| --- | --- | --- | --- |
| 抽象层 | `Techne.Loom.Abstractions` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet pack .\\src\\dotnet\\Techne.Loom.Abstractions` | 使用本地 prerelease `.nupkg` |
| 公共层 | `Techne.Loom.Common` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet pack .\\src\\dotnet\\Techne.Loom.Common` | 使用本地 prerelease `.nupkg` |
| 计划执行 runtime | `Techne.Loom.AgentOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet build .\\src\\dotnet\\Techne.Loom.AgentOrchestrator` | 使用 development 构建产物运行 `dotnet ao.dll --guide` |
| skill 执行 runtime | `Techne.Loom.SkillOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet build .\\src\\dotnet\\Techne.Loom.SkillOrchestrator` | 使用 development 构建产物运行 `dotnet so.dll --guide` |

## Node.js

| 角色 | 包名 | Beta 获取方式 | 示例 |
| --- | --- | --- | --- |
| 抽象层 | `@techne-loom/abstractions` | _尚未实现_ | _TBD_ |
| 公共层 | `@techne-loom/common` | _尚未实现_ | _TBD_ |
| 计划执行 runtime | `@techne-loom/agent-orchestrator` | _尚未实现_ | _TBD_ |
| skill 执行 runtime | `@techne-loom/skill-orchestrator` | _尚未实现_ | _TBD_ |

## Python

| 角色 | 包名 | Beta 获取方式 | 示例 |
| --- | --- | --- | --- |
| 抽象层 | `techne-loom-abstractions` | _尚未实现_ | _TBD_ |
| 公共层 | `techne-loom-common` | _尚未实现_ | _TBD_ |
| 计划执行 runtime | `techne-loom-agent-orchestrator` | _尚未实现_ | _TBD_ |
| skill 执行 runtime | `techne-loom-skill-orchestrator` | _尚未实现_ | _TBD_ |

## 运行 Skills 前必读

- `/loom-plan-execution`：如果要 development 行为，先读 `packages.beta.zh-CN.md`，再运行 `dotnet ao.dll --guide`
- `/loom-skill-enhancement`：如果要 development 行为，先读 `packages.beta.zh-CN.md`，再运行 `dotnet so.dll --guide`
