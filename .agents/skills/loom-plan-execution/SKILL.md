---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and Loom Agent Plan-Execution Orchestrator runtime surfaces.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the package and guide surface that matches the current CI/CD-managed skill package version block, then routes execution through the applicable Loom Agent Plan-Execution Orchestrator runtime surface.

Once the skill-bound package version or runtime source is chosen, this skill must first prove that the selected Loom Agent Plan-Execution Orchestrator runtime for that source is runnable and can execute the bare `dotnet ao.dll --guide` command successfully. The command reads the version-matched English docs shipped beside the executable in the runtime package and returns JSON containing the actual `version`, `docs_root`, and `guide_path` paths. Guide pages are not embedded in the executable. Before that proof exists, do not proceed to planning, authoring, validation, compile, `prompt-plan`, `prompt-replan`, run, resume, or any downstream input collection. Once the JSON result and readable `guide_path` exist, treat that guide as a hard governance handoff back onto the corresponding published AO package runtime surface for official execution. Do not let `--guide` become a detour that drifts back to repository builds, hand-assembled runtimes, or other non-governed paths.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local Loom Agent Plan-Execution Orchestrator repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill also enforces Loom Agent Plan-Execution Orchestrator-strong governance for official plan execution. In that governance model, Loom Agent Plan-Execution Orchestrator is the only official execution authority for this skill, only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs, and any direct non-Loom Agent Plan-Execution Orchestrator path stays outside official skill execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## Read This First

- Shared terminology authority: `../../../docs/en/architecture/workflow-terminology.md` (bilingual human-friendly status mapping; read it before any user-facing output).

- Runtime binding authority: this skill records only the exact bound runtime version. The platform-aware resolver selects channel, package, RID, executable, cache location, and launch path at runtime; do not hardcode or persist those details in skill-owned state.

## Published Runtime Transport Rule

Published Loom Agent Plan-Execution Orchestrator (AO), Loom Skill Orchestrator (SO), and SO-enhanced skills must never require MCP registration (`requireMCP=true` or equivalent). AO is CLI-only. SO skills may try MCP first when the host supports it, but MCP must remain optional: if registration/transport is unavailable or unsupported before dispatch, use the resolver-owned CLI. Reuse an already registered MCP server only when its runtime version and descriptor identity match; otherwise direct registration requires confirmed host support. A dispatched MCP operation failure is a failure, not a reason to conceal it with a CLI retry.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.
## Guide Hub Structure

The authoritative AO guide pages live under `../../../docs/en/guides/` and are packaged recursively into the runtime docs bundle. The extracted package uses `guides/ao-guide.md` as the `--guide` entry, with adjacent `ao-guide-flow.md`, `ao-guide-reference.md`, and `ao-guide-reference-<chapter>.md` pages. This skill publishes no AO guide files; use `reference/ao-skill-reference.md` for runtime acquisition and the fresh extracted guide for version-specific authority.
<!-- skill-package-version-block:start -->
- Current published AO package runtime version: `0.3.321`.
- This block is refreshed by the publish workflows whenever AO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->













































Follow the current skill package version block first, then derive the matching package surface:

- Package indexes remain skill-local references. The authoritative AO guide source is `../../../docs/en/guides/ao-guide.md`; the extracted runtime entry is `guides/ao-guide.md`. The target-local AO copies are complete package guide pages extracted from the exact bound runtime package; they support local inspection but never replace the fresh published-runtime `guide_path`.
- Do not add or publish any AO guide file under this skill; use the fresh guide returned by `dotnet ao.dll --guide`.

- Workflow designer subagent: `assets/agents/loom-plan-execution-workflow-designer.agent.md`
- Loom Skill Orchestrator governance baseline assets for AO enhancement:
	- Per-run plan output: `<execution-output-root>/plan/skill-plan.md` (runtime-owned; not a stable skill asset)
	- `assets/so-workflow/so-template.json`
	- `assets/so-workflow/so-package-lock.json`
	- `assets/so-workflow/node-to-file-map.md`
