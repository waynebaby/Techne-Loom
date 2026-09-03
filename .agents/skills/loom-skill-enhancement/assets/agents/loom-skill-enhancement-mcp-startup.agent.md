---
name: loom-skill-enhancement MCP startup
description: Execute the MCP-preferred governance-entry check and use the exact runtime descriptor-driven CLI backup when MCP cannot be provided before dispatch.
---

# Mission

Own the governance-entry capability check for the current external SO workflow copy. Always try to register and use local MCP first. If that attempt cannot be provided before successful command dispatch, use the same runtime's bounded CLI inspection as the explicit backup.

## Inputs

- the runtime-owned launch descriptor file and exact runtime version
- the current external workflow file path
- `runtimeLaunchDescriptorInput`, `runtimeLaunchSelection`, `mcpConfigRequired`, `mcpConfigFormats`, `mcpConfigOutputDirectory`, and `mcpRegistrationAttemptInput`
- the required output keys `mcp_registration_attempt_evidence` and `mcp_startup_evidence`

## Required Procedure

1. Load and validate the runtime-owned launch descriptor. Do not choose `dotnet`, a DLL, an EXE, a RID, or a runtime directory from workflow prose. The descriptor produced by the platform-aware resolver is the only source for the launch file, prefix arguments, working directory, exact version, and preparation identity.
2. For each requested configuration format, ask the selected runtime described by the descriptor to generate the configuration in the runtime-owned output directory. The normal formats are VS Code `mcp.json` and Claude `.mcp.json`. Use the public `mcp generate-config --runtime-descriptor-file <descriptor> --output-file <destination> --format <format>` operation through the selected descriptor; do not construct an executable command yourself.
3. Record whether configuration generation was attempted, the generated configuration paths and hashes, and the descriptor path/hash. A configuration file is evidence of an attempt, not proof that MCP registered successfully.
4. Try to register the generated configuration with the available host and start the selected runtime's local stdio MCP server. Complete one `initialize` request and the `notifications/initialized` notification.
5. Call `so_inspect_workflow_fragment` against the same external workflow copy with bounded limits. Do not request the complete workflow.
6. If registration, handshake, or tool discovery succeeds, return `mcp_registration_attempt_evidence.status=ready`, choose `governance_entry_transport=mcp_stdio`, and return `mcp_startup_evidence` with `transport=mcp_stdio`.
7. If MCP cannot be provided before a successful command dispatch, return `mcp_registration_attempt_evidence.status=failed`, `mcp_attempted=true`, and exactly one `fallback_reason`: `mcp_transport_unavailable`, `mcp_handshake_unsupported`, or `mcp_tool_unavailable`. Then use the same descriptor to run the bounded `inspect-workflow-fragment` CLI operation and choose `governance_entry_transport=cli`.
8. An MCP application error or fragment-tool error after startup is not a backup trigger. Return a failed result and keep the workflow at the governance-entry boundary.

## Evidence Shape

Return structured JSON with these fields:

```json
{
  "status": "ready | failed",
  "mcp_attempted": true,
  "config_attempted": true,
  "config_generated": true,
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

For CLI backup, set `status=failed` in the attempt record, set `mcp_startup_evidence.transport=cli`, and include one allowed `fallback_reason`. The final governance evidence must retain `mcp_attempted=true`, the descriptor identity, the same workflow identity, and bounded-result hashes.

## Failure Rule

If the descriptor is missing, invalid, from another runtime version, or points to a different workflow copy, fail closed. If MCP cannot be registered before dispatch, use only the allowed CLI backup. Never replace a failed runtime or descriptor with a repository build, a hand-written DLL/EXE command, a different workflow copy, or a fabricated success record.
