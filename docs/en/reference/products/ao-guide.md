# Loom Agent Execution Orchestrator Guide

[中文](../../../zh-cn/reference/products/ao-guide.md) | [Root](../../README.md)

Version: draft

Build: repository source

Compatibility: pre-release public runtime contract

## Overview

Treat `dotnet ao.dll --guide` as a governance anchor, not as a detour. Once a fresh guide result has been emitted from a runnable AO runtime, all governed execution must stay on the corresponding published AO package runtime surface described by that guide. Do not read the guide and then drift back to repository builds, hand-assembled runtimes, or non-governed execution paths for official AO skill execution.

Loom Agent Execution Orchestrator is the top-agent-facing orchestration product for exploratory work under uncertainty.

It does not try to hide uncertainty. It captures evolving workflow state, emits machine-first control data, and weaves out at major control seams, surfacing blocked payloads with explicit boundary fields when a caller must choose the next action deliberately.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../../../en/architecture/workflow-terminology.md). In that vocabulary, Loom Agent Execution Orchestrator weaves out at control seams, surfacing them through blocked control payload fields such as `boundary_reason` and `weave_out_request`, and callers weave back through `dotnet ao.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- the `.NET` runtime is implemented with `dotnet ao.dll --guide`, `dotnet ao.dll --help`, `dotnet ao.dll compile`, `dotnet ao.dll prompt-plan`, `dotnet ao.dll prompt-replan`, `dotnet ao.dll run`, and `dotnet ao.dll resume`
- Loom Agent Execution Orchestrator is CLI-only in this project; there is no public MCP host or MCP tool surface
- current AO control payloads emit `blocked` and `completed`; CLI/runtime failures surface as `<ao_property>` blocks with `type: error`
- AO compile emits Mermaid Markdown, HTML, and workflow JSON backup validation artifacts for an agent-authored workflow file
- AO prompt-plan and prompt-replan emit AO-owned planner/replanner prompt text through `<ao_property type="prompt">` blocks
- each AO run/resume also emits audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
- `run` can optionally accept an authored `WorkflowInstance` through `--instance-file` so the first runtime blocked step audits the same graph that compile/prompt-plan validated

## Environment Setup

Before using Loom Agent Execution Orchestrator through a skill or direct CLI:

1. For direct CLI or manual package acquisition, choose package channel from [`packages.released.md`](../../../../packages.released.md) or [`packages.beta.md`](../../../../packages.beta.md). For `/loom-plan-execution`, normal package downloads should instead follow the current CI/CD-managed skill package version block and derive `released` versus `beta` from that bound version when needed. If a future checked-in AO runtime lock is added and it ever disagrees with the current CI/CD-managed skill package version block, treat the CI/CD-managed skill package version block as the immediate download authority and update the checked-in lock to match before continuing governed execution.
2. Use NuGet.org as the first-class latest package source for install/version guidance; when local Loom Agent Execution Orchestrator execution needs NuGet download, restore the Loom Agent Execution Orchestrator runtime bundle together: `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`, all at the same channel/version. When an exact package id/version is already known, probe or download the direct `.nupkg` URL instead of waiting for page/search/registration indexing. Use the GitHub release asset links only as fallback when NuGet.org is unavailable or when you explicitly need package assets.
3. Read this guide through `dotnet ao.dll --guide`.
4. Once that fresh guide result exists, route governed execution back onto the corresponding published AO package runtime it describes. `--guide` is not permission to continue official skill execution on repository builds, hand-assembled runtimes, or other non-governed paths.
5. When useful for planning review or artifact exchange, have the calling agent author a Loom Agent Execution Orchestrator workflow JSON snapshot outside the AO CLI.
6. Prepare a writable session directory and, when needed, an explicit audit output root for compile validation artifacts and run/resume audit artifacts.
7. Keep checked-in plans and authored snapshots immutable: do not place Loom Agent Execution Orchestrator `--session-dir` outputs or `--audit-output` under a skill folder; use a runtime temp folder or explicit execution-output folder instead.

When authoring AO workflow JSON, every state node must declare a non-empty `workflowPhase`. That field means which stage of the overall workflow the node belongs to, and AO compile should reject missing or empty values with a node-specific reason and fix suggestion.

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
  workflow_instance_file: current caller-managed or runtime-owned WorkflowInstance path used for audit continuity and replan edits
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
    summary_file: structured per-step summary file for direct boundary/frontier replay
progress_output:
  type: progress
  workflow_file: current mutable workflow path
  workflow_instance_file: current caller-managed or runtime-owned WorkflowInstance path
  event_log_file: append-only AO event log path
  current_node_id: current focus node
  audit_artifacts:
    mermaid_file: current workflow Mermaid Markdown path
    html_file: current workflow HTML path
event_log:
  file_shape: append-only jsonl
  common_fields:
    - event_type
    - ts
    - session_id
    - workflow_file
    - event_log_file
    - workflow_instance_file
    - step_sequence
    - step_action
    - step_directory
    - summary_file
  boundary_event_fields:
    - boundary_reason
    - transition_id
    - correlation_key
    - pending_requirements
    - next_frontier
prompt_output:
  type: prompt
  command: prompt-plan | prompt-replan
  prompt_kind: plan | replan
  prompt_template_version: AO-owned prompt template version
  prompt: code-generated prompt text
  blocks:
    - block_id: stable machine-ingestible lookup key such as workflow.output-schema or prompt.replan.current-workflow-projection
      block_kind: guide-contract | guide-example | guide-template
      semantic_role: schema | task-contract | runtime-context | workflow-projection | workflow-instance | selected-seam | user-objective
      title: human-readable block title
      content_type: usually application/json
      order: stable render order inside the generated prompt
      consumption_requirement: required | optional for downstream prompt consumers
      content: code-generated JSON block content
      tags: optional classifier tags for downstream tooling
  allowed_node_kinds: allowed workflow node kind discriminator values
  allowed_command_kinds: allowed command invocation kind values
  workflow_file: current AO mutable workflow path when prompt-replan is used
  workflow_instance_file: explicit WorkflowInstance file path when prompt-replan is used
  selected_tbr_id: explicit TBR node id when prompt-replan is used
resume_input:
  transition_id: required, must match `workflow_file.last_transition_id` at the currently blocked seam
  correlation_key: optional caller correlation key for one boundary cycle
  payload: required structured caller result object, merged by AO into runtime context
```

