# CLI 参考

[English](../../en/reference/cli.md)

## AgentOrchestrator

- `ao --guide`
- `ao --guide --lang en|zh-cn --section <section> --export <path>`
- `ao host` — 使用官方 ModelContextProtocol C# SDK 启动 MCP/stdio 服务端
- `ao run --objective-file <path> --session-dir <path> [--context-file <path>]`
- `ao resume --session-dir <path> --session-id <id> --result-file <path>`
- 按 repo 术语，`ao run` 可能会 weave out，而 `ao resume` 是 weave-back 入口。

## AO 输出契约

- AO 自己的控制元数据以 `<ao_property>` 块的形式输出，块内是一份 JSON payload。
- 控制载荷使用 snake_case 字段名：`status`、`session_id`、`boundary_reason`、`workflow_file`、`event_log_file`、`current_node_id`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`weave_out_request`。
- `ao run` 会生成 `session_id`；调用方只需要持久化这个 ID。
- AO 基于 `session_dir + session_id` 派生产物文件：`session_<session_id>_workflow.json` 与 `session_<session_id>_events.jsonl`。
- 当前 runtime 在控制载荷中实际发出的 `status` 取值是 `blocked` 与 `completed`。
- `boundary_reason` 取值（当 `status` 为 `blocked` 时）：`clarification_required`、`delegation_required`、`tool_probe_required`、`weave_out_required`。
- 当 `boundary_reason` 为 `weave_out_required` 时，payload 会带上 `weave_out_request`，其中包含 `objective` 与 `artifacts[]`。
- `result_file` 是为未来 AO 自有输出 artifact 预留的可选字段，当前 runtime 不会填充它。
- `ao resume --result-file` 期望读取 JSON envelope，含 `transition_id`、可选 `correlation_key`、可选 `payload`。这个 envelope 就是 AO 当前的 weave-back sidecar。
- 事件日志是 append-only 的 `.jsonl` 文件，仅记录边界事件与状态变更。
- CLI/runtime 失败会以 `type: "error"` 的 `<ao_property>` 输出，而不是通过 `status: failed` 的控制载荷输出。
- 暴露的 MCP 工具：`AoRun`、`AoResume`。
- `AoRun` 与 `AoResume` 还接受一个可选的 `invocation_context` 对象，用来按调用传入宿主执行元数据，避免未来非 stdio weave-out 路径依赖 ambient `IMcpServer` 注入。

## SkillOrchestrator

- `so --guide`
- `so run`
- `so resume`
- `so status`
- `so inspect-workflow`
- `so inspect-events`
- `so ls` 等 shorthand 入口
- `so --guide --lang en|zh-cn --section <section> --export <path>`
- 按 repo 术语，`so run` 在遇到外部拥有步骤时可能会 weave out，而 `so resume` 是 weave-back 入口。

## Skill 输出契约

- 被套壳的外部命令输出会先打开 `<wrapped_exec>` 块，把流式内容写进 `<exectionstream>`，并在命令结束时闭合。
- 每个 wrapped block 都包含 `<commandline>` 与 `<exectionstream>` 子元素。
- SO 自己的控制元数据会单独输出为一个 `<so_property>` 块，块内是一份 JSON payload。
- 更细的事件历史仍会落在 workflow 旁边的 `.events.jsonl` sidecar 文件中。
- 当前 JSON payload 使用 snake_case 契约字段，例如 `workflow_file`、`event_log_file`、`current_node_id`、`required_inputs`、`memory_for_next_step`。
- `so resume --result-file` 期望读取一个带 `transition_id`、可选 `correlation_key` 和 `payload` 的 JSON 对象。这个 JSON 对象就是 SO 当前的 weave-back sidecar。