- `assets/so-workflow/reference/ao/runtime-contracts.md`
- `assets/so-workflow/reference/ao/runtime-behavior.md`
- `assets/so-workflow/reference/document-copy-manifest.json`

## Plain-Language Feedback For Every Language

All user-facing progress, blocked, error, and completion updates from this skill must be understandable to a high-school reader with no workflow background, in the language requested by the user. English is not automatically plain language, and the rule applies equally to every supported language.

Use short sentences, familiar words, and direct verbs. Say four things in order: what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and exactly what will happen next.

Do not make the reader translate status values, step kinds, node IDs, gate names, handoff terms, runtime details, or audit jargon. Explain a necessary technical word in ordinary language before showing its exact name. Keep commands, paths, IDs, and payload fields in a separate `Technical details` line only when they help the user act or verify the result. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the user-facing explanation; keep exact identifiers in technical details or evidence only. The same rule and the term examples below apply to any skill being enhanced feedback reported through AO; for the skill being enhanced instructions should carry a compact version of them.

## Plain-Language Term Examples

The left column is for machine records. The right column shows the meaning to express to a user. These English sentences are examples, not fixed output: translate the meaning into the user's requested language. English is not a substitute for simple language in another locale.

| Internal wording | Say this in the user's language |
| --- | --- |
| `Done` | "The requested work is complete." |
| `WaitResume` | "I need your information or confirmation before I can continue." |
| `SubagentCall` | "A specialist is checking this part." |
| `gate` | "A required check has not passed yet." |
| `transition` | "The next step is to ..." |
| `seam` or `boundary` | "The work is waiting at a handoff point." |
| `frontier` | "There are a few possible next actions." |
| `runtime` | "the program that is running the task" |
| `render unchanged` | "The earlier diagram is still valid; no new diagram was needed." |

### Example: output folder already has a record

Internal note: `step-0008-compiled` already exists; the render is unchanged.

User-facing update: "The task itself is fine. The output folder already has the earlier record, so this run did not overwrite it. The earlier diagram and report are still valid. I will use a new output folder and continue the same saved run."

### Example: the review found unresolved problems

Internal note: the review returned four findings, but the clean-review check was claimed before classification and repair.

User-facing update: "The review found four problems. The process stopped too early because it treated the review as finished instead of checking whether the problems were fixed. I will sort and fix the four problems, then check again."

### Example: waiting for a decision

Internal note: the task is waiting at a user-input step.

User-facing update: "I need one decision from you before I can continue: [state the decision in one short sentence]."

Do not copy the internal note into the user-facing update. Keep exact commands, paths, IDs, and evidence fields in a separate `Technical details` section when they are needed for action or verification.


## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Runtime version authority: the current CI/CD-managed skill package version block; derive `released` versus `beta` from that bound version when needed
- Guide input: run bare `dotnet ao.dll --guide`; it is English-only and returns JSON with `version`, `docs_root`, and `guide_path` instead of accepting a language flag
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during Loom Agent Plan-Execution Orchestrator-based plan execution:

- Loom Agent Plan-Execution Orchestrator is the only official execution authority for this skill; only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- Official AO runtime uses the exact version supplied by this skill and delegates channel, platform/RID, package identity, executable, cache location, and launch path to the platform-aware resolver. Automatic mode probes for a usable `Microsoft.NETCore.App 9.x` or higher-major host before package lookup: it selects the exact DLL/dependency/Roslyn closure when available, otherwise the exact-RID self-contained package. Explicit mode selection is allowed, and one resolution never acquires both closures.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- If runtime extraction, startup-contract checks, or guide execution fail, stop immediately and keep `runtime_preflight_result` and guide-refresh evidence in a failed state. Do not write success proof or treat failed command stderr as a guide; record only the successful JSON result and the readable `guide_path` returned by the runtime.
- In repo-src-debug mode, build and use the current repository Loom Agent Plan-Execution Orchestrator output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state under `session_dir` or explicit execution-output roots.
- Write valid workflow, template, schema, demo, runtime-copy, audit-backup, and compile-feedback JSON outputs as indented multi-line JSON. Keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Output targets may be outside the Git worktree or ignored by Git. Return normalized real paths, verify each output exists and is readable, and use a verified workspace-relative mirror for direct editor opening when `--workspace-root` is available; Git tracking is never a delivery condition.
- After every AO CLI call or audit-producing step, including `dotnet ao.dll` and self-contained `ao.exe` (or `ao`) calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, followed by a localized `##` execution-confidence heading and one short reason. Use only current or latest verified paths; on `not_emitted`, say that the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. All user-facing progress, blocked, error, and completion text must use plain words in the active interaction language; keep workflow-only labels such as `FPx` and `xxx_preflight_xxx` in technical details or evidence only.
- For `runtime_path_only`, keep verified absolute paths as technical evidence; use workspace-relative links only when `link_resolvable=true`.
When the current VS Code host cannot dynamically register or launch MCP servers, use the resolver-owned runtime descriptor directly for the official AO CLI workflow. MCP is optional transport support and must not block the bare `dotnet ao.dll --guide` preflight or the public `dotnet ao.dll compile`, `run`, and `resume` chain.

- For every full-delivery execution of this skill itself, `dotnet ao.dll compile` is never an end state. It is only a validation checkpoint; `--guide`, `prompt-plan`, `prompt-replan`, and helper scripts are preparation or recovery surfaces only.
- After the guide handoff, create or reuse one fresh external runtime workflow instance copy and record its immutable instance identity, workflow-file path, and persisted runtime-state/session path. Run `dotnet ao.dll compile` against that exact external copy, then immediately dispatch the public `dotnet ao.dll run` against the same copy. Every later `dotnet ao.dll resume` must reference the same persisted instance and state; never switch to a new workflow copy between compile, run, or a block. Do not stop at a preflight explanation, local-source debug output, planning output, compile output, or a blocked-state description when the user has required execution.
- If `run` returns a runtime-owned block, continue with `dotnet ao.dll resume` against that exact workflow instance and persisted state. If the runtime reports a recoverable failure, preserve its evidence and resume from the previous state on the same persisted instance. Repeat until the AO runtime reaches its terminal completed state; a blocked payload alone is never a terminal outcome. Stop only when the failed instance has no recoverable previous state or the official runtime cannot start, and preserve that failure evidence.
- Never claim AO-governed completion from local orchestration, direct scripts, repo-source debug execution, compile success, guide success, prompt planning, prompt replanning, or an unresumed block. The completion report must state the official command chain, final runtime status/frontier, and the event-log and audit evidence paths.
- When the official runtime cannot be started, the result is failed preflight, not governed completion. Preserve the failure evidence and do not substitute a local or helper execution path.

Detailed assumptions, startup contracts, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/ao-skill-reference.md`

Workflow generation or revision for this skill must use the local workflow-designer subagent with context-rich relative links, not a freeform generic agent call:

- `assets/agents/loom-plan-execution-workflow-designer.agent.md`

When using that workflow-designer route, enforce deterministic workflow authoring contracts instead of descriptive-only prose:

- each transition must declare executable `guardExpression` and `succeedExpression` predicates, explicit evidence outputs, and explicit seam ownership
- each gate must declare machine-checkable pass predicates, required evidence references, and route coverage mapping
- weave-out hints must preserve resume continuity contract fields and expected payload/evidence shapes
- final workflow proposals must include transition, gate, and ownership preflight checklists before JSON output is accepted
- fail closed: reject vague transition/gate wording that lacks concrete predicates or evidence paths

That exact `.agent.md` file is the authoritative behavior source for the workflow-designer subagent. Do not require it to be mirrored into `.github/agents/`, a user-profile agent folder, or any other discoverable agent root before use. If the runtime can resolve the exact subagent name directly, invoke that name directly while keeping the declared `.agent.md` file as the contract. If direct name resolution is unavailable, resolve the declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate agent role.

## Workflow Designer Reference Pack

Before dispatching `assets/agents/loom-plan-execution-workflow-designer.agent.md`, the caller must provide a bounded `referencePackManifest` and fresh `schemaDemoInput` from the exact AO runtime. The pack must include the successful guide JSON and returned guide file, same-runtime schema/demo/demo compile audit, current target contract and requirements, current workflow source, applicable `AGENTS.md`, and latest compile feedback when revising. Every entry carries a normalized path, SHA-256, exact runtime version, authority role, read status, and validation result. An older workflow is `previous_runnable_reference` only and requires a version/hash/difference/rejected-item disposition.

The designer must return runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` with schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Keep descriptors with path, SHA-256, schemaVersion, verdict, and exact runtime version. A required semantic probe that is failed or unknown prevents readiness; compile success alone is not semantic evidence.

