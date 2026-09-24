---
name: loom-skill-enhancement MCP startup
description: Select a governance-entry transport from confirmed host capabilities and use the exact runtime descriptor for bounded CLI or MCP inspection.
---

# Mission

Own the governance-entry capability check for the current external SO workflow copy. First reuse MCP only when an already registered server's runtime version and descriptor identity match. If none matches, try ad hoc MCP only when this agent/host can start it directly. If not, use the resolver-owned CLI. MCP is optional and must never be required.

## Inputs

- the runtime-owned launch descriptor file and exact runtime version
- the current external workflow file path
- the selected governance-entry transport and evidence requirements
- the required output keys `mcp_registration_attempt_evidence`, `mcp_startup_evidence`, and `operation_id`

## Required Procedure

1. Load and validate the runtime-owned launch descriptor. Do not choose `dotnet`, a DLL, an EXE, a RID, or a runtime directory from workflow prose. The descriptor produced by the platform-aware resolver is the only source for the launch file, prefix arguments, working directory, exact version, and preparation identity.
2. Select transport before dispatch. First reuse a registered MCP server only when its runtime version and descriptor identity exactly match. If none matches, try an ad hoc MCP session only when this agent/host can start one directly without a required editor registration step. If neither option is available, use the same descriptor for CLI without blocking. Never set `requireMCP=true` or equivalent.
3. For CLI, use the same descriptor for the bounded `inspect-workflow-fragment` operation. For MCP, register the descriptor-backed server if required, complete `initialize` and `notifications/initialized`, then call `so_inspect_workflow_fragment`.
4. Inspect only the bounded fragment of the same external workflow copy and use one unique `operation_id`. Never request the whole workflow.
5. Return `mcp_startup_evidence` with the selected transport, exact runtime version, descriptor identity, workflow path/hash, bounds, operation identity, and result hash. Include MCP configuration or registration evidence only when MCP was actually reused or attempted.
6. If an MCP command has been dispatched and its application/tool operation fails, return failure. Do not retry through CLI or represent the failed MCP operation as unavailable transport.

## Evidence Shape

Return structured JSON with these fields:

```json
{
  "status": "ready | failed",
  "transport": "mcp_stdio | cli",
  "operation_id": "<same operation_id>",
  "mcp_attempted": false,
  "config_attempted": false,
  "config_generated": false,
  "config_files": ["<runtime-owned mcp.json path>"],
  "config_hashes": ["<sha256>"],
  "runtime_mode": "<descriptor value>",
  "runtime_version": "<exact version>",
  "launch_descriptor": "<descriptor path or preparation id>",
  "workflow_file": "<same external workflow file>",
  "workflow_sha256": "<sha256>",
  "fragment_bounded": true,
  "result_sha256": "<sha256>",
  "fallback_reason": null
}
```

For CLI selection, set `mcp_attempted=false`, keep MCP configuration fields empty or false, set `mcp_startup_evidence.transport=cli`, and record that no matching server was registered and ad hoc MCP was unavailable. For MCP reuse or ad hoc registration, set `mcp_attempted=true` and retain the exact versioned server and descriptor identity. The `initialized`, `tool_called`, and `tool_name` fields are required for the `mcp_stdio` branch and not for CLI.

## Failure Rule

If the descriptor is missing, invalid, from another runtime version, or points to a different workflow copy, fail closed. Never replace a failed runtime or descriptor with a repository build, a hand-written DLL/EXE command, a different workflow copy, or a fabricated success record.
