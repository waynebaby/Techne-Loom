# CLI 参考

[English](../../en/reference/cli.md)

## AgentOrchestrator

- `ao --guide`
- `ao --guide --lang en|zh-cn --section <section> --export <path>`
- `ao host` — 使用官方 ModelContextProtocol C# SDK 启动 MCP/stdio 服务端
- `ao run --objective-file <path> --workflow-file <path> --event-log-file <path> [--context-file <path>]`
- `ao resume --workflow-file <path> --event-log-file <path> --result-file <path>`

## AO 输出契约

- AO 自己的控制元数据以 `<ao_property>` 块的形式输出，块内是一份 JSON payload。
- 控制载荷使用 snake_case 字段名：`status`、`boundary_reason`、`workflow_file`、`event_log_file`、`current_node_id`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`。
- `status` 取值：`active`、`blocked`、`completed`、`failed`。
- `boundary_reason` 取值（当 `status` 为 `blocked` 时）：`clarification_required`、`delegation_required`、`tool_probe_required`、`sampling_required`。
- 当 `boundary_reason` 为 `sampling_required` 时，payload 包含 `sampling_request` 对象，含 `objective` 与 `artifacts[]`。
- `ao resume --result-file` 期望读取 JSON envelope，含 `transition_id`、可选 `correlation_key`、可选 `payload`。
- 事件日志是 append-only 的 `.jsonl` 文件，仅记录边界事件与状态变更。
- 暴露的 MCP 工具：`AoRun`、`AoResume`。

## SkillOrchestrator

- `so --guide`
- `so run`
- `so resume`
- `so status`
- `so inspect-workflow`
- `so inspect-events`
- `so ls` 等 shorthand 入口
- `so --guide --lang en|zh-cn --section <section> --export <path>`

## Skill 输出契约

- 被套壳的外部命令输出会先打开 `<wrapped_exec>` 块，把流式内容写进 `<exectionstream>`，并在命令结束时闭合。
- 每个 wrapped block 都包含 `<commandline>` 与 `<exectionstream>` 子元素。
- SO 自己的控制元数据会单独输出为一个 `<so_property>` 块，块内是一份 JSON payload。
- 更细的事件历史仍会落在 workflow 旁边的 `.events.jsonl` sidecar 文件中。
- 当前 JSON payload 使用 snake_case 契约字段，例如 `workflow_file`、`event_log_file`、`current_node_id`、`required_inputs`、`memory_for_next_step`。
- `so resume --result-file` 期望读取一个带 `transition_id`、可选 `correlation_key` 和 `payload` 的 JSON 对象。
