# MCP 参考

[English](../../en/reference/mcp.md) | [根目录](../README.md)

## 传输方式

AO 与 SO apphost 通过进程 stdin/stdout 提供本机按行传输的 JSON-RPC。Windows 运行 `ao.exe mcp stdio` 或 `so.exe mcp stdio`；Unix 运行 `ao mcp stdio` 或 `so mcp stdio`。

MCP 只是后续 workflow 步骤的可选能力。获取 package、运行 `--guide`、compile、run 或 resume 都不依赖 MCP。必须先完成精确 package 校验、安全解压和 fresh guide capture。AO skill 的官方执行仍遵循其 skill 契约中的 CLI-only 规则。

客户端先发送包含 `protocolVersion`、`capabilities` 和 `clientInfo` 的 `initialize`，再发送不带 `id` 的 `notifications/initialized`。握手完成前调用工具会被拒绝。每个工具调用都必须带安全的 `operation_id`：使用 1-128 个 ASCII 字母、数字、`.`、`_` 或 `-`；结构化结果会返回同一个 ID。

## 可选配置

SO apphost 可以根据当前进程 identity 生成用户级 MCP 配置：

```powershell
.\so.exe mcp generate-config --output-file outputs\mcp.json --format vscode
```

Unix 使用 `so mcp generate-config ...`。配置会绑定当前 product、精确版本、RID、apphost 路径、启动参数和 executable hash。不要创建或使用 resolver-owned descriptor。只有 server 报告的版本和 apphost identity 都匹配请求 package 时，才可复用该 server。如果后续操作不需要 MCP，就不要注册 server。

MCP 应用或工具在派发后失败时，必须如实保留失败。派发前发现传输不可用时，只有当该后续操作有直接 apphost CLI 路径才可以跳过 MCP；不能用 MCP 重试掩盖已经执行过的命令错误。

## 工具契约

AO 与 SO 是独立产品，各自使用带产品前缀的工具名。本机 workflow 工具集包含以下七个工具：

| 工具 | 必填输入 | 作用 |
| --- | --- | --- |
| `<prefix>_capture_guide` | `operation_id` | 从当前 apphost 获取 fresh guide |
| `<prefix>_inspect_workflow_fragment` | `operation_id`、`workflow_file` | 返回摘要或一个有界 JSON Pointer 片段 |
| `<prefix>_inspect_workflow_events` | `operation_id`、`workflow_file` | 返回有界事件日志尾部 |
| `<prefix>_list_workflow_artifacts` | `operation_id`、`workflow_file` | 返回 workflow 和已知 sidecar 清单 |
| `<prefix>_run_workflow` | `operation_id`、`workflow_file` | 运行到完成或外部结果边界 |
| `<prefix>_resume_workflow` | `operation_id`、`workflow_file`、`result_file` | 应用一个落盘的 resume envelope |
| `<prefix>_get_workflow_status` | `operation_id`、`workflow_file` | 返回紧凑状态投影 |

将 `<prefix>` 替换为 `ao` 或 `so`。公开 CLI 的 `compile` 命令不是 MCP 工具。

## 片段优先读取

`*_inspect_workflow_fragment` 默认返回摘要元数据。显式提供 `json_pointer` 时，返回一个有界片段，并可设置 `max_bytes`、`max_array_items`、`max_object_properties` 和 `max_depth`。超过限制的内容会标为截断。`*_inspect_workflow_events` 只返回有界事件尾部；`*_list_workflow_artifacts` 返回已知路径，不读取完整 workflow。默认没有工具会打印完整 workflow。

## 文件输入与结果

`workflow_file`、`context_file` 和 `result_file` 都是已有文件路径，不是内联 JSON。调用工具前必须写完并关闭每个输入文件。Resume envelope 使用以下结构：

```json
{
  "transition_id": "transition.plan",
  "correlation_key": null,
  "result_id": "plan-result-001",
  "payload": {
    "plan": {
      "answer": "approved"
    }
  }
}
```

canonical workflow 和 `.events.jsonl` sidecar 是持久业务状态。改变状态的 `run` 和 `resume` 使用 `operation_id` 实现幂等；结果不确定的操作不会重放。MCP connection 和宿主进程不是 session store。