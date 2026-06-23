# CLI Usage

[中文](../../zh-cn/guides/cli-usage.md) | [Root](../README.md)

The current implemented v1 public CLI surface covers both `so` and `ao`.

## Intended Commands

- `dotnet so.dll --guide`
- `dotnet so.dll --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n>`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`
- shorthand entrypoints such as `dotnet so.dll ls`
- `dotnet so.dll --guide --lang en|zh-cn --section Overview --export guide.md`

## AO Surface

- `dotnet ao.dll --guide [--lang en|zh-cn] [--section <name>] [--export <path>]`
- `dotnet ao.dll --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n>`
- `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]`
- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>]`
- `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path>`

AO control state is emitted as `<ao_property>{json}</ao_property>` using snake_case field names. In repo terminology, `dotnet ao.dll run` may weave out and `dotnet ao.dll resume` is the weave-back entry point. `dotnet ao.dll run` generates `session_id`; callers persist only that ID. AO derives workflow/event artifact paths from `session_dir + session_id`. The resume envelope expects `transition_id`, optional `correlation_key`, and optional `payload`. The event log is append-only `.jsonl` recording boundary events and status changes only. When AO workflow JSON is needed, the calling agent authors it first and uses `dotnet ao.dll compile` as the validation step. Compile fails instead of overwriting existing audit artifact files.

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

The JSON inside `<so_property>` uses snake_case field names, and `dotnet so.dll resume --result-file` expects a JSON envelope with `transition_id`, optional `correlation_key`, and `payload`.
