# Beta Runtime 包索引
[English](packages.beta.md) | [Released](packages.released.zh-CN.md)

本页列出已发布的 beta runtime package 集合。每个 RID 对应的 `Techne.Loom.Runtime.<rid>` 包都包含一个自包含的 `loom` apphost，可执行 Agent 和 Skill 模块命令；无 RID 的 `Techne.Loom.Abstractions` SDK 以相同版本发布。源项目仍可在仓库中构建和测试，但不属于 runtime package。

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
- 当前已发布的 beta 包版本 `0.5.7-beta` 同时适用于全部八个 Loom RID runtime 包和无 RID 的 `Techne.Loom.Abstractions` 契约 SDK。
- beta 发布会先读取 stable 与 beta 已发布包中的最高数值版本，再生成下一个版本并追加 `-beta`。
<!-- package-version-block:end -->

请使用上面的精确版本，不要请求 `latest`、版本范围或其他 RID。

## 安装命令

<!-- package-dotnet-block:start -->
| 产品 | RID | Runtime package | NuGet 获取方式 | GitHub 回退 |
| --- | --- | --- | --- | --- |
| Loom Runtime | `win-x64` | `Techne.Loom.Runtime.win-x64` | `dotnet add package Techne.Loom.Runtime.win-x64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.win-x64.0.5.7-beta.nupkg) |
| Loom Runtime | `win-arm64` | `Techne.Loom.Runtime.win-arm64` | `dotnet add package Techne.Loom.Runtime.win-arm64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.win-arm64.0.5.7-beta.nupkg) |
| Loom Runtime | `linux-x64` | `Techne.Loom.Runtime.linux-x64` | `dotnet add package Techne.Loom.Runtime.linux-x64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-x64.0.5.7-beta.nupkg) |
| Loom Runtime | `linux-arm64` | `Techne.Loom.Runtime.linux-arm64` | `dotnet add package Techne.Loom.Runtime.linux-arm64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-arm64.0.5.7-beta.nupkg) |
| Loom Runtime | `linux-musl-x64` | `Techne.Loom.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.Runtime.linux-musl-x64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-musl-x64.0.5.7-beta.nupkg) |
| Loom Runtime | `linux-musl-arm64` | `Techne.Loom.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.Runtime.linux-musl-arm64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.linux-musl-arm64.0.5.7-beta.nupkg) |
| Loom Runtime | `osx-x64` | `Techne.Loom.Runtime.osx-x64` | `dotnet add package Techne.Loom.Runtime.osx-x64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.osx-x64.0.5.7-beta.nupkg) |
| Loom Runtime | `osx-arm64` | `Techne.Loom.Runtime.osx-arm64` | `dotnet add package Techne.Loom.Runtime.osx-arm64 --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Runtime.osx-arm64.0.5.7-beta.nupkg) |
| SDK | `无 RID` | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.5.7-beta` | [精确 `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.0.5.7-beta.nupkg) |
<!-- package-dotnet-block:end -->

## 获取与校验

根据操作系统、CPU 架构和 Linux libc 检测一个受支持的 RID，然后只获取该 RID 对应的精确 `Techne.Loom.Runtime.<rid>` 包。此包提供唯一的 `loom` apphost：Agent 命令使用 `loom agent`，Skill 命令使用 `loom skill`。无 RID 的 Abstractions SDK 是同版本发布的独立包。

**NuGet 优先的回退流程：** 先从下方 NuGet 官方 Flat Container URL 下载精确 `.nupkg`，读取该精确版本的 registration/catalog JSON，并在解压前用 `catalogEntry.packageHash`（SHA-512）校验包字节；通过后再校验包身份、RID 和包内 `runtime.json`。NuGet 下载、registration/catalog、hash、解压或包校验任一失败（包括 404，或跳转离开 `api.nuget.org`，例如 `nuget.azure.cn`）都视为 NuGet 尝试失败；不要跟随该跳转，也不要探测其他镜像。先询问一次是否批准使用本索引列出的精确 GitHub release `.nupkg` 及同名 `.nupkg.sha512`；只有取得明确批准后才能下载，绝不默认允许。无人值守或无法取得明确批准时，不下载 GitHub 资产，并保持在可恢复的 acquisition-pending 暂停状态；明确拒绝时同样保持 pending。若已批准，只用 GitHub sidecar 校验，并且只允许跳转到 `github.com`、`release-assets.githubusercontent.com` 或 `objects.githubusercontent.com`。NuGet 请求必须使用关闭自动重定向的 L3 进程内 HTTP 客户端；不要使用会自动跟随或隐藏重定向目标的托管 fetch。

NuGet 精确版本 URL：

```text
https://api.nuget.org/v3-flatcontainer/<package-id-lowercase>/0.5.7-beta/<package-id-lowercase>.0.5.7-beta.nupkg
```

GitHub 精确包及校验文件位于 beta release 下，文件名相同：

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.0.5.7-beta.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.0.5.7-beta.nupkg.sha512
```

解压前校验 package id、精确版本、RID、nuspec、`tools/<rid>/runtime.json`、压缩包路径与大小、apphost 和英文 guide 文件。拒绝不匹配或不安全的 ZIP 条目。

## 第一个命令

直接运行 RID 对应的 `loom` apphost。先运行 `--guide`，读取与当前版本匹配的文档路径，再使用对应模块命令。

```powershell
.\loom.exe --guide
.\loom.exe agent --help
.\loom.exe skill --help
```

```bash
./loom --guide
./loom agent --help
./loom skill --help
```

Node.js 与 Python 目前仅作为预留 source root；尚未提交可运行的实现。
