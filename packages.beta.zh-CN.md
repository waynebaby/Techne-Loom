# Beta Runtime 包索引

[English](packages.beta.md) | [Released](packages.released.zh-CN.md)

本页列出已发布的 beta runtime packages。AO 与 SO 只发布自包含的 product+RID 包；源项目仍可在仓库中构建和测试，但不属于 NuGet runtime package。

## 当前发布集合

<!-- active-runtime-set:start -->
统一后的 Loom 发布集合包含八个自包含、按 RID 区分的 runtime package，以及同版本、无 RID 的 `Techne.Loom.Abstractions` 契约 SDK（共 9 个包）。请根据操作系统、CPU 架构和 Linux libc 选择一个 runtime package。

| RID | Loom runtime package | Apphost |
| --- | --- | --- |
| `win-x64` | `Techne.Loom.Runtime.win-x64` | `loom.exe` |
| `win-arm64` | `Techne.Loom.Runtime.win-arm64` | `loom.exe` |
| `linux-x64` | `Techne.Loom.Runtime.linux-x64` | `loom` |
| `linux-arm64` | `Techne.Loom.Runtime.linux-arm64` | `loom` |
| `linux-musl-x64` | `Techne.Loom.Runtime.linux-musl-x64` | `loom` |
| `linux-musl-arm64` | `Techne.Loom.Runtime.linux-musl-arm64` | `loom` |
| `osx-x64` | `Techne.Loom.Runtime.osx-x64` | `loom` |
| `osx-arm64` | `Techne.Loom.Runtime.osx-arm64` | `loom` |
<!-- active-runtime-set:end -->

## 版本

<!-- package-version-block:start -->
- 当前最新已发布的 beta 包版本 `0.5.1-beta` 同时适用于全部 16 个 AO/SO RID runtime 包和无 RID 的 Abstractions SDK。
- beta 发布会先读取 stable 与 beta 已发布包中的最高数值版本，再生成下一个版本并追加 `-beta`；这样两个分支共享单调递增的版本基线。
<!-- package-version-block:end -->

































使用上面的精确版本。不要请求 `latest`、版本范围或其他 RID。

## 安装命令

<!-- package-dotnet-block:start -->
| 产品 | RID | Runtime package | NuGet 获取方式 | GitHub 回退 |
| --- | --- | --- | --- | --- |
| Loom Runtime | `win-x64` | `Techne.Loom.Runtime.win-x64` | `dotnet add package Techne.Loom.Runtime.win-x64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.win-x64.0.5.1-beta.nupkg) |
| Loom Runtime | `win-arm64` | `Techne.Loom.Runtime.win-arm64` | `dotnet add package Techne.Loom.Runtime.win-arm64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.win-arm64.0.5.1-beta.nupkg) |
| Loom Runtime | `linux-x64` | `Techne.Loom.Runtime.linux-x64` | `dotnet add package Techne.Loom.Runtime.linux-x64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-x64.0.5.1-beta.nupkg) |
| Loom Runtime | `linux-arm64` | `Techne.Loom.Runtime.linux-arm64` | `dotnet add package Techne.Loom.Runtime.linux-arm64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-arm64.0.5.1-beta.nupkg) |
| Loom Runtime | `linux-musl-x64` | `Techne.Loom.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.Runtime.linux-musl-x64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-musl-x64.0.5.1-beta.nupkg) |
| Loom Runtime | `linux-musl-arm64` | `Techne.Loom.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.Runtime.linux-musl-arm64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-musl-arm64.0.5.1-beta.nupkg) |
| Loom Runtime | `osx-x64` | `Techne.Loom.Runtime.osx-x64` | `dotnet add package Techne.Loom.Runtime.osx-x64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.osx-x64.0.5.1-beta.nupkg) |
| Loom Runtime | `osx-arm64` | `Techne.Loom.Runtime.osx-arm64` | `dotnet add package Techne.Loom.Runtime.osx-arm64 --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.osx-arm64.0.5.1-beta.nupkg) |
| SDK | `无 RID` | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.5.1-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.0.5.1-beta.nupkg) |
<!-- package-dotnet-block:end -->

































## 获取与校验

宿主先根据系统、架构和 Linux libc 检测一个受支持的 RID，再获取该产品唯一对应的精确版本包。优先复用标准 NuGet global-packages cache 中通过校验的包。NuGet 下载包只与该精确版本 registration `catalogEntry` 中的 `packageHash` 比较；不要求 NuGet 提供单独的 `.nupkg.sha512` sidecar。GitHub 回退则使用下方精确版本 `.nupkg.sha512` 链接，并将其与 GitHub 包字节比较。两种来源的校验权威必须分开。

NuGet 精确版本 URL：

```text
https://www.nuget.org/api/v2/package/<PackageId>/0.5.1-beta
```

GitHub package 与校验文件使用相同文件名：

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.0.5.1-beta.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.0.5.1-beta.nupkg.sha512
```

解压前校验 package id、精确版本、RID、SHA-512、nuspec、`tools/<rid>/runtime.json`、压缩包路径与大小、apphost 和英文 guide 文件。拒绝不匹配或不安全的 ZIP 条目，并解压到每次运行专用的外部目录。

## 第一个命令

解压后，直接运行 apphost 的 `--guide`，这是第一个 runtime 操作。确认版本和 guide 文件可读后，再使用同一个 apphost 生成 schema/demo、compile、run 和 resume。

```powershell
.\so.exe --guide
.\ao.exe --guide
```

Unix 使用不带 `.exe` 的 `so --guide` 或 `ao --guide`。不需要安装 .NET host、framework-dependent DLL package、runtime resolver、launch descriptor 或固定 bootstrap script。

三个 retired 实现类 NuGet package ID 已不属于活动发布集合。它们的已发布版本只用于单独保存的单调递增版本基线；Abstractions 是活动 SDK 包。

## 其他生态

Node.js 与 Python 目前是预留 source root，本索引不包含可运行 package。
