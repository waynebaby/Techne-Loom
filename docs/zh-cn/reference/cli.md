# CLI 参考

[English](../../en/reference/cli.md) | [根目录](../README.md)

## AgentOrchestrator（`dotnet ao.dll`）

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage、命令表面与校验产物说明 |
| `--guide` | 无 | 无 | 安装与版本匹配的英文文档包，并输出 JSON 路径 |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围 |
| `--schema-demo-output` | `<directory>` | 无 | 从当前 runtime 合同和 demo 一次性写出完整文件集：`workflow.schema.json`、`workflow.demo.json`、`workflow.model.cs`、`workflow.demo.cs` 与 `workflow.demo.verify.cs` |
| `--workflow-script` | `--mode`、`--script-file`、`--input-file`、`--output-file` | `--base-workflow-file`、`--verify-script`、`--reference-workflow-file`、`--verification-output-file`、`--audit-output`、`--workspace-root` | 执行磁盘上的普通 `.cs` Build 或 Edit 脚本，运行内置验证检查和可选 Verify 脚本，并写出 candidate/audit 文件；不需要 project 文件 |
| `compile` | `--workflow-file` | `--audit-output`、`--workspace-root` | 校验已有 AO workflow JSON，并输出 Mermaid/HTML 校验产物 |
| `prompt-plan` | `--objective-file` | `--context-file` | 输出 AO 自有的 planner prompt 文本，用于 WorkflowInstance 文件生成 |
| `prompt-replan` | `--session-dir`、`--session-id`、`--instance-file`、`--tbr-id` | 无 | 输出 AO 自有的 replanner prompt 文本，用于 WorkflowInstance 的 TBR 结点替换 |
| `run` | `--objective-file`、`--session-dir` | `--context-file`、`--instance-file`、`--audit-output`、`--workspace-root` | 执行 AO，直到 blocked 或 completed |
| `resume` | `--session-dir`、`--session-id`、`--result-file` | `--audit-output`、`--workspace-root` | 通过结构化结果 envelope 恢复 AO |

### 文件输入契约

所有 `*-file` 参数和所有表示已有文件的路径参数都只接受磁盘文件路径，不接受内联内容。调用者必须在启动一次 CLI 命令前，一次性生成、写完并关闭全部输入文件：脚本、JSON 输入、基础 workflow、reference workflow、验证脚本、patch 内容、patch 目标、workflow、objective、context、instance 和 resume result。

CLI 会先检查这一批输入文件，再读取输入、执行脚本、修改目标或写出结果。它不会在多次调用之间临时拼文件，也不会在上层缝缝补补。`--script-content`、`--input-json`、`--patch-content`、`--replacement-text` 等内联内容参数都会被拒绝。输出文件和输出目录是 CLI 的写入目标；调用者只提供目标路径，不把它们当作输入内容。
### Guide 契约

运行不带额外参数的 `dotnet ao.dll --guide`。它会读取与可执行文件放在同一个完整 runtime package 中的英文 `docs/en` 文档树，并输出包含实际 `version`、`docs_root` 与 `guide_path` 绝对路径的 JSON 对象。可执行文件本身不包含 guide 页面；如果 package docs 缺失，命令会报错。

标准输出只包含一个 JSON 对象：

```json
{
  "version": "<package-version>",
  "docs_root": "C:\\runtime\\docs\\<package-version>",
  "guide_path": "C:\\runtime\\docs\\<package-version>\\guides\\ao-guide.md"
}
```

将 `guide_path` 作为与当前版本匹配的权威 guide 入口。只有 guide 无法消除疑问时，才查看 `docs_root`。非致命安装警告写入 stderr。命令只支持英文，并拒绝 `--lang`、`--section` 与 `--export`。

### AO 示例

```bash
dotnet ao.dll --guide
dotnet ao.dll --patch --patch-content-file patch.txt --patch-target target.cs --from-line 120 --to-line 148
dotnet ao.dll compile --workflow-file ao-plan.json --audit-output outputs\audit
dotnet ao.dll --schema-demo-output outputs\schema-demo
dotnet ao.dll --workflow-script --mode build --script-file outputs\schema-demo\workflow.demo.cs --input-file inputs\ao.json --output-file outputs\candidate.json --verify-script outputs\schema-demo\workflow.demo.verify.cs --reference-workflow-file outputs\schema-demo\workflow.demo.json --verification-output-file outputs\verification.json
dotnet ao.dll prompt-plan --objective-file objective.md --context-file context.json
dotnet ao.dll prompt-replan --session-dir outputs\sessions --session-id 20260609010101_abc12345 --instance-file workflow-instance.json --tbr-id transition.main_tbr
dotnet ao.dll run --objective-file objective.md --context-file context.json --instance-file workflow-instance.json --session-dir outputs\sessions --audit-output outputs\audit
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260609010101_abc12345 --result-file resume.json --audit-output outputs\audit
```

