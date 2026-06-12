# CLI Reference

[中文](../../zh-cn/reference/cli.md) | [Root](../README.md)

## AgentOrchestrator (`dotnet ao.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage, command surface, and validation-output note |
| `--guide` | none | `--lang`, `--section`, `--export` | Emit the authored guide surface |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing AO workflow JSON and emit Mermaid/HTML validation artifacts |
| `run` | `--objective-file`, `--session-dir` | `--context-file`, `--audit-output` | Run AO until blocked or completed |
| `resume` | `--session-dir`, `--session-id`, `--result-file` | `--audit-output` | Resume AO from a structured result envelope |

### AO examples

```bash
dotnet ao.dll --guide --lang en --export ao-guide.md
dotnet ao.dll compile --workflow-file ao-plan.json --audit-output outputs\audit
dotnet ao.dll run --objective-file objective.md --context-file context.json --session-dir outputs\sessions --audit-output outputs\audit
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260609010101_abc12345 --result-file resume.json --audit-output outputs\audit
```

### AO output contract highlights

- control payloads are emitted inside `<ao_property>`
- current payload fields: `status`, `session_id`, `workflow_file`, `event_log_file`, `current_node_id`, `boundary_reason`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `weave_out_request`, `audit_artifacts`
- compile validation artifacts and run/resume audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- when `--audit-output` is omitted, AO uses a temporary output root
- AO workflow JSON is authored outside the AO CLI, typically by the calling agent, and then validated with `dotnet ao.dll compile --workflow-file <path>`
- compile fails rather than overwriting existing artifact files in the target step directory and reports the conflicting paths in its error payload
- AO is CLI-only in this project; there is no public MCP surface

## SkillOrchestrator (`dotnet so.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage, command surface, and validation-output note |
| `--guide` | none | `--lang`, `--section`, `--export` | Emit the authored guide surface |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing SO workflow JSON and emit Mermaid/HTML validation artifacts |
| `run` | `--workflow-file` | `--context-file`, `--audit-output` | Run SO until blocked or completed |
| `resume` | `--workflow-file`, `--result-file` | `--audit-output` | Resume SO from a structured result envelope |
| `status` | `--workflow-file` | none | Emit current status payload |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-events` | `--workflow-file` | none | Print the `.events.jsonl` sidecar |
| `ls` | path argument optional | none | Run the built-in sample deterministic workflow |

Review target for the public SO parameter contract:

- `planner` remains AO terminology and should not be treated as part of the SO public command contract
- SO public CLI review target is: author or obtain workflow JSON elsewhere, then use `compile` to validate it and emit Mermaid/HTML outputs

### SO examples

```bash
dotnet so.dll --guide --lang en --export so-guide.md
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
```

### SO output contract highlights

- wrapped command output streams inside `<wrapped_exec>`
- control payloads are emitted inside `<so_property>`
- current payload fields include `workflow_file`, `instance_id`, `status`, `current_node_id`, `current_step_kind`, `skill_hint`, `memory_for_next_step`, `required_inputs`, `event_log_file`, `audit_artifacts`
- compile validation artifacts and run/resume audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- when `--audit-output` is omitted, SO uses a temporary output root
- SO compile also fails rather than overwriting existing artifact files in the target step directory and reports the conflicting paths in its error payload
