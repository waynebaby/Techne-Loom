# CLI 参考

[English](../../en/reference/cli.md)

## AgentOrchestrator

- `reference/products/ao-guide.md` 记录的是 AO 的目标契约。
- 当前仓库还没有交付经过 review 的公开 AO CLI/runtime surface。
- 在 AO 代码切片真正落地之前，不要把 `ao --guide`、`ao run`、`ao resume`、`ao status` 当成已实现命令。

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
