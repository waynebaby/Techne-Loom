---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill for Loom Skill Orchestrator (`dotnet so.dll`).

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the Loom Skill Orchestrator package flow. Business scope is always target-skill delivery; runtime validation is supporting work only.

Every enhancement pass must first prove that the skill-bound published Loom Skill Orchestrator runtime is runnable. Immediately after that exact runtime preflight, before guide capture, planning, authoring, validation, compile, run, resume, or any downstream input collection, start that runtime's local `mcp stdio` server and use `so_inspect_workflow_fragment` against the same external workflow copy. Complete `initialize` and `notifications/initialized`, record `mcp_startup_evidence`, and stop on MCP failure. Only after the MCP check succeeds may the pass execute the bare `dotnet so.dll --guide` command, parse its JSON, and read the returned `guide_path`. This MCP-first rule applies equally to ordinary target-skill enhancement and `/loom-skill-enhancement` self-bootstrap; it describes the governed SO workflow, not the current editor's `mcp.json`. Derive runtime channel from the bound package version when needed; do not ask the user to choose released versus beta during normal SO enhancement runs.

## Read First

- Shared terminology authority: ../../../docs/en/architecture/workflow-terminology.md (bilingual human-friendly status mapping; read it before any user-facing output).

- Runtime binding authority: this skill records only the exact bound runtime version. The platform-aware resolver selects channel, package, RID, executable, cache location, and launch path at runtime; do not hardcode or persist those details in skill-owned state.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.
## Workflow Identity

Every root `templateKind: so-governed-target-skill` workflow declares `taskType`, `workflowKind`, `caseId`, and `runId`. Use `skill_enhancement` with `so_self_bootstrap` for self-bootstrap or `target_skill_enhancement` for an outer enhancement run. Use a target-specific business task with `target_skill_business` for target business work. `caseId` remains the business-case link; a checked-in template may mark `runId` with `template:` and the first fresh materialization or `ReadyToStart` run replaces it with one generated `run-<guid>`. Compile, run, resume, audit, and completion evidence for that external copy must preserve the same runId.
## Guide Hub Structure

The authoritative SO guide pages live under `../../../docs/en/guides/` and are packaged recursively into the runtime docs bundle. The extracted package uses `guides/so-guide.md` as the `--guide` entry, with adjacent `so-guide-flow.md`, `so-guide-reference.md`, and `so-guide-reference-<chapter>.md` pages. This skill publishes no SO guide files; use `reference/so-skill-reference.md` for runtime acquisition and the fresh extracted guide for version-specific authority.
<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.3.258-beta`.
- This block is refreshed by the publish workflows whenever SO package versions change, so the skill contract stays aligned with the latest published beta package set.
<!-- skill-package-version-block:end -->




































- Released package index: `reference/packages.released.md`
- Beta package index: `reference/packages.beta.md`
- Target-local SO reference copies: complete package guide pages at `assets/so-workflow/reference/so/runtime-contracts.md` and `assets/so-workflow/reference/so/runtime-governance.md`.
- Copy provenance: `assets/so-workflow/reference/document-copy-manifest.json`.
- Authoritative SO guide source: `../../../docs/en/guides/so-guide.md`; the extracted runtime entry is `guides/so-guide.md`.
- Do not add or publish any SO guide file under this skill; use `reference/so-skill-reference.md` for runtime acquisition and the fresh guide returned by `dotnet so.dll --guide` for version-specific authority.
- Workflow designer subagent: `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`
- MCP-first startup subagent: `assets/agents/loom-skill-enhancement-mcp-startup.agent.md`
- Reusable weave-out subagents:
	- `assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md`
	- Re-enhancement strategy reviewer: `assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md`
	- `assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md`
	- `assets/agents/loom-skill-enhancement-review-fix-loop.agent.md`
- `assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md`
	- `assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md`
	- `assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md`
	- `assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md`
- Authority command: bare `dotnet so.dll --guide`; parse its JSON result and read the returned `guide_path` first, then inspect `docs_root` only when necessary

## Plain-Language Feedback For Every Language

All user-facing progress, blocked, error, and completion updates from SO and every target skill it creates or updates must be understandable to a high-school reader with no workflow background, in the language requested by the user. English is not automatically plain language, and the rule applies equally to every supported language.

Use short sentences, familiar words, and direct verbs. Say four things in order: what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and exactly what will happen next.

Do not make the reader translate status values, step kinds, node IDs, gate names, handoff terms, runtime details, or audit jargon. Explain a necessary technical word in ordinary language before showing its exact name. Keep commands, paths, IDs, and payload fields in a separate `Technical details` line only when they help the user act or verify the result. When this skill creates or updates a target skill, copy the same rule, a compact term-conversion table, and at least one before/after example into the target `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints.

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


