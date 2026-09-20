---
name: Loom Skill Governance Rules
description: Planning, enhancement, reviewer, subagent, and governed-skill evidence rules for Loom skill work.
applyTo: ".agents/skills/**,demos/**,docs/**"
---

# Loom Skill Governance Rules

Use these rules when authoring, reviewing, enhancing, or validating Loom skills and their governed workflow assets.

## Workflow Designer Reference And Layered Validation

- Before authoring or revising an AO or SO workflow, consume a bounded reference pack. It must identify the exact runtime version and include the fresh guide JSON and returned guide, same-runtime schema and demo, successful same-runtime demo compile audit, current target contract and requirements, current workflow source, latest compile diagnostics when revising, and applicable `AGENTS.md`.
- Every reference entry records normalized path, SHA-256, runtime version, `authorityRole`, read status, and validation result. Schema, demo, and demo audit share one generation-set identity.
- `authority` covers exact-runtime guide/schema/demo/successful audit and version-matched runtime contract docs. `current_contract` covers target contract, requirements, applicable `AGENTS.md`, and current workflow. `diagnostic_evidence` covers compile feedback and historical probes. `previous_runnable_reference` covers only an older runnable workflow. `supplemental` covers generated models, fixtures, excerpts, and other non-authoritative material.
- Authority precedence is exact-runtime guide/schema, same-runtime demo/audit, current requirements/contract, current runtime governance docs, current workflow/diagnostics, previous runnable reference, then runtime source/reflection only as supplemental evidence.
- A previous runnable workflow must never override current schema, requirements, or runtime behavior. Record its version, hash, copy time, reusable shapes, current differences, and rejected/deprecated items before using it as comparison material.
- Designer validation is ordered `runtime`, `JSON`, `graph`, `enum`, `expression`, `projection`, `dataflow`, `gate`, `ownership`, `semantic`. Stop at the first failed or required-unknown layer, repair only that layer, then rerun earlier layers. Compile is a later checkpoint and never proves semantic readiness.
- Each design run keeps runtime-owned `reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` under `<execution-output-root>/workflow-design/`. Use schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`.
- A governance wrapper exposes only governance responsibilities and a structured handoff to the owning domain orchestrator. It must not duplicate the domain workflow.

## Subagent Authority Rules

- When a skill or target skill names a subagent markdown file such as `./assets/agents/<agent-name>.agent.md`, that exact file is authoritative.
- Do not require skill-owned or target-skill-owned agent files to be mirrored into `.github/agents/` or another discoverable root.
- If the runtime resolves the exact subagent name, call it directly while treating the declared file as the behavior contract. If not, resolve the declared file path and pass its full content into the subagent-driving call.
- Resolve a declared skill-owned or target-skill-owned file from the current repository first and the corresponding global installed-skill copy second. Do not improvise a near-match role or substitute repository-global prose once a file is named.

## Loom Skill Enhancement Governance

- `/loom-skill-enhancement` must plan before editing a target skill: analyze inputs, outputs, nodes, guards, branches, loops, user seams, runtime seams, checks, and output evidence.
- Re-enhancement strategy belongs in repository governance and skill references, not publishable subagent bodies. Apply it equally when the target is `/loom-skill-enhancement`: self-bootstrap uses its checked-in old template, current contract/concept references, and fresh guide as one input set; record the strategy for the run and never recursively launch another enhancement run.
- Self-bootstrap-only exceptions must not alter generic published skill or subagent behavior. Applicability comes from repository policy and run context.
- `/loom-skill-enhancement` and every Loom-governanced target skill use the Loom Skill Orchestrator route. No step advances until it passes a boundary check on the exact external runtime workflow copy and receives explicit approval or structured continuation. Compile-clean is only a precondition; inferred intent, prose, stale guide results, unapproved drafts, local orchestration, and direct workflow JSON edits are not valid continuation.
- The workflow template JSON is authoritative. Mermaid, HTML, and localized plan text are display layers; feedback must update the template or source plan inputs, not only rendered Mermaid.
- Full-delivery enhancement success continues on the public `dotnet so.dll run`/`resume` chain through final completion. Compile-review completion, blocked seams, and compile-ready wording are not normal completion states unless the user changes the contract before implementation.
- When a governed route includes business-intake or `AskUser` seams, completion must weave back through them on the same workflow-copy lineage. A blocked seam is blocked evidence, not completion.
- Workflow visualizations use stable node-type semantics: AI/model/subagent work green, code/tool work blue, optional user choices yellow, mandatory mid-run user input red, and required checks white or light gray.
- Enhancement completion evidence includes the final workflow template, generated Mermaid, node-to-file or node-to-artifact mapping, actual implementation/audit evidence, and changed target-skill deliverables. Runtime-only validation is insufficient.
- Step 1 of `/loom-skill-enhancement` is the reusable foundation: plan mode, workflow analysis, template generation, compile-generated Mermaid, confirmation loop, node-to-file mapping, and final evidence reporting.
- Step 2 self-bootstrap begins only after Step 1 review/fix/validate/commit. Self-bootstrap may consume the current repository build result only as audit evidence; future official skill behavior still restores the latest package/channel runtime and lock semantics.
- Enhancement plans and mutable run checklists are per-run evidence under the execution output root. They are not stable target-skill assets; completion manifests may reference them without copying them into a skill bundle.
- Self-bootstrap backups occur after Step 1 commit and before Step 2 edits. Back up only skill-local files to the audit root unless a wider snapshot is explicitly requested.

## SO Enhancement Batch Review Method

- Build one bounded, hashable shared review context after governance-entry fragment proof and fresh guide capture. It carries the source manifest, bounded source snapshots, guide/schema/runtime references, content hash, and external workflow-copy identity.
- Independent review or validation responsibilities consume the shared context by reference and run as one `ConcurrencyStrategy.All` external batch when independent. A batch is complete only after every expected transition returns.
- Aggregate all findings into one explicit findings record before repair. The repair step receives the complete aggregate and applies one coordinated repair pass across affected deliverables.
- After repair, run independent post-fix checks as a second `ConcurrencyStrategy.All` batch. Keep final parse, graph/dataflow, compile, and ordered runtime checks in one serial validation phase after all post-fix results arrive.
- Missing, duplicate, or incomplete batch results fail closed and remain on the same persisted workflow copy.
