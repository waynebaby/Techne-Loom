# Configuration Reference

[中文](../../zh-cn/reference/configuration.md)

The current public configuration surface is intentionally small and mostly CLI-driven.

## Runtime Inputs

- `--workflow-file` points to the persisted workflow file that SO will execute or inspect.
- `--context-file` injects an initial structured context object into `so run`.
- `--result-file` injects a structured resume envelope into `so resume`.
- `--guide --lang ... --section ... --export ...` controls guide resolution and export behavior.

## Sidecar Files

- The workflow file itself is rewritten with current state.
- `.events.jsonl` beside the workflow file is used as append-on-growth event history.
- Published `so --guide` assets live under `guide-assets/<lang>/so-guide.md`.

## Practical Examples

Example `--context-file` JSON:

```json
{
  "review": {
    "approved": true,
    "summary": "ready to ship"
  },
  "notes": ["carry this into memory extraction"]
}
```

Example `--result-file` JSON for `so resume`:

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

## Current Runtime Defaults

- The reviewed public SO slice uses the in-memory instance store inside the current process.
- Nested JSON from workflow/context/result files is normalized into runtime dictionaries/lists before evaluation.
- No large central config file exists yet.

## Planned Extension Points

- File-backed or alternative instance stores may become public configuration later.
- AO will need official MCP/stdio host configuration once its runtime lands.
- Additional schema or config artifacts can be added after the current public CLI contract is stabilized.