## Workflow Contract

### Inputs

- target skill root path that directly contains `SKILL.md` and `assets/so-workflow/`
- deterministic skill goal or upgrade request
- requested target-skill changes
- runtime version authority: the checked-in `assets/so-workflow/so-package-lock.json` plus the current CI/CD-managed skill package version block; derive channel from the bound version shape when needed instead of asking the user
- Guide input: run bare `dotnet so.dll --guide`; it is English-only and returns JSON with `version`, `docs_root`, and `guide_path` instead of accepting a language flag
- optional JSON context file
- optional audit output root

### Self-Bootstrap Assets

- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`
- `assets/so-workflow/restore-so-runtime.ps1`

### Defaults

- The planning artifact is runtime-owned: write plan/skill-plan.md under the current exec-<timestamp>-loom-skill-enhancement-result/ output root and pass its path/hash through workflow context. Do not require or publish a checked-in assets/so-workflow/skill-plan.md.
- Keep stable Loom Skill Orchestrator-owned template, lock, reference, and agent materials under `assets/so-workflow/`; keep mutable plans and run checklists under the execution output root.
- For every Loom-governanced target skill, official workflow operations and package downloads must use the published Loom Skill Orchestrator package artifacts bound to the current CI/CD-managed skill version block and checked-in package lock, not repository source builds, ad hoc local project outputs, or hand-assembled runtime folders, unless the user explicitly approves a last-resort blocked-state workaround.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- Treat the checked-in workflow template as immutable; every new official SO run must start from a freshly copied external runtime workflow file derived from the template or current checked-in source workflow, and any later resume in that same execution chain must continue against that same persisted runtime copy rather than mutating the checked-in file in place.
- Keep compile and audit artifacts outside the skill folder unless the user explicitly chooses otherwise.
- Write valid workflow, template, schema, demo, runtime-copy, audit-backup, analysis, dataflow, and compile-feedback JSON files as indented multi-line JSON. Keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Output targets may be outside the Git worktree or ignored by Git. Return the normalized real path for every output, verify that it exists and is readable, and use a verified workspace-relative mirror for direct editor opening when a workspace root is available; Git tracking is never a delivery condition.
- In exclusive Loom Skill Orchestrator governance mode, only `dotnet so.dll run` and `dotnet so.dll resume` count as official runs.
- Official SO runtime uses the exact version supplied by this skill and delegates channel, platform/RID, package identity, executable, cache location, and launch path to the platform-aware resolver. Self-contained is the resolver default; `.NET CLI mode` (`dotnet so.dll`) and repository-source debug mode are opt-in only.
- Normal enhancement governance for this skill and any Loom-governanced target skill must stay on the selected runtime's `mcp stdio` startup/use step, then `dotnet so.dll --guide`, `dotnet so.dll compile`, `dotnet so.dll run`, and `dotnet so.dll resume`. Do not treat direct workflow JSON edits as a routine control path.
- Direct edits to the running external workflow `.json` copy are allowed only when the current `dotnet so.dll` path is fully blocked, the user explicitly approves a minimal workaround, the edit is the smallest change needed to unblock the next SO command, and the very next step returns to `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume`.
- When unattended-mode execution is explicitly declared in-session, a minimal autonomous workaround may be used only after a structured trade-off evaluation pass confirms that expected benefit clearly exceeds risk and that the change is reversible in one rollback step. Always emit a decision-evidence report and then return immediately to the normal `dotnet so.dll` governed path.
- If runtime extraction, startup-contract checks, MCP startup/use, or guide execution fail, stop immediately and keep runtime, `mcp_startup_evidence`, and guide-refresh evidence in a failed state. Do not write success proof or treat failed command stderr as a guide; record only successful MCP evidence and the successful JSON result with its readable `guide_path`.
- After every SO CLI call or audit-producing step, including `dotnet so.dll` and self-contained `so.exe` (or `so`) calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file`, verify existence and readability, and never guess an audit path. If `--workspace-root` was supplied, use only the hash-verified `workspace_relative_mermaid_file` and `workspace_relative_html_file` values for clickable workspace links. If the card tool is available, pass `card_input_file` directly to it without asking another agent to return the file contents; a card is not proof of artifact delivery. Otherwise show the verified Mermaid link first and the HTML link second. When no fresh render exists, reuse only a previously verified link and state that the render is unchanged. On `delivery_failed`, report the failure and do not emit a guessed link.
- Do not infer unattended mode from prior turns. For each critical decision boundary, re-confirm current attended versus unattended status instead of reusing stale status assumptions.