### AO 输出契约重点

- `--guide` 返回 `version`、`docs_root`、`guide_path` 三个 JSON 字段；不会把 guide Markdown 写入标准输出
- 控制载荷通过 `<ao_property>` 输出
- 当前 payload 字段包括：`status`、`session_id`、`workflow_file`、`workflow_instance_file`、`event_log_file`、`current_node_id`、`boundary_reason`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`weave_out_request`、`audit_artifacts`
- prompt 命令会输出 `<ao_property type="prompt">`，其中包含 AO 自有、由代码生成的 prompt 文本，以及 `command`、`prompt_kind`、`prompt_template_version`、`blocks`、`allowed_node_kinds`、`allowed_command_kinds` 和 prompt 专用 workflow/TBR 锚点元数据
- compile 校验产物与 run/resume 审计产物都落在 `{output}/wf-{wfid}/step-{seq}-{action}/`
- `audit_artifacts` 当前还会返回 `summary_file`；该文件汇总本 step 的状态、boundary、frontier、workflow 路径与 artifact links，适合作为直接复盘入口
- `--workspace-root <directory>` 是可选参数，但必须指向 skill 目录之外的已有目录。传入后，AO 会把 Mermaid 和 HTML 镜像到 workspace 下新的、被忽略的 `temp/exec-<timestamp>-mermaid-delivery-result/` 目录，并用 SHA-256 校验两个副本。
- `audit_artifacts.mermaid_delivery` 分开记录 `artifact_generated`、`link_resolvable`、`visual_preview_rendered` 和 `card_display_available`。它的 `status` 可以是 `workspace_mirror`、`runtime_path_only` 或 `delivery_failed`；只有其中经过验证的 workspace 相对路径可以作为链接目标。
- `must_show_to_user_files` 只是审计连续性清单，不保证链接可打开。宿主可以把 `card_input_file` 传给 Mermaid card 工具；否则应先放已验证的 Mermaid 链接，再放 HTML 链接，交付失败后绝不能猜路径。
- 未传 `--audit-output` 时，AO 默认使用临时输出根目录
- AO workflow JSON 由 AO CLI 之外的调用方产出，通常由调用 agent 编写，然后再通过 `dotnet ao.dll compile --workflow-file <path>` 做校验
- `run --instance-file <path>` 允许调用方把运行时起点显式锚定到一个外部编写的 `WorkflowInstance`，这样从 compile 到第一次 blocked runtime audit 都沿同一份图推进
- 未传 `--instance-file` 时，AO 仍会生成 runtime audit artifact，但默认图模式是 `minimal-sidecar-only`：图上会直接显示 blocked seam 与 boundary metadata，不能把它误读为 caller-authored 的完整执行图
- AO 当前故意保留两种运行时持久化形状：`workflow_file` 继续作为 blocked seam 校验用的 snapshot 控制文件，而 `workflow_instance_file` 则指向用于审计连续性与 replan 编辑的调用方图或 runtime sidecar 图
- 在 `session_dir` 下，AO 还会维护 `session_<id>_runtime.workflow.json` 作为 runtime `WorkflowInstance` sidecar，并维护 `session_<id>_runtime.workflow.pointer.json` 作为可选指针文件，用来记住外部 caller-managed `workflow_instance_file`
- `session_<id>_events.jsonl` 现在会附带 step 级审计链接字段，如 `step_sequence`、`step_directory`、`summary_file`，以及 boundary 事件上的 `pending_requirements`、`next_frontier`
- compile 遇到目标 step 目录里已有 artifact 文件时会直接失败，而不是覆盖，并在错误 payload 里报告冲突路径
- AO 在本项目里是 CLI-only；没有公开 MCP 表面
- 当 GitHub Copilot 场景满足条件时，优先直接使用 `--patch` 作为按行替换接口；在其他平台或工具中，可把它视为常规补丁应用失败后的命令行兜底方案

