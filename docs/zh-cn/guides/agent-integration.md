# Agent 集成

[English](../../en/guides/agent-integration.md)

当调用方在路线仍演化时需要显式编排决策，应使用 AO。

## 集成规则

- 为可变 workflow 文件和 append-only 事件日志保留稳定位置。
- 优先把 AO 输出当作控制数据读取，其次才是 prose。
- 在明确边界处，用结构化结果与 artifact 引用恢复 AO。
- 不要把 AO 当成确定性 workflow 执行器。

## 当前公开方向

- AO 预期运行在官方 `ModelContextProtocol` C# SDK 之上。
- `MCP/stdio` 是规范运行时 transport。
- 当前公开 AO guide 先于代码落地，因此它就是下一批实现切片的当前契约。

## 控制载荷示例

```json
{
  "status": "blocked",
  "boundary_reason": "clarification_required",
  "workflow_file": "current-workflow.json",
  "event_log_file": "current-events.jsonl",
  "current_node_id": "review.slice.2",
  "pending_requirements": ["filePath"]
}
```

## 常见失败模式

调用方把 AO 当成聊天外壳，只读 narrative 说明，而不读取控制态数据。这会丢掉 AO 本应拥有的 control-state surface。
