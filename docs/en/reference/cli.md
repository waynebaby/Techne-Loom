# CLI Reference

[中文](../../zh-cn/reference/cli.md)

## AgentOrchestrator

- The pages in `reference/products/ao-guide.md` document the intended AO contract.
- The current repository does not yet ship a reviewed public AO CLI/runtime surface.
- Do not treat `ao --guide`, `ao run`, `ao resume`, or `ao status` as implemented current commands until the AO slice lands in code.

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
