# CLI 参考

[English](../../en/reference/cli.md)

## AgentOrchestrator（`dotnet ao.dll`）

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage、命令表面与校验产物说明 |
| `--guide` | 无 | `--lang`、`--section`、`--export` | 输出作者维护的 guide surface |
| `planner` / `compile` | `--plan-file`、`--workflow-file` | `--context-file`、`--audit-output` | 物化 draft AO workflow JSON 计划，并输出 Mermaid/HTML 校验产物 |
| `host` | 无 | 无 | 启动官方 MCP/stdio 服务 |
| `run` | `--objective-file`、`--session-dir` | `--context-file`、`--audit-output` | 执行 AO，直到 blocked 或 completed |
| `resume` | `--session-dir`、`--session-id`、`--result-file` | `--audit-output` | 通过结构化结果 envelope 恢复 AO |

### AO 示例

```bash
dotnet ao.dll --guide --lang zh-cn --export ao-guide.md
dotnet ao.dll planner --plan-file detailed-plan.md --workflow-file ao-plan.json --context-file context.json --audit-output outputs\audit
dotnet ao.dll run --objective-file objective.md --context-file context.json --session-dir outputs\sessions --audit-output outputs\audit
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260609010101_abc12345 --result-file resume.json --audit-output outputs\audit
```

### AO 输出契约重点

- 控制载荷通过 `<ao_property>` 输出
- 当前 payload 字段包括：`status`、`session_id`、`workflow_file`、`event_log_file`、`current_node_id`、`boundary_reason`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`weave_out_request`、`audit_artifacts`
- planner/compile 校验产物与 run/resume 审计产物都落在 `{output}/wf-{wfid}/step-{seq}-{action}/`
- 未传 `--audit-output` 时，AO 默认使用临时输出根目录

## SkillOrchestrator（`dotnet so.dll`）

| 命令 | 必填参数 | 可选参数 | 作用 |
| --- | --- | --- | --- |
| `--help` | 无 | 无 | 打印 usage、命令表面与校验产物说明 |
| `--guide` | 无 | `--lang`、`--section`、`--export` | 输出作者维护的 guide surface |
| `planner` / `compile` | `--description-file`、`--workflow-file` | `--context-file`、`--audit-output` | 物化 draft SO workflow JSON，并输出 Mermaid/HTML 校验产物 |
| `run` | `--workflow-file` | `--context-file`、`--audit-output` | 执行 SO，直到 blocked 或 completed |
| `resume` | `--workflow-file`、`--result-file` | `--audit-output` | 通过结构化结果 envelope 恢复 SO |
| `status` | `--workflow-file` | 无 | 输出当前状态 payload |
| `inspect-workflow` | `--workflow-file` | 无 | 打印当前 workflow JSON |
| `inspect-events` | `--workflow-file` | 无 | 打印 `.events.jsonl` sidecar |
| `ls` | 路径参数可选 | 无 | 运行内建示例 deterministic workflow |

### SO 示例

```bash
dotnet so.dll --guide --lang zh-cn --export so-guide.md
dotnet so.dll planner --description-file skill-plan.md --workflow-file so-template.json --context-file context.json --audit-output outputs\audit
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
```

### SO 输出契约重点

- 被封装的命令输出通过 `<wrapped_exec>` 流式输出
- 控制载荷通过 `<so_property>` 输出
- 当前 payload 字段包括：`workflow_file`、`instance_id`、`status`、`current_node_id`、`current_step_kind`、`skill_hint`、`memory_for_next_step`、`required_inputs`、`event_log_file`、`audit_artifacts`
- planner/compile 校验产物与 run/resume 审计产物都落在 `{output}/wf-{wfid}/step-{seq}-{action}/`
- 未传 `--audit-output` 时，SO 默认使用临时输出根目录
