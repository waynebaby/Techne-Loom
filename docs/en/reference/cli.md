# CLI Reference

[中文](../../zh-cn/reference/cli.md)

## AgentOrchestrator

- `dotnet ao.dll --guide`
- `dotnet ao.dll --guide --lang en|zh-cn --section <section> --export <path>`
- `dotnet ao.dll host` — starts MCP/stdio server using the official ModelContextProtocol C# SDK
- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>]`
- `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path>`
- In repo terminology, `dotnet ao.dll run` may weave out and `dotnet ao.dll resume` is the weave-back entry point.

## AO Output Contract

- AO-owned control metadata is emitted as a `<ao_property>` block containing one JSON payload.
- The control payload uses snake_case field names: `status`, `session_id`, `boundary_reason`, `workflow_file`, `event_log_file`, `current_node_id`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `weave_out_request`.
- `dotnet ao.dll run` generates `session_id`; callers persist only that ID.
- AO derives artifact files from `session_dir + session_id` using `session_<session_id>_workflow.json` and `session_<session_id>_events.jsonl`.
- The current runtime emits `status` values `blocked` and `completed` in the control payload.
- `boundary_reason` values (when `status` is `blocked`): `clarification_required`, `delegation_required`, `tool_probe_required`, `weave_out_required`.
- When `boundary_reason` is `weave_out_required`, the payload includes `weave_out_request` with `objective` and `artifacts[]`.
- `result_file` is a reserved optional field for future AO-owned output artifacts and is not populated by the current runtime.
- `dotnet ao.dll resume --result-file` expects a JSON envelope with `transition_id`, optional `correlation_key`, and optional `payload`. That envelope is AO's current weave-back sidecar.
- The event log is an append-only `.jsonl` file recording boundary events and status changes only.
- CLI/runtime failures are surfaced as `<ao_property>` with `type: "error"` rather than a control payload with `status: failed`.
- MCP tools exposed: `AoRun`, `AoResume`.
- `AoRun` and `AoResume` also accept an optional `invocation_context` object for per-call host execution metadata; this avoids relying on ambient `IMcpServer` injection for future non-stdio weave-out routes.

## SkillOrchestrator

- `dotnet so.dll --guide`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`
- shorthand entrypoints such as `dotnet so.dll ls`
- `dotnet so.dll --guide --lang en|zh-cn --section <section> --export <path>`
- In repo terminology, `dotnet so.dll run` may weave out when it reaches an externally owned step, and `dotnet so.dll resume` is the weave-back entry point.

## Skill Output Contract

- Wrapped external command output opens a `<wrapped_exec>` block, streams lines inside `<exectionstream>`, and closes the block when the wrapped command ends.
- Each wrapped block contains a `<commandline>` child plus an `<exectionstream>` child.
- SO-owned control metadata is emitted as a separate `<so_property>` block containing one JSON payload.
- Event-history details continue to persist beside the workflow as `.events.jsonl` sidecar files.
- The JSON payload currently uses snake_case contract field names such as `workflow_file`, `event_log_file`, `current_node_id`, `required_inputs`, and `memory_for_next_step`.
- `dotnet so.dll resume --result-file` expects a JSON object with `transition_id`, optional `correlation_key`, and `payload`. That JSON object is SO's current weave-back sidecar.
