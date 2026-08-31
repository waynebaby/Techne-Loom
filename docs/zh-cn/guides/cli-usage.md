# CLI 使用

[English](../../en/guides/cli-usage.md) | [根目录](../README.md)

当前已实现的 v1 公开 CLI 同时涵盖 `so` 与 `ao`。

## 目标命令面

- `dotnet so.dll --guide`
- `dotnet so.dll --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n>`
- `dotnet so.dll run`
- `dotnet so.dll resume`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-workflow-fragment --workflow-file <path> [--json-pointer <pointer>] [--max-bytes <n>] [--max-array-items <n>] [--max-object-properties <n>] [--max-depth <n>]`
- `dotnet so.dll inspect-events`
- `dotnet so.dll ls` 等 shorthand 入口
- `dotnet so.dll --guide` 会读取与可执行文件放在同一个 runtime package 中的版本匹配英文文档，并返回包含 `version`、`docs_root` 与 `guide_path` 的 JSON

## AO Surface

- `dotnet ao.dll --guide`
- `dotnet ao.dll --patch --patch-content-file <path> --patch-target <path> --from-line <n> --to-line <n>`
- `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]`
- `dotnet ao.dll run --workflow-file <path> [--context-file <path>]`
- `dotnet ao.dll resume --workflow-file <path> --result-file <path>`
- `dotnet ao.dll inspect-workflow-fragment --workflow-file <path> [--json-pointer <pointer>] [--max-bytes <n>] [--max-array-items <n>] [--max-object-properties <n>] [--max-depth <n>]`
- `dotnet ao.dll mcp stdio`

AO 控制状态以 `<ao_property>{json}</ao_property>` 的形式输出，使用 snake_case 字段名。按 repo 术语，`dotnet ao.dll run` 可能会 weave out，而 `dotnet ao.dll resume` 是 weave-back 入口。canonical 的 `--workflow-file` run、resume、status 和 prompt-replan 路径都是 sessionless 的：workflow JSON 及其旁边的 `.events.jsonl` sidecar 负责保存状态，MCP/CLI 进程可以随时结束。Plan 结果 envelope 必须带非空 `result_id`；重复提交同一个 Plan 结果不会产生副作用。旧 session 形式仍作为兼容路径保留。当 AO 需要 workflow JSON 时，由调用 agent 先编写该文件，再使用 `dotnet ao.dll compile` 做校验。compile 不会覆盖已有 audit artifact 文件，而是直接失败。

对于文件编辑接口，`--patch` 在 GitHub Copilot 场景下，只要满足适用条件就直接使用；在其他平台或工具场景下，把它视为常规补丁应用失败后的命令行兜底。目标文件必须已存在；`--from-line` 与 `--to-line` 使用 1 基、闭区间行号；当 `--to-line` 超出 EOF 时会自动按最后一行处理；若 patch 内容文件为空，则语义是删除指定行段。

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

`<so_property>` 内部的 JSON 采用 snake_case 字段；`dotnet so.dll resume --result-file` 读取的 envelope 需要包含 `transition_id`、可选 `correlation_key`、Plan 结果使用的 `result_id` 以及 `payload`。
