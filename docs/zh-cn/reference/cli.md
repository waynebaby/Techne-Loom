# CLI 参考

[English](../../en/reference/cli.md) | [根目录](../README.md)

AO 与 SO 以自包含的 product+RID package 发布。Unix 直接运行解压出的 `ao`/`so` apphost，Windows 运行 `ao.exe`/`so.exe`。package 选择和完整性校验见[平台检测步骤](runtime/platform-detection.md)；CLI 不再提供 runtime resolve 命令或 DLL 启动模式。

## Loom Agent Plan-Execution Orchestrator

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage 与校验产物说明 |
| `--guide` | 无 | 无 | 返回包含精确版本、文档根目录和 guide 路径的 JSON |
| `mcp stdio` | 无 | 无 | 启动本机按行传输的 JSON-RPC MCP server |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 文件替换一个闭区间行范围 |
| `--schema-demo-output` | `<directory>` | 无 | 写出 workflow schema、demo JSON 和生成的 C# 合同文件 |
| `--workflow-script` | `--mode`、`--script-file`、`--input-file`、`--output-file` | `--base-workflow-file`、`--verify-script`、`--reference-workflow-file`、`--verification-output-file`、`--audit-output`、`--workspace-root` | 执行磁盘上的 Build 或 Edit 脚本并校验结果 |
| `compile` | `--workflow-file` | `--audit-output`、`--workspace-root` | 校验 AO workflow 并输出编译反馈和审计视图 |
| `prompt-plan` | `--objective-file` | `--context-file` | 输出 AO planner prompt，用于编写 WorkflowInstance |
| `prompt-replan` | `--workflow-file`、`--tbr-id` | `--objective-file` | 为现有 workflow 输出 replanner prompt |
| `run` | `--workflow-file` | `--context-file`、`--operation-id`、`--audit-output`、`--workspace-root` | 运行 workflow，直到阻塞或完成 |
| `resume` | `--workflow-file`，以及 `--result-file`、`--ask-id`、`--offline-submission-file` 中的一个 | `--operation-id`（仅与 `--result-file` 一起使用）、`--audit-output`、`--workspace-root` | 根据结果 envelope、已保存的 AskUser 回执或带版本的离线提交恢复运行 |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`、`--json-pointer`、大小限制 | 返回摘要或一个有界 workflow 片段 |
| `inspect-contract-fragment` | `--workflow-file` 或 `--contract-file` | `--json-pointer`、大小限制 | 为诊断读取有界 contract 片段 |

## SkillOrchestrator

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage 与校验产物说明 |
| `--guide` | 无 | 无 | 返回包含精确版本、文档根目录和 guide 路径的 JSON |
| `mcp stdio` | 无 | 无 | 启动本机按行传输的 JSON-RPC MCP server |
| `mcp generate-config` | `--output-file` | `--format vscode|claude`、`--server-name`、`--force` | 根据当前运行 apphost identity 生成可选 MCP 配置 |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 文件替换一个闭区间行范围 |
| `--schema-demo-output` | `<directory>` | 无 | 写出 workflow schema、demo JSON 和生成的 C# 合同文件 |
| `--workflow-script` | `--mode`、`--script-file`、`--input-file`、`--output-file` | 校验和审计参数 | 执行磁盘上的 workflow Build 或 Edit 脚本 |
| `compile` | `--workflow-file` | `--audit-output`、`--workspace-root` | 校验 SO workflow 并输出编译反馈和审计视图 |
| `run` | `--workflow-file` | `--context-file`、`--operation-id`、`--audit-output`、`--workspace-root` | 运行 workflow，直到阻塞或完成 |
| `resume` | `--workflow-file`，以及 `--result-file`、`--ask-id`、`--offline-submission-file` 中的一个 | `--operation-id`（仅与 `--result-file` 一起使用）、`--audit-output`、`--workspace-root` | 根据结果 envelope、已保存的 AskUser 回执或带版本的离线提交恢复运行 |
| `copy-audit-step` | `--source-step`、`--workflow-id`、`--sequence`、`--action`、`--audit-output`、`--reason`、`--verified-by` | 无 | 带 provenance 复制经过验证的审计文件；不推进 workflow 状态 |
| `status` | `--workflow-file` | 无 | 输出当前 workflow 状态 |
| `inspect-workflow` | `--workflow-file` | 无 | 打印当前 workflow JSON |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`、`--json-pointer`、大小限制 | 返回有界摘要或片段 |
| `inspect-contract-fragment` | `--workflow-file` 或 `--contract-file` | `--json-pointer`、大小限制 | 为诊断读取有界 contract 片段 |
| `inspect-events` | `--workflow-file` | 无 | 打印事件 sidecar |
| `ls` | 可选路径 | 无 | 运行内建示例 workflow |

