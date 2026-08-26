# CLI Reference

[中文](../../zh-cn/reference/cli.md) | [Root](../README.md)

## AgentOrchestrator (`dotnet ao.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage, command surface, and validation-output note |
| `--guide` | none | none | Install the version-matched English docs bundle and emit JSON paths |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range in an existing text file from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write `workflow.schema.json` and `workflow.demo.json` together from the current runtime contract and demo |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing AO workflow JSON and emit Mermaid/HTML validation artifacts |
| `prompt-plan` | `--objective-file` | `--context-file` | Emit AO-owned planner prompt text for WorkflowInstance file generation |
| `prompt-replan` | `--session-dir`, `--session-id`, `--instance-file`, `--tbr-id` | none | Emit AO-owned replanner prompt text for WorkflowInstance TBR node replacement |
| `run` | `--objective-file`, `--session-dir` | `--context-file`, `--instance-file`, `--audit-output` | Run AO until blocked or completed |
| `resume` | `--session-dir`, `--session-id`, `--result-file` | `--audit-output` | Resume AO from a structured result envelope |

### Guide contract

`dotnet ao.dll --guide` accepts no additional arguments. It installs the embedded English `docs/en` bundle into `<binary>/docs/<package-version>/`; when that location is not writable it uses `%TEMP%/docs/<package-version>/` and returns the actual location.

Standard output contains one JSON object only:

```json
{
  "version": "<package-version>",
  "docs_root": "C:\\runtime\\docs\\<package-version>",
  "guide_path": "C:\\runtime\\docs\\<package-version>\\reference\\products\\ao-guide.md"
}
```

Use `guide_path` as the authoritative version-matched guide. Use `docs_root` only when the guide leaves a question unresolved. Non-fatal installation warnings are written to standard error. The command is English-only and rejects `--lang`, `--section`, and `--export`.

### AO examples

```bash
dotnet ao.dll --guide
dotnet ao.dll --patch --patch-content-file patch.txt --patch-target target.cs --from-line 120 --to-line 148
dotnet ao.dll compile --workflow-file ao-plan.json --audit-output outputs\audit
dotnet ao.dll --schema-demo-output outputs\schema-demo
dotnet ao.dll prompt-plan --objective-file objective.md --context-file context.json
dotnet ao.dll prompt-replan --session-dir outputs\sessions --session-id 20260609010101_abc12345 --instance-file workflow-instance.json --tbr-id transition.main_tbr
dotnet ao.dll run --objective-file objective.md --context-file context.json --instance-file workflow-instance.json --session-dir outputs\sessions --audit-output outputs\audit
dotnet ao.dll resume --session-dir outputs\sessions --session-id 20260609010101_abc12345 --result-file resume.json --audit-output outputs\audit
```

### AO output contract highlights

- `--guide` returns `version`, `docs_root`, and `guide_path` JSON fields; it does not emit guide Markdown on standard output
- control payloads are emitted inside `<ao_property>`
- current payload fields: `status`, `session_id`, `workflow_file`, `workflow_instance_file`, `event_log_file`, `current_node_id`, `boundary_reason`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `weave_out_request`, `audit_artifacts`
- prompt commands emit `<ao_property type="prompt">` with AO-owned code-generated prompt text plus prompt metadata such as `command`, `prompt_kind`, `prompt_template_version`, `blocks`, `allowed_node_kinds`, `allowed_command_kinds`, and prompt-specific workflow/TBR anchors
- compile validation artifacts and run/resume audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- `audit_artifacts` now also returns `summary_file`; that file summarizes the step status, boundary, frontier, workflow paths, and artifact links as a direct replay entry point
- when `--audit-output` is omitted, AO uses a temporary output root
- AO workflow JSON is authored outside the AO CLI, typically by the calling agent, and then validated with `dotnet ao.dll compile --workflow-file <path>`
- `run --instance-file <path>` lets the caller seed runtime from an authored `WorkflowInstance` so the first blocked runtime audit continues the same graph that compile and prompt-plan used
- when `--instance-file` is omitted, AO still emits runtime audit artifacts, but the default graph mode is `minimal-sidecar-only`: the graph explicitly shows the blocked seam and boundary metadata, and should not be mistaken for a full caller-authored execution graph
- AO runtime persistence currently uses two shapes on purpose: `workflow_file` remains the snapshot control file for blocked-seam validation, while `workflow_instance_file` points at the caller-managed authored graph or the runtime sidecar graph used for audit continuity and replan edits
- under `session_dir`, AO also owns `session_<id>_runtime.workflow.json` as its runtime `WorkflowInstance` sidecar and `session_<id>_runtime.workflow.pointer.json` as the optional pointer to the caller-managed external `workflow_instance_file`
- `session_<id>_events.jsonl` now carries step-level audit linkage such as `step_sequence`, `step_directory`, `summary_file`, plus boundary replay fields like `pending_requirements` and `next_frontier`
- compile fails rather than overwriting existing artifact files in the target step directory and reports the conflicting paths in its error payload
- AO is CLI-only in this project; there is no public MCP surface
- use `--patch` as the direct line-range patch path when GitHub Copilot conditions make this command interface the preferred editing route; on other platforms or tools, treat it as a command-line fallback when normal patch application fails

