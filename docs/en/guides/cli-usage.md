# CLI Usage

[中文](../../zh-cn/guides/cli-usage.md) | [Root](../README.md)

The current implemented v1 public CLI surface covers both `so` and `ao`.

## Intended Commands

- Windows: `.\so.exe --guide`; Unix: `./so --guide`
- Windows: `.\so.exe --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n>`; Unix: `so --patch ...`
- `so.exe run` / `so run`
- `so.exe resume` / `so resume`
- `so.exe status` / `so status`
- `so.exe inspect-workflow` / `so inspect-workflow`
- `so.exe inspect-workflow-fragment --workflow-file <path> [--json-pointer <pointer>] [--max-bytes <n>] [--max-array-items <n>] [--max-object-properties <n>] [--max-depth <n>]`
- `so.exe inspect-events` / `so inspect-events`
- shorthand entrypoints such as `so.exe ls` / `so ls`
- `--guide` reads the version-matched English docs shipped beside the apphost in the runtime package and returns JSON with `version`, `docs_root`, and `guide_path`

## AO Surface

- `ao.exe --guide` / `ao --guide`
- `ao.exe --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n>` / `ao --patch ...`
- `ao.exe compile --workflow-file <path> [--audit-output <path>]` / `ao compile ...`
- `ao.exe run --workflow-file <path> [--context-file <path>]` / `ao run ...`
- `ao.exe resume --workflow-file <path> --result-file <path>` / `ao resume ...`
- `ao.exe inspect-workflow-fragment --workflow-file <path> [--json-pointer <pointer>] [--max-bytes <n>] [--max-array-items <n>] [--max-object-properties <n>] [--max-depth <n>]`
- `ao.exe mcp stdio` / `ao mcp stdio`

AO control state is emitted as `<ao_property>{json}</ao_property>` using snake_case field names. In repo terminology, `ao.exe run` / `ao run` may weave out and `ao.exe resume` / `ao resume` is the weave-back entry point. The canonical `--workflow-file` run, resume, status, and prompt-replan paths are sessionless: the workflow JSON and its `.events.jsonl` sidecar carry durable state, while the MCP/CLI process is disposable. Plan result envelopes must include a non-empty `result_id`; duplicate Plan results are no-ops. The legacy session form remains available for compatibility. When AO workflow JSON is needed, the calling agent authors it first and uses `ao.exe compile` / `ao compile` as the validation step. Compile fails instead of overwriting existing audit artifact files.

For file editing, `--patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat `--patch` as a command-line fallback when normal patch application fails. The target file must already exist; `--from-line` and `--to-line` are 1-based inclusive; when `--to-line` exceeds EOF it is clamped to the last line; and an empty patch-content file deletes the requested line range.

## Output Shape

`so` keeps wrapped execution output and SO-owned control data separate.

- Wrapped external command output is emitted as line-by-line XML-like fragments on stdout:
  `<wrapped_exec>`
  `<commandline>...</commandline>`
  `<exectionstream>`
  `...streamed output lines...`
  `</exectionstream>`
  `</wrapped_exec>`
- SO state, blocking guidance, and final result metadata are emitted as:
  `<so_property>`
  `{json}`
  `</so_property>`

This keeps shell-visible wrapped output streamable while still making SO guidance machine-readable.

The JSON inside `<so_property>` uses snake_case field names, and `so.exe resume --result-file` / `so resume --result-file` expects a JSON envelope with `transition_id`, optional `correlation_key`, `result_id` for Plan results, and `payload`.
