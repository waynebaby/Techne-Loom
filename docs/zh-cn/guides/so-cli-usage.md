# SO CLI 用法

[English](../../en/guides/so-cli-usage.md) | [根目录](../README.md)

本文是 Loom Skill Orchestrator（`so`）CLI 的唯一权威文档，集中说明命令、文件输入、AskUser、输出和审计行为。AO 请参阅独立的 [AO CLI 用法](ao-cli-usage.md)。AO 与 SO 是相互独立的产品。

SO 以自包含的 product+RID package 发布。Unix 直接运行解压出的 `so` apphost，Windows 运行 `so.exe`。package 选择和完整性校验见[平台检测步骤](../reference/runtime/platform-detection.md)；CLI 不提供 runtime resolve 命令或 DLL 启动模式。

## 命令

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage 与校验产物说明 |
| `--guide` | 无 | 无 | 返回包含精确版本、文档根目录和 guide 路径的 JSON |
| `mcp stdio` | 无 | 无 | 启动本机按行传输的 JSON-RPC MCP server |
| `mcp generate-config` | `--output-file` | `--format vscode\|claude`、`--server-name`、`--force` | 根据当前运行 apphost identity 生成可选 MCP 配置 |
| `ask start` | `--contract-file` | 无 | 无需 workflow，启动独立结构化提问会话 |
| `ask result` | `--ask-id` | 无 | 读取独立提问状态、类型化答案和回执 |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 文件替换一个闭区间行范围 |
| `--schema-demo-output` | `<directory>` | 无 | 写出 workflow schema、demo JSON 和生成的 C# 合同文件 |
| `--workflow-script` | `--mode`、`--script-file`、`--input-file`、`--output-file` | `--base-workflow-file`、`--verify-script`、`--reference-workflow-file`、`--verification-output-file`、`--audit-output`、`--workspace-root` | 执行磁盘上的 workflow Build 或 Edit 脚本 |
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

有界片段读取的大小参数为 `--max-bytes`、`--max-array-items`、`--max-object-properties` 和 `--max-depth`。不指定 `--json-pointer` 时，workflow 检查返回摘要；指定后返回选定的有界片段。

## 文件输入

每个 `*-file` 参数都是已有文件的路径，不接受内联内容。调用命令前必须准备并关闭完整输入集，包括脚本、JSON、workflow、objective、context 和 resume result。CLI 会先校验输入；输出路径由 CLI 创建和写入。

## 文件编辑

对于文件编辑接口，`--patch` 在 GitHub Copilot 场景下，只要满足适用条件就直接使用；在其他平台或工具场景下，把它视为常规补丁应用失败后的命令行兜底。目标文件必须已存在；`--from-line` 与 `--to-line` 使用 1 基、闭区间行号；当 `--to-line` 超出 EOF 时会自动按最后一行处理；若 patch 内容文件为空，则语义是删除指定行段。

## 独立 Ask User

现有 AO/SO binary 中的共享 XO Ask 有两个平级使用方：独立于 workflow 的 `/loom-ask-user` skill，以及可选的 workflow `AskUser` 节点。XO 是 AO 或 SO 的简称，不是第三个产品。独立提问不要求或修改 `WorkflowInstance`，也不需要等待状态、context 字段或 workflow resume。

当前源码实现了 `so ask start --contract-file <path>` 和 `so ask result --ask-id <id>`。启动前先在磁盘上准备完整的问题合同。选定的 runtime 提供浏览器表单，通过返回的 ask ID 读取结果。这里的命令列表不表示旧版已发布 package 已支持独立提问；必须先用精确已安装 apphost 的 `--help` 核实。

对于 workflow 中的问题，由所属产品校验回执、映射答案，并按下文恢复 workflow。共享 ask worker 从不锁定、修改或恢复 workflow。

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

```powershell
.\so.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\so.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

## Guide 输出

Windows 直接运行 `so.exe --guide`；Unix 运行 `so --guide`。每个 apphost 只输出一个 JSON 对象：

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
.\so.exe --guide
.\so.exe ask start --contract-file questions.json
.\so.exe ask result --ask-id <ask-id>
.\so.exe --schema-demo-output outputs\schema-demo
.\so.exe compile --workflow-file so-template.json --audit-output outputs\audit
.\so.exe mcp generate-config --output-file outputs\mcp.json --format vscode
.\so.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\so.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

Unix 使用 `./so` 执行相同命令。运行 apphost 的 `--help` 可核实精确已安装版本的命令范围。

## 输出形状与执行状态

`so` 会把套壳执行输出与 SO 自己的控制数据分开。

- 外部命令的流式输出按逐行 XML-like 片段发到 stdout：
  `<wrapped_exec>`
  `<commandline>...</commandline>`
  `<exectionstream>`
  `...持续流出的输出行...`
  `</exectionstream>`
  `</wrapped_exec>`
- SO 的状态、阻塞指导和最终结果元数据按如下形式输出：
  `<so_property>`
  `{json}`
  `</so_property>`

这样既能让 shell 里看到持续流出的 wrapped output，也能让 SO 的建议保持 machine-readable。

`<so_property>` 内部的 JSON 采用 snake_case 字段；`so.exe resume --result-file` / `so resume --result-file` 读取的 envelope 需要包含 `transition_id`、可选 `correlation_key`、Plan 结果使用的 `result_id` 以及 `payload`。每个 fan-out 结果还必须携带持久化 workflow 中当前条目的 `wait_id`。外部执行失败时，增加非空 `execution_error`，且不要提供非空结果 payload。

## 审计输出

编译反馈使用 `workflow.compile-feedback.v1`。成功的 compile、run 和 resume 会在 `{output}/wf-{wfid}/step-{seq}-{action}/` 写入审计文件。Compile 不会覆盖已有文件，编译失败也不会生成占位渲染。提供 `--workspace-root` 时，经过验证的 Mermaid 和 HTML 副本会镜像到 workspace，供编辑器打开。

## 相关文档

- [SO Guide](so-guide.md)
- [Ask User 指南](ask-user-guide.md)
- [AO CLI 用法](ao-cli-usage.md)
