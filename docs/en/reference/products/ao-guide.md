# AgentOrchestrator Guide

[中文](../../../zh-cn/reference/products/ao-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public runtime contract

## Overview

AO is a top-agent-facing orchestration product for exploratory work under uncertainty.

It does not try to hide uncertainty. It captures evolving workflow state, emits machine-first control data, and weaves out at major control seams, surfacing blocked payloads with explicit boundary fields when a caller must choose the next action deliberately.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../../../en/architecture/workflow-terminology.md). In that vocabulary, AO weaves out at control seams, surfacing them through blocked control payload fields such as `boundary_reason` and `weave_out_request`, and callers weave back through `dotnet ao.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- the `.NET` runtime is implemented with `dotnet ao.dll --guide`, `dotnet ao.dll planner`, `dotnet ao.dll host`, `dotnet ao.dll run`, and `dotnet ao.dll resume`
- the runtime path is the official `ModelContextProtocol` C# SDK over `MCP/stdio`
- current AO control payloads emit `blocked` and `completed`; CLI/runtime failures surface as `<ao_property>` blocks with `type: error`
- each AO run/resume also emits audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups

## Environment Setup

Before using AO through a skill or direct CLI:

1. Choose package channel from [`packages.released.md`](../../../../packages.released.md) or [`packages.beta.md`](../../../../packages.beta.md).
2. Install or build the package.
3. Read this guide through `dotnet ao.dll --guide`.
4. Prepare a writable session directory and, when needed, an explicit audit output root.

## Contracts

```guide-contract
inputs:
  objective: user goal or task request
  context: current known facts, artifacts, and prior decisions
  sessionDirectory: required MCP/object field for the AO session directory; the CLI exposes the same concept as `--session-dir`
  invocation_context: optional per-call host execution metadata; MCP tool callers can use this to declare weave-out route details without relying on ambient server injection
outputs:
  status: blocked | completed (current control-payload values)
  session_id: AO-generated stable identifier for this session
  boundary_reason: optional reason for return
  workflow_file: current mutable workflow path derived from the session directory plus session_id
  event_log_file: append-only log path derived from the session directory plus session_id
  current_node_id: current focus node
  result_file: reserved optional field for future AO-owned output artifacts; not currently populated
  pending_requirements: optional structured missing inputs
  next_frontier: optional candidate actions
  human_or_agent_hint: optional short action hint for the caller
  weave_out_request: structured AO weave-out request data when AO asks the outside world to perform comparison, planning, or similar analysis
  audit_artifacts:
    output_root: audit output root
    step_directory: per-step audit directory
    mermaid_file: point-in-time Mermaid Markdown path
    html_file: point-in-time HTML path
    workflow_backup_file: point-in-time workflow JSON backup
```

AO callers resume the product with structured results, not freeform retrospectives.

In repo terminology, a blocked AO return is a weave out, and `dotnet ao.dll resume` is the weave-back path.

## Behavior

AO should:

- inspect current context
- expand or refine the workflow frontier
- choose among clarification, probing, delegation, replanning, or completion
- persist decisions, artifacts, and blocked-payload metadata
- keep a mutable workflow file plus an append-only event or snapshot log
- express weave-out requests for external comparison, planning, or similar analysis through explicit blocked-payload fields rather than hiding them in opaque prose
- reject resume envelopes whose `transition_id` does not match the currently blocked workflow seam as recorded by the pending payload fields
- treat host or session metadata as per-call input when needed; do not depend on a durable injected `IMcpServer` for future sessionless HTTP hosts

AO should not:

- impersonate a deterministic skill executor
- hide control state inside narrative-only text
- collapse every decision into one opaque prompt roundtrip
- replace official MCP transport surfaces with repo-specific glue unless a real blocker justifies it

## Responsibilities

### Caller

- Provide the objective and current known context.
- Execute external actions requested by AO.
- Resume AO with structured results.
- Host the AO MCP server/session and preserve `session_id` between turns.
- Keep a stable session directory; CLI callers pass `--session-dir`, while MCP/object callers pass `sessionDirectory`, and both surfaces derive workflow/event paths from that directory plus `session_id`.

### Author

- Define how control-state files are stored and surfaced.
- Keep AO outputs machine-first and stable.
- Keep weave-out requests, their current wire fields, and their event-log traces visible rather than hidden in private heuristics.

### Outer-agent

- Decide whether to accept AO's proposed frontier.
- Preserve artifact references and blocked-payload context across resumes.
- Treat AO as the exploratory coordinator, not as the place to execute SO-owned deterministic work.

## Templates

```guide-template
dotnet ao.dll planner \
  --plan-file detailed-plan.md \
  --workflow-file ao-plan.json \
  --context-file context.json
```

```guide-template
dotnet ao.dll run \
  --objective-file objective.md \
  --context-file context.json \
  --session-dir outputs/sessions \
  --audit-output outputs/audit
```

```guide-template
dotnet ao.dll resume \
  --session-dir outputs/sessions \
  --session-id 20260609010101_abc12345 \
  --result-file latest-boundary-result.json
```

```guide-checklist
- objective is explicit
- session_id is preserved by caller
- session directory is stable and writable
- artifact references are durable
- caller can resume with structured data
- control outputs are persisted for audit
- official MCP/stdio hosting path is preserved
- weave-out requests are expressed explicitly, not hidden in prose
```

## Examples

```guide-example
name: clarify-missing-dimensions
input: user asks for a battery layout with incomplete envelope dimensions
ao-return:
  status: blocked
  boundary_reason: clarification_required
  pending_requirements:
    - enclosure_length
    - enclosure_width
    - enclosure_height
```

```guide-example
name: probe-local-repository
input: top agent needs to locate the code owning a failing CLI path
ao-return:
  status: blocked
  boundary_reason: tool_probe_required
  next_frontier:
    - search_cli_entrypoints
    - inspect_recent_validation_logs
```

```guide-example
name: delegate-subtask
input: orchestration requires a focused code review by a narrower agent
ao-return:
  status: blocked
  boundary_reason: delegation_required
  current_node_id: review.slice.2
```

```guide-example
name: weave-out-for-frontier-comparison
input: AO needs an external comparison of competing execution frontiers
ao-return:
  status: blocked
  boundary_reason: weave_out_required
  weave_out_request:
    objective: compare two frontier candidates
    artifacts:
      - frontier-a.json
      - frontier-b.json
```

```guide-example
name: complete-current-workflow
input: top-level task has converged and the caller resumes with completion data
ao-return:
  status: completed
  session_id: 20260609010101_abc12345
  workflow_file: outputs/sessions/session_20260609010101_abc12345_workflow.json
  current_node_id: state.completed
```

## Anti-Patterns

- Treating AO as a general-purpose chat wrapper.
- Returning prose that omits workflow, node, or artifact state.
- Using AO to execute deterministic step-by-step skill logic that belongs in SO.
- Replacing the official MCP/stdio path with a private transport layer without a clear reason.
- Letting AO imply a weave-out request informally instead of emitting an explicit structured boundary for it.
- Letting a skill hide package/channel choice instead of sending users to the package index first.
