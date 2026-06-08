# CLI 使用

[English](../../en/guides/cli-usage.md)

v1 公开 CLI 以 `ao` 和 `so` 为中心。

## 目标命令面

- `ao --guide`
- `ao run`
- `ao resume`
- `ao status`
- `so --guide`
- `so run`
- `so resume`
- `so status`
- `so inspect-workflow`
- `so inspect-events`
- `so ls` 等 shorthand 入口
- `so --guide --lang en|zh-cn --section Overview --export guide.md`

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

`<so_property>` 内部的 JSON 采用 snake_case 字段；`so resume --result-file` 读取的 envelope 需要包含 `transition_id`、可选 `correlation_key` 以及 `payload`。