AO callers resume the product with structured results, not freeform retrospectives.

In repo terminology, a blocked AO return is a weave out, and `dotnet ao.dll resume` is the weave-back path.

Current runtime persistence intentionally keeps two shapes alive:

- `workflow_file` is the AO snapshot control file. Runtime resume validates `transition_id` against this file.
- `workflow_instance_file` is the current graph-shaped `WorkflowInstance` surface used for compile continuity, runtime audit continuity, and caller-managed replan edits.
- under `session_dir`, AO owns `session_<id>_runtime.workflow.json` as its runtime `WorkflowInstance` sidecar and `session_<id>_runtime.workflow.pointer.json` as the optional pointer to an external caller-managed `workflow_instance_file`.

## Plan/Replan Playbook

This section defines the operational playbook for callers and outer agents. It maps directly to the current AO runtime behavior in `AoBoundaryPlanner` and `AoRuntimeService`.

### Schema Boundary (Current Code)

Use only current AO runtime fields for machine-level plan/replan dispatch.

Boundary/progress read fields:

- `status`
- `session_id`
- `workflow_file`
- `event_log_file`
- `current_node_id`
- `boundary_reason`
- `pending_requirements`
- `next_frontier`
- `human_or_agent_hint`
- `weave_out_request`

Resume write fields:

- `transition_id`
- `correlation_key`
- `payload`

Do not introduce new top-level AO fields in docs, prompts, or examples.

### Dispatch Convention Layer (Non-schema)

When callers need richer execution guidance, place it under resume `payload` as caller convention data.

Recommended stable convention keys:

- `payload.plan_meta.plan_phase`: `initial-plan` | `replan`
- `payload.plan_meta.unsolved_target_id`: caller-selected unresolved target node id
- `payload.plan_meta.selected_frontier_action`: one selected item from `next_frontier`
- `payload.plan_meta.method`: `u2d-expand-bridge`
- `payload.plan_meta.determined_path_ids`: ordered node ids for this cycle's deterministic path
- `payload.plan_meta.unresolved_bridge_ids`: unresolved bridge node ids kept for later cycles
- `payload.plan_meta.next_step_prompt`: imperative operator prompt for the next cycle

