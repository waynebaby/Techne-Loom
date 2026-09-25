# Released Runtime 包索引

[English](packages.released.md) | [Beta](packages.beta.zh-CN.md)

本页列出已发布的 stable runtime packages。AO 与 SO 只发布自包含的 product+RID 包；源项目仍可在仓库中构建和测试，但不属于 NuGet runtime package。

## 当前发布集合

活动集合恰好包含 16 个包：AO 覆盖 8 个 RID，SO 覆盖 8 个 RID。根据操作系统、CPU 架构和 Linux libc 选择唯一一行。

| RID | AO package | SO package | Apphost |
| --- | --- | --- | --- |
| `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `ao.exe`、`so.exe` |
| `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `ao.exe`、`so.exe` |
| `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `ao`、`so` |
| `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `ao`、`so` |
| `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `ao`、`so` |
| `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `ao`、`so` |
| `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `ao`、`so` |
| `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `ao`、`so` |

## 版本

<!-- package-version-block:start -->
- 当前最新已发布的 stable runtime 版本是 `0.3.321`。
- stable 发布会读取活动 runtime packages 的最高数值版本和单独保存的 retired-core high-water，再生成下一个 patch；`main` 不追加 prerelease 后缀。
<!-- package-version-block:end -->

使用上面的精确版本。不要请求 `latest`、版本范围或其他 RID。

## 安装命令

<!-- package-dotnet-block:start -->
| 产品 | RID | Runtime package | NuGet 获取方式 | GitHub 回退 |
| --- | --- | --- | --- | --- |
| AO | `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.win-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.win-x64.0.3.321.nupkg) |
| AO | `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.win-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.win-arm64.0.3.321.nupkg) |
| AO | `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-x64.0.3.321.nupkg) |
| AO | `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-arm64.0.3.321.nupkg) |
| AO | `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64.0.3.321.nupkg) |
| AO | `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64.0.3.321.nupkg) |
| AO | `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.osx-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-x64.0.3.321.nupkg) |
| AO | `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.osx-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-arm64.0.3.321.nupkg) |
| SO | `win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.win-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.win-x64.0.3.321.nupkg) |
| SO | `win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.win-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.win-arm64.0.3.321.nupkg) |
| SO | `linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-x64.0.3.321.nupkg) |
| SO | `linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-arm64.0.3.321.nupkg) |
| SO | `linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64.0.3.321.nupkg) |
| SO | `linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64.0.3.321.nupkg) |
| SO | `osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.osx-x64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-x64.0.3.321.nupkg) |
| SO | `osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.osx-arm64 --version 0.3.321` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-arm64.0.3.321.nupkg) |
<!-- package-dotnet-block:end -->

## 获取与校验

宿主先根据系统、架构和 Linux libc 检测一个受支持的 RID，再获取该产品唯一对应的精确版本包。优先复用标准 NuGet global-packages cache 中通过校验的包；否则下载 NuGet 精确版本，并与 registration 响应中的 `catalogEntry.packageHash` 比较。GitHub 回退只允许使用相同 package/version 且 `.sha512` sidecar 校验通过的文件。

NuGet 精确版本 URL：

```text
https://www.nuget.org/api/v2/package/<PackageId>/0.3.321
```

GitHub package 与校验文件使用相同文件名：

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.0.3.321.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.0.3.321.nupkg.sha512
```

解压前校验 package id、精确版本、RID、SHA-512、nuspec、`tools/<rid>/runtime.json`、压缩包路径与大小、apphost 和英文 guide 文件。拒绝不匹配或不安全的 ZIP 条目，并解压到每次运行专用的外部目录。

## 第一个命令

解压后，直接运行 apphost 的 `--guide`，这是第一个 runtime 操作。确认版本和 guide 文件可读后，再使用同一个 apphost 生成 schema/demo、compile、run 和 resume。

```powershell
.\so.exe --guide
.\ao.exe --guide
```

Unix 使用不带 `.exe` 的 `so --guide` 或 `ao --guide`。不需要安装 .NET host、framework-dependent DLL package、runtime resolver、launch descriptor 或固定 bootstrap script。

四个 retired core NuGet package ID 已不属于活动发布集合。它们的已发布版本只用于单独保存的单调递增版本基线。

## 其他生态

Node.js 与 Python 目前是预留 source root，本索引不包含可运行 package。
