# SkillOrchestrator Guide

[中文](../../../zh-cn/reference/products/so-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

SO is a deterministic skill execution and tracking product.

It compiles or loads a workflow, executes SO-owned steps directly, and returns only when the workflow finishes or reaches a seam that requires external participation.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../../../en/architecture/workflow-terminology.md). In that vocabulary, SO weaves out when it reaches an externally owned step, surfacing that seam on blocked `<so_property>` payloads via fields such as `current_step_kind`, and callers weave back through `dotnet so.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- the `.NET` runtime is implemented with `dotnet so.dll --guide`, `dotnet so.dll --help`, `dotnet so.dll compile`, `dotnet so.dll run`, `dotnet so.dll resume`, `dotnet so.dll status`, `dotnet so.dll inspect-workflow`, `dotnet so.dll inspect-events`, and `dotnet so.dll ls`
- SO public parameter surface uses `compile` to validate an existing `--workflow-file`
- each SO compile emits Mermaid Markdown, HTML, and workflow JSON backup validation artifacts
- SO returns audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups on run/resume surfaces

## Environment Setup

Before using SO through a skill or direct CLI:

1. Choose package channel from [`packages.released.md`](../../../../packages.released.md) or [`packages.beta.md`](../../../../packages.beta.md).
2. When installing from NuGet for local execution, restore the SO runtime bundle together: `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`, all at the same channel/version. Do not restore only `Techne.Loom.SkillOrchestrator`.
3. Read this guide through `dotnet so.dll --guide`.
4. Prepare a workflow JSON path and, when needed, an explicit audit output root for compile validation artifacts and run/resume audit artifacts.
5. Keep checked-in source templates immutable: clone them to a runtime temp folder or explicit execution-output folder before `run` or `resume`, and do not place runtime workflow copies, `.events.jsonl` sidecars, or audit outputs inside a skill folder.

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
    audit_artifacts:
      output_root: audit output root
      step_directory: per-step audit directory
      mermaid_file: current workflow Mermaid Markdown path
      html_file: current workflow HTML path
      workflow_backup_file: current workflow JSON backup path
  status:
    status: active | blocked | completed | failed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: current workflow focus node
    next_node_id: optional next node when known
    event_log_file: append-only execution event path
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
  result:
    status: completed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: terminal node or current completed node
    context: optional current context snapshot on completed result payloads
    event_log_file: append-only execution event path
    audit_artifacts:
      output_root: audit output root
      step_directory: per-step audit directory
      mermaid_file: point-in-time Mermaid Markdown path
      html_file: point-in-time HTML path
      workflow_backup_file: point-in-time workflow JSON backup
  error:
    status: failed
    instance_id: durable workflow instance identifier when available
    workflow_file: optional workflow path when available
    message: stable machine-readable error summary
    event_log_file: optional execution event path
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

In repo terminology, a blocked SO return is a weave out, and `dotnet so.dll resume` is the weave-back path.

## Behavior

SO executes these step kinds directly when they are local and deterministic:

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

SO weaves out and returns guidance for these externally owned kinds:

- `ModelThink`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `WaitResume`

`ConditionBranch` stays explicit in the workflow and is resolved by deterministic evaluation inside SO.

Current public runtime support note:

- `FirstSuccess` is the fully supported transition-group strategy in v1.
- `FirstResponse` and `All` remain model-level values, but the current public runtime will fail explicitly when multiple ready transitions require those strategies.

## Responsibilities

### Caller

- Provide the workflow JSON to compile.
- When local runtime download is needed, restore the full SO runtime bundle instead of only `Techne.Loom.SkillOrchestrator`.
- Copy checked-in source templates to a runtime temp or execution-output folder before `run` or `resume`.
- Execute the external action when SO weaves out.
- Resume SO with the structured weave-back envelope.
- Parse `<so_property>` as the authoritative SO control payload.
- Treat `<wrapped_exec>` as the streamed shell-facing wrapper surface.
- Use `transition_id`, `correlation_key`, and `payload` in the resume sidecar JSON.
- Keep runtime workflow copies, event sidecars, and audit outputs outside any skill-owned directory.
- On every progress update, surface the current workflow Mermaid Markdown and HTML paths in think-out-loud output.

### Author

- Encode step kinds explicitly.
- Define memory extraction hints when the next step requires context curation.
- Keep local deterministic steps free of hidden side channels.

### Outer-agent

- Consume `skill_hint` literally.
- Preserve `memory_for_next_step` across the blocked seam and its resume handoff.
- Avoid improvising beyond the contract of the blocking step.

## Templates

```guide-template
dotnet so.dll compile \
  --workflow-file so-template.json \
  --audit-output outputs/audit
