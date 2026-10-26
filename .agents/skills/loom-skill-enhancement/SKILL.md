---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Upgrade or create a skill being enhanced through the published Loom Skill Orchestrator (`so`) workflow. Delivery of the skill being enhanced is the business result; runtime checks are supporting evidence.

## Mandatory Reading
- [Execution contract](./reference/execution-contract.md): runtime acquisition, file and payload rules, workflow identity, compile/run/resume, version-semantic probes, and runtime modes.
- [Runtime semantic migration](./reference/runtime-semantic-migration.md): the verified 0.3.282 emitter matrix, producer rules, gov4 reachability, migration procedure, resume shape, and Windows tooling constraints.
Read only the reference needed for the current stage:

- [Execution contract](./reference/execution-contract.md): runtime acquisition, file and payload rules, workflow identity, compile/run/resume, version-semantic probes, and runtime modes.
- [Review and evidence contract](./reference/review-and-evidence-contract.md): guide assets, workflow design, batch review/repair, required outputs, audit delivery, and completion evidence.
- [Plain-language feedback](./reference/plain-language-feedback.md): required user-facing wording, term mapping, and examples.
- [SO skill reference](./reference/so-skill-reference.md): exact-runtime guide/schema/demo reference pack and package behavior.
- [Producer output evidence discovery](./reference/producer-output-evidence-contract.md): how the target-skill driver locates and assesses evidence before asking the user for a schema or sample.
- [Mermaid artifact delivery](./reference/mermaid-artifact-delivery.md): verified four-artifact output handling and append-only per-execution progress logs.
- Shared terminology authority: use the English workflow-terminology page supplied through the validated runtime `referencePackManifest` when available; Chinese mirrors are localization aids only. Do not locate documentation by traversing from an installed skill directory.

## Runtime Binding

Published AO and SO runtimes use only self-contained product+RID packages. The host agent determines the current OS, architecture, and Linux libc and selects exactly one supported RID. No resolver executable, launch descriptor, or installed `dotnet` host is required.

MCP is optional and never blocks package startup, guide retrieval, or official CLI execution. Do not register or inspect MCP before the fresh guide step. A later workflow step may use MCP when available; its binding comes from the running self-contained apphost identity, not a descriptor file. An MCP application/tool failure after dispatch remains a failure and is not hidden by retrying through another transport.

<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.4.6-beta`.
- This block is refreshed by the publish workflows whenever SO package versions change, so the skill contract stays aligned with the latest published beta package set.
<!-- skill-package-version-block:end -->





















- `assets/so-workflow/so-package-lock.json` is the exact-version authority and checked-in lock reference target. Derive the channel from that version; do not ask the user to choose it.
- Use only `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for the bound version and detected RID. Do not probe or select DLL/FDD mode and do not persist the machine RID or apphost path in the skill's checked-in state.
- Released and beta package indexes are `reference/packages.released.md` and `reference/packages.beta.md`.
- [Migration script playbook](./reference/migration-script-playbook.md): path-safe migration entry points, producer boundaries, dry-run behavior, and repeatable fixture checks.

Every enhancement pass follows this bootstrap:

1. Create a fresh external workflow copy and preserve one `caseId`/`runId` lineage.
2. Read the exact package version from the lock, derive its channel, and detect the host RID.
3. Reuse a fully validated immutable AO/SO generation from the shared current-user OS-temp cache when exact package ID/version/RID and full `.nupkg` SHA-512 match. On a miss, check the standard NuGet cache, then fetch the exact package and compare its SHA-512 with exact registration metadata; the same-version GitHub fallback requires its `.sha512` sidecar.
4. Verify package id, version, nuspec, RID, manifest, entrypoint, archive size and paths before safely extracting the apphost and docs. Stage and validate new or repaired generations before atomic publication; never overwrite a different valid or in-use generation. Do not add a cache lock, fixed checked-in bootstrap script, second resolver, or descriptor file. Stop with a concrete capability error if the host cannot perform a required check.
5. Directly run `so.exe --guide` on Windows or `so --guide` on Unix. Validate the JSON version, absolute docs and guide paths, containment, and readability. Use this fresh guide to continue into planning, authoring, validation, compile, run, or resume on the same apphost.

Every enhancement pass must prove that the exact locked published SO package apphost is runnable before downstream work.
## Workflow Procedure

