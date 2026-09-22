# MCP 参考

[English](../../en/reference/mcp.md) | [根目录](../README.md)

## 传输方式

AO 和 SO 只公开一种本机 MCP 传输：进程 stdin 和 stdout 上按行传输的 JSON-RPC。

```text
dotnet ao.dll mcp stdio
dotnet so.dll mcp stdio
```

这个表面只支持本机 stdio。不提供 Web、HTTP、socket 或远程 MCP host。宿主进程必须是可信的：文件参数只接受路径，并按该进程的操作系统权限读取或写入文件。

客户端必须先发送带有 `protocolVersion`、`capabilities` 和 `clientInfo` 对象的 `initialize`，再发送不带 `id` 的 `notifications/initialized` 通知。在握手完成前调用工具会被拒绝。每个 `tools/call` 的 arguments 对象还必须带安全的 `operation_id`：只能使用 1-128 个 ASCII 字母、数字、`.`、`_` 或 `-`；结构化工具结果会返回同一个 ID。

## 用户级版本化注册

默认配置目录是当前用户的 Loom 目录：

```text
~/.skills/loomed/mcp/<product>/<exact-version>/mcp.json
~/.skills/loomed/mcp/<product>/<exact-version>/.mcp.json
```

生成的 server key 是 `loom-so-<exact-version>` 或 `loom-ao-<exact-version>`。工具名保持稳定，例如 `so_inspect_workflow_fragment`。只有当已有 MCP 报告的 `serverInfo.version`、runtime mode、RID、preparation ID、启动命令/文件、启动参数 hash 和 descriptor hash 都与请求的 runtime identity 一致时，才可以复用 session；否则会替换过期 session。用户级适配器负责加载这个专用配置并持有 stdio 句柄。Loom 不扫描或接管孤立进程。

由 descriptor 生成的配置包含 `TECHNE_LOOM_MCP_BINDING_REQUIRED=true`，以及 product、精确版本、mode、RID、preparation ID、启动命令/文件、启动参数 hash 和用于 binding/session reuse 的 canonical descriptor hash 等 runtime identity。受控 AO 或 SO MCP server 在要求 binding 但缺失或格式不正确时会拒绝 `initialize`；依赖 descriptor 的 guide 和 compile 工具会执行完整 descriptor 匹配。配置结果同时保留 descriptor 文件原始 SHA-256 与 canonical descriptor hash，并分别标明含义，避免证据混淆。

## 受治理的 SO 入口

对于每个由 Loom Skill Orchestrator 治理的 skill being enhanced 校验，包括 `/loom-skill-enhancement` 自举，精确的发布 runtime 必须先为同一份外部 workflow copy 返回由 resolver 生成的 launch descriptor。
公开的 `dotnet so.dll runtime resolve --version <version> --runtime-descriptor-file <path>` 操作会写出该 descriptor。平台、RID、包身份、可执行文件、缓存和启动路径都由 resolver 决定。

1. 使用该 descriptor 通过选定 runtime 生成所需的 VS Code `mcp.json` 和 Claude `.mcp.json`。resolver 决定使用 self-contained executable 还是 framework-dependent DLL；workflow 文本不得自行选择。
2. 尝试注册生成的配置，完成 `initialize` 和 `notifications/initialized`，再用有界参数调用 `so_inspect_workflow_fragment`。
3. 成功后保存 `mcp_registration_attempt_evidence.status=ready`，设置 `governance_entry_transport=mcp_stdio`，并返回带有相同 descriptor 与 workflow 身份的 `mcp_startup_evidence`。
4. 如果 MCP 在成功派发命令前无法提供，就保存 `mcp_registration_attempt_evidence.status=failed`、`mcp_attempted=true`，并且只能使用一个允许原因：`mcp_transport_unavailable`、`mcp_handshake_unsupported` 或 `mcp_tool_unavailable`。然后使用同一个 descriptor 执行有界的 `inspect-workflow-fragment` CLI backup，并设置 `governance_entry_transport=cli`。
5. MCP 启动后的应用错误或命令错误不能触发 backup。保留保存的 workflow 失败边界。
6. 只有某一种传输方式生成 `mcp_startup_evidence` 后，workflow 才能捕获 `--guide`，再继续规划、编写、校验、compile、run 或 resume。

