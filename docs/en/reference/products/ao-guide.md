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

- the `.NET` runtime is implemented with `dotnet ao.dll --guide`, `dotnet ao.dll --help`, `dotnet ao.dll compile`, `dotnet ao.dll run`, and `dotnet ao.dll resume`
- AO is CLI-only in this project; there is no public MCP host or MCP tool surface
- current AO control payloads emit `blocked` and `completed`; CLI/runtime failures surface as `<ao_property>` blocks with `type: error`
- AO compile emits Mermaid Markdown, HTML, and workflow JSON backup validation artifacts for an agent-authored workflow file
- each AO run/resume also emits audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups

## Environment Setup

Before using AO through a skill or direct CLI:

1. Choose package channel from [`packages.released.md`](../../../../packages.released.md) or [`packages.beta.md`](../../../../packages.beta.md).
2. Use NuGet.org as the first-class latest package source for install/version discovery; when local AO execution needs NuGet download, restore the AO runtime bundle together: `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`, all at the same channel/version. Use the GitHub release asset links only as fallback when NuGet.org is unavailable or when you explicitly need package assets.
3. Read this guide through `dotnet ao.dll --guide`.
4. When useful for planning review or artifact exchange, have the calling agent author an AO workflow JSON snapshot outside the AO CLI.
5. Prepare a writable session directory and, when needed, an explicit audit output root for compile validation artifacts and run/resume audit artifacts.
6. Keep checked-in plans and authored snapshots immutable: do not place AO `--session-dir` outputs or `--audit-output` under a skill folder; use a runtime temp folder or explicit execution-output folder instead.

## Contracts

```guide-contract
inputs:
  objective: user goal or task request
  context: current known facts, artifacts, and prior decisions
  session_dir: required CLI field for the AO session directory, exposed as `--session-dir`; must be outside any skill folder
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
progress_output:
  type: progress
  workflow_file: current mutable workflow path
  event_log_file: append-only AO event log path
  current_node_id: current focus node
  audit_artifacts:
    mermaid_file: current workflow Mermaid Markdown path
    html_file: current workflow HTML path
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
- treat session metadata as explicit CLI input when needed instead of depending on hidden host state

AO should not:

- impersonate a deterministic skill executor
- hide control state inside narrative-only text
- collapse every decision into one opaque prompt roundtrip
- hide or bypass the documented CLI control surface with private wrappers

## Responsibilities

### Caller

- Provide the objective and current known context.
- When local runtime download is needed, restore the full AO runtime bundle instead of only `Techne.Loom.AgentOrchestrator`.
- Execute external actions requested by AO.
- Resume AO with structured results.
- Preserve `session_id` between turns.
- Keep a stable session directory and pass it through `--session-dir`.
- Keep `--session-dir` outputs and any `--audit-output` outside skill-owned directories.
- On every AO progress update, surface the current workflow Mermaid Markdown and HTML paths in think-out-loud output.

### Author

- Define how control-state files are stored and surfaced.
- Keep AO outputs machine-first and stable.
- Keep weave-out requests, their current wire fields, and their event-log traces visible rather than hidden in private heuristics.

### Outer-agent

- Decide whether to accept AO's proposed frontier.
- Preserve artifact references and blocked-payload context across resumes.
- Treat AO as the exploratory coordinator, not as the place to execute SO-owned deterministic work.
- When a pre-authored AO workflow file is needed, generate that JSON so it matches the AO snapshot schema before calling `dotnet ao.dll compile`.
- Keep audit artifacts, intermediate workflow materializations, and conversation-referenceable outputs under a runtime temp root, repo-root temp root, or an explicit user-chosen execution output root, never under a skill folder by default.

## Templates

```guide-template
dotnet ao.dll compile \
  --workflow-file ao-plan.json \
  --audit-output outputs/audit
```

`ao-plan.json` can stay as a checked-in or exchanged source artifact, but `outputs/audit` should resolve outside any skill folder.

```guide-template
dotnet ao.dll run \
  --objective-file objective.md \
  --context-file context.json \
  --session-dir outputs/sessions \
  --audit-output outputs/audit
```

`outputs/sessions` and `outputs/audit` must live outside any skill-owned directory so AO runtime state does not dirty checked-in skill assets.

```guide-template
dotnet ao.dll resume \
  --session-dir outputs/sessions \
  --session-id 20260609010101_abc12345 \
  --result-file latest-boundary-result.json
```

Resume must point back to the same external session directory, not to a path under a skill folder.

```guide-checklist
- objective is explicit
- when the caller wants a reusable AO workflow snapshot artifact, the calling agent authors that AO workflow JSON file before validation handoff
- compile writes Mermaid Markdown and HTML validation outputs before execution handoff
- session_id is preserved by caller
- session directory is stable and writable
- session directory and audit output stay outside skill folders
- artifact references are durable
- caller can resume with structured data
- control outputs are persisted for audit
- documented CLI control path is preserved
- weave-out requests are expressed explicitly, not hidden in prose
- audit and intermediate outputs stay in temp-root or explicit execution-output locations outside skill folders by default
- compile must fail instead of overwriting pre-existing artifact files
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
- Replacing the documented CLI/package control path with a private wrapper without a clear reason.
- Letting AO imply a weave-out request informally instead of emitting an explicit structured boundary for it.
- Letting a skill hide package/channel choice instead of sending users to the package index first.
