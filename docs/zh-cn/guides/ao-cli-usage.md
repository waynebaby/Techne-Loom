# AO CLI 用法

[English](../../en/guides/ao-cli-usage.md) | [根目录](../README.md)

本文是 Loom Agent Plan-Execution Orchestrator（`ao`）CLI 的唯一权威文档，集中说明命令、文件输入、AskUser、输出和审计行为。SO 请参阅独立的 [SO CLI 用法](so-cli-usage.md)。AO 与 SO 是相互独立的产品。

AO 以自包含的 product+RID package 发布。Unix 直接运行解压出的 `ao` apphost，Windows 运行 `ao.exe`。package 选择和完整性校验见[平台检测步骤](../reference/runtime/platform-detection.md)；CLI 不提供 runtime resolve 命令或 DLL 启动模式。

## 命令

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage 与校验产物说明 |
| `--guide` | 无 | 无 | 返回包含精确版本、文档根目录和 guide 路径的 JSON |
| `mcp stdio` | 无 | 无 | 启动本机按行传输的 JSON-RPC MCP server |
| `ask start` | `--contract-file` | 无 | 无需 workflow，启动独立结构化提问会话 |
| `ask result` | `--ask-id` | 无 | 读取独立提问状态、类型化答案和回执 |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 文件替换一个闭区间行范围 |
| `--schema-demo-output` | `<directory>` | 无 | 写出 workflow schema、demo JSON 和生成的 C# 合同文件 |
| `--workflow-script` | `--mode`、`--script-file`、`--input-file`、`--output-file` | `--base-workflow-file`、`--verify-script`、`--reference-workflow-file`、`--verification-output-file`、`--audit-output`、`--workspace-root` | 执行磁盘上的 Build 或 Edit 脚本并校验结果 |
| `skeleton-check` | `--workflow-file`、`--skeleton-file` | 无 | 检查独立阶段骨架与所属 workflow 是否一致 |
| `phase-compile` | `--workflow-file`、`--skeleton-file`、`--fragment-file`、`--phase-id`、`--output-file` | 无 | 对一个隔离阶段执行静态校验 |
| `impact` | `--previous-workflow-file`、`--previous-skeleton-file`、`--candidate-workflow-file`、`--candidate-skeleton-file`、`--output-file` | `--previous-result-file`、`--candidate-result-file`、`--changed-phase-id`（可重复） | 比较结构化变更并确定需要重验的阶段 |
| `merge` | `--workflow-file`、`--skeleton-file`、`--result-file`（可重复）、`--output-file` | 无 | 用通过校验的阶段结果写入新 candidate |
| `compile` | `--workflow-file` | `--audit-output`、`--workspace-root` | 校验 AO workflow 并输出编译反馈和审计视图 |
| `prompt-plan` | `--objective-file` | `--context-file` | 输出 AO planner prompt，用于编写 WorkflowInstance |
| `prompt-replan` | `--workflow-file`、`--tbr-id` | `--objective-file` | 为现有 workflow 输出 replanner prompt |
| `run` | `--workflow-file` | `--context-file`、`--operation-id`、`--audit-output`、`--workspace-root` | 运行 workflow，直到阻塞或完成 |
| `resume` | `--workflow-file`，以及 `--result-file`、`--ask-id`、`--offline-submission-file` 中的一个 | `--operation-id`（仅与 `--result-file` 一起使用） | 根据结果 envelope、已保存的 AskUser 回执或带版本的离线提交恢复运行 |
| `status` | `--workflow-file` | 无 | 输出当前 workflow 状态 |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`、`--json-pointer`、大小限制 | 返回摘要或一个有界 workflow 片段 |
| `inspect-contract-fragment` | `--workflow-file` 或 `--contract-file` | `--json-pointer`、大小限制 | 为诊断读取有界 contract 片段 |

有界片段读取的大小参数为 `--max-bytes`、`--max-array-items`、`--max-object-properties` 和 `--max-depth`。不指定 `--json-pointer` 时，workflow 检查返回摘要；指定后返回选定的有界片段。

## 文件输入

每个 `*-file` 参数都是已有文件的路径，不接受内联内容。调用命令前必须准备并关闭完整输入集，包括脚本、JSON、workflow、objective、context 和 resume result。CLI 会先校验输入；输出路径由 CLI 创建和写入。

## 分阶段 Workflow 设计

只统计已编写状态。少于 40 个已编写 `StateNode` 的 workflow 使用整体设计路线；达到 40 个时，先创建并冻结类型化的 `loom.workflow-skeleton.v1`，再编写隔离阶段。每阶段以 25 个已编写状态为软上限。整条路线都使用同一个已选 AO apphost。

`skeleton-check` 检查骨架与 workflow 是否一致。`phase-compile` 只检查单个片段的图、表达式、投影和已声明接口；不会运行脚本、HTTP、AskUser 或 subagent。集中汇总接口变更请求并更新骨架，然后运行 `impact`。Impact 比较结构化定义和依赖，不比较哈希。只重验受影响阶段及已证明的依赖项；所有权或依赖关系未知时，必须完整重验。

`merge` 要求每个预期阶段恰好有一个通过的结果，并写入新的 candidate。随后对这个精确的 candidate 执行 AO 完整 `compile`。不得修改或迁移正在运行的 `WorkflowInstance`。每阶段最多修复三次，合并后完整 compile 最多修复两轮；达到任一上限时，保留证据并请求明确决定。

Designer 返回链接到现有 `static-contract-review.json` 中 `phaseDesignEvidence` 的 `phase_design_evidence`。详细阶段证据保留在该记录中，不新增第四份报告。骨架、片段、结果、candidate 和审计文件都放在 checked-in skill bundle 之外。共享规则见[Workflow 模型](../architecture/workflow-model.md)，SO 命令见独立的 [SO CLI 用法](so-cli-usage.md)。

```powershell
.\ao.exe skeleton-check --workflow-file candidate.json --skeleton-file workflow-skeleton.json
.\ao.exe phase-compile --workflow-file candidate.json --skeleton-file workflow-skeleton.json --fragment-file phase-01.json --phase-id p01 --output-file phase-01.result.json
.\ao.exe impact --previous-workflow-file previous.json --previous-skeleton-file previous-skeleton.json --candidate-workflow-file candidate.json --candidate-skeleton-file workflow-skeleton.json --output-file impact.json
.\ao.exe merge --workflow-file candidate.json --skeleton-file workflow-skeleton.json --result-file phase-01.result.json --result-file phase-02.result.json --output-file merged-candidate.json
.\ao.exe compile --workflow-file merged-candidate.json --audit-output audit\merged-candidate
```

Unix 使用 `./ao` 执行相同命令。

## 文件编辑

对于文件编辑接口，`--patch` 在 GitHub Copilot 场景下，只要满足适用条件就直接使用；在其他平台或工具场景下，把它视为常规补丁应用失败后的命令行兜底。目标文件必须已存在；`--from-line` 与 `--to-line` 使用 1 基、闭区间行号；当 `--to-line` 超出 EOF 时会自动按最后一行处理；若 patch 内容文件为空，则语义是删除指定行段。

## 独立 Ask User

现有 AO/SO binary 中的共享 XO Ask 有两个平级使用方：独立于 workflow 的 `/loom-ask-user` skill，以及可选的 workflow `AskUser` 节点。XO 是 AO 或 SO 的简称，不是第三个产品。独立提问不要求或修改 `WorkflowInstance`，也不需要等待状态、context 字段或 workflow resume。

当前源码实现了 `ao ask start --contract-file <path>` 和 `ao ask result --ask-id <id>`。启动前先在磁盘上准备完整的问题合同。选定的 runtime 提供浏览器表单，通过返回的 ask ID 读取结果。这里的命令列表不表示旧版已发布 package 已支持独立提问；必须先用精确已安装 apphost 的 `--help` 核实。

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
.\ao.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\ao.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

## Guide 输出

Windows 直接运行 `ao.exe --guide`；Unix 运行 `ao --guide`。每个 apphost 只输出一个 JSON 对象：

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
.\ao.exe ask start --contract-file questions.json
.\ao.exe ask result --ask-id <ask-id>
.\ao.exe --schema-demo-output outputs\schema-demo
.\ao.exe compile --workflow-file workflow.json --audit-output outputs\audit
.\ao.exe run --workflow-file workflow.json --context-file context.json --operation-id run-001
.\ao.exe resume --workflow-file workflow.json --result-file resume.json --operation-id resume-001
.\ao.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\ao.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

Unix 使用 `./ao` 执行相同命令。运行 apphost 的 `--help` 可核实精确已安装版本的命令范围。

## 输出形状与执行状态

AO 控制状态以 `<ao_property>{json}</ao_property>` 的形式输出，使用 snake_case 字段名。按 repo 术语，`ao.exe run` / `ao run` 可能会 weave out，而 `ao.exe resume` / `ao resume` 是 weave-back 入口。canonical 的 `--workflow-file` run、resume、status 和 prompt-replan 路径都是 sessionless 的：workflow JSON 及其 `.events.jsonl` sidecar 负责保存状态，MCP/CLI 进程可以随时结束。Plan 结果 envelope 必须带非空 `result_id`；重复提交同一个 Plan 结果不会产生副作用。旧 session 形式仍作为兼容路径保留。当 AO 需要 workflow JSON 时，由调用 agent 先编写该文件，再使用 `ao.exe compile` / `ao compile` 做校验。Compile 不会覆盖已有 audit artifact 文件，而是直接失败。旧版 AO snapshot 仅在 `context` 为空时可用于只读编译渲染；带 context 值的 snapshot 必须迁移为通过 `contractBinding` 指向所属 `contract.json` 的 `WorkflowInstance`。

`ao resume --result-file` 读取包含 `transition_id`、可选 `correlation_key` 和 `payload` 的 JSON envelope。Plan 结果必须携带非空 `result_id`；每个 fan-out 结果还必须携带持久化 workflow 中当前条目的 `wait_id`。外部执行失败时，提供非空 `execution_error`，且不要提供非空结果 payload。

## 审计输出

编译反馈使用 `workflow.compile-feedback.v1`。成功的 compile、run 和 resume 会在 `{output}/wf-{wfid}/step-{seq}-{action}/` 写入审计文件。Compile 不会覆盖已有文件，编译失败也不会生成占位渲染。提供 `--workspace-root` 时，已验证的 Mermaid、HTML 以及生成时才有的 Analysis/Dataflow 会镜像到 workspace，供编辑器打开。交付 metadata 会报告 `workspace_mirror` 或带有 `mirror_error` 的 `runtime_path_only`；镜像失败时原 runtime 路径仍可用。

## 相关文档

- [AO Guide](ao-guide.md)
- [Ask User 指南](ask-user-guide.md)
- [SO CLI 用法](so-cli-usage.md)
