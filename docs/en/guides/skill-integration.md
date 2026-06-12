# Skill Integration

[中文](../../zh-cn/guides/skill-integration.md) | [Root](../README.md)

Use SO when a skill must stay on-rail after the next-step contract is known.

In repo terminology, SO **weaves out** only when it reaches an externally owned step, surfacing that seam on blocked `<so_property>` payloads via fields such as `current_step_kind`, and callers **weave back** through `dotnet so.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

## Integration Rules

- Compile shorthand or source input into a persisted workflow before execution.
- Let SO execute deterministic local steps directly.
- Treat `skill_hint` and `memory_for_next_step` as canonical outputs when SO blocks.
- Resume with a structured result envelope, not a prose recap.

## Current Public Caller Contract

- Run `dotnet so.dll run --workflow-file <path>` to advance a persisted workflow.
- Optionally add `--context-file <path>` to inject initial structured context.
- When SO blocks or completes, parse the JSON inside `<so_property>`.
- When a wrapped command runs, consume its shell-facing output from `<wrapped_exec>`.

Example `dotnet so.dll resume --result-file` payload:

```json
{
  "transition_id": "transition.ask",
  "correlation_key": null,
  "payload": {
    "review": {
      "approved": true
    }
  }
}
```

## Example Boundary Interpretation

```xml
<so_property>
{"type":"boundary","payload":{"status":"blocked","current_step_kind":"AskUser","required_inputs":["filePath"]}}
</so_property>
```

Caller interpretation:

- The workflow did not finish; it has woven out at an explicit seam.
- The current boundary belongs to `AskUser`.
- The caller must collect `filePath`, write the weave-back sidecar, and run `dotnet so.dll resume`.

## Common Failure Mode

The outer skill agent re-derives state from recent conversation instead of respecting workflow context. That is exactly the drift SO is intended to prevent.
