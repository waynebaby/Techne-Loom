# CLI 使用

[English](../../en/guides/cli-usage.md)

当前已实现的 v1 公开 CLI 同时涵盖 `so` 与 `ao`。

## 目标命令面

- `dotnet so.dll --guide`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`
- `dotnet so.dll ls` 等 shorthand 入口
- `dotnet so.dll --guide --lang en|zh-cn --section Overview --export guide.md`

## AO Surface

- `dotnet ao.dll --guide [--lang en|zh-cn] [--section <name>] [--export <path>]`
- `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]`
- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>]`
- `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path>`

AO 控制状态以 `<ao_property>{json}</ao_property>` 的形式输出，使用 snake_case 字段名。按 repo 术语，`dotnet ao.dll run` 可能会 weave out，而 `dotnet ao.dll resume` 是 weave-back 入口。`dotnet ao.dll run` 会生成 `session_id`，调用方只需保存该 ID。AO 通过 `session_dir + session_id` 派生 workflow/event 产物路径。resume envelope 需要包含 `transition_id`、可选 `correlation_key` 以及可选 `payload`。事件日志是 append-only 的 `.jsonl`，仅记录边界事件与状态变更。当 AO 需要 workflow JSON 时，由调用 agent 先编写该文件，再使用 `dotnet ao.dll compile` 做校验。compile 不会覆盖已有 audit artifact 文件，而是直接失败。

## 输出形状

`so` 会把套壳执行输出与 SO 自己的控制数据分开。

- 外部命令的流式输出按逐行 XML-like 片段发到 stdout：
  `<wrapped_exec>`
  `<commandline>...</commandline>`
  `<exectionstream>`
  `...持续流出的输出行...`
  `</exectionstream>`
  `</wrapped_exec>`
- SO 的状态、阻塞指导和最终结果元数据按如下形式输出：
  `<so_property>`
  `{json}`
  `</so_property>`

这样既能让 shell 里看到持续流出的 wrapped output，也能让 SO 的建议保持 machine-readable。

`<so_property>` 内部的 JSON 采用 snake_case 字段；`dotnet so.dll resume --result-file` 读取的 envelope 需要包含 `transition_id`、可选 `correlation_key` 以及 `payload`。
