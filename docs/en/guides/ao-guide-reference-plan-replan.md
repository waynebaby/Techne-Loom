# Loom Agent Execution Orchestrator Guide: Plan And Replan

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [Root](../README.md)

Version: 0.3.288
Build: published package 0.3.288

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

For every weave-out, `weave_out_request` must carry a minimal `evidence_references` manifest for the documents that caused or support the next action. Do not add a new AO top-level field for this manifest.

Each citation must contain:

- `path`: workspace-relative or runtime-output-relative path, never an absolute machine path
- `start_line` and `end_line`: verified 1-based inclusive line numbers from the exact file content used for the weave-out
- `role`: why the excerpt is required for the next action

When a guide controls the decision, cite the actual successful `guide_path` returned by the latest `dotnet ao.dll --guide` JSON result and its output lines. Citing only the guide source is insufficient. The command does not export a guide file; a weave-out without verified `evidence_references` is incomplete and must not be woven back as successful evidence. Keep the response compact: return the next action, the minimal citation manifest, and the resume payload contract; do not repeat the full context-pack inventory.

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

### Blocked-Route History Handoff

When the current route is confirmed unable to progress, do not send only the latest boundary payload to the planner. Persist and pass a structured `replan_history` containing:

- the current `workflow_file`, `workflow_instance_file`, blocked `current_node_id`, and `last_transition_id`
- the blocker reason and exact unmet requirement
- ordered attempted actions, their outcomes, and their verified `evidence_references`
- event-log and audit-artifact references from the failed route
- the terminal business objective and prior route decisions
- the selected replan anchor and strategy

The planner must select exactly one strategy:

- `continue_from_current`: preserve the current state and design a new viable bridge
- `rollback_to_unconfirmed`: move to the latest unconfirmed or not-yet-designed node and design forward
- `redesign_from_current`: preserve completed history while replacing the failing continuation
- `full_redesign`: replace the route design while retaining blocker history and the terminal objective
- `reversible_workaround`: apply the smallest reversible workaround and provide a one-step rollback plan

Every strategy must return a candidate path from its selected anchor to the terminal business outcome. A workaround without rollback evidence is invalid. The planner must not silently discard failed attempts, blocker history, prior route decisions, or their artifact references.

### Default Runtime Audit Graphs

When `run` executes without `--instance-file`, AO still emits valid runtime audit artifacts, but the graph mode is `minimal-sidecar-only`: it guarantees that the blocked seam, wait-resume transition, and boundary metadata remain auditable, but it is not equivalent to a full caller-authored execution graph.

When `run --instance-file <path>` is used explicitly, AO tries to preserve graph continuity across compile, prompt-plan, the first blocked runtime audit, and later replans. In that mode, `workflow_instance_file` should be treated as the primary graph source for audit continuity.

Do not reuse a prior `transition_id` after AO has moved to a newer blocked seam. Runtime rejects resumes whose `transition_id` does not match the currently blocked transition.

### Completion Gate

AO accepts a completion request only when one of these boolean flags is true and `terminal_evidence` is non-empty:

- `mark_completed`
- `completed`
- `is_completed`
- `terminal_evidence` (required evidence object or reference)

Operationally:

1. Set one of the completion flags and provide non-empty `terminal_evidence` only when top-level work has actually converged.
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
