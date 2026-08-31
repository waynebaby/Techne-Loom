# MCP 参考

[English](../../en/reference/mcp.md) | [根目录](../README.md)

## 传输方式

AO 和 SO 只公开一种本机 MCP 传输：进程 stdin 和 stdout 上按行传输的 JSON-RPC。

```text
dotnet ao.dll mcp stdio
dotnet so.dll mcp stdio
```

这个表面只支持本机 stdio。不提供 Web、HTTP、socket 或远程 MCP host。宿主进程必须是可信的：文件参数只接受路径，并按该进程的操作系统权限读取或写入文件。

客户端必须先发送带有 `protocolVersion`、`capabilities` 和 `clientInfo` 对象的 `initialize`，再发送不带 `id` 的 `notifications/initialized` 通知。在握手完成前调用工具会被拒绝。

## 受治理的 SO 入口

对于每个由 Loom Skill Orchestrator 治理的 target skill 校验，包括 `/loom-skill-enhancement` 自举，精确的发布 runtime 预检通过后，本机 MCP server 是第一个外部接口。

1. 使用 `dotnet so.dll mcp stdio` 或已经核验的 self-contained 等价入口启动选定的发布 runtime。
2. 完成 `initialize` 和不带 `id` 的 `notifications/initialized` 通知。
3. 针对同一份外部 workflow copy 调用 `so_inspect_workflow_fragment`，并保留有界结果。
4. 只有 `mcp_startup_evidence` 完整后，workflow 才能继续捕获 `--guide`，再进入规划、编写、校验、compile、run 或 resume。

这是受治理 workflow 的步骤，不是要求配置当前编辑器的 `mcp.json`。如果 MCP 无法启动或片段调用失败，就把保存的 workflow 停在失败预检状态；direct CLI 和本地编排不能绕过它。MCP 调用用于支持校验，但不能替代正式的 `dotnet so.dll run` / `dotnet so.dll resume` 链路。

## 工具契约

AO 和 SO 仍是彼此独立的产品。AO 注册 `ao_` 工具，SO 注册 `so_` 工具。共享的协议实现不会合并它们的 runtime 或发布身份。

两个产品各自公开同样的六个 workflow 工具：

| 工具 | 必填输入 | 作用 |
| --- | --- | --- |
| `<prefix>_inspect_workflow_fragment` | `workflow_file` | 默认返回摘要元数据；显式请求时返回有界 JSON Pointer 片段 |
| `<prefix>_inspect_workflow_events` | `workflow_file` | 返回事件 sidecar 最近的有界尾部 |
| `<prefix>_list_workflow_artifacts` | `workflow_file` | 返回 canonical workflow 和已知 sidecar 清单 |
| `<prefix>_run_workflow` | `workflow_file` | 执行 canonical workflow 文件，直到完成或到达外部结果边界 |
| `<prefix>_resume_workflow` | `workflow_file`、`result_file` | 应用一个落盘的结果 envelope；Plan 结果必须带非空 `result_id` |
| `<prefix>_get_workflow_status` | `workflow_file` | 返回紧凑的状态投影，不返回完整 workflow |

将 `<prefix>` 替换为 `ao` 或 `so`。

## 片段优先读取

`*_inspect_workflow_fragment` 默认绝不会返回完整 workflow。默认响应包含摘要元数据和有界 context key。显式的 `json_pointer` 可以请求一个有界片段，并可设置 `max_bytes`、`max_array_items`、`max_object_properties` 和 `max_depth`。超过限制时只返回截断信息，不会展开完整内容。`*_inspect_workflow_events` 只返回最近的有界事件尾部，并支持 `max_events` 和 `max_bytes`；不会打印完整事件日志。`*_list_workflow_artifacts` 只报告 canonical workflow 及其已知的 `.events.jsonl` sidecar。

这里有意不提供打印完整 workflow 的 MCP 工具。Agent 应只请求完成下一步决策所需的最小片段。

## 文件输入与结果

`workflow_file`、`context_file` 和 `result_file` 都是已有文件的路径，不是内联 JSON。调用方必须在一次工具调用前生成、写完并关闭这些输入文件。结果 envelope 使用与 CLI resume 相同的结构：

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

canonical workflow 文件及其旁边的 `.events.jsonl` sidecar 才是持久业务状态。MCP 连接、宿主进程和内存中的工具注册表都不是 session store。