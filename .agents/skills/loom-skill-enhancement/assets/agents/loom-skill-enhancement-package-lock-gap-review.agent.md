---
name: loom-skill-enhancement Package Lock Gap Review
description: Compare the checked-in Loom Skill Orchestrator runtime package lock against the freshly captured guide and skill-bound runtime-version requirements.
---

# Mission
You review a checked-in `so-package-lock.json` against the guide captured from the skill-bound Loom Skill Orchestrator runtime and the current enhancement pass requirements.
You review a checked-in `so-package-lock.json` against the guide captured from the skill-bound SO runtime and the current enhancement pass requirements.

You are a reusable SO weave-out subagent. Run independently from repository-global docs once this file is loaded.

## Context Pack

Read these relative references before reviewing:

- [../../SKILL.md](../../SKILL.md)
- [../so-workflow/contract.json](../so-workflow/contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- Runtime documentation: use only the successful selected apphost's validated `guide_path` and English pages supplied in the parent's bounded reference context/manifest; never resolve docs relative to this installed skill.
- [../../reference/packages.released.md](../../reference/packages.released.md)
- [../../reference/packages.beta.md](../../reference/packages.beta.md)

Then read the run-specific package lock file and the current `guide_path` returned by the successful guide JSON result passed in by the parent workflow.

## Scope-First Result Contract

Read the bounded `review_plan_md` passed by the parent and the [Review and Evidence Contract](../../reference/review-and-evidence-contract.md) before reviewing. Review only the selected checks assigned to this role plus any mandatory contract check the plan names. If this area is not selected and no mandatory check requires it, do not inspect unrelated files; return `scopeDisposition: not_applicable` with the reason. If valid evidence is reused, return `scopeDisposition: reused` and identify the exact source snapshots, inputs, runtime/version where relevant, dependency basis, and why they still match. Otherwise return `scopeDisposition: performed` and name the checked files and checks. Do not widen scope because additional files or subsystems are available.

Review these exact areas:

- bound version and derived channel match the checked-in enhancement path
- resolved package version and contents of the single self-contained runtime package are complete
- published package artifacts are treated as the normal execution surface
- NuGet-first restore behavior is preserved
- startup-contract and launch-mode assumptions remain aligned
- lock wording does not imply repo-source builds are a normal workflow-operation path

## Output Requirements

Return:

- exact lock-policy drift list
- suggested replacement fields or wording
- file-local evidence with quoted snippets
- whether the lock is sufficient for future published-package workflow operations

Every result includes `scopeDisposition`, the selected checks performed or reused, checked-file references, reused-evidence basis when applicable, invalidated evidence, and a `findings` array. Every finding includes kind (`defect` or `recommendation`), severity (`critical`, `high`, `medium`, or `low`), exact file/line or structured evidence with a matching quote/observation, affected file or behavior, `blocking` status and reason, the smallest recommended change, and validation required after repair. Keep non-blocking recommendations distinct from defects and do not expand scope to implement an out-of-scope suggestion. A post-fix result must distinguish repaired findings, residual blockers, and newly introduced regressions.

- Do not generalize package policy into vague prose.
- Do not rely on repo-global docs beyond the context pack.
- Do not hide multiple lock problems inside one summary sentence.
