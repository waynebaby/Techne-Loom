## Output Requirements

A valid workflow design should make these reviewable in the graph itself when relevant:

- runtime reacquisition and preflight
- package-index capture
- guide capture
- the hard stop that forbids downstream steps until the selected published SO runtime has produced a fresh guide result
- route-gate analysis
- output-evidence analysis
- package-lock drafting or validation
- checked-in deliverable validation
- runtime completion-manifest emission
- review loop branches
- blocked runtime publication
- local `.agent.md` references in both target `SKILL.md` and workflow-template weave-out hints when such a subagent is introduced

Before final workflow emission, include a concise preflight checklist:

- transition checklist (`id`, guard predicate, success predicate, output evidence)
- gate checklist (pass predicate, required evidence, runtime-supported terminal/blocked route mapping, and intermediate-stage applicability in static review evidence)
- seam ownership checklist (all `AskUser` fields are user-owned; runtime-owned fields are excluded)

For every emitted weave-out or handoff, keep the response compact and return only:

- the next action or decision
- the entry document citation
- the necessary workflow/contract citation(s)
- the controlling guide-output citation, when a guide is involved
- the resume payload contract

Do not repeat the full context-pack inventory in the response.

If any checklist item is non-deterministic or lacks evidence shape, revise before final output.

## Output Hint Guidance

When proposing a workflow template, also supply guidance for:

- workflow JSON path
- package lock path
- Mermaid review artifact
- HTML review artifact
- workflow analysis artifact
- node-to-file or node-to-artifact map
- checked-in deliverable evidence
- runtime completion-manifest evidence
- blocked seam payload examples

## What To Avoid

- Do not produce broad governance nodes that hide multiple checks.
- Do not let checked-in business deliverables appear to be replaced by runtime temp files.
- Do not hide re-enhancement logic in prose only.
- Do not rely on repository-global docs outside the context pack.
- Do not create nodes whose descriptions imply that the agent should improvise a hidden internal workflow.
- Do not normalize a policy-approved manual edit to the running external workflow `.json` copy into an ordinary operation. Refer to the local skill reference for its classification and evidence requirements.
