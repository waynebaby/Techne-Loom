# SkillOrchestrator Guide: Contracts

[中文](../../zh-cn/guides/so-guide-reference-contracts.md) | [Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.325-beta
Build: published package 0.3.325-beta
<!-- guide-version:end -->







## Guide Output

Run `so.exe --guide` on Windows or `so --guide` on Unix. It reads the English docs shipped beside the apphost and returns JSON with the actual `version`, `docs_root`, and `guide_path`. The apphost does not embed guide pages; missing package docs are an error.

Use `guide_path` as the authoritative entry for this package version. Inspect `docs_root` only when this guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`; non-fatal installation warnings are written to stderr.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

Treat the direct SO apphost `--guide` result as the version authority, not as a detour. Once the exact package guide is readable, keep governed execution on that same published apphost. Do not switch to repository builds or a manually assembled runtime.

SO is a deterministic skill execution and tracking product.

It compiles or loads a workflow, executes SO-owned steps directly, and returns only when the workflow finishes or reaches a seam that requires external participation.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../architecture/workflow-terminology.md). In that vocabulary, callers weave back through direct `so.exe resume` or `so resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- The self-contained SO apphost supports `--guide`, `--help`, `--patch`, `--schema-demo-output`, `compile`, `run`, `resume`, `status`, `inspect-workflow`, `inspect-workflow-fragment`, `inspect-events`, `ls`, and `copy-audit-step`.
- SO public parameter surface uses `compile` to validate an existing `--workflow-file`
- each SO compile emits Mermaid Markdown, HTML, workflow JSON backup, and workflow analysis validation artifacts
- After every SO apphost call, follow [Mermaid artifact delivery](../../../.agents/skills/loom-skill-enhancement/reference/mermaid-artifact-delivery.md): verify returned paths and readability, then provide Mermaid, HTML, Analysis, and Dataflow link-plus-path pairs in order. Follow them with localized `## Execution confidence: x%` and `## Estimated overall progress: x%` headings, each with one short reason or progress sentence. Use only verified paths and plain language.
- `--workspace-root <directory>` optionally mirrors verified Mermaid and HTML into a new ignored workspace `temp/exec-<timestamp>-mermaid-delivery-result/` directory. `audit_artifacts.mermaid_delivery` records `status`, `generation_status`, `artifact_generated`, `link_resolvable`, workspace-relative paths, SHA-256 values, `visual_preview_rendered`, `card_display_available`, and failure details. `must_show_to_user_files` remains an audit list rather than a link guarantee.
- `--patch` replaces an inclusive line range in an existing text file from an external patch-content file
- Mermaid renders use light node backgrounds and stable emoji labels derived from workflow step kind semantics plus owned-input metadata: `🔎` AI/model/subagent work in green, `⚙️` code/tool work in blue, `💬` user-owned optional branch choices in yellow, `🚧` required user input in red, `❓` generic conditional branches in amber/yellow, and `📜` gate/governance states in white or very light gray

Use the direct SO apphost `--patch` command for line-range patches when that command interface is preferred; otherwise use the repository-approved editing mechanism.

## Workflow File Language



Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Use English for workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Environment Setup

Before using SO through a skill or direct CLI:

1. Read the exact package version from the owning skill's lock and version block. Never float to `latest`.
2. Detect one supported RID from OS, architecture, and Linux libc.
3. Reuse only a valid exact package from the standard NuGet global-packages cache. Otherwise verify the exact NuGet registration hash or same-version GitHub `.sha512` sidecar.
4. Before extraction, verify package ID, version, RID, nuspec, manifest, archive safety, apphost, and English guide files. Extract the validated package to an external per-run directory.
5. Directly run `so.exe --guide` on Windows or `so --guide` on Unix as the first runtime operation. Validate the returned version and readable contained paths.
6. Use that same apphost for schema/demo, compile, run, and resume. Keep workflow copies, event sidecars, and audit outputs outside skill folders.
7. For `/loom-skill-enhancement` and governed skills, only direct apphost `so.exe run`/`so.exe resume` on Windows or `so run`/`so resume` on Unix are official workflow execution surfaces.
## B+ Contract Context

SO runtime may consume a target contract through the shared bounded provider. The skill being enhanced keeps `assets/so-workflow/contract.json`; workflow root `contractBinding` points to it and a transition declares the JSON Pointer fragments it needs in `contractRefs`.

`compile` validates only workflow binding and reference syntax. `run` and `resume` read the current contract before the referenced transition, use one byte snapshot for parse and fragment projection, and inject bounded fragments into the transition's `contract_context`. Successful read metadata includes the path, optional SHA-256, cache state, returned bytes, and refs. A fragment that exceeds any configured bound fails closed before prior contract context is replaced. Manual contract edits are allowed; a changed hash refreshes the path-plus-hash cache and does not by itself block execution.

SO uses the same B+ provider and semantics. The runtime does not interpret domain meaning; the business step does. See [Contract Context Reference](../../architecture/contract-context-reference.md) and [Contract Consistency Rules](../../architecture/contract-consistency-rules.md).

## Contracts

```guide-contract
inputs:
  workflow_file: source or validated workflow path; `run` and `resume` must target a runtime copy outside any skill folder
  context_file: optional initial context
  external_result: optional structured weave-back result for a previously blocked step
so_property_types:
  progress:
    status: active | blocked | completed | failed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: current workflow focus node
    next_node_id: optional next node when known
    event_log_file: append-only execution event path
    can_resume: true for WaitingExternal with an active wait group or Failed with failure history, a previous state, and an owned most recent failed transition; otherwise false
    fresh_instance_required: true for Succeeded or unrecoverable Failed; false for recoverable Failed, WaitingExternal, and active states
    audit_artifacts:
      output_root: audit output root
      step_directory: per-step audit directory
      mermaid_file: current workflow Mermaid Markdown path
      html_file: current workflow HTML path
      workflow_backup_file: current workflow JSON backup path
      analysis_file: current workflow analysis JSON path when available
      dataflow_file: current workflow dataflow JSON path when available
      reuse_manifest_file: audit-reuse.json path when this step was copied
      artifact_origin: fresh-runtime | verified-copy
      official_execution_evidence: false when artifact_origin is verified-copy
      mermaid_delivery: structured delivery evidence for Mermaid and HTML generation, link resolution, preview, card capability, hashes, and failure state
      workspace_relative_mermaid_file: verified workspace-relative Mermaid link when workspace mirroring succeeds
      workspace_relative_html_file: verified workspace-relative HTML preview link when workspace mirroring succeeds
  status:
    status: active | blocked | completed | failed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: current workflow focus node
    next_node_id: optional next node when known
    event_log_file: append-only execution event path
    can_resume: true for WaitingExternal with an active wait group or Failed with failure history, a previous state, and an owned most recent failed transition; otherwise false
    fresh_instance_required: true for Succeeded or unrecoverable Failed; false for recoverable Failed, WaitingExternal, and active states
  boundary:
    status: blocked
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: current workflow focus node
    current_step_kind: current blocking step kind
    skill_hint: strict instruction for the next external action
    memory_for_next_step: curated memory summary plus referenced context slice
    required_inputs: optional structured inputs needed to continue
    event_log_file: append-only execution event path
    can_resume: true for a resumable boundary; false when no active wait group or recoverable failed transition exists
    fresh_instance_required: true only when the persisted instance cannot be resumed safely
  result:
    status: completed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: terminal node or current completed node
    context: optional current context snapshot on completed result payloads
    event_log_file: append-only execution event path
    can_resume: false for a completed result
    fresh_instance_required: true for a completed result because Succeeded instances are terminal
    audit_artifacts:
      output_root: audit output root
      step_directory: per-step audit directory
      mermaid_file: point-in-time Mermaid Markdown path
      html_file: point-in-time HTML path
      workflow_backup_file: point-in-time workflow JSON backup
      analysis_file: point-in-time workflow analysis JSON path when available
      dataflow_file: point-in-time workflow dataflow JSON path when available
      reuse_manifest_file: audit-reuse.json path when this step was copied
      artifact_origin: fresh-runtime | verified-copy
      official_execution_evidence: false when artifact_origin is verified-copy
      mermaid_delivery: structured delivery evidence for Mermaid and HTML generation, link resolution, preview, card capability, hashes, and failure state
      workspace_relative_mermaid_file: verified workspace-relative Mermaid link when workspace mirroring succeeds
      workspace_relative_html_file: verified workspace-relative HTML preview link when workspace mirroring succeeds
  error:
    status: failed
    instance_id: durable workflow instance identifier when available
    workflow_file: optional workflow path when available
    message: stable machine-readable error summary
    event_log_file: optional execution event path
    can_resume: true only when the Failed instance has failure history, a previous state, and an owned most recent failed transition
    fresh_instance_required: true for Succeeded or unrecoverable Failed; false for a recoverable Failed instance
resume_envelope:
  transition_id: target blocked transition identifier
  correlation_key: optional blocked correlation key
  payload: structured result data for the blocked step
cli_stream:
  wrapped_exec_block:
    - <wrapped_exec>
    - <commandline>...</commandline>
    - <exectionstream>
    - ...streamed output lines...
    - </exectionstream>
    - </wrapped_exec>
  so_property_block:
    - <so_property>
    - {json}
    - </so_property>
```

The CLI keeps wrapped execution output streamable without forcing SO metadata into the same raw stream lines. Callers should treat the `type` field in `<so_property>` as the primary branch point for payload parsing.

A Failed instance may resume on the same persisted workflow when `transition_id` identifies the most recent failed transition belonging to the previous state. The runtime restores the instance to `Running`, retries from that state, and preserves the failure history and event evidence. Missing failure history, previous-state, or transition-ownership evidence is unrecoverable and must fail closed. A Succeeded instance remains terminal and requires a fresh external workflow copy.

The CLI serializes operations for one persisted workflow file with an adjacent cross-process file lock. Concurrent `run`, `resume`, `status`, `compile`, and inspection commands wait for the lock and then re-read the current workflow file before continuing.

In repo terminology, a blocked SO return is a weave out, and direct `so.exe resume` or `so resume` is the weave-back path.
