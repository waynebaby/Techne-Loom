## Dataflow And Plan-Ownership Protocol

Before emitting a workflow template, complete these phases and reject the design if any phase is incomplete:

### Phase A: Lock The Runtime Contract

Record the package-lock version/channel, published guide path, runtimeBinding, expressionBinding, external result merge behavior, gate evaluator behavior, and failed-instance resume behavior. Unknown runtime behavior must remain unknown and cannot be filled by template intuition.

### Phase B: Emit A Per-Transition Dataflow Manifest

For every external transition, record `transition_id`, step kind, payload paths, required inputs, `resumeOutputKey`, `outputPath`, projection mode, produced context paths, output family bindings, route names, and the expected post-resume context shape. The plan path must reference the temporary runtime-owned `<execution-output-root>/plan/skill-plan.md`, never `assets/so-workflow/skill-plan.md`. Never copy, move, migrate, or include the plan content in the skill being enhanced's asset root, package, release, or deliverables. Runtime-owned context and evidence may retain only its path and hash, never its content.

### Phase C: Build The Gate-To-Producer Matrix

For every gate family, record the producer transition, exact output path or binding, route reachability, value semantics, instance binding, and failure next action. `satisfiesGateIds` and `publishesOutputFamilies` are declarations only; an unresolved producer rejects the template.

### Phase D: Generate Minimal Resume Fixtures

For each external seam, provide a fresh ready-to-start fixture, blocked payload, valid resume envelope, expected context paths, next guard/succeed/gate result, and negative cases for missing fields, duplicate wrappers, empty collections, and wrong transition ids.

### Phase E: Reject Unsafe Output

Reject any implicit payload wrapper, missing projection, family without a concrete producer, undeclared empty-value semantics, plan path inside a skill being enhanced, missing failure guidance evidence, missing fixture, or claim of `Done` before the official same-copy run/resume chain reaches final `Done`.
## Re-Enhancement Rules

For targets already under Loom Skill Orchestrator governance:

- model governance-state classification explicitly
- model inspection of existing `SKILL.md`, package lock, and workflow governance assets explicitly
- model explicit reuse of the checked-in bound runtime version and derived channel from `so-package-lock.json` rather than a user-facing released-versus-beta choice
- model runtime reacquisition and guide refresh explicitly
- model guide-delta review explicitly for each important governed artifact family
- do not collapse the whole re-enhancement path into one branch and one compare node

## Reusable Local Weave-Out Subagents

Prefer these existing reusable local subagents before inventing new generic review nodes:

- [loom-skill-enhancement-skill-markdown-gap-review.agent.md](./loom-skill-enhancement-skill-markdown-gap-review.agent.md)
- [loom-skill-enhancement-package-lock-gap-review.agent.md](./loom-skill-enhancement-package-lock-gap-review.agent.md)
- [loom-skill-enhancement-workflow-governance-gap-review.agent.md](./loom-skill-enhancement-workflow-governance-gap-review.agent.md)
- [loom-skill-enhancement-scope-input-output-analysis.agent.md](./loom-skill-enhancement-scope-input-output-analysis.agent.md)
- [loom-skill-enhancement-route-gate-analysis.agent.md](./loom-skill-enhancement-route-gate-analysis.agent.md)
- [loom-skill-enhancement-evidence-node-map-analysis.agent.md](./loom-skill-enhancement-evidence-node-map-analysis.agent.md)

