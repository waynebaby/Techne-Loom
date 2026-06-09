# CLI Reference

[中文](../../zh-cn/reference/cli.md)

## AgentOrchestrator

- `ao --guide`
- `ao --guide --lang en|zh-cn --section <section> --export <path>`
- `ao host` — starts MCP/stdio server using the official ModelContextProtocol C# SDK
- `ao run --objective-file <path> --workflow-file <path> --event-log-file <path> [--context-file <path>]`
- `ao resume --workflow-file <path> --event-log-file <path> --result-file <path>`

## AO Output Contract

- AO-owned control metadata is emitted as a `<ao_property>` block containing one JSON payload.
- The control payload uses snake_case field names: `status`, `boundary_reason`, `workflow_file`, `event_log_file`, `current_node_id`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`.
- `status` values: `active`, `blocked`, `completed`, `failed`.
- `boundary_reason` values (when `status` is `blocked`): `clarification_required`, `delegation_required`, `tool_probe_required`, `sampling_required`.
- When `boundary_reason` is `sampling_required`, the payload includes a `sampling_request` object with `objective` and `artifacts[]`.
- `ao resume --result-file` expects a JSON envelope with `transition_id`, optional `correlation_key`, and optional `payload`.
- The event log is an append-only `.jsonl` file recording boundary events and status changes only.
- MCP tools exposed: `AoRun`, `AoResume`.

## SkillOrchestrator

- `so --guide`
- `so run`
- `so resume`
- `so status`
- `so inspect-workflow`
- `so inspect-events`
- shorthand entrypoints such as `so ls`
- `so --guide --lang en|zh-cn --section <section> --export <path>`

## Skill Output Contract

- Wrapped external command output opens a `<wrapped_exec>` block, streams lines inside `<exectionstream>`, and closes the block when the wrapped command ends.
- Each wrapped block contains a `<commandline>` child plus an `<exectionstream>` child.
- SO-owned control metadata is emitted as a separate `<so_property>` block containing one JSON payload.
- Event-history details continue to persist beside the workflow as `.events.jsonl` sidecar files.
- The JSON payload currently uses snake_case contract field names such as `workflow_file`, `event_log_file`, `current_node_id`, `required_inputs`, and `memory_for_next_step`.
- `so resume --result-file` expects a JSON object with `transition_id`, optional `correlation_key`, and `payload`.
