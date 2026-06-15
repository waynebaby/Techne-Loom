# AO Blocked Replan Run

[中文](../../zh-cn/examples/ao-blocked-replan-run.md) | [Root](../README.md)

This example shows an end-to-end AO path where `run` starts from an authored `WorkflowInstance`, then blocks, the caller refreshes runtime facts outside AO, asks AO for a code-managed replanner prompt through `prompt-replan`, edits a WorkflowInstance seam, and then resumes with a structured result envelope.

## Scenario

The objective is an AO runtime investigation route. AO blocks at a tool-probe seam because the next move needs grounded runtime facts before the selected `tbr` seam can be expanded. The caller first refreshes or confirms `probe_report` and related `plan_meta` facts, then asks AO for a richer replanner prompt before editing the selected seam.

## Step 1: Start AO And Capture The Blocked Return

Prepare an authored `workflow-instance.json` first, then start AO from that same graph:

```powershell
dotnet ao.dll run --objective-file objective.md --context-file context.json --instance-file workflow-instance.json --session-dir outputs\sessions --audit-output outputs\audit
```

Expected shape:

```guide-example
name: ao-run-blocked-probe
ao-return:
  type: boundary
  status: blocked
  session_id: 20260613000000_abc12345
  workflow_file: outputs/sessions/session_20260613000000_abc12345_workflow.json
  workflow_instance_file: workflow-instance.json
  current_node_id: boundary.tool_probe
  boundary_reason: tool_probe_required
  pending_requirements:
    - probe_report
  next_frontier:
    - probe_repo_structure
    - probe_recent_logs
```

This blocked return means the next weave-back must preserve a stable `probe_report` key. If the existing runtime facts are stale, regenerate or refresh them before asking AO for a replanner prompt.

## Step 2: Refresh Runtime Facts Outside AO

When the blocked seam needs fresh evidence, gather it outside AO and keep the resulting artifact outside the skill folder.

```powershell
tool-probe.ps1 -OutputFile outputs\reports\probe-report.json
```

Runtime fact highlights:

```guide-example
name: runtime-fact-artifacts
probe_report:
  status: fresh
  repo_summary: runtime pointers confirmed
  unresolved_surface:
    - selected_tbr replacement path still missing
plan_meta:
  selected_frontier_action: probe_repo_structure
  next_step_prompt: Carry probe facts through the seam edit and the next resume.
```

Those external facts do not mutate AO's blocked snapshot by themselves. They become replan inputs for the current cycle only when the caller uses them during seam editing and then weaves them back through stable keys on the next `resume` payload.

```guide-example
name: runtime-fact-reentry
generated_outside_ao:
  - outputs/reports/probe-report.json
consumed_during_replan:
  - prompt.replan.runtime-context
  - workflow-instance.json seam edit
woven_back_into_ao:
  - payload.probe_report
  - payload.plan_meta.selected_frontier_action
```

## Step 3: Ask AO For A Replanner Prompt

Use the returned `workflow_instance_file` as the current graph-shaped runtime surface, then ask AO for a typed prompt payload.

```powershell
dotnet ao.dll prompt-replan --session-dir outputs\sessions --session-id 20260613000000_abc12345 --instance-file workflow-instance.json --tbr-id transition.main_tbr
```

Expected prompt payload highlights:

```guide-example
name: ao-prompt-replan-payload
ao-prompt:
  type: prompt
  command: prompt-replan
  prompt_kind: replan
  prompt_template_version: ao.workflow.prompt.v3
  blocks:
    - block_id: prompt.replan.runtime-context
      block_kind: guide-template
    - block_id: prompt.replan.blocked-boundary-context
      block_kind: guide-template
    - block_id: prompt.replan.selected-tbr-projection
      block_kind: guide-example
    - block_id: prompt.replan.current-workflow-projection
      block_kind: guide-example
    - block_id: prompt.replan.current-workflow-instance
      block_kind: guide-example
    - block_id: workflow.output-schema
      block_kind: guide-contract
```

Use the returned block set by stable `block_id` rather than by prompt line position. In this flow, the caller consumes:

- `prompt.replan.runtime-context`
- `prompt.replan.blocked-boundary-context`
- `prompt.replan.selected-tbr-projection`
- `prompt.replan.current-workflow-projection`
- `prompt.replan.current-workflow-instance`
- `workflow.output-schema`
- `workflow.root-field-contract`

The caller also reads `probe-report.json` alongside the AO prompt blocks. AO does not ingest arbitrary external files automatically; the caller must carry their stable field names back through the seam edit and the next `resume` payload.

## Step 4: Edit The WorkflowInstance Seam

The caller updates the selected `tbr` seam in `workflow-instance.json` so the replacement path still reconnects to the original predecessor and target while preserving at least one remaining `tbr` elsewhere in the graph.

Minimal before/after idea:

```guide-example
name: selected-tbr-edit-intent
before:
  selected_tbr_id: transition.main_tbr
  predecessor_state_ids:
    - state.review
  target_node_id: state.end
after:
  replacement_path:
    - transition.route_from_probe_report
    - transition.inspect_runtime_pointer
    - transition.capture_followup_gap
  seam_designNotes:
    - Ground the route in probe_report.repo_summary = runtime pointers confirmed.
    - Preserve payload.probe_report and payload.plan_meta.selected_frontier_action on the next resume.
  preserved_remaining_tbr:
    - transition.remaining_tbr
```

## Step 5: Resume AO With Structured Result Data

The caller still resumes AO through the public control payload, not by sending the modified WorkflowInstance back as a new top-level AO schema.

```json
{
  "transition_id": "transition.tool_probe",
  "correlation_key": "runtime-probe",
  "payload": {
    "probe_report": {
      "status": "fresh",
      "repo_summary": "runtime pointers confirmed",
      "unresolved_surface": [
        "selected_tbr replacement path still missing"
      ]
    },
    "plan_meta": {
      "selected_frontier_action": "probe_repo_structure",
      "next_step_prompt": "Carry probe report keys through the updated seam and next resume."
    }
  }
}
```

```powershell
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260613000000_abc12345 --result-file result-probe.json --audit-output outputs\audit
```

## What This Example Establishes

- Caller-managed runtime fact artifacts become replan inputs only when the caller uses them during seam editing and then weaves them back through stable resume payload keys.
- `prompt-replan` emits `prompt.replan.runtime-context` for runtime facts.
- The caller preserves stable keys such as `probe_report` and `payload.plan_meta.*` instead of collapsing those decisions into prose-only notes.
- `prompt-replan` is an AO-owned support surface that generates typed prompt blocks from code.
- The caller consumes prompt blocks by `block_id`, not by fuzzy prose matching.
- The actual official AO run surfaces remain `dotnet ao.dll run` and `dotnet ao.dll resume`.
- `workflow_file` remains the snapshot control file, while `workflow_instance_file` carries the current graph continuity used by audits and replan edits.
- WorkflowInstance editing stays caller-managed and lives outside the AO session folder unless the user explicitly chooses another output root.
