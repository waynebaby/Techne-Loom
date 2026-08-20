# SkillOrchestrator Guide

[中文](../../../zh-cn/reference/products/so-guide.md) | [Root](../../README.md)

Version: draft

Build: repository source

## Guide Output

Run the bare `dotnet so.dll --guide` command. It installs the embedded English `docs/en` bundle under `<binary>/docs/<package-version>/` and emits one JSON object with the actual `version`, `docs_root`, and `guide_path` absolute paths. If the binary directory is not writable, the runtime uses `%TEMP%/docs/<package-version>/` and returns the actual paths.

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

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../../../en/architecture/workflow-terminology.md). In that vocabulary, SO weaves out when it reaches an externally owned step, surfacing that seam on blocked `<so_property>` payloads via fields such as `current_step_kind`, and callers weave back through `dotnet so.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

Current implementation status:

- the `.NET` runtime is implemented with `dotnet so.dll --guide`, `dotnet so.dll --help`, `dotnet so.dll --patch`, `dotnet so.dll compile`, `dotnet so.dll run`, `dotnet so.dll resume`, `dotnet so.dll status`, `dotnet so.dll inspect-workflow`, `dotnet so.dll inspect-events`, and `dotnet so.dll ls`
- SO public parameter surface uses `compile` to validate an existing `--workflow-file`
- each SO compile emits Mermaid Markdown, HTML, workflow JSON backup, and workflow analysis validation artifacts
- SO returns audit artifact links for Mermaid Markdown, HTML, workflow JSON backups, and workflow analysis reports on run/resume surfaces
- `--patch` replaces an inclusive line range in an existing text file from an external patch-content file
- Mermaid renders use light node backgrounds and stable emoji labels derived from workflow step kind semantics plus owned-input metadata: `🔎` AI/model/subagent work in green, `⚙️` code/tool work in blue, `💬` user-owned optional branch choices in yellow, `🚧` required user input in red, `❓` generic conditional branches in amber/yellow, and `📜` gate/governance states in white or very light gray

For file editing, `dotnet so.dll --patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat it as a command-line fallback when normal patch application fails.

## Environment Setup

Before using SO through a skill or direct CLI:

1. For direct CLI or manual package acquisition, choose the package channel from [`packages.released.md`](../../../../packages.released.md) or [`packages.beta.md`](../../../../packages.beta.md). For `/loom-skill-enhancement` and Loom-governanced target skills, normal execution should instead reuse the runtime version already bound by the checked-in lock and current CI/CD-managed skill package version block. If those two authorities ever disagree, treat the current CI/CD-managed skill package version block as the immediate download authority and update the checked-in lock to match before continuing governed execution.
2. When installing from NuGet for local execution, restore the SO runtime bundle together: `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`, all at the same channel/version. Do not restore only `Techne.Loom.SkillOrchestrator`. When an exact package id/version is already known, probe or download the direct `.nupkg` URL instead of waiting for page/search/registration indexing.
3. For `/loom-skill-enhancement` and any Loom-governanced target skill, official workflow operations should use the published package artifacts for the bound runtime version and its derived channel rather than repository source builds or hand-assembled local runtimes, unless a blocked-state emergency exception was explicitly approved.
4. Read this guide through `dotnet so.dll --guide`.
5. Before any target-skill planning, authoring, validation, compile, run, resume, or downstream input collection, prove that the selected published SO runtime is runnable and can emit a fresh `dotnet so.dll --guide` result from that runtime.
6. Once that fresh guide result exists, route governed execution for `/loom-skill-enhancement` itself and for any Loom-governanced target skill back onto the corresponding published SO package runtime it describes. `--guide` is not permission to continue official skill or target-skill execution on repository builds, hand-assembled runtimes, or other non-governed paths.
7. Prepare a workflow JSON path and, when needed, an explicit audit output root for compile validation artifacts and run/resume audit artifacts.
8. Keep checked-in source templates immutable: before a new official `run`, clone the checked-in source workflow to a runtime temp folder or explicit execution-output folder, and do not place runtime workflow copies, `.events.jsonl` sidecars, or audit outputs inside a skill folder. Later `resume` calls in that same execution chain must continue against that same persisted runtime copy.
9. For `/loom-skill-enhancement` and any Loom-governanced target skill, keep normal workflow governance on `dotnet so.dll --guide`, `dotnet so.dll compile`, `dotnet so.dll run`, and `dotnet so.dll resume`. Do not use direct workflow JSON edits as a normal maintenance path.

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
      analysis_file: current workflow analysis JSON path when available
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
      analysis_file: point-in-time workflow analysis JSON path when available
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

