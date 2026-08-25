# 平台检测步骤

[English](../../../en/reference/runtime/platform-detection.md)

本页定义 Loom Agent Execution Orchestrator（AO）与 Loom Skill Orchestrator（SO）共享的运行时选择契约，适用于 direct CLI、手动调用以及需要恢复 Loom runtime package 的 skill。

> **快照状态：** 当前仓库快照可能早于 runtime package 的实际发布。CI/CD 会在发布时填入实际 runtime 包版本、release asset 和 SHA-512。不要根据本页虚构 runtime 版本或 hash。

## 1. 版本权威与适用范围

direct 或手动获取应从 released 或 beta package index 开始。受治理的 skill run 只能把 owning skill 的 locked exact runtime version、CI/CD 管理的 version block 或 checked-in runtime lock 作为版本权威。不要查询 `latest`、使用兼容范围或漂移到相邻版本。

framework-dependent 模式使用可用的 `Microsoft.NETCore.App 9.x` host 启动 IL bundle：

```text
dotnet exec --runtimeconfig <bundle>/ao.runtimeconfig.json <bundle>/ao.dll <args>
dotnet exec --runtimeconfig <bundle>/so.runtimeconfig.json <bundle>/so.dll <args>
```
framework 模式必须提供 `--runtimeconfig`。如果对应的 `ao.deps.json` 或 `so.deps.json` 存在，且 host 要求显式 dependency binding，则在 `--runtimeconfig` 前加入 `--depsfile <bundle>/<entry>.deps.json`。

self-contained single-file 模式直接运行 `ao` 或 `so`；Windows 下运行 `ao.exe` 或 `so.exe`。两种模式暴露相同的 CLI 参数、workflow state、guide 输出、audit artifacts 和治理语义。resolver 必须返回实际 launch descriptor，不应让调用方自行拼装命令。

## 2. 探测 .NET host

先确认 `dotnet` 命令可以解析，再检查 `dotnet --list-runtimes`，只接受其中 major version 为 `9` 的 `Microsoft.NETCore.App` 条目。不要用 SDK 版本或 `dotnet --version` 替代这一步。已安装的 .NET runtime patch version 与 Loom package version 是两个不同字段，不能混淆。

## 3. 执行启动预检并分类失败

