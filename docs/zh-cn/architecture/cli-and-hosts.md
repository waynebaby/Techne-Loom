# CLI 与宿主

[English](../../en/architecture/cli-and-hosts.md) | [根目录](../README.md)

Techne Loom 只发布一套 `loom` CLI，并为每个 RID 发布一个自包含 runtime package。`loom agent` 和 `loom skill` 通过同一个 apphost 暴露不同模块命令；独立提问只使用唯一顶层入口 `loom ask`。

## Loom Agent Plan-Execution Orchestrator

- 实现模块：`Techne.Loom.AgentOrchestrator`
- 主要职责：输出控制态决策、可持久 workflow 文件路径和 blocked payload 元数据；旧 session 标识仅作为兼容数据保留。
- 公开命令入口：`loom agent`；workflow 操作保持落盘且 sessionless。
- 控制载荷使用 `<ao_property>` 块和 snake_case 字段，例如 `status`、`workflow_file`、`event_log_file`、`result_file` 与 `weave_out_request`。
- planner 和 replanner prompt 文本使用 `<ao_property type="prompt">` 块。

## Skill Orchestrator

- 实现模块：`Techne.Loom.SkillOrchestrator`
- 主要职责：编译或加载 workflow、执行模块拥有的步骤，并在需要外部工作时返回严格 payload。
- 公开命令入口：`loom skill`。
- 面向 shell 的 wrapped output 放在 `<wrapped_exec>` 块里；SO 模块控制元数据仍使用 `<so_property>` 块。
- workflow state 持久化到 workflow file，事件历史写入 `.events.jsonl` sidecar。
- 被阻塞的 `<so_property>` payload 是 SO 模块的 weave-out surface；通过 `loom skill resume --workflow-file ... --result-file ...` 继续。

## 统一 Ask 入口

`loom ask start`、`loom ask result` 和 `loom ask wait` 提供 workflow-independent 提问。不要暴露 `loom agent ask` 或 `loom skill ask`；可选 workflow `AskUser` node 是独立适配层。

## 模块边界

AO 与 SO 仍是独立实现模块，不会互相调用，也不是父子 runtime。它们共享公开的 `loom` 可执行文件和 package 系列，同时保留各自领域语义、workflow state、validation 及 compile/run 路由。

## 继续实现时的实践规则

继续实现时应保留每个模块既有的契约和测试。统一的本机 MCP 路由为 `loom agent mcp stdio` 和 `loom skill mcp stdio`；不要加入 Web 或远程 MCP 传输。
