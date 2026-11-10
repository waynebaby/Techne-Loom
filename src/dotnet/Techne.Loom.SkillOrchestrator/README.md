# Techne.Loom.SkillOrchestrator



## English



Deterministic workflow execution and tracking for Techne Loom skills.



This source project implements the SkillOrchestrator module. Published commands use the unified Loom runtime through `loom skill`. It exposes the version-matched offline docs bundle, the compile/run/resume/status/inspect entrypoints, optional apphost-bound MCP configuration generation, and workflow audit artifacts that capture Mermaid Markdown, HTML, and workflow JSON backups step by step. `compile` validates an existing workflow JSON directly and, for target-skill templates under Loom Skill Orchestrator governance, also enforces the governed-template validation contract, route-aware business-output gates, seam ownership, and done reachability.



### Runtime package

Acquire the exact `Techne.Loom.Runtime.<rid>` package from the stable or beta package index. Verify its package hash, RID, manifest, apphost, and guide before use. The repository project remains available for source builds and tests; published runtime execution uses the package apphost.

### Primary entrypoints

- `loom.exe --guide` on Windows or `loom --guide` on Unix
- `loom.exe skill --help` on Windows or `loom skill --help` on Unix
- `loom.exe skill compile`, `loom.exe skill run`, `loom.exe skill resume`, `loom.exe skill status`, `loom.exe skill inspect-workflow`, and `loom.exe skill inspect-events` on Windows
- The same commands with `loom skill` on Unix
- `loom.exe skill mcp generate-config --output-file <path>` / `loom skill mcp generate-config --output-file <path>` for optional apphost-bound MCP

### Guide output

Run the apphost directly with `--guide`. It reads the English docs and version-matched workflow contract docs shipped beside the executable and emits JSON with `version`, `docs_root`, `guide_path`, `contract_docs_root`, `contract_index_path`, and `builder_template_path`. The apphost does not embed these pages; missing package docs are an error.

### Workflow builder and contract references

The same-version package includes `docs/contracts/index.md`, fenced `.cs.md` references generated from Abstractions, and the `workflow-instance-builder.csx.md` template. These are Markdown references, not standalone `.cs` or `.csx` samples. The `--guide` result returns absolute `contract_docs_root`, `contract_index_path`, and `builder_template_path` values. Author new workflows with `Build(WorkflowScriptInput)` and revise drafts with `Edit(WorkflowInstance, WorkflowScriptInput)`. `--mode export` creates an `Edit` starter from an eligible external draft; `--mode edit --base-workflow-file` reads that external JSON, while `--mode build` creates a new canonical workflow JSON. Compile with the same apphost and version. See [the complete authoring route](../../../docs/en/guides/skill-usage.md).
### Docs



- Product guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/so-guide.md>

- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/so-cli-usage.md>

- Skill I/O reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/skills.md>

- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>

- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>



## 中文



Techne Loom SkillOrchestrator 的确定型 workflow 执行与跟踪包。



这个源代码项目实现 SkillOrchestrator 模块。发布命令通过 `loom skill` 使用统一的 Loom runtime。它暴露与版本匹配的离线英文文档包、compile/run/resume/status/inspect 入口，以及按 step 保存 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact。`compile` 负责直接校验已有 workflow JSON；对于 target-skill template under Loom Skill Orchestrator governance，它还会强制 governed-template validation 契约、route-aware business-output gates、seam ownership 与 done reachability。



### Runtime package

从稳定版或 beta package index 获取精确的 `Techne.Loom.Runtime.<rid>` package。使用前校验 package hash、RID、manifest、apphost 和 guide。仓库项目仍用于源码构建和测试；已发布 runtime 通过 package apphost 运行。

### 主要入口

- Windows 使用 `loom.exe --guide`，Unix 使用 `loom --guide`
- Windows 使用 `loom.exe skill --help`，Unix 使用 `loom skill --help`
- Windows 使用 `loom.exe skill compile`、`loom.exe skill run`、`loom.exe skill resume`、`loom.exe skill status`、`loom.exe skill inspect-workflow` 和 `loom.exe skill inspect-events`
- Unix 使用相同命令名的 `loom skill` route
- `loom.exe skill mcp generate-config --output-file <path>` / `loom skill mcp generate-config --output-file <path>` 用于后续可选的 apphost-bound MCP

### Guide 输出

直接运行 apphost 并传入 `--guide`。它会读取可执行文件旁边的英文 docs 和同版本 workflow contract 文档，并输出包含 `version`、`docs_root`、`guide_path`、`contract_docs_root`、`contract_index_path` 和 `builder_template_path` 的 JSON。Apphost 不嵌入这些页面；package 缺少文档时命令会报错。

### Workflow builder 与契约参考

同版本 package 包含 `docs/contracts/index.md`、从 Abstractions 生成且用 fenced code 展示的 `.cs.md` 参考页，以及 `workflow-instance-builder.csx.md` 模板。这些是 Markdown 参考，不是独立 `.cs` 或 `.csx` 示例。`--guide` 结果会返回绝对路径 `contract_docs_root`、`contract_index_path` 和 `builder_template_path`。新建 workflow 使用 `Build(WorkflowScriptInput)`，修订 draft 使用 `Edit(WorkflowInstance, WorkflowScriptInput)`。`--mode export` 从符合条件的外部 draft 生成 `Edit` 起始脚本；`--mode edit --base-workflow-file` 读取外部 JSON，`--mode build` 新建规范 workflow JSON。之后必须由同一 apphost 和版本 compile。完整 authoring route 见[使用指南](../../../docs/zh-cn/guides/skill-usage.md)。
### 文档



- 产品 guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/so-guide.md>

- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/so-cli-usage.md>

- Skill 输入输出参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/skills.md>

- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>

- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
