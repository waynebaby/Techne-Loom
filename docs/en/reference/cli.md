# CLI Reference

[中文](../../zh-cn/reference/cli.md) | [Root](../README.md)

## AgentOrchestrator (`dotnet ao.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage, command surface, and validation-output note |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `--guide` | none | none | Install the version-matched English docs bundle and emit JSON paths |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range in an existing text file from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write the complete demo set: `workflow.schema.json`, `workflow.demo.json`, `workflow.model.cs`, `workflow.demo.cs`, and `workflow.demo.verify.cs` from the current runtime contract and demo |
| `--workflow-script` | `--mode`, `--script-file`, `--input-file`, `--output-file` | `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--verification-output-file`, `--audit-output`, `--workspace-root` | Execute a disk-backed `.cs` Build or Edit script, run built-in verification checks plus optional Verify script, and write candidate/audit files; no project file is required |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an existing AO workflow JSON and emit `workflow.compile-feedback.json` plus Mermaid/HTML artifacts on success; parse or validation failures emit feedback and a workflow backup without placeholder renders |
| `prompt-plan` | `--objective-file` | `--context-file` | Emit AO-owned planner prompt text for WorkflowInstance file generation |
| `prompt-replan` | `--workflow-file`, `--tbr-id` | `--objective-file` | Emit AO-owned replanner prompt text from a canonical workflow file; the legacy session form remains supported |
| `run` | `--workflow-file` | `--context-file` | Run the canonical sessionless AO workflow until blocked or completed; legacy session inputs remain supported |
| `resume` | `--workflow-file`, `--result-file` | none | Resume the canonical sessionless AO workflow from a structured result envelope; legacy session inputs remain supported |
| `inspect-workflow-fragment` | `--workflow-file` | `--json-pointer`, `--max-bytes`, `--max-array-items`, `--max-object-properties`, `--max-depth` | Return a bounded workflow summary or JSON Pointer fragment without printing the full workflow by default |

### File input contract



Every `*-file` option and every existing file path argument is an input path, not inline content. The caller must create, finish, and close all required input files before starting one CLI command: scripts, JSON inputs, base workflows, reference workflows, verifier scripts, patch content, patch targets, workflow files, objectives, contexts, instances, and resume results.



The CLI checks the complete input set before it reads an input, runs a script, changes a target, or writes a command result. It never assembles missing files or applies incremental repairs between calls. Inline content options such as `--script-content`, `--input-json`, `--patch-content`, and `--replacement-text` are rejected. Output files and output directories are destinations owned by the CLI; the caller supplies their paths but does not use them as input content.

### Guide contract

`dotnet ao.dll --guide` accepts no additional arguments. It reads the English `docs/en` tree shipped beside the executable in a complete runtime package and returns the actual `version`, `docs_root`, and `guide_path` locations. The executable does not contain guide pages; a missing package docs tree is an error.

Standard output contains one JSON object only:

```json
{
  "version": "<package-version>",
  "docs_root": "C:\\runtime\\docs\\<package-version>",
  "guide_path": "C:\\runtime\\docs\\<package-version>\\guides\\ao-guide.md"
}
```

Use `guide_path` as the authoritative version-matched guide. Use `docs_root` only when the guide leaves a question unresolved. Non-fatal installation warnings are written to standard error. The command is English-only and rejects `--lang`, `--section`, and `--export`.

### AO examples

```bash
dotnet ao.dll --guide
dotnet ao.dll mcp stdio
dotnet ao.dll --patch --patch-content-file patch.txt --patch-target target.cs --from-line 120 --to-line 148
dotnet ao.dll compile --workflow-file ao-plan.json --audit-output outputs\audit
dotnet ao.dll --schema-demo-output outputs\schema-demo
dotnet ao.dll --workflow-script --mode build --script-file outputs\schema-demo\workflow.demo.cs --input-file inputs\ao.json --output-file outputs\candidate.json --verify-script outputs\schema-demo\workflow.demo.verify.cs --reference-workflow-file outputs\schema-demo\workflow.demo.json --verification-output-file outputs\verification.json
dotnet ao.dll prompt-plan --objective-file objective.md --context-file context.json
dotnet ao.dll prompt-replan --workflow-file workflow-instance.json --objective-file objective.md --tbr-id transition.main_tbr
dotnet ao.dll run --workflow-file workflow-instance.json --context-file context.json
dotnet ao.dll resume --workflow-file workflow-instance.json --result-file resume.json
dotnet ao.dll inspect-workflow-fragment --workflow-file workflow-instance.json --json-pointer /context/plan_meta --max-bytes 16384
```

### AO output contract highlights

