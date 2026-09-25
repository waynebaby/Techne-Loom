# Agent Integration

[中文](../../zh-cn/guides/agent-integration.md) | [Root](../README.md)

Use Loom Agent Plan-Execution Orchestrator when a caller needs explicit orchestration decisions while the route is still evolving.

In repo terminology, AO **weaves out** at control seams, surfacing them through blocked control payload fields such as `boundary_reason` and `weave_out_request`, and callers **weave back** through `ao.exe resume` or `ao resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

## Integration Rules

- Keep one stable external workflow file and preserve its path across turns.
- Keep the workflow's `.events.jsonl` event sidecar beside that same external workflow file.
- Read AO outputs as control data first and prose second.
- Resume AO at deliberate seams with structured results and artifact references, using the corresponding blocked payload fields as the protocol surface.
- Do not treat AO as a deterministic workflow runner.

## Current Public Direction

- AO exposes the documented CLI and a local stdio-only MCP server.
- Use `ao.exe mcp stdio` on Windows or `ao mcp stdio` on Unix only when the host requests MCP; direct CLI integration uses the documented apphost `compile`, `run`, and `resume` commands.
- The MCP server is local and trusted-host only; it does not provide Web or remote transport.
- When AO needs a reusable workflow snapshot artifact, the calling agent authors that JSON outside the AO apphost and validates it with `ao.exe compile --workflow-file <path>` or `ao compile ...`.
- Keep audit and intermediate outputs referenceable in conversation, but store them under a temp root, repo-root temp root, or explicit execution output root rather than any skill folder by default.
- Read [Workflow Terminology](../architecture/workflow-terminology.md) for the repo-wide meaning of weave out, weave back, seam, and strand.
- The public Loom Agent Plan-Execution Orchestrator guide reflects the implemented `.NET` runtime and should stay in lockstep with runtime behavior.

## Example Control Payload Shape

```json
{
  "status": "blocked",
  "boundary_reason": "clarification_required",
  "workflow_file": "outputs/workflow-instance.json",
  "event_log_file": "outputs/workflow-instance.json.events.jsonl",
  "current_node_id": "review.slice.2",
  "pending_requirements": ["filePath"]
}
```

## Common Failure Mode

The caller treats AO like a chat wrapper and only reads the narrative explanation. That loses the control-state surface AO is supposed to own.