When `MemoryRead` is used to inspect checked-in target-skill assets during re-enhancement or governance review, it must load real file snapshots instead of placeholder context copies, and every inspected asset path must remain under the declared target-skill asset root.

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
- Before a new official `run`, copy checked-in source templates to a runtime temp or execution-output folder. When the workflow later blocks, `resume` must continue against that same persisted runtime copy.
- Execute the external action when SO weaves out.
- Resume SO with the structured weave-back envelope.
- Parse `<so_property>` as the authoritative SO control payload.
- Treat `<wrapped_exec>` as the streamed shell-facing wrapper surface.
- Use `transition_id`, `correlation_key`, and `payload` in the resume sidecar JSON.
- Keep runtime workflow copies, event sidecars, and audit outputs outside any skill-owned directory.
- On every progress update, surface the current workflow Mermaid Markdown and HTML paths in think-out-loud output.
- Treat `workflow.analysis.json` as the machine-readable summary of inputs, output families, branches, loops, user seams, runtime seams, gates, and Turing-complete control risk.

### Author

- Encode step kinds explicitly.
- Define memory extraction hints when the next step requires context curation.
- Keep local deterministic steps free of hidden side channels.

### Outer-agent

- Consume `skill_hint` literally.
- Preserve `memory_for_next_step` across the blocked seam and its resume handoff.
- Avoid improvising beyond the contract of the blocking step.

## Mandatory Loom Skill Orchestrator Governance Rules for Enhanced Skills

Apply this section when a skill is being enhanced by `/loom-skill-enhancement` or is already operating under Loom Skill Orchestrator governance. The skill does not need to identify itself as a target skill before applying these rules. This section does not redefine AO behavior or apply to unrelated workflows.

### Deterministic Transition Contract

Every workflow transition authored or reviewed for that skill must declare all of the following:

- `guardExpression`: an executable boolean predicate evaluated before the transition runs; it proves eligibility from available inputs and must not claim that execution output already exists
- `succeedExpression`: an executable boolean predicate evaluated after the transition runs; it proves acceptance from declared output evidence and must not merely repeat the guard
- explicit user-owned versus runtime-owned input ownership
- explicit output evidence paths or output-family declarations
- a blocked route or terminal route when the transition can leave the current state

Reject transition definitions that use descriptive-only prose, an implicit predicate, an unbounded natural-language condition, or the same semantic test for both guard and success. A missing predicate, ownership declaration, or evidence shape is a failed authoring check.

### Boundary Check And Approval Gate (Compulsory)

The skill is forced onto the Loom Skill Orchestrator-governanced route. No next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step:

- A **boundary check** validates every transition before advancing: `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state, gate predicates (`passExpression` / `succeedExpression`) over runtime evidence plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) explicit user approval/instruction at `AskUser` seams for declared user-owned fields or decisions, or (b) structured non-human continuation payloads whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, compile success, an unapproved draft copy, local orchestration, or direct workflow JSON edits — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Every transition on the same external runtime copy must pass this gate until final `Done`.

### Expression Contract

The current .NET expression language is **C#**, compiled in-process by Roslyn. The root workflow declares `runtimeBinding` and `expressionBinding`; do not add per-node language overrides. The binding includes `language`, `languageVersion`, `contractId`, `contractVersion`, `requiredExpressionCapabilities`, and `compileFeedbackContract: "detailedCompileFeedbackV1"`.

`guardExpression`, `succeedExpression`, and `passExpression` use structured `ExpressionDefinition` values with `kind`, `source`, `entryPoint`, and `resultType`. A string is accepted only as a compatibility shorthand with an explicit C# binding and is always serialized as an object. Current expressions are synchronous predicates; asynchronous constructs and legacy non-C# expression syntax are invalid and fail closed. Use the read-only context contract, such as `context.Get<T>("path")`, rather than implicit bare identifiers.

Every compile emits `ExpressionCompileFeedback` under `detailedCompileFeedbackV1`, including location, source span, stable code/category, severity, actionable message, suggested fix, referenced symbols, compiler identity, resolved form, result type, capabilities, and warnings. Raw compiler text alone is insufficient. Rust+CEL is a future fourth runtime route using the same schema and feedback contract, not Rust code execution; Node.js and Python remain adapter routes until they implement the same contract.
### Deterministic Gate Contract

Every gate authored or reviewed for that skill must declare all of the following:

- `passExpression`: a machine-checkable boolean predicate evaluated against the runtime evidence context
- a machine-checkable pass predicate
- required evidence references and output families
- the route coverage that can satisfy the gate
- the strongest-earned blocked gate when the route cannot continue
- the terminal route and business-output gate required before `Done`

Governance-only artifacts cannot satisfy a business-output gate. A route must fail closed when its required evidence, output family, blocked route, or terminal route declaration is missing.

### Explicit Unattended-Mode Contract

An unattended workaround is permitted only for a blocked SO path and only when unattended mode is explicitly declared in the current session. Do not infer unattended mode from an earlier turn. Re-confirm attended versus unattended status at every critical decision boundary.

Before an autonomous workaround, require a structured decision-evidence record showing that expected benefit clearly exceeds risk, the alternatives considered, the smallest reversible change selected, and a rollback plan executable in one step. After the workaround, immediately return to the public `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume` path. The post-run acknowledgement request is non-blocking unless the user explicitly requires blocking behavior.

### Weave-Out Citation Contract

Every weave-out or external handoff must return a minimal citation manifest for the documents that caused or support the next action. Do not dump the full context pack or cite every file that was read. Include only the entry document, the necessary workflow or contract files, and the specific guide evidence that controls the decision.

Each citation must contain:

- `path`: a workspace-relative or runtime-output-relative path, never an absolute machine path
- `start_line` and `end_line`: verified 1-based inclusive line numbers from the exact file content used for this weave-out
- `role`: why the cited excerpt is required for the next action

When a guide is involved, cite the actual successful `guide_path` returned by the latest `dotnet so.dll --guide` JSON result and cite its output line numbers. Citing only the guide source location is insufficient. The command does not export a guide file; if no `guide_path` can be read, identify the failed runtime evidence instead. A weave-out without verified `evidence_references` is incomplete and must not be woven back as successful evidence.

Keep every weave-out response compact: return the next action or decision, the minimal `evidence_references` manifest, and the resume payload contract. Do not repeat the full context-pack inventory.

These rules are mandatory guide requirements during the skill's authoring, review, compile readiness, and governed execution handoff under SO.

## Templates

```guide-template
dotnet so.dll compile \
  --workflow-file so-template.json \
  --audit-output outputs/audit
```

`so-template.json` remains the checked-in source template. Place `outputs/audit` outside the skill folder.

For `/loom-skill-enhancement` and any Loom-governanced target skill, do not directly edit checked-in workflow JSON as a normal maintenance path. Only when the active `dotnet so.dll` path is fully blocked and the user explicitly approves a narrow workaround may you make the smallest direct JSON change needed to unblock the next `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume`, then immediately return to the Loom-governanced path.

Manual edits to the running external workflow `.json` copy are also last-resort blocked-state emergency workarounds only, not part of the normal workflow-operation path.

For Loom-governanced target-skill templates, set root `templateKind: so-governed-target-skill` and a root `validation` contract. `compile` validates structural integrity plus route-aware business-output gates, seam ownership, blocked strongest-earned outputs, and done reachability before the workflow may become execution authority.

`compile` also requires every state node to declare a non-empty `workflowPhase`. That field means which stage of the overall workflow the node belongs to, and compile uses it to enforce swimlane-ready authoring instead of treating phase grouping as optional rendering metadata.

If a target-skill modification intends that governed workflow to become runnable execution authority, the materialized runtime workflow must also be executable on the current public `dotnet so.dll run` and `dotnet so.dll resume` path. Do not leave the runnable workflow in `Drafting`, and do not depend on private or unavailable built-in tool names that the current public runtime does not expose. If a checked-in workflow JSON is only a draft or compile-review source template, label it that way explicitly and do not present it as directly runnable.

For wording discipline, treat guide refresh, checked-in asset authoring, and compile validation as intermediate milestones rather than normal completion states. For full-delivery governed slices, the stable completion wording should describe a Loom-governanced target skill whose official execution surface is the public `dotnet so.dll run` and `dotnet so.dll resume` path against a materialized runtime workflow copy, and whose governed run has actually reached final `Done`.

Compile also writes `workflow.analysis.json` beside `workflow.mermaid.md`, `workflow.html`, and `workflow.json`. Use that analysis artifact to review control-flow structures before execution: branches, switch-like groups, loops, requested inputs, published output families, user seams, runtime seams, and gate coverage.

```guide-template
dotnet so.dll run \
  --workflow-file workflow.current.json \
  --context-file context.json \
  --audit-output outputs/audit
