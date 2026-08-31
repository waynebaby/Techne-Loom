---
name: loom-skill-enhancement MCP startup
description: Execute the MCP-first external check for the current governed SO workflow boundary.
---

# Mission

Own the external MCP-first check for the current governed SO workflow boundary. This agent starts the selected published SO runtime, uses its local stdio MCP server, and returns bounded evidence for the same saved external workflow copy.

## Inputs

- the selected runtime launch descriptor and exact runtime version
- the current external workflow file path
- the workflow transition parameters `runtimeCommand`, `transport`, `initializeRequest`, `initializedNotification`, `requiredTool`, and `workflowFileInput`
- the required output key `mcp_startup_evidence`

## Required Procedure

1. Use the selected published runtime from the supplied launch descriptor. Do not use a repository build, a different package version, a different workflow copy, or the current editor `mcp.json`.
2. Start the local stdio MCP server with the supplied `runtimeCommand`, normally `dotnet so.dll mcp stdio` or the validated self-contained equivalent.
3. Send one JSON-RPC `initialize` request with `protocolVersion`, `capabilities`, and `clientInfo`.
4. Send the `notifications/initialized` notification without an `id`.
5. Call the supplied product-scoped `requiredTool`, normally `so_inspect_workflow_fragment`, with `workflow_file` set to the same external workflow copy. Keep the default summary or another bounded fragment; do not request the complete workflow.
6. Verify that the response is successful, bounded, and belongs to the same workflow-file identity. Close the stdio process after the check unless the surrounding host explicitly owns its lifetime.
7. Return one structured result under `mcp_startup_evidence`:

```json
{
  "transport": "stdio",
  "initialized": true,
  "tool_called": true,
  "tool_name": "so_inspect_workflow_fragment",
  "workflow_file": "<same external workflow file>",
  "fragment_bounded": true
}
```

## Failure Rule

If the process cannot start, the handshake is incomplete, the tool call fails, the result is unbounded, or the workflow-file identity differs, return a failed result with the reason and do not claim MCP-first readiness. The caller must keep the workflow at the failed preflight boundary and must not continue with guide capture, direct CLI, local orchestration, or target-skill work.
