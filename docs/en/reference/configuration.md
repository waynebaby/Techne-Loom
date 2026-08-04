# Configuration Reference

[中文](../../zh-cn/reference/configuration.md) | [Root](../README.md)

The current public configuration surface is intentionally small and mostly CLI-driven.

## Runtime Inputs

- `--workflow-file` points to the persisted workflow file that SO will execute or inspect.
- `--context-file` injects an initial structured context object into `dotnet so.dll run`.
- `--result-file` injects a structured resume envelope into `dotnet so.dll resume`.
- bare `--guide` installs the version-matched embedded English docs bundle and returns JSON with `version`, `docs_root`, and `guide_path`; it rejects `--lang`, `--section`, and `--export`

## Sidecar Files

- The workflow file itself is rewritten with current state.
- `.events.jsonl` beside the workflow file is used as append-on-growth event history.
- Published `dotnet so.dll --guide` assets are installed under `<binary>/docs/<package-version>/`, or `%TEMP%/docs/<package-version>/` when the binary directory is not writable.

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

Example `--result-file` JSON for `dotnet so.dll resume`:

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
- AO uses the documented CLI/package contract in this project; no MCP host configuration is required.
- Additional schema or config artifacts can be added after the current public CLI contract is stabilized.