1. Classify the target as new or already under Loom Skill Orchestrator governance; lock the requested deliverables of the skill being enhanced.
2. Enter plan mode before editing deliverables of the skill being enhanced. Map the skill being enhanced's `SKILL.md` first as Priority 1 in `assets/so-workflow/node-to-file-map.md`, including its exact path, owning nodes, requested changes, and validation evidence; map supporting documents after it. Build the bounded reference pack and per-run plan under `<execution-output-root>/plan/`.
3. Analyze inputs, outputs, nodes, branches, loops, ownership seams, gates, and concrete evidence producers; reconcile every context path with the owning contract's `context_fields` catalog and make typed reads and output projections match its JSON type. For every added or changed field, follow [Producer Output Evidence Discovery](./reference/producer-output-evidence-contract.md): inspect the target skill's own contracts, agents, schemas, fixtures, and available run/probe evidence first; let the target-skill driver decide whether evidence is sufficient and ask the user only for a necessary gap that remains. Compare against independent producer evidence, record `contextFieldTypeComparisons` in the existing static review, and block readiness on unresolved required evidence rather than guessing.
4. Generate or revise the workflow JSON through `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`; keep JSON as authority and Mermaid as presentation.
   Resolve required English runtime pages only under the selected apphost's successful `--guide` JSON `docs_root`; validate the exact version, absolute `docs_root`/`guide_path`, containment, and readability, then pass the resolved paths through `referencePackManifest`. Supply Chinese pages only as localization aids. Never construct documentation links relative to the installed skill.
5. For independent checks, build one shared bounded context, run complete external `whenAllPass` batches, aggregate once, repair once, and revalidate as described in the [review contract](./reference/review-and-evidence-contract.md). Require every target `SKILL.md` to carry the conditional `Runtime Documentation References` rule; the post-fix reviewer and aggregator must pass `runtime_doc_resolution_policy` with exact snapshot-verified evidence before the aggregate can be complete.
6. Run exact-runtime compile. Treat compile as structural evidence only.
7. Run the exact external workflow copy through public `run` and every required `resume` until final `Done`. Never claim completion from compile, a blocked payload, local orchestration, or a different workflow copy.
8. Verify target deliverable changes, event/audit records, output-family values, Mermaid/HTML paths, and the completion manifest.

## Selective JSON Reading

For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective inspection: use search or regex only to locate the target key or path, then read a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract the needed subtree. Read the full JSON only when the user explicitly requests it or whole-document validation requires it. This rule applies to JSON data; `.agent.md` role contracts remain a separate full-read requirement.

## Validation Runtime Efficiency Contract (Required)

For enhancement-run validation scripts and validation workflow steps, enforce these defaults:

- Keep each single validation run within 5 minutes by design. If a check is likely to exceed 5 minutes, split it into smaller independent validations.
- Keep internal delay between validation steps at 5 seconds or less. Do not insert long sleeps or manual pacing gaps.
- Run independent validation checks in parallel wherever correctness permits, and keep serial checks only for hard dependencies.
- After each parallel validation batch, aggregate once and execute only the minimum serial final checks.
- If platform constraints block these limits, fail closed with explicit blocker evidence and return a concrete split/parallel remediation plan.

### Automated Polling Timer Rule (Required)

For automated readiness polling, check immediately when supported and begin with the smallest valid interval. Increase the next wait only after a recorded `pending` or `not-ready` result shows the current timing did not permit verification; increase by exactly 1 second per retry. Do not use exponential backoff or add delay without observed evidence.

Keep each idle wait between validation steps at or below 5 seconds and preserve existing operation and session deadlines. If a check remains unready at the 5-second ceiling, continue only with the existing runtime deadline or report the blocker; do not extend a runtime deadline. This polling rule does not shorten a human response wait.

## AO/SO AskUser Test Isolation (Required)

When a validation workflow tests AskUser behavior through AO or SO:

