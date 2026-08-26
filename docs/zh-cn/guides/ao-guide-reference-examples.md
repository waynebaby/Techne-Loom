# Loom Agent Execution Orchestrator Guide：Examples

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [English](../../en/guides/ao-guide-reference-examples.md) | [根目录](../README.md)

版本：draft
构建：repository source

## Examples

```guide-example
name: clarify-missing-dimensions
input: 用户请求电池布局，但包络尺寸不完整
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
input: 顶层 agent 需要定位一个失败 CLI 路径的控制代码
ao-return:
  status: blocked
  boundary_reason: tool_probe_required
  next_frontier:
    - search_cli_entrypoints
    - inspect_recent_validation_logs
```

```guide-example
name: delegate-subtask
input: 编排过程需要将代码审查委派给更窄的 agent
ao-return:
  status: blocked
  boundary_reason: delegation_required
  current_node_id: review.slice.2
```

```guide-example
name: weave-out-for-frontier-comparison
input: AO 需要外部比较两个竞争的 execution frontier
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
input: 顶层任务已经收敛，调用方带着完成数据恢复 AO
ao-return:
  status: completed
  session_id: 20260609010101_abc12345
  workflow_file: outputs/sessions/session_20260609010101_abc12345_workflow.json
  current_node_id: state.completed
  audit_artifacts:
    step_directory: outputs/audit/wf-20260609010101_abc12345/step-0002-completed
    summary_file: outputs/audit/wf-20260609010101_abc12345/step-0002-completed/summary.json
```
