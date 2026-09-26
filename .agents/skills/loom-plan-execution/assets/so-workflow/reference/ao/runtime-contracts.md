# AO Runtime Contract Reference Copy

<!-- loom-document-copy:start -->
- source_document: `tools/linux-x64/docs/en/guides/ao-guide-reference-contracts.md`
- source_reference_path: `docs/en/guides/ao-guide-reference-contracts.md`
- source_package_id: `Techne.Loom.AgentOrchestrator.Runtime.linux-x64`
- source_package_rid: `linux-x64`
- source_product: `ao`
- source_channel: `released`
- source_version: `0.3.326`
- source_sha256: `efd8e5ecb04bb589dbd55894455fa060db5f7065f616de31870871ba7b6a9495`
- source_package_sha512: `WjShGvz9EpAzNcqPcdSEBnU/wA2VoTxwgnCLeL2qOUd1q3rhTl0O9elG21xvf/iYdt2aRRr8DsIlc2+E0bKXAQ==`
- target_bound_version: `0.3.326`
- content_mode: `full-document`
- artifact_origin: `verified-copy`
- content_authority: `published-package`
- authority_scope: `target-local exact published package copy; fresh published-runtime guide_path remains authoritative`
- refresh_policy: `refresh this copy, its manifest, the node map, and the package lock together when the bound AO version changes`
<!-- loom-document-copy:end -->

This target-local file is the complete AO contracts page extracted from the exact published runtime package. It supports this skill but does not replace the fresh package guide returned by `dotnet ao.dll --guide`.

# Loom Agent Plan-Execution Orchestrator Guide: Contracts

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.326
Build: published package 0.3.326
<!-- guide-version:end -->







## Guide Output

Run `ao.exe --guide` on Windows or `ao --guide` on Unix. It reads the English `docs/en` tree shipped beside the apphost in the complete runtime package and emits JSON with the actual `version`, `docs_root`, and `guide_path`. Missing package docs are an error.

Use `guide_path` as the authoritative entry for this package version. Inspect `docs_root` only when this guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`; non-fatal installation warnings are written to stderr.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

Treat the direct AO apphost `--guide` result as a governance anchor, not as a detour. Once it succeeds, keep governed execution on that same published package runtime. Do not switch to repository builds or a manually assembled runtime.

Loom Agent Plan-Execution Orchestrator is the top-agent-facing orchestration product for exploratory work under uncertainty.

It does not try to hide uncertainty. It captures evolving workflow state, emits machine-first control data, and weaves out at major control seams, surfacing blocked payloads with explicit boundary fields when a caller must choose the next action deliberately.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../architecture/workflow-terminology.md). In that vocabulary, Loom Agent Plan-Execution Orchestrator weaves out at control seams, and callers weave back through direct `ao.exe resume` or `ao resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- The self-contained AO apphost supports `--guide`, `--help`, `--patch`, `compile`, `prompt-plan`, `prompt-replan`, `run`, and `resume`.
- Loom Agent Plan-Execution Orchestrator exposes the CLI and a local stdio-only MCP surface through `ao.exe mcp stdio` or `ao mcp stdio`; it does not provide Web or remote MCP transport.
- current AO control payloads emit `blocked` and `completed`; CLI/runtime failures surface as `<ao_property>` blocks with `type: error`
- AO compile emits Mermaid Markdown, HTML, and workflow JSON backup validation artifacts for an agent-authored workflow file
- AO prompt-plan and prompt-replan emit AO-owned planner/replanner prompt text through `<ao_property type="prompt">` blocks
- Each AO run/resume emits audit artifact links for Mermaid Markdown, HTML, workflow JSON backups, and workflow analysis reports. Follow [Mermaid artifact delivery](../../../.agents/skills/loom-plan-execution/reference/mermaid-artifact-delivery.md) after every AO apphost call: verify the returned paths, emit Mermaid, HTML, Analysis, and Dataflow link-plus-path pairs in order, then print localized `## Execution confidence: x%` and `## Estimated overall progress: x%` headings, each followed by one short reason or progress sentence.
- `--workspace-root <directory>` optionally mirrors verified Mermaid and HTML into a new ignored workspace `temp/exec-<timestamp>-mermaid-delivery-result/` directory. `audit_artifacts.mermaid_delivery` records `status`, `generation_status`, `artifact_generated`, `link_resolvable`, workspace-relative paths, SHA-256 values, `visual_preview_rendered`, `card_display_available`, and failure details. `must_show_to_user_files` remains an audit list rather than a link guarantee.
- `run` can optionally accept an authored `WorkflowInstance` through `--instance-file` so the first runtime blocked step audits the same graph that compile/prompt-plan validated
- `--patch` replaces an inclusive line range in an existing text file from an external patch-content file

For file editing, use the direct AO apphost `--patch` command when that command interface is preferred; otherwise use the repository-approved editing mechanism.

## Workflow File Language



Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Use English for workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Environment Setup

Before using Loom Agent Plan-Execution Orchestrator through a skill or direct CLI:

1. Read the exact package version from the package index or owning skill's version block and lock; resolve disagreements before governed execution.
2. Detect one supported RID from OS, architecture, and Linux libc.
3. Reuse only a valid exact package from the standard NuGet global-packages cache. Otherwise verify the exact NuGet registration SHA-512 or same-version GitHub `.sha512` sidecar.
4. Before extraction, validate package ID, version, RID, nuspec, `runtime.json`, archive safety, apphost, and English guide files. Extract the validated package to an external per-run directory.
5. Run `ao.exe --guide` on Windows or `ao --guide` on Unix as the first runtime operation. Verify the returned version and readable guide path.
6. Use the same apphost for `compile`, `prompt-plan`, `prompt-replan`, `run`, and `resume`. CLI errors after startup do not trigger another runtime path.
7. Keep workflow copies, session directories, compile artifacts, and audit outputs outside skill-owned paths. Only explicit `run` and `resume` are official AO skill execution surfaces.
## B+ Contract Context

AO runtime may consume a target contract through the shared bounded provider. The skill being enhanced keeps `assets/so-workflow/contract.json`; workflow root `contractBinding` points to it and a transition declares the JSON Pointer fragments it needs in `contractRefs`.

`compile` validates only workflow binding and reference syntax. `run` and `resume` read the current contract before the referenced transition, use one byte snapshot for parse and fragment projection, and inject bounded fragments into the transition's `contract_context`. Successful read metadata includes the path, optional SHA-256, cache state, returned bytes, and refs. A fragment that exceeds any configured bound fails closed before prior contract context is replaced. Manual contract edits are allowed; a changed hash refreshes the path-plus-hash cache and does not by itself block execution.

SO uses the same B+ provider and semantics. The runtime does not interpret domain meaning; the business step does. See [Contract Context Reference](../../architecture/contract-context-reference.md) and [Contract Consistency Rules](../../architecture/contract-consistency-rules.md).

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

In repo terminology, a blocked AO return is a weave out, and direct `ao.exe resume` or `ao resume` is the weave-back path.

Current runtime persistence intentionally keeps two shapes alive:

- `workflow_file` is the AO snapshot control file. Runtime resume validates `transition_id` against this file.
- `workflow_instance_file` is the current graph-shaped `WorkflowInstance` surface used for compile continuity, runtime audit continuity, and caller-managed replan edits.
- under `session_dir`, AO owns `session_<id>_runtime.workflow.json` as its runtime `WorkflowInstance` sidecar and `session_<id>_runtime.workflow.pointer.json` as the optional pointer to an external caller-managed `workflow_instance_file`.