```

`so-template.json` remains the checked-in source template. Place `outputs/audit` outside the skill folder.

```guide-template
dotnet so.dll run \
  --workflow-file workflow.current.json \
  --context-file context.json \
  --audit-output outputs/audit
```

`workflow.current.json` is a mutable runtime copy created outside the skill folder. Do not point `--workflow-file` back at `<target-skill-root>/assets/so-workflow/`, and do not place `outputs/audit` there either.

```guide-template
{
  "transition_id": "transition.ask",
  "correlation_key": null,
  "payload": {
    "answer": "approved"
  }
}
```

```guide-template
dotnet so.dll resume \
  --workflow-file workflow.current.json \
  --result-file external-step-result.json
```

Resume continues against the same external runtime copy, not the checked-in source template.

```guide-checklist
- workflow JSON is materialized before execution
- checked-in source template stays clean; run/resume target an external mutable workflow copy such as `workflow.current.json`
- audit outputs also stay outside the skill folder
- compile writes Mermaid Markdown and HTML validation outputs before execution handoff
- step kinds are explicit
- local tools are deterministic
- memory extraction is defined or derivable
- caller can send structured external results back
```

## Examples

For a full narrative example of an SO-governed target-skill run with stage gates, branch fan-out, validation, audit evidence, and Mermaid route diagrams, see [SO-Enhanced Skill Run Example](../../../en/examples/so-enhanced-skill-run.md).

```guide-example
name: local-tool-then-block-for-user
flow:
  - ToolCall: ls working directory
  - AskUser: choose target file
result:
  status: blocked
  current_step_kind: AskUser
```

```guide-example
name: model-think-with-memory
flow:
  - MemoryRead: summarize prior review findings
  - ModelThink: propose minimal code edit
result:
  status: blocked
  current_step_kind: ModelThink
  memory_for_next_step: curated summary of prior findings
```

```guide-example
name: wait-for-external-signal
flow:
  - WaitResume: wait for webhook completion
result:
  status: blocked
  current_step_kind: WaitResume
  required_inputs:
    - correlation_id
    - payload
```

```guide-example
name: finished-deterministic-run
flow:
  - ToolCall: generate output
  - ArtifactEmit: write report
result:
  status: completed
  current_node_id: state.done
  context:
    output_path: outputs/report.md
```

```guide-example
name: enhanced-target-skill-runtime-lock-reference
target_skill_markdown: |
  ## SO-Enhanced Runtime Lock

  This skill is enhanced by Loom SO.
  Authoritative SO runtime version lock: `assets/so-workflow/so-package-lock.json`.
  Routine SO DLL restoration must resolve the exact locked version from NuGet first; if the local cache already holds that same version, reuse it, otherwise download it again from NuGet.
notes:
  - keep the reference checked in with the target skill
  - treat the lock file as the authority for day-to-day SO runtime restoration
```

```guide-example
name: minimal-so-package-lock
so_package_lock_json: |
  {
    "package_id": "Techne.Loom.SkillOrchestrator",
    "channel": "released",
    "resolved_version": "1.2.3",
    "runtime_restore": {
      "source": "nuget",
      "fresh_download": true,
      "allow_local_cache_when_exact_version_matches": true,
      "fallback_source": "github-release-asset"
    },
    "enhancement": {
      "resolved_at_utc": "2026-06-12T00:00:00Z",
      "selected_language": "en"
    },
    "notes": [
      "Resolve the exact version from NuGet first.",
      "Freshly download unless the local cache already holds the exact same version.",
      "Use GitHub release assets only when NuGet.org is unavailable."
    ]
  }
restore_rule:
  - resolve the exact version from NuGet first
  - reuse local cache only when it already holds that exact version
  - otherwise download the exact version again from NuGet
```

## Anti-Patterns

- Letting callers infer the next action from prose alone.
- Hiding memory in prompts instead of workflow context.
- Running shorthand commands without compiling them into a persisted workflow.
- Mixing wrapped command output and SO boundary payloads into one undifferentiated plain-text stream.
- Letting a skill hide package/channel choice instead of sending users to the package index first.
