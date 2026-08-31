# MCP Reference

[中文](../../zh-cn/reference/mcp.md) | [Root](../README.md)

## Transport

AO and SO expose one local MCP transport: newline-delimited JSON-RPC over the process stdin and stdout streams.

```text
dotnet ao.dll mcp stdio
dotnet so.dll mcp stdio
```

This surface is local and stdio-only. It does not provide MCP over Web, HTTP, a socket, or a remote host. The host process must be trusted: file paths are path-only inputs and are read or written with the operating system permissions of that process.

A client must send `initialize` with an object containing `protocolVersion`, `capabilities`, and `clientInfo`, then send the `notifications/initialized` notification without an `id`. Tool calls before that handshake are rejected.

## Governed SO Entry

For every Loom Skill Orchestrator-governanced target-skill verification, including `/loom-skill-enhancement` self-bootstrap, the local MCP server is the first external interface after exact published runtime preflight.

1. Start the selected published runtime with `dotnet so.dll mcp stdio` or its validated self-contained equivalent.
2. Complete `initialize` and the `notifications/initialized` notification.
3. Call `so_inspect_workflow_fragment` against the same external workflow copy and preserve the bounded result.
4. Only after `mcp_startup_evidence` is complete may the workflow capture `--guide` and continue to planning, authoring, validation, compile, run, or resume.

This is a governed workflow step, not a request to configure the current editor's `mcp.json`. If MCP cannot start or the fragment call fails, stop the saved workflow at failed preflight; direct CLI or local orchestration cannot bypass it. MCP calls support verification but do not replace the official `dotnet so.dll run` / `dotnet so.dll resume` chain.

## Tool Contract

AO and SO remain independent products. AO registers the `ao_` tools and SO registers the `so_` tools. The shared protocol implementation does not merge their runtime or release identity.

Each product exposes the same six workflow tools:

| Tool | Required inputs | Purpose |
| --- | --- | --- |
| `<prefix>_inspect_workflow_fragment` | `workflow_file` | Return summary metadata by default, or a bounded JSON Pointer fragment when explicitly requested |
| `<prefix>_inspect_workflow_events` | `workflow_file` | Return a bounded tail of the event sidecar |
| `<prefix>_list_workflow_artifacts` | `workflow_file` | Return the canonical workflow and known sidecar manifest |
| `<prefix>_run_workflow` | `workflow_file` | Run the canonical workflow file until completion or an external result boundary |
| `<prefix>_resume_workflow` | `workflow_file`, `result_file` | Apply one disk-backed result envelope; Plan results require a non-empty `result_id` |
| `<prefix>_get_workflow_status` | `workflow_file` | Return a compact status projection without returning the full workflow |

Replace `<prefix>` with `ao` or `so`.

## Fragment-First Reading

`*_inspect_workflow_fragment` never returns the full workflow by default. The default response contains summary metadata and bounded context keys. An explicit `json_pointer` requests one bounded fragment and may also set `max_bytes`, `max_array_items`, `max_object_properties`, and `max_depth`. Over-limit fragments are reported as truncated instead of being expanded. `*_inspect_workflow_events` returns only a bounded recent event tail with `max_events` and `max_bytes`; it never prints the complete event log. `*_list_workflow_artifacts` reports only the canonical workflow and its known `.events.jsonl` companion.

There is intentionally no MCP tool that prints the complete workflow. Agents should request the smallest fragment needed for the next decision.

## File Inputs and Results

`workflow_file`, `context_file`, and `result_file` are existing file paths, not inline JSON. The caller must finish and close each input file before sending one tool call. A result envelope uses the same shape as the CLI resume contract:

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

The canonical workflow file and its `.events.jsonl` sidecar remain the durable business state. The MCP connection, process, and in-memory tool registry are not a session store.