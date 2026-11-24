---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement for the unified `loom skill` route and version-matched Loom package documentation.
---

# /loom-skill-enhancement

## Stage-Specific Required References

Keep this entrypoint at or below 200 lines. Detailed contracts live in this skill's own reference directory; read the applicable contract before the named stage, not just its summary.

- Before acquiring dependencies or dispatching any named agent: [Runtime Acquisition And Agent Dispatch](./reference/runtime-and-dispatch.md).
- Before planning, reviewing, authoring, or validating a workflow: [Planning And Validation Contract](./reference/planning-and-validation.md).

## Runtime Version

<!-- skill-package-version-block:start -->
- Current published Loom runtime version for the Skill module: `0.5.11`.
- This block is refreshed whenever the published Loom runtime changes, so this skill remains aligned with the latest package set.
<!-- skill-package-version-block:end -->

Upgrade or create a skill being enhanced through the unified `loom skill` route backed by Loom Skill Orchestrator. Delivery of the skill being enhanced is the business result; runtime checks are supporting evidence.

## Mandatory Reading
- [Execution contract](./reference/execution-contract.md): runtime acquisition, file and payload rules, workflow identity, compile/run/resume, version-semantic probes, and runtime modes.
- [Runtime semantic migration](./reference/runtime-semantic-migration.md): the emitter matrix (baseline verified on 0.3.282; re-probe on the version-block runtime), producer rules, gov4 reachability, migration procedure, resume shape, and Windows tooling constraints.
Read only the reference needed for the current stage:

- [Execution contract](./reference/execution-contract.md): runtime acquisition, file and payload rules, workflow identity, compile/run/resume, version-semantic probes, and runtime modes.
- [Review and evidence contract](./reference/review-and-evidence-contract.md): guide assets, workflow design, batch review/repair, required outputs, audit delivery, and completion evidence.
- [Plain-language feedback](./reference/plain-language-feedback.md): required user-facing wording, term mapping, and examples.
- [SO skill reference](./reference/so-skill-reference.md): exact-runtime guide/schema/demo reference pack and package behavior.
- [Producer output evidence discovery](./reference/producer-output-evidence-contract.md): how the target-skill driver locates and assesses evidence before asking the user for a schema or sample.
- [Mermaid artifact delivery](./reference/mermaid-artifact-delivery.md): verified four-artifact output handling and append-only per-execution progress logs.
- Shared terminology authority: use the English workflow-terminology page supplied through the validated runtime `referencePackManifest` when available; Chinese mirrors are localization aids only. Do not locate documentation by traversing from an installed skill directory.

## Runtime Binding

Published runtime execution uses one self-contained `Techne.Loom.Runtime.<rid>` package and the `loom` apphost for the detected host RID. The apphost exposes independent `agent` and `skill` module routes; no resolver executable, launch descriptor, or installed `dotnet` host is required.

MCP is optional and never blocks package startup, guide retrieval, or official CLI execution. Do not register or inspect MCP before the fresh guide step. A later workflow step may use MCP when available; its binding comes from the running self-contained apphost identity, not a descriptor file. An MCP application/tool failure after dispatch remains a failure and is not hidden by retrying through another transport.

## Autonomous Enhancement Execution

Unless the user explicitly asks for planning or analysis only, treat a bounded enhancement request as authorization to progress autonomously through planning, candidate creation, evidence-based repair, review, required validation, and the enhancement workflow's required public `run`/`resume` chain to final `Done`. Use repository contracts, exact locks, fresh guide evidence, and source to derive routine facts; do not ask about inferable values or ordinary technical choices. If genuine user-owned decisions remain, combine them into one structured ask where feasible.

Autonomy uses the required workflow boundary checks and documented structured continuation; it never bypasses explicit approval, genuine human approval, runtime validation, or safety policy. Pause for missing user-only input, a real approval gate, an unauthorized consequential external/irreversible action, or an explicit security/network/policy block. Never invent a user decision or approval. Diagnose failures before retrying, do not repeat an unchanged failed operation, keep repair bounded, and rerun only affected checks. Preserve unresolved evidence and continue independent safe work where possible.

Before writing a candidate back, verify portable repository paths and version agreement. Before completion, reconcile intended targets, candidate/source equality, active legacy references, unexpected deletions, and whether the candidate was built, compiled, written back, and run/resumed to `Done`; these are distinct states. Enhancing a skill does not itself authorize running its business workflow or committing/pushing unless the user separately requested those actions.

## Fixed Progress And Recovery Contract