These keys are conventions, not AO-owned wire-schema fields.

AO now also owns two prompt-generation support surfaces:

- `dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>]`: generate planner prompt text for authoring a WorkflowInstance JSON file
- `dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id>`: generate replanner prompt text for modifying the current WorkflowInstance by replacing one selected `tbr` node

AO run also has one authored-graph continuity surface:

- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--instance-file <path>] [--audit-output <path>]`: when `--instance-file` is provided, AO seeds runtime from that authored `WorkflowInstance` and keeps returning it as `workflow_instance_file` until runtime chooses or updates the sidecar/pointer-backed graph.

Those prompt commands are AO-owned inspection/authoring surfaces. They are not additional AO execution modes, and they do not change the AO top-level run/resume wire schema.

### Trigger Matrix

Treat AO plan or replan as required when one of these is true:

- AO returns `status: blocked`
- AO progress or boundary payload exposes a non-empty `next_frontier`
- AO boundary payload exposes `boundary_reason`
- AO boundary payload exposes `weave_out_request`

Current boundary reasons and default planner outputs:

- `clarification_required`: `current_node_id=boundary.clarification`, `transition_id=transition.clarify`, `pending_requirements=[confirmed_scope]`
- `tool_probe_required`: `current_node_id=boundary.tool_probe`, `transition_id=transition.tool_probe`, `pending_requirements=[probe_report]`
- `delegation_required`: `current_node_id=boundary.delegation`, `transition_id=transition.delegation`, `pending_requirements=[delegation_result]`
- `weave_out_required`: `current_node_id=boundary.weave_out`, `transition_id=transition.weave_out`, `pending_requirements=[weave_back_result]`, and structured `weave_out_request`

When `context.force_boundary_reason` is provided, AO normalizes and applies that forced reason at planning time.
When `confirmed_scope` is resumed as true and no forced boundary reason is present, the current default planner exits the clarification seam and continues into the default `tool_probe_required` seam. `payload.plan_meta.selected_frontier_action` is currently preserved as structured caller decision metadata in context, but it does not by itself introduce a separate boundary reason.

### Plan On First Blocked Return

1. Read `<ao_property type="boundary">` and capture `status`, `boundary_reason`, `current_node_id`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `workflow_file`, and `event_log_file`.
1. Load `workflow_file` and read `last_transition_id` from the AO workflow snapshot. This is mandatory because `transition_id` is validated by runtime resume and is not emitted as a top-level field in the boundary payload.
1. If `workflow_instance_file` is present, treat it as the current graph source of truth for audit continuity and any caller-managed replan edit. It can be either the authored input file passed to `run --instance-file` or the runtime sidecar graph tracked under `session_dir`.
1. When AO-owned prompt text is useful, call `dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>]`. The generated prompt should require a WorkflowInstance file-generation result that includes at least one viable route to the end state and at least one `tbr` path that can still reach the end state.
1. Build one focused action plan that satisfies the current `pending_requirements` and picks one frontier branch from `next_frontier`.
1. Execute only the minimum external work needed for that branch.
1. Write a structured resume envelope JSON with:

- `transition_id`: exactly the snapshot `last_transition_id`
- `correlation_key`: optional stable key for this boundary cycle
- `payload`: structured external result fields plus optional caller convention metadata (for example `payload.plan_meta.unsolved_target_id` and `payload.plan_meta.next_step_prompt`)

1. Resume through `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path>`.

### Replan Loop On Subsequent Blocked Returns

1. After every resume, parse AO output again. If AO returns `status: blocked`, start a new replan cycle.
2. Re-read the latest `workflow_file` snapshot and refresh `last_transition_id`, `last_boundary_reason`, `pending_requirements`, and `next_frontier`. Also refresh `workflow_instance_file` if AO returned one.
3. Treat old frontier choices as stale unless they still match the latest blocked payload.
4. When AO-owned prompt text is useful, call `dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id>`, where `--instance-file` should normally be the latest `workflow_instance_file` returned by AO. The generated prompt should explicitly state that the most recent selected frontier action did not converge, that the selected `tbr` node now needs expansion into a viable replacement path between its upstream and downstream graph points, and that one or more `tbr` nodes must remain in the overall graph.
5. Recompute the external action slice and write a new `result-file` envelope for the new boundary. Carry forward only still-valid convention metadata under `payload.plan_meta`.
6. Resume again with the new envelope.

### Default Runtime Audit Graphs

When `run` executes without `--instance-file`, AO still emits valid runtime audit artifacts, but the graph mode is `minimal-sidecar-only`: it guarantees that the blocked seam, wait-resume transition, and boundary metadata remain auditable, but it is not equivalent to a full caller-authored execution graph.

When `run --instance-file <path>` is used explicitly, AO tries to preserve graph continuity across compile, prompt-plan, the first blocked runtime audit, and later replans. In that mode, `workflow_instance_file` should be treated as the primary graph source for audit continuity.

Do not reuse a prior `transition_id` after AO has moved to a newer blocked seam. Runtime rejects resumes whose `transition_id` does not match the currently blocked transition.

### Completion Gate

AO marks completed when merged context contains one of these boolean flags set to true:

- `mark_completed`
- `completed`
- `is_completed`

Operationally:

1. Set one of the completion flags in resume payload only when top-level work has actually converged.
2. Resume once with that payload.
3. Accept completion only when AO returns `status: completed` and `current_node_id: state.completed`.

### Return Explanation Template

When reporting AO plan or replan decisions, use this structure. The first block is AO runtime fields; the second block is caller convention metadata carried in `payload`:

- `status`: blocked | completed
- `boundary_reason`: current AO boundary reason when blocked
- `current_node_id`: current AO node
- `transition_id_source`: `workflow_file.last_transition_id`
- `pending_requirements`: list from current AO payload
- `external_actions_executed`: concrete actions performed outside AO
- `resume_envelope_written`: path and key payload fields
- `resume_result`: blocked | completed and key returned fields
- `payload.plan_meta.plan_phase`: initial-plan | replan
- `payload.plan_meta.unsolved_target_id`: unresolved target node id for this cycle
- `payload.plan_meta.selected_frontier_action`: one chosen action from `next_frontier`
- `payload.plan_meta.method`: recommended `u2d-expand-bridge`
- `payload.plan_meta.determined_path_ids`: deterministic path node ids produced this cycle
- `payload.plan_meta.unresolved_bridge_ids`: unresolved bridge node ids deferred to later cycles
- `payload.plan_meta.next_step_prompt`: imperative next-step instruction for the following cycle

## Behavior

AO should:

- inspect current context
- expand or refine the workflow frontier
- choose among clarification, probing, delegation, replanning, or completion
- persist decisions, artifacts, and blocked-payload metadata
- keep a mutable workflow file plus an append-only event or snapshot log
- generate AO-owned planner/replanner prompt text from code when callers ask for prompt-plan or prompt-replan support surfaces
- express weave-out requests for external comparison, planning, or similar analysis through explicit blocked-payload fields rather than hiding them in opaque prose
- reject resume envelopes whose `transition_id` does not match the currently blocked workflow seam as recorded by the pending payload fields
- treat session metadata as explicit CLI input when needed instead of depending on hidden host state

AO should not:

- impersonate a deterministic skill executor
- hide control state inside narrative-only text
- collapse every decision into one opaque prompt roundtrip
- hide or bypass the documented CLI control surface with private wrappers
- treat prompt-plan or prompt-replan as official AO run surfaces equal to run/resume

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
  audit_artifacts:
    step_directory: outputs/audit/wf-20260609010101_abc12345/step-0001-blocked-clarification_required
    summary_file: outputs/audit/wf-20260609010101_abc12345/step-0001-blocked-clarification_required/summary.json
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
  audit_artifacts:
    step_directory: outputs/audit/wf-20260609010101_abc12345/step-0002-completed
    summary_file: outputs/audit/wf-20260609010101_abc12345/step-0002-completed/summary.json
```

## Anti-Patterns

- Treating AO as a general-purpose chat wrapper.
- Returning prose that omits workflow, node, or artifact state.
- Using AO to execute deterministic step-by-step skill logic that belongs in SO.
- Replacing the documented CLI/package control path with a private wrapper without a clear reason.
- Letting AO imply a weave-out request informally instead of emitting an explicit structured boundary for it.
- Letting a governed skill ask users to choose package/channel when the runtime version is already bound by the CI/CD-managed skill package version block or checked-in runtime lock.
