## 获取当前 Workflow 示例

本页刻意不提供手写 workflow JSON 示例。请使用与当前 host RID 对应的精确自包含 runtime package 获取运行时认可的 JSON 形状。

从解压后的 package 目录直接运行 apphost：

```powershell
.\so.exe --help
.\so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
```

Unix 使用相同参数调用 `./so`。`compile` 只校验已有 workflow 文件，不会从空白创建 workflow。请检查返回 audit step 目录中的 `workflow.json`、`workflow.compile-feedback.json`、`workflow.mermaid.md`、`workflow.html`、`workflow.analysis.json` 和 `workflow.dataflow.json`。Feedback JSON 是编译计数与诊断的结构化来源；workflow 备份是该精确 runtime 接受的序列化形状。编译失败不会生成占位 Mermaid 或 HTML。

Mermaid Markdown 在 fenced block 中保留完整图，然后按状态的 `workflowPhase`、`name`、`id` 和 `description` 附加分阶段业务摘要。浅色节点和图例需使用明确的深色文字。

成功编译的 HTML 是审计报告，不只是图预览。它记录产品/runtime 与 workflow provenance、编译计数和诊断、控制流与 ownership 分析、gates、artifact 映射以及同一次编译生成的 transition dataflow evidence；它不会推断 workflow 的运行结果。

检查已保存的 runtime workflow 时，使用同一个 apphost 执行 `inspect-workflow --workflow-file <external-workflow.json>`。不要把 `--guide` 返回的 JSON 当作 workflow 示例；其中只有 guide 路径。

### 一起导出 Schema 与 Demo

使用一次 runtime 调用生成当前 schema contract 和可编译 demo：

```powershell
.\so.exe --schema-demo-output <external-output-directory>
.\so.exe compile --workflow-file <external-output-directory>\workflow.demo.json --audit-output <external-audit-root>
```

Unix 使用相同参数调用 `./so`。导出会生成 `workflow.schema.json`、`workflow.demo.json`、`workflow.model.cs`、`workflow.demo.cs` 和 `workflow.demo.verify.cs`。生成的 C# 文件由内置 Roslyn host 执行，不需要单独的 project 或脚本 runtime。除非用户明确要求，否则生成文件放在 skill 目录之外。更新本页时，以当前 schema 和同版本 demo 编译结果为准。

公开模型可能比当前 SO runtime 实现更广。未支持的多 transition strategy 会明确失败，不会静默降级。`so.exe compile` 或 `so compile`、run 和 resume 会在 audit step 目录写入 `workflow.analysis.json`；它概括请求输入、输出族、分支、循环、seams、gates 和控制流风险。

`copy-audit-step` 只复制经过明确验证且未变化的审计文件，并在 `audit-reuse.json` 记录 hashes；它不会推进 workflow 状态，也不会创建正式 run evidence。`compile` 会拒绝缺少非空 `workflowPhase` 的状态节点，也不会覆盖已有审计文件。
