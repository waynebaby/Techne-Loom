# Agent Integration

[中文](../../zh-cn/guides/agent-integration.md) | [Root](../README.md)

Use AO when a caller needs explicit orchestration decisions while the route is still evolving.

In repo terminology, AO **weaves out** at control seams, surfacing them through blocked control payload fields such as `boundary_reason` and `weave_out_request`, and callers **weave back** through `dotnet ao.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

## Integration Rules

- Keep a stable session directory, pass it through `--session-dir`, and preserve only `session_id` between turns.
- Derive workflow/event artifact paths from that session directory plus `session_id`.
- Read AO outputs as control data first and prose second.
- Resume AO at deliberate seams with structured results and artifact references, using the corresponding blocked payload fields as the protocol surface.
- Do not treat AO as a deterministic workflow runner.

## Current Public Direction

- AO is CLI-only in this project.
- Use the documented `compile`, `run`, and `resume` commands as the integration contract.
- When AO needs a reusable workflow snapshot artifact, the calling agent authors that JSON outside the AO CLI and then validates it with `dotnet ao.dll compile --workflow-file <path>`.
- Keep audit and intermediate outputs referenceable in conversation, but store them under a temp root, repo-root temp root, or explicit execution output root rather than any skill folder by default.
- Read [Workflow Terminology](../architecture/workflow-terminology.md) for the repo-wide meaning of weave out, weave back, seam, and strand.
- The public AO guide reflects the implemented `.NET` runtime and should stay in lockstep with AO behavior.

## Example Control Payload Shape

```json
{
  "status": "blocked",
  "session_id": "20260609010101_abc12345",
  "boundary_reason": "clarification_required",
  "workflow_file": "outputs/sessions/session_20260609010101_abc12345_workflow.json",
  "event_log_file": "outputs/sessions/session_20260609010101_abc12345_events.jsonl",
  "current_node_id": "review.slice.2",
  "pending_requirements": ["filePath"]
}
```

## Common Failure Mode

The caller treats AO like a chat wrapper and only reads the narrative explanation. That loses the control-state surface AO is supposed to own.