### Exact-Version Runtime Cache

- Treat `assets/so-workflow/so-package-lock.json` as the only version authority for this skill. Read its `resolved_version` and derive the channel from that locked value; do not ask for or discover a newer version during the restore.
- Before any network request, inspect the local NuGet cache for all three locked packages: `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`.
- A cache hit is valid only when all three `.nupkg` files are present and their package ids, exact versions, and nuspec identities match the lock. A partial, corrupt, or mismatched cache is a miss for the bundle.
- Use `assets/so-workflow/restore-so-runtime.ps1` for the Windows-compatible check. A valid hit returns `cache_hit: true` and `downloaded_packages: []`; a miss downloads only the exact locked version through direct NuGet URLs. Latest package resolution and `*.latest.nupkg` aliases are forbidden on this path.
- Retain `cache_hit`, `downloaded_packages`, `cache_validation`, `resolved_runtime_version`, and `runtime_bundle_packages` as runtime evidence before the startup-contract and guide gates.

### Verified Audit-Step Reuse

- `dotnet so.dll copy-audit-step` is a supporting artifact command, not an official run or resume. It copies only a previously verified audit step to a new output position and writes `audit-reuse.json` with source paths, source hashes, verifier, reason, `artifact_origin: verified-copy`, and `official_execution_evidence: false`.
- The source must contain `workflow.mermaid.md`, `workflow.html`, and `workflow.json`; existing `workflow.analysis.json`, `workflow.dataflow.json`, and `summary.json` are copied when present. Source and destination files are hash-checked, and any non-empty destination step is rejected without overwrite.
- For `run` / `resume` reuse, compare the stable workflow graph/configuration projection and reject structural drift. Compare source Mermaid/HTML with the current render: copy exact matches and regenerate changed renders from the current instance. Always write the current runtime instance's `workflow.json` and fresh analysis/dataflow files when available; record copied and replaced file names in `audit-reuse.json`.
- Every actual `run` and `resume`, including state changes, external results, gate evaluation, and event logging, remains official and must not be skipped because an audit step was copied.
### Boundary Check And Approval Gate (Compulsory)

This gate applies to every Loom-governanced target skill that this skill enhances. Both the SO skill's own execution and its enhanced skills are compelled onto the Loom Skill Orchestrator-governanced route: no next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step.

