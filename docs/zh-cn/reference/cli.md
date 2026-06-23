# CLI 参考

[English](../../en/reference/cli.md) | [根目录](../README.md)

## AgentOrchestrator（`dotnet ao.dll`）

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage、命令表面与校验产物说明 |
| `--guide` | 无 | `--lang`、`--section`、`--export` | 输出作者维护的 guide surface |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围 |
| `compile` | `--workflow-file` | `--audit-output` | 校验已有 AO workflow JSON，并输出 Mermaid/HTML 校验产物 |
| `prompt-plan` | `--objective-file` | `--context-file` | 输出 AO 自有的 planner prompt 文本，用于 WorkflowInstance 文件生成 |
| `prompt-replan` | `--session-dir`、`--session-id`、`--instance-file`、`--tbr-id` | 无 | 输出 AO 自有的 replanner prompt 文本，用于 WorkflowInstance 的 TBR 结点替换 |
| `run` | `--objective-file`、`--session-dir` | `--context-file`、`--instance-file`、`--audit-output` | 执行 AO，直到 blocked 或 completed |
| `resume` | `--session-dir`、`--session-id`、`--result-file` | `--audit-output` | 通过结构化结果 envelope 恢复 AO |

### AO 示例

```bash
dotnet ao.dll --guide --lang zh-cn --export ao-guide.md
dotnet ao.dll --patch --patch-content-file patch.txt --patch-target target.cs --from-line 120 --to-line 148
dotnet ao.dll compile --workflow-file ao-plan.json --audit-output outputs\audit
dotnet ao.dll prompt-plan --objective-file objective.md --context-file context.json
dotnet ao.dll prompt-replan --session-dir outputs\sessions --session-id 20260609010101_abc12345 --instance-file workflow-instance.json --tbr-id transition.main_tbr
dotnet ao.dll run --objective-file objective.md --context-file context.json --instance-file workflow-instance.json --session-dir outputs\sessions --audit-output outputs\audit
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260609010101_abc12345 --result-file resume.json --audit-output outputs\audit
```

### AO 输出契约重点

- 控制载荷通过 `<ao_property>` 输出
- 当前 payload 字段包括：`status`、`session_id`、`workflow_file`、`workflow_instance_file`、`event_log_file`、`current_node_id`、`boundary_reason`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`weave_out_request`、`audit_artifacts`
- prompt 命令会输出 `<ao_property type="prompt">`，其中包含 AO 自有、由代码生成的 prompt 文本，以及 `command`、`prompt_kind`、`prompt_template_version`、`blocks`、`allowed_node_kinds`、`allowed_command_kinds` 和 prompt 专用 workflow/TBR 锚点元数据
- compile 校验产物与 run/resume 审计产物都落在 `{output}/wf-{wfid}/step-{seq}-{action}/`
- `audit_artifacts` 当前还会返回 `summary_file`；该文件汇总本 step 的状态、boundary、frontier、workflow 路径与 artifact links，适合作为直接复盘入口
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
| `--guide` | 无 | `--lang`、`--section`、`--export` | 输出作者维护的 guide surface |
| `--patch` | `--patch-content-file`、`--patch-target`、`--from-line`、`--to-line` | 无 | 从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围 |
| `compile` | `--workflow-file` | `--audit-output` | 校验已有 SO workflow JSON，并输出 Mermaid/HTML 校验产物 |
| `run` | `--workflow-file` | `--context-file`、`--audit-output` | 执行 SO，直到 blocked 或 completed |
| `resume` | `--workflow-file`、`--result-file` | `--audit-output` | 通过结构化结果 envelope 恢复 SO |
| `status` | `--workflow-file` | 无 | 输出当前状态 payload |
| `inspect-workflow` | `--workflow-file` | 无 | 打印当前 workflow JSON |
| `inspect-events` | `--workflow-file` | 无 | 打印 `.events.jsonl` sidecar |
| `ls` | 路径参数可选 | 无 | 运行内建示例 deterministic workflow |

SO 公开参数契约的 review 目标：

- `planner` 保持 AO 术语，不应继续视为 SO 的公开命令名
- SO 公开 CLI 的 review 目标是：先在别处产出 workflow JSON，再用 `compile` 负责合法性校验和 Mermaid/HTML 输出；对于 SO-governed target-skill template，`compile` 还会校验根 governed-template 契约、route-aware business-output gates、seam ownership 与 done reachability

### SO 示例

```bash
dotnet so.dll --guide --lang zh-cn --export so-guide.md
dotnet so.dll --patch --patch-content-file patch.txt --patch-target workflow.current.json --from-line 25 --to-line 40
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
```

### SO 输出契约重点

- 被封装的命令输出通过 `<wrapped_exec>` 流式输出
- 控制载荷通过 `<so_property>` 输出
- 当前 payload 字段包括：`workflow_file`、`instance_id`、`status`、`current_node_id`、`current_step_kind`、`skill_hint`、`memory_for_next_step`、`required_inputs`、`event_log_file`、`audit_artifacts`
- compile 校验产物与 run/resume 审计产物都落在 `{output}/wf-{wfid}/step-{seq}-{action}/`
- 未传 `--audit-output` 时，SO 默认使用临时输出根目录
- SO compile 也会在目标 step 目录里已有 artifact 文件时直接失败，而不是覆盖，并在错误 payload 里报告冲突路径
- 对于 SO-governed target-skill template，SO compile 与 workflow load 会拒绝缺失根 `validation` 契约、非法 `AskUser` ownership 请求、只靠治理字段到达 `done` 的路径，以及未发布 strongest-earned business outputs 的 blocked route
- 当 GitHub Copilot 场景满足条件时，优先直接使用 `--patch` 作为按行替换接口；在其他平台或工具中，可把它视为常规补丁应用失败后的命令行兜底方案