## 工具契约

AO 和 SO 仍是彼此独立的产品。AO 注册 `ao_` 工具，SO 注册 `so_` 工具。共享的协议实现不会合并它们的 runtime 或发布身份。

两个产品各自公开同样的八个 workflow 工具。除了文件输入外，所有 workflow 工具都必须带 `operation_id`：

| 工具 | 必填输入 | 作用 |
| --- | --- | --- |
| `<prefix>_capture_guide` | `operation_id`、`runtime_descriptor_file` | 从选定 runtime 捕获 fresh guide 表面 |
| `<prefix>_compile_workflow` | `operation_id`、`workflow_file`、`runtime_descriptor_file` | 返回结构化 compile feedback 和可选 feedback artifact |
| `<prefix>_inspect_workflow_fragment` | `operation_id`、`workflow_file` | 默认返回摘要元数据；显式请求时返回有界 JSON Pointer 片段 |
| `<prefix>_inspect_workflow_events` | `operation_id`、`workflow_file` | 返回事件 sidecar 最近的有界尾部 |
| `<prefix>_list_workflow_artifacts` | `operation_id`、`workflow_file` | 返回 canonical workflow 和已知 `.events.jsonl` 与 `.operations.jsonl` sidecar 清单 |
| `<prefix>_run_workflow` | `operation_id`、`workflow_file` | 执行 canonical workflow 文件，直到完成或到达外部结果边界 |
| `<prefix>_resume_workflow` | `operation_id`、`workflow_file`、`result_file` | 应用一个落盘的结果 envelope；Plan 结果必须带非空 `result_id` |
| `<prefix>_get_workflow_status` | `operation_id`、`workflow_file` | 返回紧凑的状态投影，不返回完整 workflow |

将 `<prefix>` 替换为 `ao` 或 `so`。

## 片段优先读取

`*_inspect_workflow_fragment` 默认绝不会返回完整 workflow。默认响应包含摘要元数据和有界 context key。显式的 `json_pointer` 可以请求一个有界片段，并可设置 `max_bytes`、`max_array_items`、`max_object_properties` 和 `max_depth`。超过限制时只返回截断信息，不会展开完整内容。`*_inspect_workflow_events` 只返回最近的有界事件尾部，并支持 `max_events` 和 `max_bytes`；不会打印完整事件日志。`*_list_workflow_artifacts` 只报告 canonical workflow 及其已知的 `.events.jsonl` 与 `.operations.jsonl` sidecar。

这里有意不提供打印完整 workflow 的 MCP 工具。Agent 应只请求完成下一步决策所需的最小片段。

## 文件输入与结果

`workflow_file`、`context_file`、`result_file` 和 `runtime_descriptor_file` 都是已有文件的路径，不是内联 JSON。`operation_id` 是调用方为每次 MCP 操作生成的操作身份。改变状态的 `run` 和 `resume` 调用会把它作为幂等 ID；只读和 compile 调用用它关联结果与 artifact。`run`/`resume` 请求哈希使用 canonical workflow JSON：对象属性按名称排序，数组保留原顺序，可选的 null 成员会省略，空对象和空数组仍保持区别。调用方必须在一次工具调用前生成、写完并关闭这些输入文件。结果 envelope 使用与 CLI resume 相同的结构：

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

canonical workflow 文件及其旁边的 `.events.jsonl` sidecar 才是持久业务状态。对于改变状态的 `run` 和 `resume`，相邻的 `.operations.jsonl` ledger 会记录 `started` 和 `completed` 结果；只有请求 hash 相同的已完成操作才会重放，未确定状态的操作不会重放。MCP 连接、宿主进程和内存中的工具注册表都不是 session store。