- `--guide` returns `version`, `docs_root`, and `guide_path` JSON fields; it does not emit guide Markdown on standard output
- `inspect-workflow-fragment` returns summary metadata when `--json-pointer` is omitted; explicit JSON Pointers return bounded JSON, and an over-limit response keeps `fragment: null` with `truncated` and `truncationReason`
- control payloads are emitted inside `<ao_property>`
- current payload fields: `status`, `session_id`, `workflow_file`, `workflow_instance_file`, `event_log_file`, `current_node_id`, `boundary_reason`, `result_file`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `weave_out_request`, `audit_artifacts`
- prompt commands emit `<ao_property type="prompt">` with AO-owned code-generated prompt text plus prompt metadata such as `command`, `prompt_kind`, `prompt_template_version`, `blocks`, `allowed_node_kinds`, `allowed_command_kinds`, and prompt-specific workflow/TBR anchors
- compile validation artifacts and run/resume audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- `audit_artifacts` now also returns `summary_file`; that file summarizes the step status, boundary, frontier, workflow paths, and artifact links as a direct replay entry point
- `--workspace-root <directory>` is optional but must name an existing directory outside the skill folder. When supplied, AO mirrors Mermaid and HTML into a new ignored workspace `temp/exec-<timestamp>-mermaid-delivery-result/` directory and verifies both copies with SHA-256.
- `audit_artifacts.mermaid_delivery` separates `artifact_generated`, `link_resolvable`, `visual_preview_rendered`, and `card_display_available`. Its `status` is `workspace_mirror`, `runtime_path_only`, or `delivery_failed`; only its verified workspace-relative paths are link targets.
- `must_show_to_user_files` is an audit continuity list, not a link guarantee. A host may pass `card_input_file` to a Mermaid card tool; otherwise it must put the verified Mermaid link first, the HTML link second, and never guess a path after delivery failure.
- `workflow.compile-feedback.json` uses the shared `workflow.compile-feedback.v1` contract. Valid workflow, template, schema, demo, runtime-copy, audit-backup, analysis, and dataflow JSON files are written as indented multi-line JSON; compact JSON remains for JSONL and protocol payloads.
- `--audit-output` and other output targets may be outside the Git worktree or ignored. Payloads return normalized real paths, and the runtime verifies that reported files exist and are readable. With `--workspace-root`, use the verified workspace-relative mirror for direct editor opening; Git tracking is not required.
- when `--audit-output` is omitted, AO uses a temporary output root
- AO workflow JSON is authored outside the AO CLI, typically by the calling agent, and then validated with `dotnet ao.dll compile --workflow-file <path>`
- `run --instance-file <path>` lets the caller seed runtime from an authored `WorkflowInstance` so the first blocked runtime audit continues the same graph that compile and prompt-plan used
- when `--instance-file` is omitted, AO still emits runtime audit artifacts, but the default graph mode is `minimal-sidecar-only`: the graph explicitly shows the blocked seam and boundary metadata, and should not be mistaken for a full caller-authored execution graph
- AO runtime persistence currently uses two shapes on purpose: `workflow_file` remains the snapshot control file for blocked-seam validation, while `workflow_instance_file` points at the caller-managed authored graph or the runtime sidecar graph used for audit continuity and replan edits
- under `session_dir`, AO also owns `session_<id>_runtime.workflow.json` as its runtime `WorkflowInstance` sidecar and `session_<id>_runtime.workflow.pointer.json` as the optional pointer to the caller-managed external `workflow_instance_file`
- `session_<id>_events.jsonl` now carries step-level audit linkage such as `step_sequence`, `step_directory`, `summary_file`, plus boundary replay fields like `pending_requirements` and `next_frontier`
- compile fails rather than overwriting existing artifact files in the target step directory and reports the conflicting paths in its error payload
- AO exposes both the CLI and a local stdio MCP surface; it does not expose Web or remote MCP transport
- use `--patch` as the direct line-range patch path when GitHub Copilot conditions make this command interface the preferred editing route; on other platforms or tools, treat it as a command-line fallback when normal patch application fails