- A **boundary check** is the machine-readable validation of every transition before advancing. It must confirm `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state it must satisfy gate predicates (`passExpression` / `succeedExpression`) over runtime evidence, plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) an explicit approval/instruction from the user at `AskUser` seams for declared user-owned fields or decisions, or (b) a structured non-human continuation payload whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, an unapproved draft copy, local orchestration, direct workflow JSON edits, or an MCP startup without a real fragment call — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Every governed target-skill enhancement slice must apply this gate at every transition on the same external runtime copy until final `Done`.

### Non-Negotiable Official Execution Gate

- For every full-delivery enhancement or re-enhancement of a Loom-governanced target skill, `dotnet so.dll compile` is never an end state. It is only a validation checkpoint and one boundary-check precondition.
- After the MCP-first check and guide handoff, create or reuse one fresh external runtime workflow instance copy and record its immutable instance identity, workflow-file path, and persisted runtime-state/session path. Run `dotnet so.dll compile` against that exact external copy, pass the compile-boundary check, then immediately dispatch the public `dotnet so.dll run` against the same copy. Every later `dotnet so.dll resume` must reference the same persisted instance and state; never switch to a new workflow copy between compile, run, or a block. Do not stop at a preflight explanation, local geometry/tool validation, a draft workflow, compile output, or a blocked-state description when the user has required execution.
- If `run` returns a runtime-owned block, pass that boundary check and continue with `dotnet so.dll resume` against that exact workflow copy and persisted state. If the runtime reports a recoverable failure, preserve its evidence and resume from the previous state on the same persisted copy. Repeat until final `Done`; a blocked payload alone is never a terminal outcome and never approval to skip the next gate. Stop only when the failed instance has no recoverable previous state or the official runtime cannot start, and preserve that failure evidence.
- Never claim Loom-governanced completion from local orchestration, direct scripts, compile success, a guide result, a materialized workflow copy, or an unresumed block. The completion report must state the official command chain, final runtime status/node, the boundary-check/approval trail at each transition, and the event-log and audit evidence paths.
- When the official runtime cannot be started, the result is failed preflight, not governed completion. Preserve the failure evidence and do not substitute a local workflow execution.
### Shared Context And Batch Verification

Build one bounded shared review context after MCP-first runtime proof and fresh guide capture, before any independent enhancement review. The context is produced once from real checked-in asset snapshots and runtime inputs. It carries a source manifest, bounded snapshots, guide/schema/runtime references, a deterministic `context_hash`, and the identity of the same external workflow copy. Independent subagents read this context by reference; they do not rebuild or silently widen it.

Use `ConcurrencyStrategy.All` for independent external `SubagentCall` reviews and validations. The SO runtime registers every expected transition in one persisted batch and keeps the workflow at the current state until every result has been returned. A missing or duplicate result fails closed. Use this only for independent external transitions with one shared target state; keep synchronous mutations and ordered checks in separate serial groups.

After each parallel review batch, run one explicit aggregation step that preserves every finding, source, severity, and disposition (`accepted`, `rebutted`, or `needs_validation`). Run one coordinated batch repair against the complete aggregate. Do not launch one rewrite per finding. Then run the independent post-fix validators as a second parallel batch, aggregate their results, and finish with one serial validation stage for JSON, graph/dataflow, compile, and ordered runtime checks before official run/resume continuation.

This batching policy belongs to SO enhancement planning and delivery governance. It is not a generic AO/SO runtime Review engine and does not change AO behavior.

### Workflow Baseline

- Enter plan mode before editing target-skill deliverables.
- When creating or revising workflow templates, invoke the local workflow-designer subagent with relative-link context, not a freeform generic agent.
- When a route names a specific local or target-skill `.agent.md` file, treat that exact file as the only authoritative subagent contract. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders before use.
- If direct exact-name subagent resolution is available, invoke that exact subagent name while keeping the named `.agent.md` file as the authority. If direct resolution is unavailable, resolve the named `.agent.md` path from the current repository/workspace copy first and the corresponding global installed-skill copy second, then pass the resolved file path plus the full file content into the subagent-driving call.
- Do not replace a named `.agent.md` route with a freeform approximate role, a repository-global prompt, or an ad hoc summary of the intended subagent behavior.
- Analyze inputs, outputs, branches, loops, seams, gates, and expected evidence.
- Generate the workflow template JSON first, then compile it.
- Keep the workflow template JSON as the authority.
- Repeat a user confirmation loop by updating the template or its source planning inputs and recompiling.
- For target-skill templates that declare root `templateKind: so-governed-target-skill`, also declare a root `validation` contract with `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`.
- For full-delivery target-skill templates with root `templateKind: so-governed-target-skill`, the materialized runtime workflow copy must execute on the current public `dotnet so.dll run` and `dotnet so.dll resume` path until final `Done`. Do not leave the runnable copy in `Drafting`, and do not depend on private or unavailable built-in tool names.
- If a checked-in workflow JSON is only a draft or compile-review source template, label it explicitly as source-only and do not present it as directly runnable.
- When `MemoryRead` inspects checked-in target-skill assets, it must load real file snapshots from an explicit target-skill asset root and must reject absolute paths or traversal outside that root.
- Never author a node whose purpose says or implies `run a multistep plan`.
- For weave-out design, prefer existing capable subagents whenever they can already complete the goal instead of emitting generic agent placeholders.
- If a target-skill enhancement weave-out is clearly reusable and benefits from a dedicated subagent, recommend creating a detailed `{target-skill-name}-{task-name}.agent.md` under `{skill-folder}/assets/`, route future workflow nodes to that subagent explicitly, add a relative-link reference to that file in the target `SKILL.md`, and reference the same relative path in the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.
- For any target-skill local subagent route introduced under `{skill-folder}/assets/`, the target skill must treat that exact `.agent.md` file as the subagent's authority source during both documentation handoff and runtime invocation. Resolution should test the target skill's repository/workspace copy first and the corresponding global installed-skill copy second before failing.

### Required Outputs

- bound runtime version confirmation plus derived channel evidence
- runtime-ready evidence for the selected published SO bundle before downstream work
- MCP-first startup/use evidence from the same external workflow copy, including initialize, notifications/initialized, and a bounded so_inspect_workflow_fragment call
- Windows package-channel runtime acquisition evidence when PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast proof when extraction or guide generation fails
- package index links
- guide surface references
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- target `SKILL.md` workflow-file language wording that requires English as the canonical information carrier for workflow-owned schema and control metadata, while preserving source/request-language user and business payloads
- target `SKILL.md` governance wording that keeps ordinary workflow changes on the SO CLI path and limits direct workflow JSON edits to blocked-state, user-approved emergency workarounds
- target `SKILL.md` execution-status wording for both creation and update slices that states `dotnet so.dll compile` is validation only, requires the default governed success path to continue on public `dotnet so.dll run` and `dotnet so.dll resume` until final `Done`, and forbids claiming governed completion before that chain has reached final `Done`
- target `SKILL.md` runtime hardening wording that forbids pseudo-success preflight/guide records and requires ZIP-based `.nupkg` extraction on Windows PowerShell 5.1 package-channel restores
- per-run plan output path and hash (runtime-owned; not a stable target-skill asset)
- workflow template path
- workflow-designer subagent dispatch record and relative-link context set used for workflow generation
- weave-out suitability review that checks whether every current weave-out should become a dedicated target-skill local `{skillname}-{taskname}.agent.md`
- target-skill local subagent definition paths created or refreshed under `assets/`
- target `SKILL.md` and target reference-doc relative-link updates for any newly required target-skill local weave-out subagents
- re-enhancement template-change strategy and conflict evidence, including the selected strategy and old-template/current-requirements input mapping
- `shared_review_context`, `aggregated_reenhancement_findings`, and `aggregated_plan_findings` produced from one bounded context and complete independent review batches
- `aggregated_review_findings` and `batch_repair_evidence` proving that every pre-fix finding was considered and repaired in one coordinated pass
- `aggregated_post_fix_validation` and `serial_validation_evidence` proving that all post-fix validators returned before the final ordered validation stage
- workflow analysis report
- `workflow.compile-feedback.json` and the `workflow.compile-feedback.v1` payload, including parse/validation status, counts, phase blockers, candidate path/hash, and runtime identity/version.
- Verified real output paths for workflow, feedback, analysis, dataflow, Mermaid, and HTML artifacts, including paths outside the Git worktree or ignored workspace mirrors.
- compiled Mermaid
- node-to-file or node-to-artifact map
- package lock metadata split into:
	- checked-in lock reference target
	- resolved runtime bundle version/channel evidence
	- runtime-owned completion-manifest reference to the checked-in lock asset
- checked-in skill-markdown governance outcome split into:
	- checked-in skill-markdown target path
	- governed evidence that the checked-in skill markdown is the source deliverable for this slice
	- runtime-owned completion-manifest reference to that checked-in source asset
- boundary-check/approval-gate trail covering every governed transition on the same external runtime copy, including the gate predicates checked (bound to concrete output fields and instance), seam ownership verified, route coverage confirmed, and the explicit approval or structured non-human continuation that allowed each next step
- target-deliverable-change evidence for terminal completion: `completion_by_target_skill_changes` or file/diff evidence showing the requested checked-in deliverables were created or modified, not merely present as paths
- fixed governance verdict and evidence checklist surface carried by the runtime-owned completion manifest, including the verdict rule, current status and node, whether final `Done` was reached, any missing evidence, the next action, and explicit mappings back to the existing runtime-owned evidence families instead of a parallel completion schema or a terminal self-certification surface
- runtime audit artifact links
- After every SO CLI call or audit-producing step, including `dotnet so.dll` and self-contained `so.exe` (or `so`) calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file`, verify existence and readability, and never guess an audit path. If `--workspace-root` was supplied, use only the hash-verified `workspace_relative_mermaid_file` and `workspace_relative_html_file` values for clickable workspace links. If the card tool is available, pass `card_input_file` directly to it without asking another agent to return the file contents; a card is not proof of artifact delivery. Otherwise show the verified Mermaid link first and the HTML link second. When no fresh render exists, reuse only a previously verified link and state that the render is unchanged. On `delivery_failed`, report the failure and do not emit a guessed link.

