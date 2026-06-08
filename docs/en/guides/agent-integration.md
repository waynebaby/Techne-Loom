# Agent Integration

[中文](../../zh-cn/guides/agent-integration.md)

Use AO when a caller needs explicit orchestration decisions while the route is still evolving.

## Integration Rules

- Keep a stable location for the mutable workflow file and the append-only event log.
- Read AO outputs as control data first and prose second.
- Resume AO at deliberate boundaries with structured results and artifact references.
- Do not treat AO as a deterministic workflow runner.

## Current Public Direction

- AO is intended to run over the official `ModelContextProtocol` C# SDK.
- `MCP/stdio` is the canonical runtime transport.
- The public AO guide is ahead of code and should be treated as the current contract for the next implementation slice.

## Example Control Payload Shape

```json
{
  "status": "blocked",
  "boundary_reason": "clarification_required",
  "workflow_file": "current-workflow.json",
  "event_log_file": "current-events.jsonl",
  "current_node_id": "review.slice.2",
  "pending_requirements": ["filePath"]
}
```

## Common Failure Mode

The caller treats AO like a chat wrapper and only reads the narrative explanation. That loses the control-state surface AO is supposed to own.
