# SO Runtime Contract Reference Copy

<!-- loom-document-copy:start -->
- source_document: `tools/linux-x64/docs/en/guides/so-guide-reference-contracts.md`
- source_reference_path: `docs/en/guides/so-guide-reference-contracts.md`
- source_package_id: `Techne.Loom.SkillOrchestrator.Runtime.linux-x64`
- source_package_rid: `linux-x64`
- source_product: `so`
- source_channel: `beta`
- source_version: `0.3.283-beta`
- source_sha256: `975894c482cdb76c9984f2cd2bbd5eccce0f43db3c550a8345eb773ce331f4da`
- source_package_sha512: `I03so07KxSKyv/QLzJ4sfTB0LAkZKMslX0Ebu6OlVz9w7do3zU2LokJK83xOMdes2lo0s3VQcATfXUAa1q7JYg==`
- target_bound_version: `0.3.283-beta`
- content_mode: `full-document`
- artifact_origin: `verified-copy`
- content_authority: `published-package`
- authority_scope: `target-local exact published package copy; fresh published-runtime guide_path remains authoritative`
- refresh_policy: `refresh this copy, its manifest, the node map, and the package lock together when the bound SO version changes`
<!-- loom-document-copy:end -->

This target-local file is the complete SO contracts page extracted from the exact published runtime package. It supports this skill but does not replace the fresh SO guide returned by `dotnet so.dll --guide`.

# SkillOrchestrator Guide: Contracts

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [Root](../README.md)

Version: 0.3.283-beta
Build: published package 0.3.283-beta

## Guide Output

Run the bare `dotnet so.dll --guide` command. It reads the English `docs/en` tree shipped beside the executable in a complete runtime package and emits one JSON object with the actual `version`, `docs_root`, and `guide_path` absolute paths. The executable does not contain guide pages; a missing package docs tree is an error.

Use `guide_path` as the authoritative entry for this package version. Inspect `docs_root` only when this guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`; non-fatal installation warnings are written to stderr.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

Treat `dotnet so.dll --guide` as a governance anchor, not as a detour. For `/loom-skill-enhancement` itself and for any Loom-governanced target skill, once a fresh guide result has been obtained from a runnable SO runtime, all governed execution must stay on the corresponding published SO package runtime surface described by that guide. It does not matter whether the guide was reached from a skill entry point, direct CLI use, or a restored runtime bundle: once that guide exists, official governed execution must route back to the published SO package runtime it describes. Do not read the guide and then drift back to repository builds, hand-assembled runtimes, or non-governed execution paths for official SO skill or target-skill execution.

SO is a deterministic skill execution and tracking product.

It compiles or loads a workflow, executes SO-owned steps directly, and returns only when the workflow finishes or reaches a seam that requires external participation.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../architecture/workflow-terminology.md). In that vocabulary, SO weaves out when it reaches an externally owned step, surfacing that seam on blocked `<so_property>` payloads via fields such as `current_step_kind`, and callers weave back through `dotnet so.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- the `.NET` runtime is implemented with `dotnet so.dll --guide`, `dotnet so.dll --help`, `dotnet so.dll --patch`, `dotnet so.dll compile`, `dotnet so.dll run`, `dotnet so.dll resume`, `dotnet so.dll status`, `dotnet so.dll inspect-workflow`, `dotnet so.dll inspect-events`, and `dotnet so.dll ls`, and `dotnet so.dll copy-audit-step`
- SO public parameter surface uses `compile` to validate an existing `--workflow-file`
- each SO compile emits Mermaid Markdown, HTML, workflow JSON backup, and workflow analysis validation artifacts
- SO returns audit artifact links for Mermaid Markdown, HTML, workflow JSON backups, and workflow analysis reports on run/resume surfaces; user-facing think-out-loud must use a Mermaid card-display tool when the chat agent provides one by passing the existing Mermaid file path directly without reading or returning its contents again solely for display, and otherwise render the Mermaid file as a direct clickable Markdown file link
- `--workspace-root <directory>` optionally mirrors verified Mermaid and HTML into a new ignored workspace `temp/exec-<timestamp>-mermaid-delivery-result/` directory. `audit_artifacts.mermaid_delivery` records `status`, `generation_status`, `artifact_generated`, `link_resolvable`, workspace-relative paths, SHA-256 values, `visual_preview_rendered`, `card_display_available`, and failure details. `must_show_to_user_files` remains an audit list rather than a link guarantee.
- `--patch` replaces an inclusive line range in an existing text file from an external patch-content file
- Mermaid renders use light node backgrounds and stable emoji labels derived from workflow step kind semantics plus owned-input metadata: `🔎` AI/model/subagent work in green, `⚙️` code/tool work in blue, `💬` user-owned optional branch choices in yellow, `🚧` required user input in red, `❓` generic conditional branches in amber/yellow, and `📜` gate/governance states in white or very light gray

For file editing, `dotnet so.dll --patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat it as a command-line fallback when normal patch application fails.

## Workflow File Language



Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Use English for workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Environment Setup

Before using SO through a skill or direct CLI:

1. Direct CLI or manual callers choose released or beta from the package index. `/loom-skill-enhancement` and Loom-governanced target skills use the current CI/CD-managed version block plus checked-in lock as the exact-version authority and must resolve disagreements before continuing.
2. Follow [Platform Detection Steps](../reference/runtime/platform-detection.md), detect OS/architecture/libc, and run the candidate .NET 9 CLI startup preflight before any target-skill planning, authoring, validation, compile, run, resume, or downstream input collection.
3. Before network access, validate a complete local exact-version SO IL bundle when the host branch is eligible. A valid framework bundle contains `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` at one version.
4. When the .NET 9 host and CLI preflight pass, use explicit `dotnet exec` against that unified IL bundle. Keep the bundle outside the skill folder.
5. When the host is missing or cannot start the CLI, resolve one supported RID and acquire one exact `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package. Verify its hash, nuspec, manifest, ZIP safety, and entrypoint before launching its direct `so` or `so.exe` executable.
6. Run a fresh `--guide` with the selected launch descriptor, verify its JSON `version`, and read the returned `guide_path`. Do not begin target-skill work from stale or failed guide output.
7. Keep the launch descriptor, exact runtime version, and RID stable for `compile`, `run`, `resume`, `status`, and inspection commands. CLI errors after startup are not fallback triggers.
8. Clone checked-in workflow templates to an external runtime copy and keep compile/audit outputs and event sidecars outside skill-owned paths.
9. For `/loom-skill-enhancement` and governed target skills, only public `dotnet so.dll run` and `dotnet so.dll resume` against that runtime copy are official workflow execution surfaces; `--guide` and `compile` are preparation or validation.

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

In repo terminology, a blocked SO return is a weave out, and `dotnet so.dll resume` is the weave-back path.
