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
- [Mermaid artifact delivery](./reference/mermaid-artifact-delivery.md): verified Mermaid and HTML output handling.
- Shared terminology authority: `../../../docs/en/architecture/workflow-terminology.md`.

## Runtime Binding

Published AO and SO runtimes use only self-contained product+RID packages. The host agent determines the current OS, architecture, and Linux libc and selects exactly one supported RID. No resolver executable, launch descriptor, or installed `dotnet` host is required.

MCP is optional and never blocks package startup, guide retrieval, or official CLI execution. Do not register or inspect MCP before the fresh guide step. A later workflow step may use MCP when available; its binding comes from the running self-contained apphost identity, not a descriptor file. An MCP application/tool failure after dispatch remains a failure and is not hidden by retrying through another transport.

<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.3.325-beta`.
- This block is refreshed by the publish workflows whenever SO package versions change, so the skill contract stays aligned with the latest published beta package set.
<!-- skill-package-version-block:end -->



- `assets/so-workflow/so-package-lock.json` is the exact-version authority and checked-in lock reference target. Derive the channel from that version; do not ask the user to choose it.
- Use only `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for the bound version and detected RID. Do not probe or select DLL/FDD mode and do not persist the machine RID or apphost path in the skill's checked-in state.
- Released and beta package indexes are `reference/packages.released.md` and `reference/packages.beta.md`.
- [Migration script playbook](./reference/migration-script-playbook.md): path-safe migration entry points, producer boundaries, dry-run behavior, and repeatable fixture checks.

Every enhancement pass follows this bootstrap:

1. Create a fresh external workflow copy and preserve one `caseId`/`runId` lineage.
2. Read the exact package version from the lock, derive its channel, and detect the host RID.
3. Reuse only a verified exact package in the standard NuGet cache. Otherwise fetch the exact NuGet package and compare its SHA-512 with exact registration metadata; the same-version GitHub fallback requires its `.sha512` sidecar.
4. Verify package id, version, nuspec, RID, manifest, entrypoint, archive size and paths before safely extracting the apphost and docs. Do not add a fixed checked-in bootstrap script, a Loom-specific runtime cache, or a descriptor file. Stop with a concrete capability error if the host cannot perform a required check.
5. Directly run `so.exe --guide` on Windows or `so --guide` on Unix. Validate the JSON version, absolute docs and guide paths, containment, and readability. Use this fresh guide to continue into planning, authoring, validation, compile, run, or resume on the same apphost.

Every enhancement pass must prove that the exact locked published SO package apphost is runnable before downstream work.
## Workflow Procedure

1. Classify the target as new or already under Loom Skill Orchestrator governance; lock the requested deliverables of the skill being enhanced.
2. Enter plan mode before editing deliverables of the skill being enhanced. Build the bounded reference pack and per-run plan under `<execution-output-root>/plan/`.
3. Analyze inputs, outputs, nodes, branches, loops, ownership seams, gates, and concrete evidence producers.
4. Generate or revise the workflow JSON through `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`; keep JSON as authority and Mermaid as presentation.
5. For independent checks, build one shared bounded context, run complete `ConcurrencyStrategy.All` batches, aggregate once, repair once, and revalidate as described in the [review contract](./reference/review-and-evidence-contract.md).
6. Run exact-runtime compile. Treat compile as structural evidence only.
7. Run the exact external workflow copy through public `run` and every required `resume` until final `Done`. Never claim completion from compile, a blocked payload, local orchestration, or a different workflow copy.
8. Verify target deliverable changes, event/audit records, output-family values, Mermaid/HTML paths, and the completion manifest.

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

Every `.agent.md` named by this skill is the exact behavior contract for that role; host registration is only a dispatch mechanism.

- First invoke the exact declared agent name. Resolve the exact `.agent.md` named by this skill from its `assets/agents/` folder. In standalone installs, resolve that same relative path under the active skill root, such as `~/.agents/skills/<skill-folder>/` or `~/.claude/skills/<skill-folder>/`.
- If the host responds `agent not found`, do not switch to a similar role or treat the failure as a review result. If the exact matching `.agent.md` exists, invoke an available registered generic subagent only as the driver and pass the exact file path, its full contents, and all required inputs and reference context. The driver must perform the declared role and return its declared output contract.
- Never pass only a path or summary in place of the full file. Do not use a read-only agent when the contract requires design, editing, validation, or other work.
- If the file is missing or ambiguous, or no available driver can perform its contract, stop at this step, preserve failed evidence, and report the concrete blocker. Do not claim completion or advance dependent checks. A direct/manual fallback requires explicit user approval.

## Core Governance

- Workflow-owned schema and control metadata are English; user/business payloads and localized presentation retain their source/request language.
- User-facing progress, blocked, error, and completion messages for SO and every skill being enhanced must use plain words in the current interaction language. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the explanation; keep exact identifiers in technical details or evidence only.
- All CLI file inputs are complete, closed, path-only files. Keep mutable plans, runtime copies, events, audit output, and decision evidence outside the skill bundle.
- `AskUser` requests only user-owned decisions or values. Runtime-owned facts and artifact paths use runtime-owned continuation.
- Every next step must pass its boundary check on the same external copy; owner-crossing steps also require explicit approval or structured continuation.
- Direct edits to a running workflow copy are blocked-state-only, explicitly approved, minimal emergency workarounds followed immediately by normal SO compile/run/resume.
- Follow the exact-file dispatch and full-content fallback procedure in [Named Agent Resolution](#named-agent-resolution); never substitute a near-match role.
- Write assumptions, corrections, decisions, probes, `events.jsonl`, and audit references under `<execution-output-root>/evidence/`; conversation text is not execution evidence.

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
