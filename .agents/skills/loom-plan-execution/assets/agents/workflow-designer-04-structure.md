## Node Granularity Rules

Every node must satisfy all of these:

- One node, one visible responsibility.
- No node may imply “do a multistep plan” or “figure out the rest.”
- If the instruction could naturally be split into two reviewable actions, split it.
- If a node both gathers context and makes a policy decision, split it.
- If a node both evaluates and writes, split it unless the write is the direct atomic result of that single evaluation.
- If a node both chooses a weave-out route and describes external execution, split the route decision from the external-action handoff.

## Phase-Sliced Workflow Design (Required)

Count only authored state nodes (`StateNode`); exclude transitions and synthetic harness nodes. When a workflow has fewer than 40 authored state nodes, keep whole-workflow design. When a workflow has 40 or more authored state nodes, start with a standalone versioned `loom.workflow-skeleton.v1` design view; it is not a runnable `WorkflowInstance`.

- Freeze stable phase IDs and prefixes, entries and exits, typed imports and exports, dependencies, gate and terminal ownership, cross-phase routes, and explicit backedges before authoring fragments. The owning `context_fields` catalog is the sole authority for context paths, JSON/container types, nullability, and ownership. Every shared business field has one producing phase; owner classification is separate from producer phase.
- New workflows mark every phase `phaseSliced`. A migration may mix `phaseSliced` and `legacy`; every cross-phase read/write remains declared and typed, and strict prefix lint applies to `phaseSliced` phases.
- Treat 25 authored state nodes per phase as a soft cap and record any exception. Each author edits only its phase fragment after the skeleton passes `skeleton-check`; request interface changes instead of editing the frozen skeleton.
- Use the same selected AO apphost for `skeleton-check`, `phase-compile`, `impact`, `merge`, and final `compile`. `phase-compile` is static only: it must not execute business scripts, HTTP, AskUser, or subagents. Aggregate interface-change requests centrally, revise the skeleton, run `impact`, and redo only affected phases and proven dependents. Unknown dependency or ownership requires full revalidation.
- `merge` accepts exactly one passing result for every expected phase, writes a new candidate, and runs complete AO validation. Then run AO `compile` against that exact merged candidate and preserve its audit. Skeleton checks, phase results, and merge success alone are not workflow readiness. Never modify or migrate a live persisted workflow copy.
- Allow at most three repair attempts per phase and two post-merge full-compile repair rounds. At either cap, preserve failure evidence and weave out for an explicit decision; do not retry automatically beyond the cap.

Record `phaseDesignEvidence` inside the existing `static-contract-review.json`; return a `phase_design_evidence` summary linked to that record beside `designEvidence`. Include mode (`wholeWorkflow`, `phaseSliced`, or `mixed`), authored-state count, skeleton/check result when used, per-phase results, aggregated interface changes, impact decision, merged-candidate full-compile result, repair counts, and verdict. Do not create a fourth evidence report. Keep skeletons and fragments as external run support; optional hashes are not freshness or re-attestation gates. Required package-integrity and business-output hash checks remain unchanged.

```text
skeleton-check --workflow-file <path> --skeleton-file <path>
phase-compile --workflow-file <path> --skeleton-file <path> --fragment-file <path> --phase-id <id> --output-file <path>
impact --previous-workflow-file <path> --previous-skeleton-file <path> --candidate-workflow-file <path> --candidate-skeleton-file <path> --output-file <path>
merge --workflow-file <path> --skeleton-file <path> --result-file <path>... --output-file <path>
```

`impact` may also receive repeated `--previous-result-file`, `--candidate-result-file`, and `--changed-phase-id` options. `merge` writes a new candidate; use the same AO apphost to compile that exact file.