### Governance

- Exclusive Loom Skill Orchestrator governance mode uses Loom Skill Orchestrator as the only official execution authority.
- Every Loom-governanced target skill is forced onto the Loom Skill Orchestrator-governanced route: no transition may advance without passing a boundary check on the exact external runtime copy, then receiving explicit approval or structured continuation instruction for that next step. There is no autonomous shortcut off this route.
- For any Loom-governanced target skill, execution authority must come from published SO package artifacts for the chosen channel. Do not normalize repository-source builds or manually assembled binaries into the default workflow-operation path.
- For both new target-skill creation slices and update or re-enhancement slices, do not present guide refresh, checked-in asset creation, workflow-template authoring, or `dotnet so.dll compile` success as governed completion or official governed run evidence by themselves.
- AskUser seams may request only declared user-owned fields or decisions.
- Runtime-owned facts and artifact paths belong to runtime-owned seams such as `WaitResume`.
- Route-aware terminal and blocked business-output gates are required for governed routes.
- Gate predicates must bind the declared required output fields explicitly — non-empty values, success/passed state, and belonging to the current workflow instance — not a single aggregate flag such as `gate_outputs_present == true`. A gate that only checks an aggregate boolean cannot prove its listed evidence exists.
- Official runnable route guards after review-fix must require both `review_fix_loop_evidence != null` AND `commit_report_ready.status == 'ready'`, with explicit blocked/needs-validation stop or wait paths when readiness is not proven. A non-empty evidence object alone must never authorize the official run chain.
- Terminal business-output gates before final `Done` must include both a boundary-check/approval-gate trail covering every transition on the same external runtime copy and concrete target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence for the requested checked-in deliverables). Checked-in asset path existence alone cannot satisfy a business-output gate.
- For target-skill modifications, runtime-ready evidence, successful MCP startup/use evidence, and fresh-guide evidence must exist before downstream planning, authoring, validation, compile, run, or resume work starts.
- If a governed workflow is presented as runnable execution authority, its materialized runtime copy must actually execute on the current public `dotnet so.dll run` and `dotnet so.dll resume` path rather than being only compile-clean.
- Full-delivery governed slices must continue from compile-review approval onto the public `dotnet so.dll run` path, then weave back through any blocked business-intake or `AskUser` seams with public `dotnet so.dll resume` until final `Done`, passing a boundary check and explicit approval at every transition.
- Do not keep `compile-only`, `compile-ready governance integration`, or `official run evidence pending` as supported completion outcomes for full-delivery governed enhancement slices unless the user has explicitly changed the slice contract before implementation begins.
- File-backed checked-in asset inspection must stay rooted under the declared target-skill asset root and must not degrade into placeholder context-copy review.
- Before completion, review every current weave-out and decide whether it should be implemented as a dedicated target-skill local subagent under `assets/{skillname}-{taskname}.agent.md`; when the answer is yes, create or refresh that subagent file and add the relative-link reference in the target `SKILL.md` and target reference docs before the workflow can complete.
- Before completion, the review-fix loop must build one shared bounded context, run independent reviews as a complete `ConcurrencyStrategy.All` batch, aggregate every finding before one coordinated repair, run a complete parallel post-fix validation batch, and finish with serial validation; compile-clean or one returned subagent result is insufficient.
- Before completion, run an explicit review-skill -> fix-skill loop on the target skill, then prepare commit-and-report-ready evidence for the final handoff instead of stopping immediately after template compile or first-pass edits.
- When a slice still uses checked-in source assets as the authoritative business deliverables, the governed route must name those checked-in assets explicitly as done outputs and must emit a runtime-owned completion manifest that references them instead of pretending runtime-owned temporary files replaced them.
- The runtime-owned completion manifest is the fixed governance verdict and evidence checklist surface for final completion handoff. Reuse the existing runtime-owned evidence families for that verdict surface instead of introducing a parallel completion schema, and do not let the terminal manifest step invent its own replacement route-proof evidence.
- Completion requires requested target-skill deliverables to be created or modified.
- Post-run workaround reminders are non-blocking by default: highlight the decision report path and key risk summary, request explicit user acknowledgement, and keep execution continuity unless the user overrides with a blocking policy.

