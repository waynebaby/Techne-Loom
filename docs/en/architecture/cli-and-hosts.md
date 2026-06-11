# CLI And Hosts

[中文](../../zh-cn/architecture/cli-and-hosts.md)

AO and SO expose different runtime contracts because they solve different problems.

## AgentOrchestrator

- Canonical interface: CLI/package contract
- Primary job: emit control-state decisions, session_id, derived artifact paths, and blocked-payload metadata
- Runtime: implemented in `.NET` (net9.0 exe) as a CLI-first surface.
- `dotnet ao.dll planner`, `dotnet ao.dll compile`, `dotnet ao.dll run`, and `dotnet ao.dll resume` drive file-based planning, validation, and session persistence via `session_dir + session_id`.
- Control payload is a `<ao_property>` block with snake_case fields: `status`, `session_id`, `boundary_reason`, `workflow_file`, `event_log_file`, `current_node_id`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `weave_out_request`.
- AO weave-out comparison flows use `boundary_reason: weave_out_required` and the `weave_out_request` sub-object.

## SkillOrchestrator

- Canonical interface: local CLI and package contract
- Primary job: compile or load a workflow, execute SO-owned steps, and block with a strict payload when external work is required
- Supports shorthand invocations only by compiling them into a persisted workflow first
- Current CLI split:
  - wrapped shell-facing command output is emitted inside `<wrapped_exec>` blocks
  - SO-owned control metadata is emitted in separate `<so_property>` blocks
  - workflow state persists to the workflow file, with sidecar event history in `.events.jsonl`
- A blocked `<so_property>` payload is SO's current weave-out surface, and `dotnet so.dll resume --result-file` is the weave-back entry point.

## Host Separation Rule

Do not treat AO as a wrapper over SO or SO as a child runtime of AO. They can share low-level contracts while remaining independently packaged and invoked.

## Practical Continuation Rule

If another agent continues implementation work:

- treat SO docs plus tests as the current public baseline that must not be casually broken
- treat AO docs and tests as the current public AO baseline that must not be casually broken
- do not reintroduce AO MCP-specific hosting back into the public surface
