# CLI Reference

[中文](../../zh-cn/reference/cli.md)

## AgentOrchestrator (`dotnet ao.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--guide` | none | `--lang`, `--section`, `--export` | Emit the authored guide surface |
| `planner` | `--plan-file`, `--workflow-file` | `--context-file` | Materialize a draft AO workflow JSON plan |
| `host` | none | none | Start the official MCP/stdio server |
| `run` | `--objective-file`, `--session-dir` | `--context-file`, `--audit-output` | Run AO until blocked or completed |
| `resume` | `--session-dir`, `--session-id`, `--result-file` | `--audit-output` | Resume AO from a structured result envelope |

### AO examples

```bash
dotnet ao.dll --guide --lang en --export ao-guide.md
dotnet ao.dll planner --plan-file detailed-plan.md --workflow-file ao-plan.json --context-file context.json
dotnet ao.dll run --objective-file objective.md --context-file context.json --session-dir outputs\sessions --audit-output outputs\audit
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260609010101_abc12345 --result-file resume.json --audit-output outputs\audit
```

### AO output contract highlights

- control payloads are emitted inside `<ao_property>`
- current payload fields: `status`, `session_id`, `workflow_file`, `event_log_file`, `current_node_id`, `boundary_reason`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `weave_out_request`, `audit_artifacts`
- audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- when `--audit-output` is omitted, AO uses a temporary output root

## SkillOrchestrator (`dotnet so.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--guide` | none | `--lang`, `--section`, `--export` | Emit the authored guide surface |
| `planner` | `--description-file`, `--workflow-file` | `--context-file` | Materialize a draft SO workflow JSON |
| `run` | `--workflow-file` | `--context-file`, `--audit-output` | Run SO until blocked or completed |
| `resume` | `--workflow-file`, `--result-file` | `--audit-output` | Resume SO from a structured result envelope |
| `status` | `--workflow-file` | none | Emit current status payload |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-events` | `--workflow-file` | none | Print the `.events.jsonl` sidecar |
| `ls` | path argument optional | none | Run the built-in sample deterministic workflow |

### SO examples

```bash
dotnet so.dll --guide --lang en --export so-guide.md
dotnet so.dll planner --description-file skill-plan.md --workflow-file so-template.json --context-file context.json
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
```

### SO output contract highlights

- wrapped command output streams inside `<wrapped_exec>`
- control payloads are emitted inside `<so_property>`
- current payload fields include `workflow_file`, `instance_id`, `status`, `current_node_id`, `current_step_kind`, `skill_hint`, `memory_for_next_step`, `required_inputs`, `event_log_file`, `audit_artifacts`
- audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- when `--audit-output` is omitted, SO uses a temporary output root
