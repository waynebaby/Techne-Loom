# CLI Usage

[中文](../../zh-cn/guides/cli-usage.md)

The current implemented v1 public CLI surface centers on `so`.

## Intended Commands

- `so --guide`
- `so run`
- `so resume`
- `so status`
- `so inspect-workflow`
- `so inspect-events`
- shorthand entrypoints such as `so ls`
- `so --guide --lang en|zh-cn --section Overview --export guide.md`

## Future AO Surface

- AO command shapes are documented for the next implementation slice, but the current repository does not yet ship them as a reviewed public CLI surface.
- Treat the AO guide as the contract target for future work, not as a currently available binary interface.

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
