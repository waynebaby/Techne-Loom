# MCP 参考

[English](../../en/reference/mcp.md)

## AgentOrchestrator MCP 表面

当前 AO 通过官方 `MCP/stdio` 宿主暴露以下 MCP tools：

| Tool | 输入 | 输出 |
| --- | --- | --- |
| `AoRun` | `objective`、`context`、`sessionDirectory`、可选 `invocation_context`、可选 `audit_output` | `AoControlPayload` |
| `AoResume` | `sessionDirectory`、`sessionId`、`transitionId`、可选 `correlationKey`、可选 `payload`、可选 `invocation_context`、可选 `audit_output` | `AoControlPayload` |

### `AoControlPayload`

- `status`：`blocked` 或 `completed`
- `session_id`
- `workflow_file`
- `event_log_file`
- `current_node_id`
- `boundary_reason`
- `result_file`
- `pending_requirements`
- `next_frontier`
- `human_or_agent_hint`
- `weave_out_request`
- `audit_artifacts`

### `audit_artifacts`

- `output_root`
- `workflow_id`
- `sequence`
- `action`
- `step_directory`
- `mermaid_file`
- `html_file`
- `workflow_backup_file`

审计产物按 `{output}/wf-{wfid}/step-{seq}-{action}/` 落盘。若未传 `audit_output`，AO 默认使用临时输出根目录。

## SkillOrchestrator MCP 表面

当前仓库切片里，SO 还没有公开 MCP tool 表面。

SO 请使用 CLI / package 契约。
