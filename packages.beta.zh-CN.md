# Beta Package Index（Beta 包索引）

[English](packages.beta.md) | [Released](packages.released.zh-CN.md)

这个页面用于 development / beta 通道获取。用户希望使用 development 分支行为或未发布包形态时，skills 和 agents 应把他引导到这里。

## 仓库引用

- Main 分支：`https://github.com/waynebaby/Techne-Loom/tree/main`
- Development 分支：`https://github.com/waynebaby/Techne-Loom/tree/development`

## GitHub Release Fallback

如果你想直接拿到最新 beta 包而不在本地重建，可以用下面这些链接。beta fallback release 会以 GitHub prerelease 的形式保存精确 prerelease `.nupkg` 与稳定的 `*.latest.nupkg` 别名。

- 最新 beta fallback release 页面：<https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-beta-latest>

## NuGet.org 最新版本

如果你想先确认 NuGet.org 上当前发布的最新 prerelease 版本号，再决定是否固定 `--version`，请先看对应的包页面。

- 打开 NuGet.org 包页面后，查看版本列表即可确认当前最新已发布的 prerelease。
- 如果只想获取最新 prerelease 而不固定版本号，可以使用 `dotnet add package <PackageId> --prerelease`。
- 如果需要固定精确 prerelease 版本，请先从 NuGet.org 复制版本号，再使用 `dotnet add package <PackageId> --version <latest-beta-version>`。

| 包名 | NuGet.org | 最新 beta 示例 |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions --prerelease` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common --prerelease` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator --prerelease` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator --prerelease` |

## 版本形态

- 当前仓库里所有可打包项目都从 `src/dotnet/Directory.Build.props` 继承 `VersionPrefix=0.1.0`。
- `development` 分支上的 beta 发布会把 `major.minor.<distance>-beta` 版本推到 NuGet.org，因此如果要固定精确最新版 prerelease 号，应先以 NuGet.org 页面为准。

## .NET

| 角色 | 包 / 源 | Beta 获取方式 | GitHub fallback | 示例 |
| --- | --- | --- | --- | --- |
| 抽象层 | `Techne.Loom.Abstractions` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet pack .\\src\\dotnet\\Techne.Loom.Abstractions` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.latest.nupkg) | 使用最新 prerelease `.nupkg` |
| 公共层 | `Techne.Loom.Common` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet pack .\\src\\dotnet\\Techne.Loom.Common` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Common.latest.nupkg) | 使用最新 prerelease `.nupkg` |
| 计划执行 runtime | `Techne.Loom.AgentOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet build .\\src\\dotnet\\Techne.Loom.AgentOrchestrator` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | 使用 development 构建产物运行 `dotnet ao.dll --guide` |
| skill 执行 runtime | `Techne.Loom.SkillOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` 后执行 `dotnet build .\\src\\dotnet\\Techne.Loom.SkillOrchestrator` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | 使用 development 构建产物运行 `dotnet so.dll --guide` |

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
