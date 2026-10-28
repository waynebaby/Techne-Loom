# 平台检测步骤

[English](../../../en/reference/runtime/platform-detection.md)

本契约适用于 AO、SO 以及需要获取其发布版 runtime 的 skill。每个 runtime package 都是自包含的，并且只对应一个 product 和一个 RID。

## 版本与包范围

使用 owning skill 的 checked-in lock 或版本区块中的精确版本。直接调用者从 package index 选择 channel。不要解析 `latest`、使用版本范围或替换为相邻版本。

协调后的 NuGet 发布集合包含 16 个自包含 runtime package：8 个 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` 和 8 个 `Techne.Loom.SkillOrchestrator.Runtime.<rid>`，另有一个不绑定 RID、使用相同精确版本和渠道发布的 `Techne.Loom.Abstractions` 契约 SDK（共 17 个包）。SDK 单独安装，不参与宿主 RID 选择。源项目仍可用于开发和测试；只有声明的 Abstractions SDK 属于活动非 runtime package。3 个 retired 实现包的版本单独用于保持版本单调递增，不会被 pack 或 push。较早的仅含 runtime 的发布版本可能尚未包含 SDK。

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

只获取已选 product 与 RID 对应的 package ID。首先在当前用户 OS temp 共享缓存 `<OS-temp>/TechneLoom/runtime-cache/` 中查找并复用经过校验的 generation。Generation identity 由精确 package ID、版本、RID 和完整、已验证 `.nupkg` SHA-512 组成；复用前重新计算哈希并校验 package identity。

缓存未命中时，再检查标准 NuGet global-packages cache 中是否有通过校验的精确包。新建或修复 generation 必须先暂存并校验，再原子发布；不得覆盖其他有效或正在使用的 generation。

下载的 NuGet package 必须与精确 registration catalog entry 中的 Base64 `packageHash` 匹配。同版本 GitHub Release asset 只允许作为回退，并且必须通过对应 `.nupkg.sha512` sidecar 校验。使用精确文件名，不要浮动版本。不要添加固定 bootstrap script、resolver、descriptor 文件、缓存锁或其他 runtime mode。宿主可以使用已有的 shell 或脚本引擎。

### NuGet SHA-512 排查

针对精确 package ID 和版本，获取 `https://api.nuget.org/v3/registration5-semver1/<lowercase-package-id>/<version>.json`，跟随所选条目的 `catalogEntry` 链接并读取其 `packageHash`。请对原始 `.nupkg` 字节计算哈希，不要对解压后的文件计算。

```powershell
$expectedHex = [BitConverter]::ToString([Convert]::FromBase64String($packageHash)).Replace('-', '')
$actualHex = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash
if ($actualHex -ine $expectedHex) { throw 'The package does not match the NuGet registration SHA-512.' }
```

哈希不匹配或无法获取 registration metadata 时，不要解压或发布 package；保留 expected/actual hash 和失败证据。只可回退到 sidecar 匹配且校验通过的同版本 GitHub asset；否则停止。

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
