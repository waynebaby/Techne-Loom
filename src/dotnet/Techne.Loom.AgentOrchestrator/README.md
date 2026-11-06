# Techne.Loom.AgentOrchestrator



## English



Exploratory orchestration CLI for Loom Agent Plan-Execution Orchestrator.



This package is the Loom Agent Plan-Execution Orchestrator runtime surface. It exposes the version-matched offline docs bundle, the compile entrypoint, Loom Agent Plan-Execution Orchestrator-owned prompt-plan/prompt-replan support surfaces, and CLI run/resume commands that emit machine-readable control payloads plus audit artifact links. Loom Agent Plan-Execution Orchestrator is CLI-only in this project.



### Runtime package

Acquire the exact `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package from the stable or beta package index. Verify its package hash, RID, manifest, apphost, and guide before use. The repository project remains available for source builds and tests; published runtime execution uses the package apphost.

### Primary entrypoints

- `ao.exe --guide` on Windows or `ao --guide` on Unix
- `ao.exe --help` on Windows or `ao --help` on Unix
- `ao.exe compile`, `ao.exe prompt-plan`, `ao.exe prompt-replan`, `ao.exe run`, and `ao.exe resume` on Windows
- The same commands with `ao` on Unix
- `ao.exe mcp stdio` / `ao mcp stdio` for optional local MCP

### Guide output

Run the apphost directly with `--guide`. It reads the English docs and version-matched workflow contract docs shipped beside the executable and emits JSON with `version`, `docs_root`, `guide_path`, `contract_docs_root`, `contract_index_path`, and `builder_template_path`. The apphost does not embed these pages; missing package docs are an error.

### Workflow builder and contract references

The same-version package includes `docs/contracts/index.md`, fenced `.cs.md` references generated from Abstractions, and the `workflow-instance-builder.csx.md` template. These are Markdown references, not standalone `.cs` or `.csx` samples. The `--guide` result returns absolute `contract_docs_root`, `contract_index_path`, and `builder_template_path` values. `--workflow-script --mode export` creates an editable script from an eligible draft; `--mode build` generates the canonical workflow JSON, which must be compiled by the same apphost and version. See [the complete authoring route](../../../docs/en/guides/skill-usage.md).
### Docs



- Loom Agent Plan-Execution Orchestrator guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/ao-guide.md>

- CLI reference: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/guides/ao-cli-usage.md>

- Stable package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>

- Beta package index: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>



## 中文



Loom Agent Plan-Execution Orchestrator 的探索式编排 CLI 包。



这个包是 Loom Agent Plan-Execution Orchestrator 的 runtime 表面。它暴露与版本匹配的离线英文文档包、compile 入口、Loom Agent Plan-Execution Orchestrator 自有的 prompt-plan / prompt-replan 支持表面，以及会返回机器可读控制载荷和审计 artifact links 的 run/resume CLI。Loom Agent Plan-Execution Orchestrator 在本项目里是 CLI-only。



### Runtime package

从稳定版或 beta package index 获取精确的 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package。使用前校验 package hash、RID、manifest、apphost 和 guide。仓库项目仍用于源码构建和测试；已发布 runtime 通过 package apphost 运行。

### 主要入口

- Windows 使用 `ao.exe --guide`，Unix 使用 `ao --guide`
- Windows 使用 `ao.exe --help`，Unix 使用 `ao --help`
- Windows 使用 `ao.exe compile`、`ao.exe prompt-plan`、`ao.exe prompt-replan`、`ao.exe run` 和 `ao.exe resume`
- Unix 使用相同命令名的 `ao` apphost
- `ao.exe mcp stdio` / `ao mcp stdio` 用于后续可选本机 MCP

### Guide 输出

直接运行 apphost 并传入 `--guide`。它会读取可执行文件旁边的英文 docs 和同版本 workflow contract 文档，并输出包含 `version`、`docs_root`、`guide_path`、`contract_docs_root`、`contract_index_path` 和 `builder_template_path` 的 JSON。Apphost 不嵌入这些页面；package 缺少文档时命令会报错。

### Workflow builder 与契约参考

同版本 package 包含 `docs/contracts/index.md`、从 Abstractions 生成且用 fenced code 展示的 `.cs.md` 参考页，以及 `workflow-instance-builder.csx.md` 模板。这些是 Markdown 参考，不是独立 `.cs` 或 `.csx` 示例。`--guide` 结果会返回绝对路径 `contract_docs_root`、`contract_index_path` 和 `builder_template_path`。`--workflow-script --mode export` 从符合条件的 draft 生成可编辑脚本；`--mode build` 生成规范 workflow JSON，之后必须由同一 apphost 和版本 compile。完整 authoring route 见[使用指南](../../../docs/zh-cn/guides/skill-usage.md)。
### 文档



- Loom Agent Plan-Execution Orchestrator guide：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/ao-guide.md>

- CLI 参考：<https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/guides/ao-cli-usage.md>

- 稳定包索引：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>

- Beta 包索引：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>
