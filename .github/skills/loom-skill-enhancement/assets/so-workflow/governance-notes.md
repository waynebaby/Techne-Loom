# Loom Skill Enhancement Self-Bootstrap Governance Notes

- This skill is self-bootstrapped through a checked-in SO-governed workflow template.
- This self-bootstrap pass uses `/loom-skill-enhancement` as the current target skill; it does not change the generic mission of rewriting any target skill.
- The template authority is `assets/so-workflow/so-template.json`.
- The planning artifact is `assets/so-workflow/skill-plan.md`.
- The runtime lock is `assets/so-workflow/so-package-lock.json`.
- The workflow first classifies governance state. If the target is already SO-enhanced, it asks exactly one latest-channel question with two choices: released or beta.
- Every pass reacquires the selected runtime bundle and captures a fresh `dotnet so.dll --guide` surface before analysis or validation.
- The checked-in template stays immutable; runtime copies live outside the skill folder.
- The governed template explicitly models draft, compile, AskUser review, blocked runtime, and final completion-manifest steps.
- The self-bootstrap pass must show a user-confirmed review loop before the final lock step.
- The blocked runtime boundary publishes the strongest-earned runtime outputs through a dedicated blocked-governance gate before the final completion manifest step.
- The executable runtime path does not overwrite checked-in source assets; the final runtime step emits an external completion manifest under the OS temp root that references the checked-in source deliverables instead.
- Official run surfaces remain explicit `dotnet so.dll run` and `dotnet so.dll resume` only.
