# AgentOrchestrator Guide

[中文](../../../zh-cn/reference/products/ao-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

AO is a top-agent-facing orchestration product for exploratory work under uncertainty.

It does not try to hide uncertainty. It captures evolving workflow state, emits machine-first control data, and returns at major control boundaries so a caller can choose the next action deliberately.

Current implementation status:

- this guide is ahead of the current AO code
- the repository already treats this page as the public handoff contract for the next major implementation slice
- the target runtime path is the official `ModelContextProtocol` C# SDK over `MCP/stdio`, with a sampling-planner route preserved by design

## Contracts

```guide-contract
inputs:
  objective: user goal or task request
  context: current known facts, artifacts, and prior decisions
  workflow_file: optional existing mutable workflow snapshot
  event_log_file: optional append-only event log
outputs:
  status: active | blocked | completed | failed
  boundary_reason: optional reason for return
  workflow_file: current mutable workflow path
  event_log_file: append-only log path
  current_node_id: current focus node
  result_file: optional final or intermediate result path
  pending_requirements: optional structured missing inputs
  next_frontier: optional candidate actions
  human_or_agent_hint: optional short action hint for the caller
  sampling_request: optional structured planner/sampling request when AO wants the outer host to invoke model-side sampling
```

AO callers resume the product with structured results, not freeform retrospectives.

## Behavior

AO should:

- inspect current context
- expand or refine the workflow frontier
- choose among clarification, probing, delegation, replanning, or completion
- persist decisions, artifacts, and boundary metadata
- keep a mutable workflow file plus an append-only event or snapshot log
- express model-side sampling or planner needs through the official MCP route rather than hiding them in opaque prose

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
- Host the AO MCP server/session and preserve the current workflow plus event log paths between turns.

### Author

- Define how control-state files are stored and surfaced.
- Keep AO outputs machine-first and stable.
- Keep sampling/planner integration visible in the event log and control payloads rather than hidden in private heuristics.

### Outer-agent

- Decide whether to accept AO's proposed frontier.
- Preserve artifact references and boundary context across resumes.
- Treat AO as the exploratory coordinator, not as the place to execute SO-owned deterministic work.

## Templates

```guide-template
ao run \
  --objective-file objective.md \
  --context-file context.json \
  --workflow-file current-workflow.json \
  --event-log-file current-events.jsonl
```

```guide-template
ao resume \
  --workflow-file current-workflow.json \
  --event-log-file current-events.jsonl \
  --result-file latest-boundary-result.json
```

```guide-checklist
- objective is explicit
- existing workflow path is stable
- artifact references are durable
- caller can resume with structured data
- control outputs are persisted for audit
- official MCP/stdio hosting path is preserved
- sampling/planner requests are expressed explicitly, not hidden in prose
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
  status: active
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
name: request-sampling-planner
input: AO needs a model-side comparison of competing execution frontiers
ao-return:
  status: blocked
  boundary_reason: sampling_required
  sampling_request:
    objective: compare two frontier candidates
    artifacts:
      - frontier-a.json
      - frontier-b.json
```

```guide-example
name: complete-with-artifact
input: top-level task has converged and final outputs are written
ao-return:
  status: completed
  result_file: outputs/final-report.md
  workflow_file: outputs/current-workflow.json
```

## Anti-Patterns

- Treating AO as a general-purpose chat wrapper.
- Returning prose that omits workflow, node, or artifact state.
- Using AO to execute deterministic step-by-step skill logic that belongs in SO.
- Replacing the official MCP/stdio path with a private transport layer without a clear reason.
- Letting AO request sampling/planning informally instead of emitting a structured boundary for it.