## SkillOrchestrator (`dotnet so.dll`)

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage, command surface, and validation-output note |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range in an existing text file from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write the complete demo set: `workflow.schema.json`, `workflow.demo.json`, `workflow.model.cs`, `workflow.demo.cs`, and `workflow.demo.verify.cs` from the current runtime contract and demo |
| `--workflow-script` | `--mode`, `--script-file`, `--input-file`, `--output-file` | `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--verification-output-file`, `--audit-output`, `--workspace-root` | Execute a disk-backed `.cs` Build or Edit script, run built-in verification checks plus optional Verify script, and write candidate/audit files; no project file is required |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range in an existing text file from an external patch-content file |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an existing SO workflow JSON and emit `workflow.compile-feedback.json` plus Mermaid/HTML artifacts on success; parse or validation failures emit feedback and a workflow backup without placeholder renders |
| `run` | `--workflow-file` | `--context-file`, `--audit-output`, `--workspace-root` | Run SO until blocked or completed |
| `resume` | `--workflow-file`, `--result-file` | `--audit-output`, `--workspace-root` | Resume SO from a structured result envelope |
| `copy-audit-step` | `--source-step`, `--workflow-id`, `--sequence`, `--action`, `--audit-output`, `--reason`, `--verified-by` | none | Copy verified audit artifacts with reuse provenance; never advances workflow state |
| `status` | `--workflow-file` | none | Emit current status payload |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-workflow-fragment` | `--workflow-file` | `--json-pointer`, `--max-bytes`, `--max-array-items`, `--max-object-properties`, `--max-depth` | Return a bounded summary or JSON Pointer fragment; omit `--json-pointer` to avoid workflow values |
| `inspect-events` | `--workflow-file` | none | Print the `.events.jsonl` sidecar |
| `ls` | path argument optional | none | Run the built-in sample deterministic workflow |

### SO guide contract

`dotnet so.dll --guide` follows the same JSON contract and directory policy as AO. Its `guide_path` points to `guides/so-guide.md`. It accepts no additional arguments and rejects `--lang`, `--section`, and `--export`.

### SO examples

```bash
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll --schema-demo-output outputs\schema-demo
dotnet so.dll --patch --patch-content-file patch.txt --patch-target workflow.current.json --from-line 25 --to-line 40
dotnet so.dll compile --workflow-file so-template.json --audit-output outputs\audit
dotnet so.dll run --workflow-file workflow.json --context-file context.json --audit-output outputs\audit
dotnet so.dll resume --workflow-file workflow.json --result-file resume.json --audit-output outputs\audit
dotnet so.dll status --workflow-file workflow.json
dotnet so.dll inspect-workflow-fragment --workflow-file workflow.json --json-pointer /context/plan_meta --max-bytes 16384
```

### SO output contract highlights

- `--guide` returns `version`, `docs_root`, and `guide_path` JSON fields; it does not emit guide Markdown on standard output
- wrapped command output streams inside `<wrapped_exec>`
- `--workspace-root <directory>` is optional but must name an existing directory outside the skill folder. When supplied, SO mirrors Mermaid and HTML into a new ignored workspace `temp/exec-<timestamp>-mermaid-delivery-result/` directory and verifies both copies with SHA-256.
- `audit_artifacts.mermaid_delivery` separates `artifact_generated`, `link_resolvable`, `visual_preview_rendered`, and `card_display_available`. Its `status` is `workspace_mirror`, `runtime_path_only`, or `delivery_failed`; only its verified workspace-relative paths are link targets.
- `must_show_to_user_files` is an audit continuity list, not a link guarantee. A host may pass `card_input_file` to a Mermaid card tool; otherwise it must put the verified Mermaid link first, the HTML link second, and never guess a path after delivery failure.
- `workflow.compile-feedback.json` uses the shared `workflow.compile-feedback.v1` contract. Valid workflow, template, schema, demo, runtime-copy, audit-backup, analysis, and dataflow JSON files are written as indented multi-line JSON; compact JSON remains for JSONL and protocol payloads.
- `--audit-output` and other output targets may be outside the Git worktree or ignored. Payloads return normalized real paths, and the runtime verifies that reported files exist and are readable. With `--workspace-root`, use the verified workspace-relative mirror for direct editor opening; Git tracking is not required.
- when `--audit-output` is omitted, SO uses a temporary output root
- current payload fields include `workflow_file`, `instance_id`, `status`, `current_node_id`, `current_step_kind`, `skill_hint`, `memory_for_next_step`, `required_inputs`, `event_log_file`, `audit_artifacts`
- compile validation artifacts and run/resume audit artifacts live under `{output}/wf-{wfid}/step-{seq}-{action}/`
- when `--audit-output` is omitted, SO uses a temporary output root
- SO compile also fails rather than overwriting existing artifact files in the target step directory and reports the conflicting paths in its error payload
- for Loom-governanced target-skill templates, SO compile and workflow load reject missing root `validation` contracts, invalid `AskUser` ownership requests, governance-only done paths, and blocked routes that do not publish strongest-earned business outputs
- use `--patch` as the direct line-range patch path when GitHub Copilot conditions make this command interface the preferred editing route; on other platforms or tools, treat it as a command-line fallback when normal patch application fails

Review target for the public SO parameter contract:

- `planner` remains AO terminology and should not be treated as part of the SO public command contract
- SO public CLI review target is: author or obtain workflow JSON elsewhere, then use `compile` to validate it and emit Mermaid/HTML outputs; for Loom-governanced target-skill templates, `compile` also validates the root governed-template contract, route-aware business-output gates, seam ownership, and done reachability