Use one request-level task ledger with stable task IDs and statuses `未开始` (not started), `进行中` (in progress), `受阻` (blocked), or `完成` (complete). Every item carries verifiable evidence, or an explicit no-evidence-yet statement, and its next action; completed items say no further action is required. Keep the task set and denominator fixed across phases. Report a progress percentage only against that fixed denominator and state the basis; otherwise show the checklist without a percentage. If scope changes, record and re-baseline the ledger explicitly rather than silently changing the denominator. Execution confidence is a separate estimate, not task completion.

Planners and designers must apply the [live-wait and recovery checklist](./reference/planning-and-validation.md#continuation-planning-and-recovery) before planning a continuation or resuming. Keep candidate generation, compile success, source writeback, and terminal execution as separate milestones.
## Workflow Procedure

Before planning or editing the skill being enhanced, apply the [parallel fan-out and interactive subsession contract](./reference/review-and-evidence-contract.md#parallel-fan-out-and-interactive-subsessions).
Enter plan mode before editing deliverables of the skill being enhanced. Map its target-skill requirements to the target `SKILL.md` and applicable local planner/designer `.agent.md` files; record exact evidence or a not-applicable reason.

1. Classify the target as new or already under Loom Skill Orchestrator governance; lock the requested deliverables of the skill being enhanced.
2. Map the skill being enhanced's `SKILL.md` first as Priority 1 in `assets/so-workflow/node-to-file-map.md`, including its exact path, owning nodes, requested changes, and validation evidence; map supporting documents after it. Build the bounded reference pack and per-run plan under `<execution-output-root>/plan/`.
3. Analyze inputs, outputs, nodes, branches, loops, ownership seams, gates, and concrete evidence producers; reconcile every context path with the owning contract's `context_fields` catalog and make typed reads and output projections match its JSON type. For every added or changed field, follow [Producer Output Evidence Discovery](./reference/producer-output-evidence-contract.md): inspect the target skill's own contracts, agents, schemas, fixtures, and available run/probe evidence first; let the target-skill driver decide whether evidence is sufficient and ask the user only for a necessary gap that remains. Compare against independent producer evidence, record `contextFieldTypeComparisons` in the existing static review, and block readiness on unresolved required evidence rather than guessing.
4. Author workflow changes as C# builder source through `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`: use `Build(WorkflowScriptInput)` for new workflows and `Edit(WorkflowInstance, WorkflowScriptInput)` for revisions. The selected self-contained `loom` apphost through `loom skill` alone reads/deserializes external workflow JSON and writes the canonical candidate; scripts must not embed full JSON or perform file I/O. Use `--mode export` only to produce an `Edit` starter from an eligible external draft. Keep JSON as canonical persisted state and Mermaid as presentation. Direct edits to checked-in workflow JSON are not a normal maintenance path. Only if the current direct `loom skill` apphost path is fully blocked and the user explicitly approves may you make the smallest workaround needed to unblock it, then immediately return to the same published apphost `loom skill compile`, `run`, or `resume` path.
   Follow [Runtime Documentation References](#runtime-documentation-references) whenever this workflow or a declared local agent needs AO/SO runtime documentation.
5. Before any broad review or run, publish the bounded `review_plan_md` with actual changed files, behavior boundaries, risk, the minimal checks and rationale, evidence producers, valid evidence reuse, invalidation causes, excluded checks, and mandatory approval/terminal gates. Pass this plan to each retained review, aggregation, repair, and post-fix validation role. Keep complete `whenAllPass` result accounting, but limit each reviewer to its selected scope and require an explicit `not_applicable` result for an unselected role. Aggregate once, make one coordinated authorized repair pass, then rerun only invalidated checks and proven direct dependents. Preserve any public `run`/`resume` and final `Done` gates required by the active contract. See the [review contract](./reference/review-and-evidence-contract.md). The post-fix reviewer and aggregator must still pass `runtime_doc_resolution_policy` with exact current-source evidence before the aggregate can be complete.
   For synthetic scenarios, record dependency edges, state/artifact isolation, and resource limits before execution. Parallelize independent isolated scenarios within capacity, serialize dependent or unisolatable scenarios, and retain scenario-level synthetic input, expected outcome, pass condition, observed evidence, verdict, and synthetic designation as specified in the review contract.
   Only in an explicitly isolated connectivity test may clearly marked synthetic approval/review inputs be passed through a supported test path to exercise downstream routing. Isolation requires a dedicated test workflow copy, case/run state, session/store, and artifact root; no live business data, external side effects, or consequential actions; and synthetic labels on every input and result. This may satisfy only the enhancement submission's connectivity-test criterion; all other submission requirements still apply. It is not a genuine approval, review decision, authorization, human approval, or real business evidence, cannot satisfy an official/business approval gate, and must not be used to fabricate a workflow receipt.
6. Run exact-runtime compile. Treat compile as structural evidence only.
7. In exclusive Loom Skill Orchestrator governance, Loom Skill Orchestrator remains the official workflow authority. Ordinary workflow operations run through the exact locked unified `Techne.Loom.Runtime.<rid>` apphost using `loom.exe skill run`/`resume` on Windows or `loom skill run`/`resume` on Unix. Run the exact external workflow copy through that public chain and every required `resume` until final `Done`; never claim completion from compile, a blocked payload, local orchestration, or a different workflow copy.
   - For workflows whose contract paths are rooted by caller-owned `target_skill_path`, set the CLI working directory to the exact skill being enhanced for `compile`, `run`, and `resume`. Use `.` only as a portable template bootstrap; pass the skill root as an absolute value in the run `--context-file` and keep that value in the same external workflow copy for later resumes.
8. Verify target deliverable changes, event/audit records, output-family values, Mermaid/HTML paths, and the completion manifest.

## Runtime Documentation References

When the skill being enhanced or one of its declared local agents needs AO/SO runtime documentation, the caller must use the selected exact apphost's successful `--guide` JSON. Validate its runtime version, absolute `docs_root` and `guide_path`, containment, and readability. Resolve English pages only under `docs_root`. For workflow/task contracts, also validate same-version `contract_docs_root`, `contract_index_path`, and `builder_template_path`; follow index links only within `contract_docs_root` and pass exact resolved paths through `referencePackManifest` or the bounded parent context. Translated pages are localization aids only. Do not construct `docs/...` paths relative to an installed skill, and never require runtime package documentation to be copied into or bundled with a skill. Require every target `SKILL.md` to carry the conditional `Runtime Documentation References` rule when the target or its declared agent needs AO/SO technical documentation.

## Selective JSON Reading

For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective inspection: use search or regex only to locate the target key or path, then read a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract the needed subtree. Read the full JSON only when the user explicitly requests it or whole-document validation requires it. This rule applies to JSON data; `.agent.md` role contracts remain a separate full-read requirement. For large workflows and review results, return an existing readable file path plus a bounded status/summary, needed JSON Pointer fragments, and evidence locations. Do not dump the complete workflow JSON into the conversation. Whole-document parsing or validation stays file-backed; full conversational output requires an explicit request and applicable size limits.

## Artifact Address Usability

Use a complete local file URL for every artifact address in the standard three-slash form (`file://` plus an empty authority plus the absolute path): Windows `file:///C:/...`; Linux/macOS `file:///...`. Drive-letter case is insignificant; never use the two-slash `file://C:/...` form, which places the drive letter in the URL authority and does not open. Verify the URL resolves to an existing readable file in the active host. If a file URL cannot be opened, create and verify a mirror in the active workspace and use its file URL. Encode every space in the URL as `%20`; use the identical encoded URL in the clickable Markdown link and following text fence. Never return a literal-space URL, bare path, truncated or stale URL, or code fence without a clickable link. If no URL opens, report the delivery blocker instead of claiming the address works.

## Per-Execution Progress Log

After each post-execution progress report from `/loom-skill-enhancement` or a skill it creates or updates, write the complete Markdown entry to a separate external file with ISO 8601 UTC and local timestamps including numeric UTC offset and OS timezone ID. Invoke the selected Loom apphost directly with `progress append --entry-file <entry-file> --output-root <execution-output-root>` (use `loom.exe` on Windows and `loom` on Unix), passing file paths as separate CLI arguments. The command reads only the entry file, append-writes `<execution-output-root>/progress-report.md` in an existing output root, and prints its complete file URL; reuse that exact URL if reporting the log location. Never read or inspect the existing report, use manual append/read-modify-write, or bundle progress-report scripts. On failure, preserve prior entries, report the failure, pause later business-step progression without changing workflow execution state, and never claim an unsuccessful append succeeded.

## Stable Assets

- `assets/so-workflow/loom-skill-enhancement--so-template.json`
- `assets/so-workflow/so-package-lock.json`
- `assets/so-workflow/node-to-file-map.md`
- `assets/so-workflow/governance-notes.md`
- Optional target-local guide-copy metadata: `assets/so-workflow/reference/document-copy-manifest.json` (informational only; not a runtime or readiness gate).
- Workflow designer subagent: [Workflow Designer](./assets/agents/loom-skill-enhancement-workflow-designer.agent.md)
- Optional MCP support after guide capture: [MCP Startup](./assets/agents/loom-skill-enhancement-mcp-startup.agent.md)
- Reusable weave-out and review subagents:
	- [Skill Markdown Gap Review](./assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md)
	- [Package Lock Gap Review](./assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md)
	- [Workflow Governance Gap Review](./assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md)
	- Re-enhancement strategy reviewer: [Re-enhancement Conflict Judgment](./assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md)
	- [Weave-Out Subagent Fit Review](./assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md)
	- [Review Fix Loop](./assets/agents/loom-skill-enhancement-review-fix-loop.agent.md)
	- [Review Findings Aggregator](./assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md)
	- [Scope Input-Output Analysis](./assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md)
	- [Route-Gate Analysis](./assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md)
	- [Evidence Node-Map Analysis](./assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md)

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

## Default Autonomous Enhancement

Unless the user asks only for planning or analysis, default to completing a bounded enhancement through required planning, candidate work, evidence-based repair, review, compile, tests, and the same-workflow public `run`/`resume` path to `Done`. Derive routine facts from contracts, exact package locks, fresh guide evidence, and source instead of asking the user.

- Ask only for necessary missing user-owned business input or genuine human approval; combine independent decisions in one structured question where possible. Continue safe independent work, respect scope and approval boundaries, and never fabricate a decision, approval, or receipt.
- Diagnose failures from preserved evidence before repair; do not repeat an unchanged action. Apply a bounded, evidence-backed repair and rerun only changed checks and their proven dependents.
- Before adopting candidates, verify the intended target files, repository-portable paths, exact version/channel references, active legacy references, and unexpected deletions. Distinguish candidate generated, compile passed, source updated, and workflow `Done`; reconcile the final target set and evidence before completion.
- Enhancement does not authorize running the skill being enhanced for its separate business process or committing/pushing.

See [Autonomous Enhancement Evidence](./reference/review-and-evidence-contract.md#autonomous-enhancement-evidence) for the full execution and recovery contract.

## Autopilot AskUser Policy for the Skill Being Enhanced

Apply the same conditional policy below to this enhancing skill's own questions and workflow `AskUser` boundaries. Every skill being enhanced must include it in its own `SKILL.md`. Every target-local agent that initiates an ask must read and apply it, and every target workflow `AskUser` caller-facing hint must state it. Review and repair all applicable callers in the coordinated batch; if none exist, record an explicit no-applicable-callers reason rather than inventing a node. Post-fix validation must inspect the current repaired files and gate on exact evidence. Keep unbounded source inventories and caller evidence in complete external files; workflow expressions consume their paths and scalar summaries rather than unbounded arrays. Preserve complete legacy repair arrays for existing consumers without materializing them in expressions.

When the host explicitly identifies an autopilot or unattended run, do not call a blocking user-input tool, open a browser, or wait. If the same active ask has a runtime-returned endpoint (AO `ask_user_endpoints` or the SO `ask_user` envelope's `endpoints`) and does not require genuine human approval, use only its supported HTTP session/submission flow to read the contract and submit context-supported, type-valid answers; never start another worker, guess URLs, or expose pairing/session credentials. If no endpoint can be used, skip the interactive ask and return the best-supported answers with a prominent warning that the agent generated them while the user was unavailable and they are not user-confirmed. When writable, keep the absence explanation in caller-provided additional-context, or in existing question `context` before a session starts; never mutate an active contract, invent a wire field, or pretend the explanation is a user answer. If the available context cannot support a valid answer, mark it unknown only when the contract allows that value; otherwise leave it unresolved rather than inventing facts.

Never fabricate a workflow receipt, infer human approval, or bypass the owning runtime's validation. If genuine human approval is required, do not submit an agent-generated approval via HTTP or consume the active session; provide suggestions with the warning and leave the session and workflow waiting. Continue a workflow node only through a supported noninteractive path; when a valid receipt or genuine human approval remains required, provide the suggestions and warning and leave the workflow waiting. Outside autopilot, preserve normal approved browser and user-input behavior.

## Named Agent Resolution

- **Preflight before dispatch:** verify the exact non-empty frontmatter `name`, required inputs and types, every linked contract, and the declared output shape. A filename or summary is not role identity. Resolve the active skill-owned or skill-being-enhanced-owned copy first and the matching global installed-skill copy second.
- `agent not found`, unknown agent, a subagent tool with no `agentName` parameter, or a silent default-agent response means that role is not registered. This is a normal host limitation, not a blocker or approval request.
- For a registered exact role, call it directly. Otherwise deliver the full contract inline or `path-only` to a capable writable generic driver. The driver must echo the frontmatter name, follow the contract, and return its output contract. Do not use a read-only driver for authoring, editing, or validation.
- Every dispatch record states the exact name, clickable `.agent.md` link, route (`direct` or `driver-fallback`), delivery mode (`inline` or `path-only`), fallback trigger evidence, and the driver's name echo; never substitute a similarly named role or repository-global prose.
- A HTTP 408, or timeout is a transport failure, not a missing agent. Retry once in `path-only` mode with large inputs passed by path; if it fails again, preserve evidence and block dependent work. Driver fallback does not require user approval.
- Stop only if the exact file is missing or ambiguous or no capable writable driver exists. Otherwise continue with the exact contract and a fixed request ledger.

## Dependency Acquisition Fallback Policy

Read the [complete ordered acquisition policy](./reference/runtime-and-dispatch.md#dependency-acquisition-fallback-policy) before fetching any dependency; it includes the exact-NuGet exception below. A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted, and only with the user's explicit approval for that exact CLI action. A direct in-process .NET `HttpClient` call from PowerShell is L3; PowerShell `Invoke-WebRequest`/`Invoke-RestMethod` remain L6, and an L6 operation requires explicit per-operation human approval.

## Exact NuGet Acquisition Completion Rule

Read [the complete acquisition policy](./reference/runtime-and-dispatch.md#dependency-acquisition-fallback-policy) before acquisition. A missing exact package requires download and verification; it is not completion. Use the installed runtime's in-process HTTP API, preserve source-specific integrity checks, and keep policy/publication blockers in a resumable acquisition-pending pause.  For Loom runtime packages, try NuGet first and verify against the exact registration/catalog `packageHash` before extraction; any NuGet request, redirect, hash, extraction or identity failure triggers a pause for explicit human approval before downloading the exact GitHub asset and sibling `.nupkg.sha512`. Without approval, including in unattended runs, keep acquisition resumably pending and do not download the asset. Explicit decline keeps acquisition pending. Never probe other mirrors.  Every skill created or updated by `/loom-skill-enhancement` must include this complete NuGet-first acquisition and GitHub fallback policy in its own `SKILL.md` or a directly linked runtime reference, and propagate it to any local agent/caller that acquires the runtime; a policy block must not be hidden by another mirror. The post-fix Markdown review must verify the target skill copy.

## Workflow Design Notes

For phase-sliced workflows, follow [the phase-sliced design contract](./assets/agents/workflow-designer-04-structure.md): use `loom.workflow-skeleton.v1` at 40 or more authored state nodes; run `skeleton-check`, `phase-compile`, `impact`, and `merge` through the same apphost; treat 25 authored state nodes per phase as a soft cap; allow three repair attempts per phase and two post-merge full-compile repair rounds; record `phase_design_evidence`.

When a local workflow-designer reference is missing or unreadable, follow that agent's `Missing Reference Fallback` section before reporting a blocker.

After exact package validation, capture the first fresh guide by running `loom.exe --guide` on Windows or `loom --guide` on Unix.

## Direct Guide Bootstrap

After acquiring and extracting the exact runtime package, directly run `loom.exe --guide` on Windows or `loom --guide` on Unix as the first runtime operation. Verify the returned version and contained, readable guide paths before downstream work; do not insert an MCP or fragment preflight.

## Missing Reference Fallback

For a missing or unreadable skill-local reference, check its exact relative path in the active skill copy, then the same normalized skill-relative path in a matching user-level installation. Verify the same skill and declared version, record attempted and selected paths, and do not mix copies or replace authoritative runtime guide/schema/demo evidence. If no matching readable copy exists, stop only dependent work. Follow the workflow designer's [Missing Reference Fallback](assets/agents/loom-skill-enhancement-workflow-designer.agent.md#missing-reference-fallback) for the complete lookup.

## Workflow Designer Contract

Count only authored state nodes, excluding transitions and synthetic harness nodes. Use whole-workflow design below 40; when there are 40 or more authored state nodes, freeze a `loom.workflow-skeleton.v1` design view and use `skeleton-check`, `phase-compile`, impact analysis, and merge. Keep each phase at or below 25 authored state nodes as a soft cap; allow at most three repair attempts per phase and two post-merge full-compile repair rounds. Return `phase_design_evidence` linked to `phaseDesignEvidence` in the existing `static-contract-review.json`.
