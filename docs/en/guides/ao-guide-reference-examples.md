# Loom Agent Execution Orchestrator Guide: Examples

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [中文](../../zh-cn/guides/ao-guide-reference-examples.md) | [Root](../README.md)

Version: 0.3.253-beta
Build: published package 0.3.253-beta

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
