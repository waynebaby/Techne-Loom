# CLI 与宿主

[English](../../en/architecture/cli-and-hosts.md)

AO 与 SO 之所以暴露不同的 host 模型，是因为它们解决的是不同问题。

## AgentOrchestrator

- 规范接口：`MCP/stdio`
- 轻量 CLI：用于回放、调试和文件驱动测试
- 主要职责：输出控制态决策、session_id、派生产物路径和 blocked payload 元数据
- 运行时：已用官方 `ModelContextProtocol` C# SDK 在 `.NET`（net9.0 exe）中实现，提供 hosted stdio server 行为。
- `ao host` 启动 MCP/stdio 服务端；`ao run` 与 `ao resume` 通过 `session_dir + session_id` 驱动会话化文件持久化。
- 暴露的 MCP 工具：`AoRun`、`AoResume`。
- `AoRun` / `AoResume` 接受一个可选的按调用 `invocation_context` 对象，方便未来由调用方显式声明非 stdio 的 weave-out route。
- 控制载荷为 `<ao_property>` 块，使用 snake_case 字段：`status`、`session_id`、`boundary_reason`、`workflow_file`、`event_log_file`、`current_node_id`、`result_file`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`weave_out_request`。
- AO 的 weave-out 比较流使用 `boundary_reason: weave_out_required` 与 `weave_out_request` 子对象。

## SkillOrchestrator

- 规范接口：本地 CLI 与 package 契约
- 主要职责：编译或加载 workflow，执行 SO 自己拥有的步骤，并在需要外部工作时返回严格 payload
- 支持简写调用，但必须先编译成持久化 workflow，再执行
- 当前 CLI 分层：
  - 面向 shell 的 wrapped command output 放在 `<wrapped_exec>` 块里
  - SO 自己的控制元数据放在独立的 `<so_property>` 块里
  - workflow state 持久化到 workflow file，事件历史落在 `.events.jsonl` sidecar
- 被阻塞的 `<so_property>` payload 就是 SO 当前的 weave-out surface，`so resume --result-file` 则是 weave-back 入口。

## 宿主分工规则

不要把 AO 当成 SO 的外壳，也不要把 SO 当成 AO 的子 runtime。它们可以共享低层契约，但必须独立打包、独立调用。

## 继续实现时的实践规则

如果另一个 agent 要继续推进实现：

- 把 SO 文档和测试视为当前公开基线，不要随意破坏
- 把 AO 文档和测试视为当前公开 AO 基线，不要随意破坏
- 不要把 MCP-specific 代码重新塞回 `Abstractions` 或 `Common`
