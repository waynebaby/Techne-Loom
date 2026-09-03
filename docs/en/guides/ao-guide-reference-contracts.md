# Loom Agent Execution Orchestrator Guide: Contracts

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [Root](../README.md)

Version: 0.3.283-beta
Build: published package 0.3.283-beta

## Guide Output

Run the bare `dotnet ao.dll --guide` command. It reads the English `docs/en` tree shipped beside the executable in a complete runtime package and emits one JSON object with the actual `version`, `docs_root`, and `guide_path` absolute paths. The executable does not contain guide pages; a missing package docs tree is an error.

Use `guide_path` as the authoritative entry for this package version. Inspect `docs_root` only when this guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`; non-fatal installation warnings are written to stderr.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

Treat `dotnet ao.dll --guide` as a governance anchor, not as a detour. Once a fresh guide result has been emitted from a runnable AO runtime, all governed execution must stay on the corresponding published AO package runtime surface described by that guide. Do not read the guide and then drift back to repository builds, hand-assembled runtimes, or non-governed execution paths for official AO skill execution.

Loom Agent Execution Orchestrator is the top-agent-facing orchestration product for exploratory work under uncertainty.

It does not try to hide uncertainty. It captures evolving workflow state, emits machine-first control data, and weaves out at major control seams, surfacing blocked payloads with explicit boundary fields when a caller must choose the next action deliberately.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../architecture/workflow-terminology.md). In that vocabulary, Loom Agent Execution Orchestrator weaves out at control seams, surfacing them through blocked control payload fields such as `boundary_reason` and `weave_out_request`, and callers weave back through `dotnet ao.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- the `.NET` runtime is implemented with `dotnet ao.dll --guide`, `dotnet ao.dll --help`, `dotnet ao.dll --patch`, `dotnet ao.dll compile`, `dotnet ao.dll prompt-plan`, `dotnet ao.dll prompt-replan`, `dotnet ao.dll run`, and `dotnet ao.dll resume`
- Loom Agent Execution Orchestrator exposes both the CLI and a local stdio-only MCP surface in this project through `dotnet ao.dll mcp stdio`; it does not provide Web or remote MCP transport
- current AO control payloads emit `blocked` and `completed`; CLI/runtime failures surface as `<ao_property>` blocks with `type: error`
- AO compile emits Mermaid Markdown, HTML, and workflow JSON backup validation artifacts for an agent-authored workflow file
- AO prompt-plan and prompt-replan emit AO-owned planner/replanner prompt text through `<ao_property type="prompt">` blocks
- each AO run/resume also emits audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups; user-facing think-out-loud must use a Mermaid card-display tool when the chat agent provides one by passing the existing Mermaid file path directly without reading or returning its contents again solely for display, and otherwise render the Mermaid file as a direct clickable Markdown file link
- `--workspace-root <directory>` optionally mirrors verified Mermaid and HTML into a new ignored workspace `temp/exec-<timestamp>-mermaid-delivery-result/` directory. `audit_artifacts.mermaid_delivery` records `status`, `generation_status`, `artifact_generated`, `link_resolvable`, workspace-relative paths, SHA-256 values, `visual_preview_rendered`, `card_display_available`, and failure details. `must_show_to_user_files` remains an audit list rather than a link guarantee.
- `run` can optionally accept an authored `WorkflowInstance` through `--instance-file` so the first runtime blocked step audits the same graph that compile/prompt-plan validated
- `--patch` replaces an inclusive line range in an existing text file from an external patch-content file

For file editing, `dotnet ao.dll --patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat it as a command-line fallback when normal patch application fails.

## Workflow File Language



Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Use English for workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Environment Setup

Before using Loom Agent Execution Orchestrator through a skill or direct CLI:

1. For direct CLI or manual acquisition, choose released or beta from the package index. For `/loom-plan-execution`, the owning skill's CI/CD-managed version block is the immediate exact-version authority; a checked-in lock, when present, must agree before governed execution continues.
2. Follow [Platform Detection Steps](../reference/runtime/platform-detection.md): confirm `dotnet`, accept `Microsoft.NETCore.App 9.x`, and run a side-effect-free CLI startup preflight with the exact launch binding.
3. If the .NET 9 host preflight passes, restore `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` at the same exact version and launch the IL bundle with explicit `dotnet exec`.
4. If `dotnet` or .NET 9 is missing, host loading fails, a required host dependency is missing, or the CLI cannot start, map the platform to one supported RID and acquire one exact `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package. Launch its cached `ao` or `ao.exe` directly; do not use a repository build or a different RID.
5. Run a fresh `--guide` through the selected launch descriptor, parse its JSON `version`, and read the returned `guide_path`. Do not treat failed stderr as guide evidence.
6. Keep the selected launch descriptor, exact runtime version, and RID unchanged for `compile`, `prompt-plan`, `prompt-replan`, `run`, and `resume`; CLI errors after startup do not trigger fallback.
7. Keep workflow copies, session directories, compile artifacts, and audit outputs outside skill-owned paths. Only explicit `run` and `resume` are official AO skill execution surfaces.

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
    mermaid_delivery: structured delivery evidence for Mermaid and HTML generation, link resolution, preview, card capability, hashes, and failure state
    workspace_relative_mermaid_file: verified workspace-relative Mermaid link when workspace mirroring succeeds
    workspace_relative_html_file: verified workspace-relative HTML preview link when workspace mirroring succeeds
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
