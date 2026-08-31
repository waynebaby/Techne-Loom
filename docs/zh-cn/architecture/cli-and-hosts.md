# CLI 与宿主

[English](../../en/architecture/cli-and-hosts.md) | [根目录](../README.md)

AO 与 SO 之所以暴露不同的运行契约，是因为它们解决的是不同问题。

## AgentOrchestrator

- 规范接口：CLI / package 契约
- 主要职责：输出控制态决策、可持久 workflow 文件路径和 blocked payload 元数据；旧 session 标识只作为兼容数据保留
- 运行时：已在 `.NET`（net9.0 exe）中实现为 CLI-first 表面。
- `dotnet ao.dll compile`、`dotnet ao.dll prompt-plan`、`dotnet ao.dll prompt-replan`、`dotnet ao.dll run` 与 `dotnet ao.dll resume` 都使用落盘 workflow 文件；canonical 的 `--workflow-file` plan、replan、run、resume 和 status 路径是 sessionless 的，旧 `session_dir + session_id` 路径则作为兼容适配层保留。
- 控制载荷为 `<ao_property>` 块，使用 snake_case 字段：`status`、`session_id`、`boundary_reason`、`workflow_file`、`event_log_file`、`current_node_id`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`weave_out_request`。
- 当调用 `prompt-plan` 或 `prompt-replan` 时，AO 还会输出 `<ao_property type="prompt">` 块，承载由代码生成的 planner / replanner prompt 文本。
- AO 的 weave-out 比较流使用 `boundary_reason: weave_out_required` 与 `weave_out_request` 子对象。

## SkillOrchestrator

- 规范接口：本地 CLI 与 package 契约
- 主要职责：编译或加载 workflow，执行 SO 自己拥有的步骤，并在需要外部工作时返回严格 payload
- 支持简写调用，但必须先编译成持久化 workflow，再执行
- 当前 CLI 分层：
  - 面向 shell 的 wrapped command output 放在 `<wrapped_exec>` 块里
  - SO 自己的控制元数据放在独立的 `<so_property>` 块里
  - workflow state 持久化到 workflow file，事件历史落在 `.events.jsonl` sidecar
- 被阻塞的 `<so_property>` payload 就是 SO 当前的 weave-out surface，`dotnet so.dll resume --result-file` 则是 weave-back 入口。

## 宿主分工规则

不要把 AO 当成 SO 的外壳，也不要把 SO 当成 AO 的子 runtime。它们可以共享低层契约，但必须独立打包、独立调用。

## 继续实现时的实践规则

如果另一个 agent 要继续推进实现：

- 把 SO 文档和测试视为当前公开基线，不要随意破坏
- 把 AO 文档和测试视为当前公开 AO 基线，不要随意破坏
- 保持 AO 本机 stdio MCP 入口与 SO 独立，不要加入 Web 或远程传输