## SkillOrchestrator（`dotnet so.dll`）

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage、命令表面与校验产物说明 |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围 |
| `--schema-demo-output` | `<directory>` | 无 | 从当前 runtime 合同和 demo 一次性写出完整文件集：`workflow.schema.json`、`workflow.demo.json`、`workflow.model.cs`、`workflow.demo.cs` 与 `workflow.demo.verify.cs` |
| `--workflow-script` | `--mode`、`--script-file`、`--input-file`、`--output-file` | `--base-workflow-file`、`--verify-script`、`--reference-workflow-file`、`--verification-output-file`、`--audit-output`、`--workspace-root` | 执行磁盘上的普通 `.cs` Build 或 Edit 脚本，运行内置验证检查和可选 Verify 脚本，并写出 candidate/audit 文件；不需要 project 文件 |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围 |
| `compile` | `--workflow-file` | `--audit-output`、`--workspace-root` | 校验已有 SO workflow JSON，并输出 Mermaid/HTML 校验产物 |
| `run` | `--workflow-file` | `--context-file`、`--audit-output`、`--workspace-root` | 执行 SO，直到 blocked 或 completed |
| `resume` | `--workflow-file`、`--result-file` | `--audit-output`、`--workspace-root` | 通过结构化结果 envelope 恢复 SO |
| `copy-audit-step` | `--source-step`、`--workflow-id`、`--sequence`、`--action`、`--audit-output`、`--reason`、`--verified-by` | 无 | 复制带 reuse provenance 的已验证审计产物；不会推进 workflow 状态 |
| `status` | `--workflow-file` | 无 | 输出当前状态 payload |
| `inspect-workflow` | `--workflow-file` | 无 | 打印当前 workflow JSON |
| `inspect-events` | `--workflow-file` | 无 | 打印 `.events.jsonl` sidecar |
| `ls` | 路径参数可选 | 无 | 运行内建示例 deterministic workflow |

### SO guide 契约

`dotnet so.dll --guide` 使用与 AO 相同的 JSON 契约和目录规则。它的 `guide_path` 指向 `guides/so-guide.md`。它不接受任何额外参数，并拒绝 `--lang`、`--section` 与 `--export`。

### SO 示例

```bash
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll --schema-demo-output outputs\schema-demo
dotnet so.dll --patch --patch-content-file patch.txt --patch-target workflow.current.json --from-line 25 --to-line 40
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
```

### SO 输出契约重点

- `--guide` 返回 `version`、`docs_root`、`guide_path` 三个 JSON 字段；不会把 guide Markdown 写入标准输出
- 被封装的命令输出通过 `<wrapped_exec>` 流式输出
- `--workspace-root <directory>` 是可选参数，但必须指向 skill 目录之外的已有目录。传入后，SO 会把 Mermaid 和 HTML 镜像到 workspace 下新的、被忽略的 `temp/exec-<timestamp>-mermaid-delivery-result/` 目录，并用 SHA-256 校验两个副本。
- `audit_artifacts.mermaid_delivery` 分开记录 `artifact_generated`、`link_resolvable`、`visual_preview_rendered` 和 `card_display_available`。它的 `status` 可以是 `workspace_mirror`、`runtime_path_only` 或 `delivery_failed`；只有其中经过验证的 workspace 相对路径可以作为链接目标。
- `must_show_to_user_files` 只是审计连续性清单，不保证链接可打开。宿主可以把 `card_input_file` 传给 Mermaid card 工具；否则应先放已验证的 Mermaid 链接，再放 HTML 链接，交付失败后绝不能猜路径。
- 未传 `--audit-output` 时，SO 默认使用临时输出根目录
- 当前 payload 字段包括：`workflow_file`、`instance_id`、`status`、`current_node_id`、`current_step_kind`、`skill_hint`、`memory_for_next_step`、`required_inputs`、`event_log_file`、`audit_artifacts`
- compile 校验产物与 run/resume 审计产物都落在 `{output}/wf-{wfid}/step-{seq}-{action}/`
- 未传 `--audit-output` 时，SO 默认使用临时输出根目录
- SO compile 也会在目标 step 目录里已有 artifact 文件时直接失败，而不是覆盖，并在错误 payload 里报告冲突路径
- 对于 Loom-governanced target-skill template，SO compile 与 workflow load 会拒绝缺失根 `validation` 契约、非法 `AskUser` ownership 请求、只靠治理字段到达 `done` 的路径，以及未发布 strongest-earned business outputs 的 blocked route
- 当 GitHub Copilot 场景满足条件时，优先直接使用 `--patch` 作为按行替换接口；在其他平台或工具中，可把它视为常规补丁应用失败后的命令行兜底方案

SO 公开参数契约的 review 目标：

- `planner` 保持 AO 术语，不应继续视为 SO 的公开命令名
- SO 公开 CLI 的 review 目标是：先在别处产出 workflow JSON，再用 `compile` 负责合法性校验和 Mermaid/HTML 输出；对于 Loom-governanced target-skill template，`compile` 还会校验根 governed-template 契约、route-aware business-output gates、seam ownership 与 done reachability
