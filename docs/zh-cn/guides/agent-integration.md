# Agent 集成

[English](../../en/guides/agent-integration.md) | [根目录](../README.md)

当调用方在路线仍演化时需要显式编排决策，应使用 Loom Agent Execution Orchestrator。

按照 repo 级术语，AO 会在控制 seam 上 **weave out**，并通过 blocked 控制载荷里的 `boundary_reason`、`weave_out_request` 等字段显式表达这个 seam；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet ao.dll resume` 结果 envelope **weave back**。

## 集成规则

- 保持稳定的会话目录，通过 `--session-dir` 传入，并在多轮之间仅保留 `session_id`。
- 通过该会话目录与 `session_id` 派生 workflow/event 产物路径。
- 优先把 AO 输出当作控制数据读取，其次才是 prose。
- 在明确的 seam 处恢复 AO，并使用对应的 blocked payload 字段作为协议面来传回结构化结果与 artifact 引用。
- 不要把 AO 当成确定性 workflow 执行器。

## 当前公开方向

- AO 在本项目里是 CLI-only。
- 集成时请以文档化的 `compile`、`run`、`resume` 命令作为契约。
- 当 AO 需要可复用的 workflow snapshot artifact 时，由调用 agent 在 AO CLI 之外编写该 JSON，再通过 `dotnet ao.dll compile --workflow-file <path>` 做校验。
- 可在对话中引用 audit 与中间输出，但默认应放在 temp 根、repo 根 temp 根，或显式 execution output 根目录，而不是任何 skill 文件夹。
- 关于 weave out、weave back、seam、strand 的 repo 级定义，请阅读 [Workflow 术语](../architecture/workflow-terminology.md)。
- 当前公开 Loom Agent Execution Orchestrator guide 应与已实现的 `.NET` runtime 保持锁步，作为运行时公开契约。

## 控制载荷示例

```json
{
  "status": "blocked",
  "session_id": "20260609010101_abc12345",
  "boundary_reason": "clarification_required",
  "workflow_file": "outputs/sessions/session_20260609010101_abc12345_workflow.json",
  "event_log_file": "outputs/sessions/session_20260609010101_abc12345_events.jsonl",
  "current_node_id": "review.slice.2",
  "pending_requirements": ["filePath"]
}
```

## 常见失败模式

调用方把 AO 当成聊天外壳，只读 narrative 说明，而不读取控制态数据。这会丢掉 AO 本应拥有的 control-state surface。