## SkillOrchestrator (`dotnet so.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage, command surface, and validation-output note |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range in an existing text file from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write `workflow.schema.json` and `workflow.demo.json` together from the current runtime contract and demo |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range in an existing text file from an external patch-content file |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing SO workflow JSON and emit Mermaid/HTML validation artifacts |
| `run` | `--workflow-file` | `--context-file`, `--audit-output` | Run SO until blocked or completed |
| `resume` | `--workflow-file`, `--result-file` | `--audit-output` | Resume SO from a structured result envelope |
| `copy-audit-step` | `--source-step`, `--workflow-id`, `--sequence`, `--action`, `--audit-output`, `--reason`, `--verified-by` | none | Copy verified audit artifacts with reuse provenance; never advances workflow state |
| `status` | `--workflow-file` | none | Emit current status payload |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-events` | `--workflow-file` | none | Print the `.events.jsonl` sidecar |
| `ls` | path argument optional | none | Run the built-in sample deterministic workflow |

### SO guide contract

`dotnet so.dll --guide` follows the same JSON contract and directory policy as AO. Its `guide_path` points to `reference/products/so-guide.md`. It accepts no additional arguments and rejects `--lang`, `--section`, and `--export`.

### SO examples

```bash
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll --schema-demo-output outputs\schema-demo
dotnet so.dll --patch --patch-content-file patch.txt --patch-target workflow.current.json --from-line 25 --to-line 40
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
```

### SO output contract highlights

- `--guide` returns `version`, `docs_root`, and `guide_path` JSON fields; it does not emit guide Markdown on standard output
- wrapped command output streams inside `<wrapped_exec>`
- control payloads are emitted inside `<so_property>`
- current payload fields include `workflow_file`, `instance_id`, `status`, `current_node_id`, `current_step_kind`, `skill_hint`, `memory_for_next_step`, `required_inputs`, `event_log_file`, `audit_artifacts`
- compile validation artifacts and run/resume audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- when `--audit-output` is omitted, SO uses a temporary output root
- SO compile also fails rather than overwriting existing artifact files in the target step directory and reports the conflicting paths in its error payload
- for Loom-governanced target-skill templates, SO compile and workflow load reject missing root `validation` contracts, invalid `AskUser` ownership requests, governance-only done paths, and blocked routes that do not publish strongest-earned business outputs
- use `--patch` as the direct line-range patch path when GitHub Copilot conditions make this command interface the preferred editing route; on other platforms or tools, treat it as a command-line fallback when normal patch application fails

Review target for the public SO parameter contract:

- `planner` remains AO terminology and should not be treated as part of the SO public command contract
- SO public CLI review target is: author or obtain workflow JSON elsewhere, then use `compile` to validate it and emit Mermaid/HTML outputs; for Loom-governanced target-skill templates, `compile` also validates the root governed-template contract, route-aware business-output gates, seam ownership, and done reachability
