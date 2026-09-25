# 平台检测步骤

[English](../../../en/reference/runtime/platform-detection.md)

本契约适用于 AO、SO 以及需要获取其发布版 runtime 的 skill。每个 runtime package 都是自包含的，并且只对应一个 product 和一个 RID。

## 版本与包范围

使用 owning skill 的 checked-in lock 或版本区块中的精确版本。直接调用者从 package index 选择 channel。不要解析 `latest`、使用版本范围或替换为相邻版本。

活动 NuGet 集合恰好包含 16 个包：8 个 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` 和 8 个 `Techne.Loom.SkillOrchestrator.Runtime.<rid>`。源项目仍可用于开发和测试，但不属于活动 runtime package。4 个 retired core package 的版本单独用于保持版本单调递增，不会被 pack 或 push。

## 检测唯一 RID

宿主根据操作系统、CPU 架构和 Linux libc 检测并选择一个受支持的 RID：

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

检测存在歧义时不要猜测。不要跨 OS、架构或 libc 选择。没有匹配的受支持 RID 时，返回明确诊断并停止。

## 获取精确包

只获取已选 product 与 RID 对应的 package ID。优先复用标准 NuGet global-packages cache 中通过校验的精确包；否则下载精确 NuGet package，并将包字节与该版本 registration 响应中的 `catalogEntry.packageHash` 比较。

同版本 GitHub Release asset 只允许作为回退，并且必须通过对应 `.nupkg.sha512` sidecar 校验。使用精确 package 文件名；不要使用浮动别名。不要添加固定 bootstrap script、resolver、descriptor 文件、Loom 专用缓存、缓存锁或其他 runtime mode。宿主可以使用已有的 shell 或脚本引擎。

## 解压前校验

把 `.nupkg` 当作 ZIP 内容处理。解压前校验：

- package ID、精确版本、RID、SHA-512 和根 nuspec identity
- `tools/<rid>/runtime.json` 中的 product、版本、RID、apphost、docs root 和 guide path
- 预期的 `ao`/`so` apphost 与完整英文 guide 文件树
- 压缩包大小、条目大小、重复路径、路径穿越和意外文件

任何不匹配或不安全的压缩包都必须拒绝。只有通过校验后，才可将 package 解压到每次运行专用的外部目录；Unix 需要时设置可执行权限。`runtime.json` 记录 package identity，不是 launch descriptor。

## 先运行 Guide

Windows 直接运行 `ao.exe --guide` 或 `so.exe --guide`；Unix 直接运行 `ao --guide` 或 `so --guide`。解压后这是第一个 runtime 操作，不需要预装 .NET host。

只接受成功返回的 JSON，并校验精确版本、绝对 `docs_root` 和 `guide_path`、guide 路径位于 docs root 内且文件可读。失败 stderr 不能作为 guide evidence。获取、校验、解压、启动或 guide 检查失败时，必须停止并保留失败证据。

通过 guide gate 后，使用同一个已解压 apphost 生成 schema/demo、compile、run 和 resume。apphost 启动后的命令错误就是命令失败，不能回退到其他包。dispatch 后不得更换 RID、版本、workflow copy 或 package source。
