# Techne.Loom.SkillOrchestrator



## English



Deterministic workflow execution and tracking for Techne Loom skills.



This package is the SO-facing runtime surface. It exposes the version-matched offline docs bundle, the compile/run/resume/status/inspect entrypoints, optional apphost-bound MCP configuration generation, and workflow audit artifacts that capture Mermaid Markdown, HTML, and workflow JSON backups step by step. `compile` validates an existing workflow JSON directly and, for target-skill templates under Loom Skill Orchestrator governance, also enforces the governed-template validation contract, route-aware business-output gates, seam ownership, and done reachability.



### Runtime package

Acquire the exact `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package from the stable or beta package index. Verify its package hash, RID, manifest, apphost, and guide before use. The repository project remains available for source builds and tests; published runtime execution uses the package apphost.

### Primary entrypoints

- `so.exe --guide` on Windows or `so --guide` on Unix
- `so.exe --help` on Windows or `so --help` on Unix
- `so.exe compile`, `so.exe run`, `so.exe resume`, `so.exe status`, `so.exe inspect-workflow`, and `so.exe inspect-events` on Windows
- The same commands with `so` on Unix
- `so.exe mcp generate-config --output-file <path>` / `so mcp generate-config --output-file <path>` for optional apphost-bound MCP

### Guide output

Run the apphost directly with `--guide`. It reads the English docs tree shipped beside the executable and emits JSON with `version`, `docs_root`, and `guide_path`. The apphost does not embed guide pages; missing package docs are an error.

### Docs



- Product guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/so-guide.md>

- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/cli.md>

- Skill I/O reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/skills.md>

- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>

- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>



## 中文



Techne Loom SkillOrchestrator 的确定型 workflow 执行与跟踪包。



这个包是 SO 面向外部的 runtime 表面。它暴露与版本匹配的离线英文文档包、compile/run/resume/status/inspect 入口，以及按 step 保存 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact。`compile` 负责直接校验已有 workflow JSON；对于 target-skill template under Loom Skill Orchestrator governance，它还会强制 governed-template validation 契约、route-aware business-output gates、seam ownership 与 done reachability。



### Runtime package

从稳定版或 beta package index 获取精确的 `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package。使用前校验 package hash、RID、manifest、apphost 和 guide。仓库项目仍用于源码构建和测试；已发布 runtime 通过 package apphost 运行。

### 主要入口

- Windows 使用 `so.exe --guide`，Unix 使用 `so --guide`
- Windows 使用 `so.exe --help`，Unix 使用 `so --help`
- Windows 使用 `so.exe compile`、`so.exe run`、`so.exe resume`、`so.exe status`、`so.exe inspect-workflow` 和 `so.exe inspect-events`
- Unix 使用相同命令名的 `so` apphost
- `so.exe mcp generate-config --output-file <path>` / `so mcp generate-config --output-file <path>` 用于后续可选的 apphost-bound MCP

### Guide 输出

直接运行 apphost 并传入 `--guide`。它会读取可执行文件旁边的英文 docs 树，并输出包含 `version`、`docs_root` 和 `guide_path` 的 JSON。Apphost 不嵌入 guide 页面；package 缺少文档时命令会报错。

### 文档



- 产品 guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/so-guide.md>

- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/cli.md>

- Skill 输入输出参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/skills.md>

- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>

- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
