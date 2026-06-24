# Loom Skill Enhancement Self-Bootstrap Governance Notes

- This skill is self-bootstrapped through a checked-in SO-governed workflow template.
- This self-bootstrap pass uses `/loom-skill-enhancement` as the current target skill; it does not change the generic mission of rewriting any target skill.
- The template authority is `assets/so-workflow/so-template.json`.
- The planning artifact is `assets/so-workflow/skill-plan.md`.
- The runtime lock is `assets/so-workflow/so-package-lock.json`.
- The workflow first classifies governance state. If the target is already SO-enhanced, it enters an explicit re-enhancement node chain: inspect current `SKILL.md` governance wording, inspect the current package lock, inspect the current workflow governance assets, reuse the exact runtime version already bound by the checked-in lock and current skill package version block, reacquire that published runtime bundle, capture a fresh guide, then route three separate reusable subagents to compare skill-markdown governance, package-lock policy, and workflow-governance assets against that guide before common planning.
- The common planning path is also decomposed into reusable subagents: scope input-output analysis, route-gate analysis, and evidence/node-map analysis before workflow template drafting.
- Every pass reacquires the selected runtime bundle, proves that selected published runtime is runnable, and captures a fresh `dotnet so.dll --guide` surface before analysis, planning, authoring, validation, compile, run, resume, or downstream input collection.
- The governed route now treats the selected guide surface and package-index references as explicit slice outputs rather than leaving them only in prose.
- The checked-in template stays immutable; runtime copies live outside the skill folder.
- The governed template explicitly models draft, compile, AskUser review, blocked runtime, and final completion-manifest steps.
- The self-bootstrap pass must show a user-confirmed review loop before the final lock step.
- The blocked runtime boundary publishes the strongest-earned runtime outputs through a dedicated blocked-governance gate before the final completion manifest step.
- The executable runtime path does not overwrite checked-in source assets; the final runtime step emits an external completion manifest under the OS temp root that references the checked-in source deliverables instead, and should not be read as proof that it recreated those checked-in files.
- Official run surfaces remain explicit `dotnet so.dll run` and `dotnet so.dll resume` only.
