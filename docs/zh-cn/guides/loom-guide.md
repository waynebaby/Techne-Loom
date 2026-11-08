# Techne Loom CLI 与 Runtime 指南

[English](../../en/guides/loom-guide.md) | [根目录](../README.md)

<!-- guide-version:start -->
版本：0.5.1-beta
构建：已发布的 0.5.1-beta 包
<!-- guide-version:end -->


## Guide 输出

Windows 运行 `loom.exe --guide`，Unix 运行 `loom --guide`。命令返回与当前版本匹配的英文 guide 的 `version`、`docs_root`、`guide_path`，以及 workflow/task contract reference 路径。

## 一个 CLI，三个入口

- `loom agent <command>` 执行 Loom Agent Plan-Execution Orchestrator 命令。
- `loom skill <command>` 执行 Loom Skill Orchestrator 命令。
- `loom ask <start|result|wait>` 是唯一独立提问入口；不要使用 `agent ask` 或 `skill ask`。
- `loom --guide` 返回共享 runtime guide 和 package version。

示例：

```powershell
loom.exe agent compile --workflow-file .\workflow.json
loom.exe skill run --workflow-file .\workflow.json
loom.exe ask start --contract-file .\question-contract.json
```

```bash
loom agent compile --workflow-file ./workflow.json
loom skill run --workflow-file ./workflow.json
loom ask start --contract-file ./question-contract.json
```

文件参数只接受路径。运行命令前，先将所有输入文件完整写入磁盘。

## Runtime Package

从 `0.5.0-beta` 开始的 breaking-change 目标发行系列计划包含八个按 RID 区分的自包含 package，命名为 `Techne.Loom.Runtime.<rid>`。根据主机操作系统、处理器架构和 Linux libc 选择唯一 package。每个 package 将包含 `loom` apphost、匹配版本的英文文档和 runtime 身份元数据。同一次发布还将包含无 RID 的 `Techne.Loom.Abstractions` SDK；两类 package 共用同一精确版本和 channel。该版本尚未发布；当前 `0.4.20-beta` package 系列仍由 AO/SO 分立 package 组成，见[beta package 索引](../../../packages.beta.md)。

解压前验证精确 package 身份、版本、RID、校验和、manifest 与归档路径。直接运行解压后的 apphost；不要启动 DLL，也不要切换到其他 package 或源码构建。

## 模块指南

- [Loom Agent Plan-Execution Orchestrator](ao-guide.md)
- [Loom Skill Orchestrator](so-guide.md)
- [独立提问 Ask](ask-user-guide.md)
- [CLI 与 Host](../architecture/cli-and-hosts.md)
- [Runtime 平台检测](../reference/runtime/platform-detection.md)