- For apphost integration or end-to-end tests, invoke the runtime selected for the run directly by its normalized absolute apphost path. Record its exact version, executable path, and working directory. Use the exact package apphost in `package-channel` mode; use current-source output only for explicit repository debugging and label that evidence development-only. Do not resolve the executable through `PATH` alone or silently substitute DLL/project output.
- Give every test lane a unique external root for its contract file, AskUser store/session data, workflow copy, event/log records, and audit outputs. Use only documented or test-supported storage-root controls and verify the resolved paths. If the store cannot be isolated, serialize the affected tests rather than sharing mutable state.
- Give every simultaneously active worker/test lane its own OS-assigned ephemeral loopback port. Consume only the endpoint returned by AO/SO and assert that active parallel lanes have different ports. Never hard-code or reuse one fixed port, and do not invent port-selection CLI flags.
- Keep pairing tokens and full route URLs private; pass only the runtime-returned approved endpoint to the browser capability.

## Phase-Sliced Workflow Design

Count authored state nodes only; exclude transitions and synthetic harness nodes. Workflows below 40 authored state nodes stay on the whole-workflow route. When a workflow has 40 or more authored state nodes, first create a standalone `loom.workflow-skeleton.v1` design view, freeze typed imports/exports, entries/exits, dependencies, gate/terminal ownership, routes, and backedges, then author isolated phases. Keep each phase at or below 25 authored state nodes as a soft cap and record exceptions. New workflows use `phaseSliced`; migrations may explicitly mix `phaseSliced` and `legacy`, but every cross-phase read and write remains declared and typed.

Use the same selected direct SO apphost for `skeleton-check`, `phase-compile`, `impact`, `merge`, and final `compile`. Phase compilation is static only and must not run business commands, HTTP, AskUser, or subagents. Aggregate interface-change requests centrally, update the skeleton, run `impact`, and redo only affected phases and proven dependents. Unknown ownership or dependency requires full revalidation. Merge exactly one passing result per phase into a new candidate and run the complete SO compile against that same file; never alter an active workflow copy.

Allow at most three repair attempts per phase and two post-merge full-compile repair rounds. At either cap, preserve failure evidence and ask for an explicit decision. Return `phase_design_evidence` linked to `phaseDesignEvidence` in the existing `static-contract-review.json`; do not create a fourth report. Optional support hashes are not freshness gates. Exact package, semantic-probe, business-output, and release checks still apply.

## Semantic Drift Gate

A runtime version change is both a contract change and an execution-semantics change.

- Before target edits or batch execution, run inherited and replacement variants of a minimal three-node fixture on the exact selected runtime. The replacement must reach final `Done` with expected non-empty context values.
- The released `0.3.282` matrix is emitter-specific: literal `updates` on plain `ToolCall`/`noop` are inert; `StateUpdate`/`MemoryWrite` write declared updates; real `echo`/`write-file` results and proven external resume projections may use `$result` after probing. Do not use a declared `outputPath` or same-transition `$context` binding as self-proof.
- Run the gov4 probe for producer-before-branch/cycle consumers. Ignore a DFS back edge on the first pass, reject producer-on-only-one-branch joins, and keep governed and ungoverned dataflow rules separate. Record unknown behavior instead of guessing.
- For repeated target patterns, run an idempotent dry scan before migration. Auto-convert only unambiguous `ToolCall`/`noop` literal-write shapes to `StateUpdate`/`state.update`; report ambiguous bindings and unknown emitters without inventing `$context` producers.
- Inspect blocked transition `requiredInputs` before every canonical resume. Required sibling fields stay at the payload top level; only the declared `resumeOutputKey` is projected from the payload.
- Final validation must return non-empty `runtime_semantic_probe_evidence`, `batch_migration_evidence`, and `decision_evidence_manifest`.

The detailed fixture, payload, manifest, and evidence requirements are in the [execution contract](./reference/execution-contract.md) and [runtime semantic migration reference](./reference/runtime-semantic-migration.md).

## Named Agent Resolution

Every `.agent.md` named by this skill is the exact behavior contract for that role; host registration is only a dispatch mechanism, not a capability gate.