## Workflow Designer Reference Pack

Before dispatching `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`, the caller must provide a bounded `referencePackManifest` and fresh `schemaDemoInput` from the exact SO runtime after the required MCP-first check. The pack must include the successful guide JSON and returned guide file, same-runtime schema/demo/demo compile audit, target contract and requirements, current workflow source, current package lock, applicable `AGENTS.md`, and latest compile feedback when revising. Every entry carries a normalized path, SHA-256, exact runtime version, authority role, read status, and validation result. An older workflow is `previous_runnable_reference` only and requires a version/hash/difference/rejected-item disposition.

The designer must return runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` with schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Keep descriptors with path, SHA-256, schemaVersion, verdict, and exact runtime version. A required semantic probe that is failed or unknown prevents readiness; compile success alone is not semantic evidence. SO governance wrappers must hand off to the owning domain orchestrator instead of copying its business steps.

## Runtime Mode Separation

Resolve the runtime mode before any package-cache lookup or network request. The two package paths are independent and must not be combined.

- `self-contained` mode is the default package-channel path. It validates and acquires only one exact-RID package for the selected product and platform: `Techne.Loom.AgentOrchestrator.Runtime.<rid>` for AO or `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for SO. It launches the validated `ao.exe` or `so.exe` directly. It must not download, validate, extract, or assemble the `.NET CLI mode` .NET runtime bundle.
- `.NET CLI mode` is explicit. Only this mode validates and acquires the same exact-version .NET runtime bundle (a NuGet restore set that includes the embedded Roslyn compiler assemblies used by the C# expression evaluator), checks the `.dll`, `.deps.json`, `.runtimeconfig.json`, Roslyn, and dependency closure, then launches through the shared .NET host.
- Once a mode is selected, a failure stays in that mode and fails closed. Do not fall back from `.NET CLI mode` to self-contained or from self-contained to `.NET CLI mode` after startup or package acquisition begins.
- Runtime evidence must identify `runtime_mode`, exact version, package ids, RID, cache validation, launch descriptor, and failure category. Never report a self-contained RID package as a .NET runtime bundle.

## Runtime Flow

1. Classify governance state and lock the goal to target-skill delivery.
2. Confirm the skill-bound package version and derived channel, prove the corresponding published Loom Skill Orchestrator runtime can run, then start its local `mcp stdio` server and use `so_inspect_workflow_fragment` against the same external workflow copy. Persist `mcp_startup_evidence` and stop on failure.
3. After the MCP-first check, execute the bare `dotnet so.dll --guide`, parse its JSON result, and read the returned `guide_path` and `docs_root`; only then enter plan mode and write the per-run plan to `<execution-output-root>/plan/skill-plan.md`.
4. Author or refresh the workflow template and package lock.
5. Apply feedback and materialize one fresh external runtime workflow copy outside the skill folder, recording its immutable instance identity, workflow-file path, and persisted runtime-state/session path.
6. Run `dotnet so.dll compile` against that exact external workflow copy, review the analysis report, pass the compile-boundary check, and update the target `SKILL.md` with the correct execution-status wording for the current slice.
7. Execute the public `dotnet so.dll run` path against that same external copy after passing its boundary-check/approval gate; then continue with `dotnet so.dll resume` whenever the route blocks or requires the next instruction, reusing the same persisted instance and state and weaving back through every business-intake or `AskUser` seam — each transition gated by a boundary check plus explicit approval or structured continuation — until final `Done`.
8. Keep runtime workflow copies, event logs, and audit artifacts outside the skill folder.

## Exclusive Loom Governance Completion

- The target skill states that it has switched into Loom-governanced execution under Loom Skill Orchestrator.
- The target skill states in its own `SKILL.md` that ordinary workflow changes stay on the Loom-governanced CLI path and that direct workflow JSON edits are blocked-state-only emergency workarounds.
- The target skill states in its own `SKILL.md` that it is forced onto the Loom Skill Orchestrator-governanced route: every transition must pass a boundary check on the exact external runtime copy, then receive explicit approval or structured continuation instruction before advancing; no step may proceed on inferred intent, compile success alone, prose, or direct JSON edits.
- The target skill states in its own `SKILL.md` that `dotnet so.dll compile` is validation evidence only and that full-delivery governed completion requires an official public `dotnet so.dll run` / `dotnet so.dll resume` chain that reaches final `Done` on the runtime workflow copy.
- Direct CLI remains a primitive path only. The local stdio MCP is the mandatory first external interface for governed SO verification of this skill and every Loom-governanced target skill after runtime preflight; it must be started and used for a bounded fragment check, including during self-bootstrap. MCP does not replace official SO `run`/`resume`, and Web or remote transport is not supported.
- Official run evidence comes only from Loom Skill Orchestrator workflow state, event log, and audit artifacts. The runtime-owned completion manifest may summarize that evidence for final handoff, but it does not replace or self-certify the underlying runtime evidence families.
