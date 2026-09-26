---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and Loom Agent Plan-Execution Orchestrator runtime surfaces.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the package and guide surface that matches the current CI/CD-managed skill package version block, then routes execution through the applicable Loom Agent Plan-Execution Orchestrator runtime surface.

Once the skill-bound version is chosen, detect one supported host RID, acquire and verify that exact published AO self-contained package, extract it safely, then directly run `ao.exe --guide` on Windows or `ao --guide` on Unix. Verify the JSON version and readable contained `guide_path` before planning or downstream work. The package carries the English docs; no guide page is embedded in the apphost.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local Loom Agent Plan-Execution Orchestrator repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill uses Loom Agent Plan-Execution Orchestrator as the official execution authority. Only direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix count as official skill runs. Other paths remain outside official execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## Read This First

- Shared terminology authority: `../../../docs/en/architecture/workflow-terminology.md` (bilingual human-friendly status mapping; read it before any user-facing output).

- Runtime binding authority: this skill records only the exact bound AO package version. The host derives OS, architecture, Linux libc, RID, and apphost from the current machine; do not persist those transient details in checked-in skill state.

## Published Runtime Transport Rule

Published AO execution uses its direct self-contained apphost and never requires MCP. Optional SO/MCP support is a separate later workflow concern; it cannot gate AO package acquisition or guide capture. A dispatched MCP application failure remains a failure.

## Named Agent Resolution

Every `.agent.md` named by this skill is the exact behavior contract for that role; host registration is only a dispatch mechanism.

- First invoke the exact declared agent name. Resolve the exact `.agent.md` named by this skill from its `assets/agents/` folder. In standalone installs, resolve that same relative path under the active skill root, such as `~/.agents/skills/<skill-folder>/` or `~/.claude/skills/<skill-folder>/`.
- If the host responds `agent not found`, do not switch to a similar role or treat the failure as a review result. If the exact matching `.agent.md` exists, invoke an available registered generic subagent only as the driver and pass the exact file path, its full contents, and all required inputs and reference context. The driver must perform the declared role and return its declared output contract.
- Never pass only a path or summary in place of the full file. Do not use a read-only agent when the contract requires design, editing, validation, or other work.
- If the file is missing or ambiguous, or no available driver can perform its contract, stop at this step, preserve failed evidence, and report the concrete blocker. Do not claim completion or advance dependent checks. A direct/manual fallback requires explicit user approval.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.
## Guide Hub Structure

The authoritative AO guide pages live under `../../../docs/en/guides/` and are packaged recursively into the runtime docs bundle. The extracted package uses `guides/ao-guide.md` as the `--guide` entry, with adjacent `ao-guide-flow.md`, `ao-guide-reference.md`, and `ao-guide-reference-<chapter>.md` pages. This skill publishes no AO guide files; use `reference/ao-skill-reference.md` for runtime acquisition and the fresh extracted guide for version-specific authority.
<!-- skill-package-version-block:start -->
- Current published AO package runtime version: `0.3.324`.
- This block is refreshed by the publish workflows whenever AO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->















































Follow the current skill package version block first, then derive the matching package surface:

- Package indexes remain skill-local references. The authoritative AO guide source is `../../../docs/en/guides/ao-guide.md`; the extracted runtime entry is `guides/ao-guide.md`. The target-local AO copies are complete package guide pages extracted from the exact bound runtime package; they support local inspection but never replace the fresh published-runtime `guide_path`.
- Do not add or publish any AO guide file under this skill; use the fresh guide returned by direct `ao.exe --guide` or `ao --guide` from the exact locked package.

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
- Guide input: directly run `ao.exe --guide` on Windows or `ao --guide` on Unix; it is English-only and returns JSON with `version`, `docs_root`, and `guide_path`.
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during Loom Agent Plan-Execution Orchestrator-based plan execution:

