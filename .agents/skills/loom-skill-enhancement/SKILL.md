---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Upgrade or create a target skill through the published Loom Skill Orchestrator (`so`) workflow. Target-skill delivery is the business result; runtime checks are supporting evidence.

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

<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.3.283-beta`.
- This block is refreshed by the publish workflows whenever SO package versions change, so the skill contract stays aligned with the latest published beta package set.
<!-- skill-package-version-block:end -->


- `assets/so-workflow/so-package-lock.json` is the exact-version authority and checked-in lock reference target. Derive the channel from that version; do not ask the user to choose it.
- The platform resolver owns runtime mode, RID, package identity, executable, cache, and launch path. Do not persist those values in skill-owned state.
- Released and beta package indexes are `reference/packages.released.md` and `reference/packages.beta.md`.
- [Migration script playbook](./reference/migration-script-playbook.md): path-safe migration entry points, producer boundaries, dry-run behavior, and repeatable fixture checks.

## Non-Negotiable Entry

Every enhancement pass must first prove that the skill-bound published Loom Skill Orchestrator runtime is runnable.

1. Create a fresh external workflow copy and preserve one `caseId`/`runId` lineage.
2. Preflight the exact published runtime according to the [execution contract](./reference/execution-contract.md). Stop on failure.
3. Ask the platform-aware resolver for `runtime_launch_descriptor_ref`. Use that descriptor to generate the requested VS Code `mcp.json` and Claude `.mcp.json` through the selected runtime, then try MCP registration, handshake, and `so_inspect_workflow_fragment` against the same external workflow copy.
4. If MCP cannot be provided before successful command dispatch, use the same descriptor for the bounded `inspect-workflow-fragment` CLI backup and record one allowed fallback reason. An MCP application or command failure after startup remains a failure.
5. Use the same descriptor for the fresh `--guide` operation, then collect downstream inputs, plan, author, validate, compile, run, or resume.

## Workflow Procedure

1. Classify the target as new or already Loom-governanced; lock the requested target-skill deliverables.
2. Enter plan mode before editing target-skill deliverables. Build the bounded reference pack and per-run plan under `<execution-output-root>/plan/`.
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

## Core Governance

- Workflow-owned schema and control metadata are English; user/business payloads and localized presentation retain their source/request language.
- All CLI file inputs are complete, closed, path-only files. Keep mutable plans, runtime copies, events, audit output, and decision evidence outside the skill bundle.
- `AskUser` requests only user-owned decisions or values. Runtime-owned facts and artifact paths use runtime-owned continuation.
- Every next step must pass its boundary check on the same external copy; owner-crossing steps also require explicit approval or structured continuation.
- Direct edits to a running workflow copy are blocked-state-only, explicitly approved, minimal emergency workarounds followed immediately by normal SO compile/run/resume.
- Never hide a visible multistep plan inside one node. Named local `.agent.md` files are the authoritative subagent contracts.
- Write assumptions, corrections, decisions, probes, `events.jsonl`, and audit references under `<execution-output-root>/evidence/`; conversation text is not execution evidence.

## Stable Assets

- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`
- `assets/so-workflow/restore-so-runtime.ps1`
- `assets/so-workflow/node-to-file-map.md`
- `assets/so-workflow/governance-notes.md`
- `assets/so-workflow/reference/document-copy-manifest.json`
- Workflow designer subagent: `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`
- MCP-first startup subagent: `assets/agents/loom-skill-enhancement-mcp-startup.agent.md`
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

- requested target-skill files were created or modified;
- exact published runtime preflight, MCP registration attempt, either MCP or descriptor-driven CLI fragment evidence, and fresh guide evidence passed;
- exact-runtime semantic probes and batch migration evidence passed where applicable;
- one workflow-copy lineage reached final `Done` through public `run`/`resume`;
- review, repair, post-fix validation, boundary checks, event log, audit artifacts, and durable decision evidence are readable;
- the runtime-owned completion manifest references existing evidence rather than self-certifying missing proof.

The package lock metadata splits into a checked-in lock reference target, resolved runtime bundle version/channel evidence, and a runtime-owned completion-manifest reference to the checked-in lock asset. The checked-in skill-markdown governance outcome likewise carries a runtime-owned completion-manifest reference to that checked-in source asset.

Use the [review and evidence contract](./reference/review-and-evidence-contract.md) for the complete output checklist and the [plain-language contract](./reference/plain-language-feedback.md) for user-facing completion or failure text.
