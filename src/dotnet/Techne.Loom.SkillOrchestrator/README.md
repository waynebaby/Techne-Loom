# Techne.Loom.SkillOrchestrator

## English

Deterministic workflow execution and tracking for Techne Loom skills.

This package is the SO-facing runtime surface. It exposes the version-matched offline docs bundle, the compile/run/resume/status/inspect entrypoints, and workflow audit artifacts that capture Mermaid Markdown, HTML, and workflow JSON backups step by step. `compile` validates an existing workflow JSON directly and, for Loom-governanced target-skill templates, also enforces the governed-template validation contract, route-aware business-output gates, seam ownership, and done reachability.

### Install

```bash
dotnet add package Techne.Loom.SkillOrchestrator --version 0.1.0
```

### Primary entrypoints

- `dotnet so.dll --guide`
- `dotnet so.dll --help`
- `dotnet so.dll compile`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`

### Guide output

Run the bare `dotnet so.dll --guide` command. It reads the English `docs/en` tree shipped beside the executable in a complete runtime package and emits one JSON object with `version`, `docs_root`, and `guide_path`. The executable does not contain guide pages; a missing package docs tree is an error. Read `guide_path` first as the version truth; inspect `docs_root` only when the guide leaves a question unresolved. `--lang`, `--section`, and `--export` are no longer supported.

### Docs

- Product guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/so-guide.md>
- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/cli.md>
- Skill I/O reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/skills.md>
- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

## 中文

Techne Loom SkillOrchestrator 的确定型 workflow 执行与跟踪包。

这个包是 SO 面向外部的 runtime 表面。它暴露与版本匹配的离线英文文档包、compile/run/resume/status/inspect 入口，以及按 step 保存 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact。`compile` 负责直接校验已有 workflow JSON；对于 Loom-governanced target-skill template，它还会强制 governed-template validation 契约、route-aware business-output gates、seam ownership 与 done reachability。

### 安装

```bash
dotnet add package Techne.Loom.SkillOrchestrator --version 0.1.0
```

### 主要入口

- `dotnet so.dll --guide`
- `dotnet so.dll --help`
- `dotnet so.dll compile`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`

### Guide 输出

运行不带额外参数的 `dotnet so.dll --guide`。它会读取与可执行文件放在同一个完整 runtime package 中的英文 `docs/en` 文档树，并输出包含 `version`、`docs_root`、`guide_path` 的 JSON 对象。可执行文件本身不包含 guide 页面；如果 package docs 缺失，命令会报错。先读取 `guide_path` 作为当前版本真相；只有 guide 无法消除疑问时，才查看 `docs_root`。`--lang`、`--section` 与 `--export` 已不再支持。

### 文档

- 产品 guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/so-guide.md>
- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/cli.md>
- Skill 输入输出参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/skills.md>
- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