## Runtime Mode Separation

Resolve the runtime mode before any package-cache lookup or network request. The two package paths are independent and must not be combined.

- Automatic mode probes the local host before any package-cache lookup or network request. A usable `Microsoft.NETCore.App 9.x` or higher-major host selects framework-dependent DLL/dependency/Roslyn mode; no usable host selects one exact-RID self-contained package. Explicit mode selection is allowed. The selected mode is immutable for one resolution and its failure does not trigger the other package closure.
- `.NET CLI mode` is explicit. Only this mode validates and acquires the same exact-version .NET runtime bundle (a NuGet restore set that includes the embedded Roslyn compiler assemblies used by the C# expression evaluator), checks the `.dll`, `.deps.json`, `.runtimeconfig.json`, Roslyn, and dependency closure, then launches through the shared .NET host.
- Raw framework `.nupkg` files are acquisition inputs, not runnable bundle roots. The resolver must stage one unified bundle and generate `ao.deps.json` beside `ao.dll`; the skill must verify that generated file and the exact package closure before the guide gate.
- Once a mode is selected, a failure stays in that mode and fails closed. Do not fall back from `.NET CLI mode` to self-contained or from self-contained to `.NET CLI mode` after startup or package acquisition begins.
- Runtime evidence must identify `runtime_mode`, exact version, package ids, RID, cache validation, launch descriptor, and failure category. Never report a self-contained RID package as a .NET runtime bundle.

## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm the current skill-bound package version, derive channel from its version shape when needed, and confirm runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build Loom Agent Plan-Execution Orchestrator from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: restore the exact product/Common/Abstractions/Roslyn closure into the resolver-owned unified framework bundle, use ZIP-based extraction for `.nupkg` on Windows PowerShell 5.1, generate and validate `ao.deps.json` from that closure, run startup-contract preflight, and use explicit launch mode. Never pass a raw product `.nupkg` extraction directly to `--guide`.
3. Prove the selected runtime can run the bare `--guide` command, parse its JSON result, and read the returned `guide_path` and `docs_root` before proceeding.
4. Only after that guide result exists, run planning surfaces (`prompt-plan`) and capture required prompt blocks.
5. When creating or revising a workflow, invoke the local workflow-designer subagent and give it the relevant skill files, guide files, plan files, and audit artifacts through relative links.
6. Materialize one external WorkflowInstance copy outside skill paths, record its immutable instance identity and persisted runtime-state/session path, then run `compile` against that exact copy.
7. Run Loom Agent Plan-Execution Orchestrator with that same external WorkflowInstance copy; every later `resume` must reuse its persisted runtime state.
8. On blocked state, use payload signals plus `prompt-replan` to update seam nodes, then `resume` with structured envelope payload.
9. When the current route is confirmed blocked, persist the current workflow state, blocker report, all attempted remedies and their outcomes, and the relevant event/audit references before asking the AO planner to replan.
10. Replan from the retained history by selecting an explicit strategy: continue from the current state, roll back to an unconfirmed design node, redesign from the current state, replace the whole plan, or apply a smallest reversible workaround.
11. Require the planner to return a viable path to the terminal business outcome, including a rollback or workaround path when selected, before resuming execution.
12. Repeat replan/resume until Loom Agent Plan-Execution Orchestrator reaches completed state.
13. Report completion only when Loom Agent Plan-Execution Orchestrator is completed and requested business deliverables are verifiable.

For AO workflow design and AO weave-out planning, prefer existing capable subagents whenever they can already complete the weave-out goal instead of emitting generic agent placeholders.

Operational details for prompt blocks, payload conventions, and blocked-state handling are defined in reference docs.

## Required Outputs

- bound runtime version confirmation with derived released/beta evidence and matching canonical links
- runtime source selection and version-derived channel resolution metadata
- package-channel runtime facts: version, bundle list, unified runtime directory, generated `ao.deps.json` path, package-closure validation, preflight result, and launch mode
- package-channel runtime acquisition facts when Windows PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast evidence when extraction or guide generation fails
- workflow/session/event paths and audit artifact links
- `workflow.compile-feedback.json` and the shared `workflow.compile-feedback.v1` result, including parse/validation status, counts, phase blockers, candidate path/hash, and AO runtime identity/version.
- Verified real paths for every output artifact, including workflow, feedback, audit, Mermaid, and HTML files outside the Git worktree or under ignored workspace mirrors.
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- required think-out-loud fields for runtime and audit updates
- After every AO CLI call or audit-producing step, including `dotnet ao.dll` and self-contained `ao.exe` (or `ao`) calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, followed by a localized `##` execution-confidence heading and one short reason. Use only current or latest verified paths; on `not_emitted`, say that the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. All user-facing progress, blocked, error, and completion text must use plain words in the active interaction language; keep workflow-only labels such as `FPx` and `xxx_preflight_xxx` in technical details or evidence only.
- business deliverable verification summary when business-first mode applies
- For `runtime_path_only`, keep verified absolute paths as technical evidence; use workspace-relative links only when `link_resolvable=true`.

For the full output matrix and field-level contracts, use reference docs.

## Prohibited Results

Reject or mark invalid any execution result that says or implies any of these:

- AO is optional for official skill execution
- AO and another path are parallel official execution modes for this skill
- AO runtime artifacts alone are accepted as final completion when the caller explicitly requested business outputs
- business-output requests are silently downgraded to runtime/meta-only execution without explicit user approval
- `compile`, `--guide`, or helper shell steps are normal skill run modes
- `compile`, `--guide`, `prompt-plan`, `prompt-replan`, or helper shell steps are normal skill run modes
- non-AO output can count as official skill execution history
- non-AO tests can count as official skill execution evidence
- prose flow or examples are official execution authority by themselves

## Completion Criteria

Do not treat execution as properly governed until all of these conditions hold:

- only explicit `dotnet ao.dll run` or `dotnet ao.dll resume` counts as an official skill run
- `dotnet ao.dll compile`, `dotnet ao.dll --guide`, `dotnet ao.dll prompt-plan`, and `dotnet ao.dll prompt-replan` are documented as preparation, validation, or authority-supporting surfaces only
- skill-level history only comes from AO workflow state, session state, event logs, or audit artifacts
- blocker history must retain the blocked node, blocker reason, attempted actions, outcomes, evidence references, and the selected replan anchor/strategy
- the planner must receive that retained history as input and must not silently discard failed attempts or prior route decisions
- a replan is invalid unless it declares a path from its selected anchor to the terminal business outcome and preserves a one-step rollback plan for any workaround
- skill-level checklist only comes from AO workflow nodes, frontiers, transitions, blocked states, and resume points
- skill-level run map only comes from the AO runtime `workflow_file`, `next_frontier`, blocked state, and audit artifacts
- skill-level evidence only comes from AO-owned runtime state and audit artifacts
- non-AO tests do not count as official skill execution evidence
- prose flow and helper command examples are explanatory only, not execution authority
- when caller objectives explicitly request business outputs, AO completion state alone is insufficient without the corresponding business deliverables

Detailed prohibited/acceptance examples are maintained in reference docs.