对于候选 .NET 9 host，恢复 owning product 的精确版本 IL bundle：Product、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`。使用将要执行命令的同一套显式 runtime binding，执行轻量且无副作用的 host/CLI 启动预检，再执行一次 fresh `--guide`。

只有以下 host-startup 失败才选择 self-contained fallback：`dotnet` 不可用、没有 .NET 9 runtime、host loading 失败、缺少 host 依赖，或 CLI 无法在该 host 下启动。CLI 已经启动之后发生的参数、模板、表达式、治理或业务错误，都是真实的命令失败；必须原样返回，不得换另一 host 重试来掩盖。

## 4. 把平台映射为 RID

只支持 Windows、Linux、macOS 的 x64 或 arm64。Linux 还必须在选择 RID 前区分 glibc 与 musl。支持的集合是：

```text
win-x64
win-arm64
linux-x64
linux-arm64
linux-musl-x64
linux-musl-arm64
osx-x64
osx-arm64
```

不要猜 RID、跨架构或把其他 OS/ABI 包当作 fallback。不支持的平台或 ABI 必须 fail-fast，并返回已检测值及支持集合。

## 5. 获取一个精确版本 runtime package

AO 使用 `Techne.Loom.AgentOrchestrator.Runtime.<rid>`，SO 使用 `Techne.Loom.SkillOrchestrator.Runtime.<rid>`。self-contained 包只包含一个 RID 的 executable；它无需预装 .NET runtime，但仍依赖目标 OS 与 ABI。

这个包分发的是一个 apphost executable。apphost 启动时可能使用 .NET single-file 的 self-extraction 路径解出 bundled framework/native content；这不会增加第二个分发 runtime 文件，并且 self-contained 路径中的内嵌 Roslyn expression compiler 需要这种行为。

使用 NuGet.org V3 flat-container 精确版本 URL，不使用 `latest` 或 registration URL。URL 中 package id 使用小写，版本使用 NuGet 规范化后的精确版本：

```text
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg.sha512
```

包内入口固定为 `tools/<rid>/ao` 或 `tools/<rid>/so`，Windows 下带 `.exe`。resolver 必须在 runtime evidence 中保留精确 package id、version、RID、package URL 与 hash URL。

## 6. 校验完整性与包形状

先下载包内容，再读取 NuGet `.sha512` sidecar，将其中的 base64 SHA-512 解码后与包字节的计算结果比较。随后校验 nuspec identity、精确版本、RID metadata 和入口。只接受规定的包文件：metadata，以及 `tools/<rid>/ao[.exe]` 或 `tools/<rid>/so[.exe]` 这一个 executable。

hash 不匹配、identity 或版本不匹配、入口缺失或重复、意外 runtime payload、ZIP 路径穿越、超大条目或超大压缩包都必须拒绝。完整性失败时必须 fail-closed；不能用另一个来源掩盖校验失败。

## 7. 缓存并原子解包

解压到可由环境配置覆盖的用户级共享缓存，按 product、精确 version 与 RID 隔离。使用跨进程锁，在临时目录中完成完整校验，再原子发布不可变 cache entry。Unix 平台要设置 executable bit。

有效的缓存 entry 可以支持离线运行。如果 hash、manifest、guide version 或 package identity 不再匹配，就废弃该 entry 并原子重建。网络不可用且没有有效精确版本缓存时，应带着缓存与获取证据阻塞；不要用仓库 build 替代。

## 8. 从 NuGet.org 回退到 GitHub

只有精确版本的 NuGet.org 包无法获取时，才尝试官方 GitHub release asset。fallback 必须使用相同 product、channel、精确 version 与 RID 的 package。允许的 asset 形态是精确版本 `.nupkg`，以及必须校验其内容与绑定精确版本相同的通道 `.latest.nupkg` 别名：

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-<channel>-latest/<PackageId>.<exact-version>.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-<channel>-latest/<PackageId>.latest.nupkg
```

fallback 也必须执行同样的 SHA-512、nuspec、manifest、ZIP 安全和入口校验。如果两个来源都失败，就阻塞。绝不能用 repository-source build 替代失败的 package 获取，也不能伪造 preflight 成功。

## 9. 启动并保留证据

返回一个包含以下机器可读字段的 launch descriptor：

```text
runtime_mode
resolved_runtime_version
rid
package_id
package_ids
package_url
package_hash
cache_root
launch_file
launch_prefix_args
preflight_result
```

framework 模式下，`package_ids` 表示精确版本的 Product + Common + Abstractions bundle，`launch_file` 是 IL 入口，`launch_prefix_args` 包含显式 `dotnet exec` binding。self-contained 模式下，`package_id` 表示一个 RID package，`launch_file` 是缓存中的 direct executable；除非宿主要求平台特定前缀，否则 `launch_prefix_args` 为空。

两种模式都必须先运行 fresh `--guide`。解析输出 JSON，校验其中的 `version`，并读取返回的 `guide_path`。只有完成这一步，调用方才可以执行 `compile`、`run` 或 `resume`。之后所有 AO/SO 命令都必须复用同一个 launch descriptor、精确 runtime version 与 RID，不能在 workflow 中途更换 host。

## 权威规则

- owning skill 的 locked exact version 是唯一 runtime version 权威。`latest`、兼容范围和相邻版本漂移都无效。
- 只有 host-startup 类失败会触发 self-contained fallback。CLI 启动后的错误必须作为命令失败返回，不能通过 fallback retry 隐藏。
- cache entry 按 product、精确 version 和 RID 隔离，受跨进程锁保护，在临时目录中校验后原子发布。
- 有效的精确版本 cache entry 支持离线执行；无网络且没有有效缓存时，结果是阻塞。
- 当前快照可能早于 runtime package 发布。CI/CD 会在发布时提供实际包版本、asset 和 SHA-512。
