# MCP Reference

[中文](../../zh-cn/reference/mcp.md) | [Root](../README.md)

## Transport

AO and SO expose one local MCP transport: newline-delimited JSON-RPC over the process stdin and stdout streams.

```text
dotnet ao.dll mcp stdio
dotnet so.dll mcp stdio
```

This surface is local and stdio-only. It does not provide MCP over Web, HTTP, a socket, or a remote host. The host process must be trusted: file paths are path-only inputs and are read or written with the operating system permissions of that process.

A client must send `initialize` with an object containing `protocolVersion`, `capabilities`, and `clientInfo`, then send the `notifications/initialized` notification without an `id`. Tool calls before that handshake are rejected. Every `tools/call` argument object must also include a safe `operation_id` using 1-128 ASCII letters, digits, `.`, `_`, or `-`; the same id is returned in the structured tool result.

## User-Level Versioned Registration

The default configuration directory is the current user's Loom directory:

```text
~/.skills/loomed/mcp/<product>/<exact-version>/mcp.json
~/.skills/loomed/mcp/<product>/<exact-version>/.mcp.json
```

The generated server key is `loom-so-<exact-version>` or `loom-ao-<exact-version>`. Tool names remain stable, such as `so_inspect_workflow_fragment`. An MCP session may be reused only when its reported `serverInfo.version`, runtime mode, RID, preparation ID, launch command/file, launch-argument hash, and descriptor hash all match the requested runtime identity; otherwise the stale session is replaced. A user-level adapter owns loading this dedicated configuration and holding stdio handles. Loom does not scan or adopt an orphaned process.

Descriptor-owned configurations include `TECHNE_LOOM_MCP_BINDING_REQUIRED=true` and the runtime identity fields for product, exact version, mode, RID, preparation ID, launch command/file, launch-argument hash, and the canonical descriptor hash used by binding and session reuse. A managed AO or SO MCP server rejects `initialize` when that binding is required but missing or malformed; descriptor-dependent guide and compile tools perform the full descriptor match. Configuration results retain both the raw descriptor-file SHA-256 and the canonical descriptor hash so evidence uses one named meaning for each value.

## Governed SO Entry

For every Loom Skill Orchestrator-governanced target-skill verification, including `/loom-skill-enhancement` self-bootstrap, the exact published runtime must first return a resolver-owned launch descriptor for the same external workflow copy.
The public `dotnet so.dll runtime resolve --version <version> --runtime-descriptor-file <path>` operation writes that descriptor. It delegates platform, RID, package identity, executable, cache, and launch-path selection to the resolver.

1. Use that descriptor to generate the requested VS Code `mcp.json` and Claude `.mcp.json` through the selected runtime. The resolver chooses the self-contained executable or framework-dependent DLL; workflow text must not choose either one.
2. Try to register the generated configuration, complete `initialize` and `notifications/initialized`, and call `so_inspect_workflow_fragment` with bounded limits.
3. On success, persist `mcp_registration_attempt_evidence.status=ready`, set `governance_entry_transport=mcp_stdio`, and return `mcp_startup_evidence` with the same descriptor and workflow identities.
4. If MCP cannot be provided before successful command dispatch, persist `mcp_registration_attempt_evidence.status=failed`, `mcp_attempted=true`, and exactly one allowed reason: `mcp_transport_unavailable`, `mcp_handshake_unsupported`, or `mcp_tool_unavailable`. Then use the same descriptor for the bounded `inspect-workflow-fragment` CLI backup and set `governance_entry_transport=cli`.
5. An MCP application or command failure after startup is not a backup trigger. Keep the saved workflow at the failed boundary.
6. Only after one transport has produced `mcp_startup_evidence` may the workflow capture `--guide` and continue to planning, authoring, validation, compile, run, or resume.

## Tool Contract

AO and SO remain independent products. AO registers the `ao_` tools and SO registers the `so_` tools. The shared protocol implementation does not merge their runtime or release identity.

Each product exposes the same eight workflow tools. All workflow tools require `operation_id` in addition to the file inputs:

| Tool | Required inputs | Purpose |
| --- | --- | --- |
| `<prefix>_capture_guide` | `operation_id`, `runtime_descriptor_file` | Capture the fresh guide surface from the selected runtime |
| `<prefix>_compile_workflow` | `operation_id`, `workflow_file`, `runtime_descriptor_file` | Return structured compile feedback and an optional feedback artifact |
| `<prefix>_inspect_workflow_fragment` | `operation_id`, `workflow_file` | Return summary metadata by default, or a bounded JSON Pointer fragment when explicitly requested |
| `<prefix>_inspect_workflow_events` | `operation_id`, `workflow_file` | Return a bounded tail of the event sidecar |
| `<prefix>_list_workflow_artifacts` | `operation_id`, `workflow_file` | Return the canonical workflow and known sidecar manifest |
| `<prefix>_run_workflow` | `operation_id`, `workflow_file` | Run the canonical workflow file until completion or an external result boundary |
| `<prefix>_resume_workflow` | `operation_id`, `workflow_file`, `result_file` | Apply one disk-backed result envelope; Plan results require a non-empty `result_id` |
| `<prefix>_get_workflow_status` | `operation_id`, `workflow_file` | Return a compact status projection without returning the full workflow |

Replace `<prefix>` with `ao` or `so`.

## Fragment-First Reading

`*_inspect_workflow_fragment` never returns the full workflow by default. The default response contains summary metadata and bounded context keys. An explicit `json_pointer` requests one bounded fragment and may also set `max_bytes`, `max_array_items`, `max_object_properties`, and `max_depth`. Over-limit fragments are reported as truncated instead of being expanded. `*_inspect_workflow_events` returns only a bounded recent event tail with `max_events` and `max_bytes`; it never prints the complete event log. `*_list_workflow_artifacts` reports only the canonical workflow and its known `.events.jsonl` and `.operations.jsonl` companions.

There is intentionally no MCP tool that prints the complete workflow. Agents should request the smallest fragment needed for the next decision.

## File Inputs and Results

`workflow_file`, `context_file`, `result_file`, and `runtime_descriptor_file` are existing file paths, not inline JSON. `operation_id` is a caller-generated operation identity. State-changing `run` and `resume` calls use it as an idempotency key; read-only and compile calls use it to correlate results and artifacts. `run`/`resume` request hashes use canonical workflow JSON: object properties are sorted by name, arrays keep their order, optional null members are omitted, and empty objects or arrays remain distinct. The caller must finish and close each input file before sending one tool call. A result envelope uses the same shape as the CLI resume contract:

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

The canonical workflow file and its `.events.jsonl` sidecar remain the durable business state. For state-changing `run` and `resume`, the adjacent `.operations.jsonl` ledger records `started` and `completed` results; a completed operation is replayed only for the same request hash, while an in-doubt operation is never replayed. The MCP connection, process, and in-memory tool registry are not a session store.