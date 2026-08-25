# Beta Package Index（Beta 包索引）

[English](packages.beta.md) | [Released](packages.released.zh-CN.md)

这个页面用于 development / beta 通道获取。direct CLI 或手动调用者如果要使用 development 分支行为或未发布包形态，可以在这里选择 beta 通道；受治理的 AO / SO skill run 则应优先跟随当前由 CI/CD 管理的 skill package version block 或 checked-in runtime lock 已绑定的 runtime 版本，并只在需要时从该绑定版本推导 `released` 或 `beta`。

本地运行时选择规则：先执行平台检测与 host 启动预检。如果可用的 `Microsoft.NETCore.App 9.x` host 能启动精确版本的 IL bundle，就使用 framework-dependent 模式，并以同一版本恢复 `Techne.Loom.AgentOrchestrator` 或 `Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`。如果 host 不可用或无法启动 CLI，就为检测出的 RID 获取一个匹配的 self-contained single-file runtime package。self-contained 包无需预装 .NET runtime，但仍依赖目标 OS 与 ABI。两种模式使用相同的 CLI 与治理契约；请遵循[平台检测步骤](docs/zh-cn/reference/runtime/platform-detection.md)，并让所有命令复用返回的 launch descriptor。

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
- 如果精确 package id 和 version 已经已知，不要等 NuGet.org 页面、搜索结果或 registration 索引刷新后再判断包是否存在；应直接探测或下载精确 `.nupkg` URL。
- 精确版本直达包 URL 形态：`https://www.nuget.org/api/v2/package/<PackageId>/<Version>`

| 包名 | NuGet.org | 最新 beta 示例 |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions --prerelease` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common --prerelease` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator --prerelease` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator --prerelease` |

直达包检查示例：

```text
https://www.nuget.org/api/v2/package/Techne.Loom.AgentOrchestrator/0.3.234-beta
https://www.nuget.org/api/v2/package/Techne.Loom.SkillOrchestrator/0.3.234-beta
```

## 版本形态

<!-- package-version-block:start -->
- 当前最新已发布的 beta 包版本是 `0.3.234-beta`。
- `development` 分支上的 beta 发布会把 `major.minor.<distance>-beta` 版本推到 NuGet.org，其中 `<distance>` 表示 GitVersion 相对当前版本源的提交距离。
<!-- package-version-block:end -->



















## .NET

<!-- package-dotnet-block:start -->
| 角色 | 包 / 源 | Beta 获取方式 | GitHub fallback | 示例 |
| --- | --- | --- | --- | --- |
| 抽象层 | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.3.234-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.latest.nupkg) | 使用精确最新 prerelease |
| 公共层 | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.3.234-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Common.latest.nupkg) | 使用精确最新 prerelease |
| 计划执行 runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.3.234-beta`，并同时恢复 `Techne.Loom.Common` 与 `Techne.Loom.Abstractions` 的 `0.3.234-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | 使用精确最新 prerelease AO runtime bundle |
| skill 执行 runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.3.234-beta`，并同时恢复 `Techne.Loom.Common` 与 `Techne.Loom.Abstractions` 的 `0.3.234-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | 使用精确最新 prerelease SO runtime bundle |
<!-- package-dotnet-block:end -->

## 运行时包族

self-contained runtime 包族不是第四个治理产品，而是同一 AO 或 SO CLI 的另一种宿主载体。当前仓库快照可能早于这些包的实际发布；CI/CD 会在发布时填入实际精确版本、asset URL 和 SHA-512。不要在这里虚构 runtime 版本或 hash。

| RID | AO runtime package | SO runtime package | 固定入口 |
| --- | --- | --- | --- |
| `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `tools/win-x64/ao.exe` / `tools/win-x64/so.exe` |
| `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `tools/win-arm64/ao.exe` / `tools/win-arm64/so.exe` |
| `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `tools/linux-x64/ao` / `tools/linux-x64/so` |
| `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `tools/linux-arm64/ao` / `tools/linux-arm64/so` |
| `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `tools/linux-musl-x64/ao` / `tools/linux-musl-x64/so` |
| `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `tools/linux-musl-arm64/ao` / `tools/linux-musl-arm64/so` |
| `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `tools/osx-x64/ao` / `tools/osx-x64/so` |
| `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `tools/osx-arm64/ao` / `tools/osx-arm64/so` |

完整矩阵是 AO x 8 加 SO x 8，共 16 个 runtime PackageId：

- AO：表中每个 RID 对应一个 `Techne.Loom.AgentOrchestrator.Runtime.<rid>`。
- SO：表中每个 RID 对应一个 `Techne.Loom.SkillOrchestrator.Runtime.<rid>`。

runtime family 的 beta GitHub fallback alias：

- AO：[win-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.win-x64.latest.nupkg)、[win-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.win-arm64.latest.nupkg)、[linux-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-x64.latest.nupkg)、[linux-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-arm64.latest.nupkg)、[linux-musl-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64.latest.nupkg)、[linux-musl-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64.latest.nupkg)、[osx-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-x64.latest.nupkg)、[osx-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-arm64.latest.nupkg)。
- SO：[win-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.win-x64.latest.nupkg)、[win-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.win-arm64.latest.nupkg)、[linux-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-x64.latest.nupkg)、[linux-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-arm64.latest.nupkg)、[linux-musl-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64.latest.nupkg)、[linux-musl-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64.latest.nupkg)、[osx-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-x64.latest.nupkg)、[osx-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-arm64.latest.nupkg)。

对绑定的精确版本使用 NuGet.org V3 flat-container URL。package id 使用小写，版本使用规范化后的精确版本：

```text
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg.sha512
```

手动获取 package 时，NuGet.org V2 精确版本 URL 仍然是：

```text
https://www.nuget.org/api/v2/package/<PackageId>/<exact-version>
```

`beta` 通道的官方 GitHub fallback 使用相同 product、version 和 RID 的 package，并且必须通过相同校验：

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.<exact-version>.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.latest.nupkg
```

请按[平台检测步骤](docs/zh-cn/reference/runtime/platform-detection.md)执行 host 预检、RID 选择、SHA-512 校验、ZIP 安全、缓存隔离和 launch descriptor 连续性。stable/beta 发布必须把这 16 个包与现有 4 个包作为统一矩阵发布，共 20 个包。



















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

- `/loom-plan-execution`：如果是 direct CLI / 手动获取，或受治理 runtime 版本已经解析到 `beta`，先读 `packages.beta.zh-CN.md`，再运行来自 Loom Agent Execution Orchestrator runtime bundle 的 `dotnet ao.dll --guide`
- `/loom-skill-enhancement`：如果是 direct CLI / 手动获取，或受治理 runtime 版本已经解析到 `beta`，先读 `packages.beta.zh-CN.md`，再运行 `dotnet so.dll --guide`