```

`workflow.current.json` is a mutable runtime copy created outside the skill folder. Do not point `--workflow-file` back at `<target-skill-root>/assets/so-workflow/`, and do not place `outputs/audit` there either. Create a fresh runtime copy when starting a new official run chain, then keep resume on that same persisted runtime copy instead of rebuilding it from checked-in source assets.

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
- every new official run chain starts from a fresh external workflow execution file copied from checked-in source assets
- resume stays on the same persisted runtime workflow copy from that run chain
- direct workflow JSON edits are not a normal governance path; blocked-state emergency workarounds require explicit user approval and immediate return to `dotnet so.dll`
- audit outputs also stay outside the skill folder
- compile writes Mermaid Markdown, HTML, workflow backup, and workflow analysis validation outputs before execution handoff
- for Loom-governanced target-skill templates, compile also requires a root validation contract, route-aware business-output gates, strongest-earned blocked-output declarations, and ownership-safe seams
- for target-skill modifications, runtime-ready evidence and fresh-guide evidence should be modeled explicitly before any downstream planning, authoring, validation, compile, run, or resume steps
- if re-enhancement review inspects checked-in assets, those inspection nodes must load real file snapshots before any gap-review subagent consumes them
- file-backed checked-in-asset inspection must declare an explicit target-skill asset root and must reject absolute paths or traversal that escapes that root
- if a governed workflow is presented as runnable execution authority, its materialized runtime copy must be executable on the current public `dotnet so.dll run` path rather than only compile-clean
- once a target skill has already switched into the Loom Skill Orchestrator governance type, the stable wording should say the target skill is a Loom-governanced target skill and that its official execution surface is the public `dotnet so.dll run` and `dotnet so.dll resume` path against a runtime workflow copy
- if a creation or re-enhancement slice has not yet produced a real public run/resume chain to final `Done`, describe it as an in-progress or blocked enhancement slice rather than a normal governed completion state
- when a workflow route uses runtime-owned completion manifests to reference checked-in source deliverables, the route contract should declare both the checked-in source deliverable output families and the runtime-owned completion-manifest output family explicitly so done reachability does not collapse into governance-only evidence
- governed completion must cite a boundary-check/approval-gate trail covering every transition on the same external runtime copy: gate predicates checked, seam ownership verified, route coverage confirmed, and the explicit approval or structured non-human continuation that allowed each next step
- step kinds are explicit
- local tools are deterministic
- memory extraction is defined or derivable
- caller can send structured external results back
```

## Examples

For a full narrative example of a Loom-governanced target-skill run with stage gates, branch fan-out, validation, audit evidence, and Mermaid route diagrams, see [Loom-Governanced Skill Run Example](../../../en/examples/so-enhanced-skill-run.md).

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
  ## Loom-Governanced Runtime Lock

  This skill is enhanced by Loom SO.
  Authoritative SO runtime version lock: `assets/so-workflow/so-package-lock.json`.
  Routine SO runtime bundle restoration must resolve the exact locked bundle from NuGet first; if the local cache already holds that same version bundle, reuse it, otherwise download it again from NuGet.
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
- Letting a governed skill ask users to choose package/channel when the runtime version is already bound by the CI/CD-managed skill package version block or checked-in runtime lock.
