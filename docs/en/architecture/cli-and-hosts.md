# CLI And Hosts

[中文](../../zh-cn/architecture/cli-and-hosts.md) | [Root](../README.md)

Techne Loom publishes one `loom` CLI and one self-contained runtime package per RID. `loom agent` and `loom skill` expose distinct module commands through the same apphost; standalone questions use the single top-level `loom ask` route.

## Loom Agent Plan-Execution Orchestrator

- Implementation module: `Techne.Loom.AgentOrchestrator`
- Primary job: emit control-state decisions, durable workflow-file paths, and blocked-payload metadata; legacy session identifiers remain compatibility data.
- Public command route: `loom agent`; workflow operations remain disk-backed and sessionless.
- Control payload is a `<ao_property>` block with snake_case fields such as `status`, `workflow_file`, `event_log_file`, `result_file`, and `weave_out_request`.
- Planner and replanner prompt text uses `<ao_property type="prompt">` blocks.

## Skill Orchestrator

- Implementation module: `Techne.Loom.SkillOrchestrator`
- Primary job: compile or load a workflow, execute module-owned steps, and block with a strict payload when external work is required.
- Public command route: `loom skill`.
- Wrapped shell-facing output is emitted inside `<wrapped_exec>` blocks; SO-owned control metadata remains in `<so_property>` blocks.
- Workflow state persists to the workflow file, with sidecar event history in `.events.jsonl`.
- A blocked `<so_property>` payload is the SO module's weave-out surface; continue through `loom skill resume --workflow-file ... --result-file ...`.

## Shared Ask Entry Point

`loom ask start`, `loom ask result`, and `loom ask wait` provide workflow-independent questions. Do not expose `loom agent ask` or `loom skill ask`; the optional workflow `AskUser` node is a separate adapter.

## Module Separation

AO and SO remain independent implementation modules and do not call each other or form a parent/child runtime pair. They share the public `loom` executable and package family while retaining their own domain semantics, workflow state, validation, and compile/run routes.

## Practical Continuation Rule

When continuing implementation, preserve each module's established contracts and tests. The AO MCP entrypoint remains local stdio only; do not add Web or remote MCP transport.
