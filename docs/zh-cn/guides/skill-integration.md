# Skill 集成

[English](../../en/guides/skill-integration.md)

当一个 skill 在“下一步契约已知”后必须保持 on-rail 时，应使用 SO。

## 集成规则

- 在执行前先把 shorthand 或源输入编译成持久化 workflow。
- 让 SO 直接执行确定性的本地步骤。
- 当 SO 阻塞时，把 `skill_hint` 与 `memory_for_next_step` 当成规范输出。
- 用结构化结果 envelope 恢复，而不是写一段 prose 回顾。

## 当前公开调用方契约

- 用 `so run --workflow-file <path>` 推进一个持久化 workflow。
- 如有需要，可额外传 `--context-file <path>` 注入初始结构化 context。
- 当 SO 阻塞或完成时，解析 `<so_property>` 内部的 JSON。
- 当 wrapped command 运行时，从 `<wrapped_exec>` 读取 shell-facing 输出。

`so resume --result-file` 载荷示例：

```json
{
  "transition_id": "transition.ask",
  "correlation_key": null,
  "payload": {
    "review": {
      "approved": true
    }
  }
}
```

## 边界解释示例

```xml
<so_property>
{"type":"boundary","payload":{"status":"blocked","current_step_kind":"AskUser","required_inputs":["filePath"]}}
</so_property>
```

调用方解释：

- workflow 还没有完成。
- 当前边界属于 `AskUser`。
- 调用方需要收集 `filePath`，写出 result-file sidecar，然后执行 `so resume`。

## 常见失败模式

外层 skill agent 不读取 workflow context，而是根据最近对话重新推导状态。这正是 SO 试图阻止的漂移。
