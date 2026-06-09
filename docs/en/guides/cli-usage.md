# CLI Usage

[中文](../../zh-cn/guides/cli-usage.md)

The current implemented v1 public CLI surface covers both `so` and `ao`.

## Intended Commands

- `so --guide`
- `so run`
- `so resume`
- `so status`
- `so inspect-workflow`
- `so inspect-events`
- shorthand entrypoints such as `so ls`
- `so --guide --lang en|zh-cn --section Overview --export guide.md`

## AO Surface

- `ao --guide [--lang en|zh-cn] [--section <name>] [--export <path>]`
- `ao host` — starts MCP/stdio server (official ModelContextProtocol C# SDK)
- `ao run --objective-file <path> --workflow-file <path> --event-log-file <path> [--context-file <path>]`
- `ao resume --workflow-file <path> --event-log-file <path> --result-file <path>`

AO control state is emitted as `<ao_property>{json}</ao_property>` using snake_case field names. The resume envelope expects `transition_id`, optional `correlation_key`, and optional `payload`. The event log is append-only `.jsonl` recording boundary events and status changes only.

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

The JSON inside `<so_property>` uses snake_case field names, and `so resume --result-file` expects a JSON envelope with `transition_id`, optional `correlation_key`, and `payload`.