- Identify the role by the exact `name` in the file's frontmatter, not by the file stem. Resolve the file from this skill's `assets/agents/` folder; in standalone installs, resolve the same relative path under the active skill root, such as `~/.agents/skills/<skill-folder>/` or `~/.claude/skills/<skill-folder>/`. Use the current repository/workspace copy first and the matching global installed-skill copy second.
- Preflight before dispatch: compare that exact name with the agent list the host exposes for subagent calls. Skill-local `assets/agents/` files are usually not registered (VS Code does not register them), so a missing name means go straight to the driver route below; do not make a direct call that is expected to fail.
- If the name is registered, call it directly. Treat any of these results as not registered and switch to the driver route in the same step: `agent not found`, an unknown agent or invalid agent name, a subagent tool with no `agentName` (or equivalent) parameter, or a call that was silently served by a default agent that did not acknowledge the declared role.
- Driver route: call an available registered general-purpose agent that can edit, run commands, and validate (in VS Code, the default agent with `agentName` omitted). Never use a read-only agent such as `Explore` when the contract requires design, editing, or validation, and never substitute a similarly named role. Deliver the contract in one of two ways:
	- Inline: include the full file content.
	- Path-only: give the resolved file path, without a hash, and instruct the driver to read the entire file itself before acting. The driver must first echo the frontmatter `name` it read and must stop if it cannot read the file.
- Every subagent invocation, including a direct named-agent call, must include both the exact `name` declared by the file and a clickable Markdown link to that same `.agent.md`, plus all required inputs and reference context; pass large inputs by path. A name alone or a summary in place of the contract is insufficient.
- Prefer path-only delivery when the agent file plus inputs is large. A request-size error, HTTP 408, or timeout is a transport failure, not a missing agent: retry once in path-only mode with inputs passed by path, then report the transport blocker if it fails again.
- Driver fallback does not require user approval; it is normal operation. Do not pause to ask the user merely because the dedicated name is not registered.
- Stop only when the exact file is missing or ambiguous, or no capable non-read-only driver exists. Then preserve failed evidence, report the concrete blocker, and do not advance dependent checks. Doing the role's work directly in the main conversation without any subagent driver requires explicit user approval.
- Record every dispatch: exact frontmatter name, `.agent.md` link, route (`direct` or `driver-fallback`), delivery mode (`inline` or `path-only`), fallback trigger evidence, and the driver's name echo.

## Core Governance