## 文件输入

每个 `*-file` 参数都是已有文件的路径，不接受内联内容。调用命令前必须准备并关闭完整输入集，包括脚本、JSON、workflow、objective、context 和 resume result。CLI 会先校验输入；输出路径由 CLI 创建和写入。

## 结构化 AskUser 恢复

执行 workflow-file resume 时，必须且只能提供一种输入来源：

- `--result-file` 保留现有结果 envelope 路径。结构化 `AskUser` 的原始答案必须改用经过校验的回执或离线提交。
- `--ask-id` 应用 ask worker 已保存并校验的回执。
- `--offline-submission-file` 导入离线表单下载的带版本 JSON。

回执模式使用回执或提交中携带的 operation ID，不接受 `--operation-id`。离线提交使用以下形状；`answers` 以稳定的 question ID 为键：

```json
{
  "schemaVersion": 1,
  "askId": "<ask-id>",
  "expectedGeneration": 4,
  "operationId": "<operation-id>",
  "answers": {
    "question-id": {
      "value": "approved",
      "skipped": false,
      "attachmentIds": []
    }
  }
}
```

离线 HTML 副本包含已有附件的元数据；下载的 JSON 会保留附件 ID，但不含元数据或字节。离线副本无法下载已有附件，也无法上传新附件。

## Guide 输出

Windows 直接运行 `ao.exe --guide`/`so.exe --guide`；Unix 运行 `ao --guide`/`so --guide`。每个 apphost 只输出一个 JSON 对象：

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

命令不接受额外参数。读取返回的 `guide_path`；只有 guide 尚未回答问题时才检查 `docs_root`。失败 stderr 不能作为 guide evidence。

## 直接运行 Apphost

```powershell
.\ao.exe --guide
.\ao.exe --schema-demo-output outputs\schema-demo
.\ao.exe compile --workflow-file workflow.json --audit-output outputs\audit
.\ao.exe run --workflow-file workflow.json --context-file context.json --operation-id run-001
.\ao.exe resume --workflow-file workflow.json --result-file resume.json --operation-id resume-001
.\ao.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\ao.exe resume --workflow-file workflow.json --offline-submission-file submission.json

.\so.exe --guide
.\so.exe --schema-demo-output outputs\schema-demo
.\so.exe compile --workflow-file so-template.json --audit-output outputs\audit
.\so.exe mcp generate-config --output-file outputs\mcp.json --format vscode
.\so.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\so.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

Unix 使用 `./ao` 或 `./so` 执行相同命令。完整命令列表可通过 apphost 的 `--help` 查看。

## 审计输出

编译反馈使用 `workflow.compile-feedback.v1`。成功的 compile、run 和 resume 会在 `{output}/wf-{wfid}/step-{seq}-{action}/` 写入审计文件。Compile 不会覆盖已有文件，编译失败也不会生成占位渲染。提供 `--workspace-root` 时，经过验证的 Mermaid 和 HTML 副本会镜像到 workspace，供编辑器打开。
