# MCP Reference

[中文](../../zh-cn/reference/mcp.md)

## AgentOrchestrator MCP Surface

AO currently exposes MCP tools over the official `MCP/stdio` host:

| Tool | Inputs | Outputs |
| --- | --- | --- |
| `AoRun` | `objective`, `context`, `sessionDirectory`, optional `invocation_context`, optional `audit_output` | `AoControlPayload` |
| `AoResume` | `sessionDirectory`, `sessionId`, `transitionId`, optional `correlationKey`, optional `payload`, optional `invocation_context`, optional `audit_output` | `AoControlPayload` |

### `AoControlPayload`

- `status`: `blocked` or `completed`
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

Audit artifacts are persisted under `{output}/wf-{wfid}/step-{seq}-{action}/`. If `audit_output` is omitted, AO uses a temporary output root.

## SkillOrchestrator MCP Surface

SO does not currently expose a public MCP tool surface in this repository slice.

Use the CLI/package contract for SO instead.