- Workflow designers use the selected runtime's fresh English guide/schema/demo, successful same-apphost compile, and version-matched English technical docs as authority; translated public docs are localization checks only. Workflow-owned schema and control metadata are English; user/business payloads and localized presentation retain their source/request language.
- Every skill being enhanced must include the selective JSON reading rule defined above in its `SKILL.md`. Require the existing Markdown gap reviewer before and after repair to emit structured `selective_json_policy` verdict and exact 1-based line/quote evidence; post-fix aggregation accepts only a passing verdict with complete evidence. Update target-local agent prompts or documents that directly inspect large JSON when applicable, and reuse the existing review, repair, validation, and aggregate nodes without adding a workflow node. Treat `evidenceQuoteVerified` as a trusted-aggregator attestation, not runtime-independent proof: the route assumes the designated aggregate result is trusted and does not prevent forged resume payloads. Require independent verification if that trust boundary is insufficient; never claim anti-forgery.
- User-facing progress, blocked, error, and completion messages for SO and every skill being enhanced must use plain words in the current interaction language. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the explanation; keep exact identifiers in technical details or evidence only. Every skill being enhanced must also require each post-execution update to include localized execution-confidence and estimated-overall-progress percentage headings in that order, each with one short reason. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress measures approximate completion of the whole request. Base both on verified evidence; neither percentage replaces required deliverables, evidence gates, or final `Done`. Every skill being enhanced must also preserve the established AO/SO report format: four Markdown links in Mermaid, HTML, Analysis, Dataflow order, each immediately followed by a `text` fence repeating the same complete absolute verified address; no extra changes to the format.
- Append each complete progress report from `/loom-skill-enhancement` and every skill it creates or updates serially to `<execution-output-root>/progress-report.md`; include ISO 8601 UTC and local timestamps with numeric offset and OS timezone ID. Resolve the reporting skill's root to an absolute path and call its own helper from `assets/scripts/AppendProgressReport.ps1` or `assets/scripts/AppendProgressReport.sh`; never use a repository-global helper or another skill's copy. On append failure, preserve the prior log, report the failure, and pause later business-step progression without changing runtime execution state. Follow [Per-Execution Progress Log](./reference/mermaid-artifact-delivery.md#per-execution-progress-log).
- All CLI file inputs are complete, closed, path-only files. Keep mutable plans, runtime copies, events, audit output, and decision evidence outside the skill bundle.
- Treat the enhancement run's `plan/skill-plan.md` as a temporary, run-scoped file under `<execution-output-root>` only. Never copy, move, migrate, or include its content in the skill being enhanced's `assets/`, package, release, or deliverables. Runtime-owned context and completion evidence may retain only its path and hash, not its content.
- `AskUser` requests only user-owned decisions or values. Runtime-owned facts and artifact paths use runtime-owned continuation.
- Every next step must pass its boundary check on the same external copy; owner-crossing steps also require explicit approval or structured continuation.
- Direct edits to a running workflow copy are blocked-state-only, explicitly approved, minimal emergency workarounds followed immediately by normal SO compile/run/resume.
- Apply the exact-file dispatch and full-content fallback procedure in [Named Agent Resolution](#named-agent-resolution) to every subagent call: state the exact agent name and include a Markdown link to the governing `.agent.md`. Require every skill being enhanced to publish this rule in its `SKILL.md` and apply it to every call; never substitute a near-match role.
- Write assumptions, corrections, decisions, probes, `events.jsonl`, and audit references under `<execution-output-root>/evidence/`; conversation text is not execution evidence.

## Artifact Address Usability

Use a complete local file URL for every artifact address in the standard three-slash form (`file://` plus an empty authority plus the absolute path): Windows `file:///C:/...`; Linux/macOS `file:///...`. Drive-letter case is insignificant; never use the two-slash `file://C:/...` form, which places the drive letter in the URL authority and does not open. Verify the URL resolves to an existing readable file in the active host. If a file URL cannot be opened, create and verify a mirror in the active workspace and use its file URL. Encode every space in the URL as `%20`; use the identical encoded URL in the clickable Markdown link and following text fence. Never return a literal-space URL, bare path, truncated or stale URL, or code fence without a clickable link. If no URL opens, report the delivery blocker instead of claiming the address works.

## Per-Execution Progress Log

After each post-execution progress report from `/loom-skill-enhancement` or a skill it creates or updates, write the complete Markdown entry to a separate external file with ISO 8601 UTC and local timestamps including numeric UTC offset and OS timezone ID. Resolve the reporting skill's root to an absolute path and invoke its own helper under `assets/scripts/`: Windows uses `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <active-skill-root>/assets/scripts/AppendProgressReport.ps1 -TargetFile <execution-output-root>/progress-report.md -EntryFile <entry-file>`; Linux/macOS uses `bash <active-skill-root>/assets/scripts/AppendProgressReport.sh --target-file <execution-output-root>/progress-report.md --entry-file <entry-file>`. The helper reads only the entry file and appends to the target. It returns the target's full file URL; reuse that exact URL if reporting the log location. Never read, open, or inspect the existing `progress-report.md`, and never use manual append or read-modify-write. Require every skill created by this skill to bundle and call its own helper. If it fails, report the failure and pause later business-step progression without changing runtime execution state.

## Stable Assets

- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`
- `assets/so-workflow/node-to-file-map.md`
- `assets/so-workflow/governance-notes.md`
- `assets/so-workflow/reference/document-copy-manifest.json`
- Workflow designer subagent: `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`
- Optional MCP support after guide capture: `assets/agents/loom-skill-enhancement-mcp-startup.agent.md`
- Reusable weave-out and review subagents:
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
## Completion

Completion requires all of the following:

- requested skill being enhanced files were created or modified;
- exact locked RID package identity/hash/archive checks, safe extraction, direct apphost startup, and fresh readable guide evidence passed; MCP is optional and is not a bootstrap gate;
- exact-runtime semantic probes and batch migration evidence passed where applicable;
- one workflow-copy lineage reached final `Done` through public `run`/`resume`;
- review, repair, post-fix validation, boundary checks, event log, audit artifacts, and durable decision evidence are readable;
- the runtime-owned completion manifest references existing evidence rather than self-certifying missing proof.

The package lock metadata splits into a checked-in lock reference target, resolved exact package version/channel evidence, and a runtime-owned completion-manifest reference to the checked-in lock asset. The checked-in skill-markdown governance outcome likewise carries a runtime-owned completion-manifest reference to that checked-in source asset.

Use the [review and evidence contract](./reference/review-and-evidence-contract.md) for the complete output checklist and the [plain-language contract](./reference/plain-language-feedback.md) for user-facing completion or failure text.
