# MCP Reference

[中文](../../zh-cn/reference/mcp.md) | [Root](../README.md)

## Transport

AO and SO expose local newline-delimited JSON-RPC over the apphost process stdin/stdout. On Windows, launch `ao.exe mcp stdio` or `so.exe mcp stdio`; on Unix, launch `ao mcp stdio` or `so mcp stdio`.

MCP is optional support for a later workflow step. It is not required to acquire a package, run `--guide`, compile, run, or resume a workflow. Complete exact package verification, safe extraction, and fresh guide capture first. AO skill execution remains CLI-only where its skill contract says so.

A client sends `initialize` with `protocolVersion`, `capabilities`, and `clientInfo`, then sends `notifications/initialized` without an `id`. Tool calls before the handshake are rejected. Every tool call requires a safe `operation_id` of 1-128 ASCII letters, digits, `.`, `_`, or `-`; the structured result returns that ID.

## Optional Configuration

The SO apphost can generate a user-level MCP configuration from its current process identity:

```powershell
.\so.exe mcp generate-config --output-file outputs\mcp.json --format vscode
```

On Unix, run `so mcp generate-config ...`. The configuration binds the current product, exact version, RID, apphost path, launch arguments, and executable hash. Do not create or consume a resolver-owned descriptor. Reuse a server only when its reported version and apphost identity match the requested package. If a later operation does not need MCP, do not register a server.

A dispatched MCP application/tool failure remains a failure. An unavailable transport before dispatch may be skipped only when that later operation has a direct apphost CLI path; never use MCP failure to conceal an error from an operation that already ran.

## Tool Contract

AO and SO are separate products and expose product-prefixed tool names. The local workflow tool set contains these seven tools:

| Tool | Required inputs | Purpose |
| --- | --- | --- |
| `<prefix>_capture_guide` | `operation_id` | Capture a fresh guide from the current apphost |
| `<prefix>_inspect_workflow_fragment` | `operation_id`, `workflow_file` | Return summary metadata or one bounded JSON Pointer fragment |
| `<prefix>_inspect_workflow_events` | `operation_id`, `workflow_file` | Return a bounded tail of the event sidecar |
| `<prefix>_list_workflow_artifacts` | `operation_id`, `workflow_file` | Return the workflow and known sidecar manifest |
| `<prefix>_run_workflow` | `operation_id`, `workflow_file` | Run until completion or an external-result boundary |
| `<prefix>_resume_workflow` | `operation_id`, `workflow_file`, `result_file` | Apply one disk-backed resume envelope |
| `<prefix>_get_workflow_status` | `operation_id`, `workflow_file` | Return a compact status projection |

Replace `<prefix>` with `ao` or `so`. The public CLI `compile` command is not an MCP tool.

## Fragment-First Reading

`*_inspect_workflow_fragment` returns summary metadata by default. An explicit `json_pointer` requests one bounded fragment and may set `max_bytes`, `max_array_items`, `max_object_properties`, and `max_depth`. Over-limit content is reported as truncated. `*_inspect_workflow_events` returns only a bounded event tail; `*_list_workflow_artifacts` returns known paths without reading the full workflow. There is no tool that prints the full workflow by default.

## File Inputs And Results

`workflow_file`, `context_file`, and `result_file` are existing file paths, not inline JSON. Finish and close each input before the tool call. A resume envelope uses this shape:

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

The canonical workflow and `.events.jsonl` sidecar remain durable business state. State-changing `run` and `resume` use `operation_id` for idempotency; an uncertain operation is not replayed. MCP connections and host processes are not a session store.