- Loom Agent Plan-Execution Orchestrator is the only official execution authority for this skill; official runs use direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- Official AO runtime uses the exact version supplied by this skill and the single product+RID package selected by the host. No host probe, DLL closure, second runtime mode, or cross-mode fallback is supported.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- If package validation, extraction, apphost startup, or guide execution fails, stop with failed evidence. Never record success from stderr; keep only the successful JSON result and readable `guide_path` returned by the direct apphost.
- In repo-src-debug mode, build and use the current repository Loom Agent Plan-Execution Orchestrator output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state in the same external workflow file or explicit execution-output roots.
- Write valid workflow, template, schema, demo, runtime-copy, audit-backup, and compile-feedback JSON outputs as indented multi-line JSON. Keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Output targets may be outside the Git worktree or ignored by Git. Return normalized real paths, verify each output exists and is readable, and use a verified workspace-relative mirror for direct editor opening when `--workspace-root` is available; Git tracking is never a delivery condition.
- After every AO apphost call or audit-producing step, including `ao.exe` and `ao` calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, followed by a localized `##` execution-confidence heading and one short reason. Use only current or latest verified paths; on `not_emitted`, say the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. Keep user-facing text plain and in the active interaction language.
- For `runtime_path_only`, keep verified absolute paths as technical evidence; use workspace-relative links only when `link_resolvable=true`.
AO is CLI-only. Execute the extracted AO apphost directly; dynamic MCP registration, a runtime descriptor, and an MCP preflight are not required for guide, compile, run, or resume.

- For every full-delivery execution of this skill itself, direct apphost compile is validation only; it is not an end state.
- After guide handoff and compile validation, use one fresh external runtime workflow copy. Run the same apphost against that copy, then use that apphost and persisted state for every resume until terminal completion.
- If the runtime returns a blocked state, preserve its evidence and resume the same workflow copy. Continue until terminal completion or a documented unrecoverable failure; a blocked payload alone is never completion.
- Never claim governed completion from local orchestration, direct scripts, repository-debug output, guide success, prompt planning, compile success, or an unresumed block.
- If the published apphost cannot start, preserve failed package/startup evidence and stop. Do not substitute a local or helper execution path.

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

## Self-Contained Runtime Contract

There is one package-channel runtime path: the exact published AO product+RID package for the current host.

- Detect exactly one supported RID from OS, architecture, and Linux libc. Use the exact AO version bound by the skill.
- Reuse only a valid exact package in the standard NuGet global-packages cache. On a miss, verify exact NuGet bytes against `catalogEntry.packageHash`; a same-version GitHub Release fallback requires a valid `.sha512` sidecar.
- Before extraction, verify package ID, exact version, RID, SHA-512, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide files. Extract only after all checks pass.
- Run `ao.exe --guide` on Windows or `ao --guide` on Unix immediately after extraction. Verify its version and readable contained paths before downstream work.
- Use the same extracted apphost for schema/demo, compile, run, and resume. A failed package, extraction, startup, or guide check fails closed.
- Do not require an installed .NET host, DLL mode, runtime resolver, launch descriptor, fixed bootstrap script, or Loom-specific cache.

## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm the current skill-bound package version, derive channel from its version shape when needed, and confirm runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build Loom Agent Plan-Execution Orchestrator from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: acquire and verify the exact AO product+RID package, safely extract it outside the skill folder, then run its direct apphost `--guide`. No DLL, Roslyn bundle, generated `.deps.json`, or framework startup contract is used.
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
- package-channel runtime evidence: exact package ID/version/RID, source, verified SHA-512, nuspec/manifest/archive checks, extraction result, apphost path, and fresh guide result
- package-channel runtime acquisition facts when Windows PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast evidence when extraction or guide generation fails
- workflow/session/event paths and audit artifact links
- `workflow.compile-feedback.json` and the shared `workflow.compile-feedback.v1` result, including parse/validation status, counts, phase blockers, candidate path/hash, and AO runtime identity/version.
- Verified real paths for every output artifact, including workflow, feedback, audit, Mermaid, and HTML files outside the Git worktree or under ignored workspace mirrors.
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- required think-out-loud fields for runtime and audit updates
- After every AO apphost call or audit-producing step, including `ao.exe` and `ao` calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, followed by a localized `##` execution-confidence heading and one short reason. Use only current or latest verified paths; on `not_emitted`, say the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. Keep user-facing text plain and in the active interaction language.
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

- only direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix counts as an official skill run
- direct apphost `compile`, `--guide`, `prompt-plan`, and `prompt-replan` are preparation or validation surfaces only
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
