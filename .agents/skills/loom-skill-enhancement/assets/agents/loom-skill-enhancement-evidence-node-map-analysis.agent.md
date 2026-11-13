---
name: loom-skill-enhancement Evidence Node Map Analysis
description: Analyze output evidence, node-to-file mapping, and artifact coverage for skill enhancement workflow design.

---

# Mission

You analyze output evidence coverage, node-to-file mapping, and artifact traceability before workflow template drafting.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../so-workflow/contract.json](../so-workflow/contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- Runtime documentation: use only the successful selected apphost's validated `guide_path` and English pages supplied in the parent's bounded reference context/manifest; never resolve docs relative to this installed skill.

Then read the run-specific target `SKILL.md`, the current `guide_path` returned by the successful guide JSON result, node map, plan draft, and target deliverable notes passed in by the parent workflow.

## Priority 1: Skill Being Enhanced Document (Required)

Map the skill being enhanced's `SKILL.md` first and make it the first node-to-document mapping. Record its exact root-relative path, responsible node(s), requested changes, and validation evidence before mapping supporting assets. Do not complete analysis or hand off template drafting until this priority mapping is present and every requested document has an owner node.

## Scope-First Result Contract

Read the bounded `review_plan_md` passed by the parent and the [Review and Evidence Contract](../../reference/review-and-evidence-contract.md) before reviewing. Review only the selected checks assigned to this role plus any mandatory contract check the plan names. If this area is not selected and no mandatory check requires it, do not inspect unrelated files; return `scopeDisposition: not_applicable` with the reason. If valid evidence is reused, return `scopeDisposition: reused` and identify the exact source snapshots, inputs, runtime/version where relevant, dependency basis, and why they still match. Otherwise return `scopeDisposition: performed` and name the checked files and checks. Do not widen scope because additional files or subsystems are available.

Return explicit findings for:

- output family coverage
- node-to-file or node-to-artifact mapping
- checked-in deliverables vs runtime-owned artifacts
- completion-manifest referencing requirements
- review evidence sufficiency

Every result includes `scopeDisposition`, the selected checks performed or reused, checked-file references, reused-evidence basis when applicable, invalidated evidence, and a `findings` array. Every finding includes kind (`defect` or `recommendation`), severity (`critical`, `high`, `medium`, or `low`), exact file/line or structured evidence with a matching quote/observation, affected file or behavior, `blocking` status and reason, the smallest recommended change, and validation required after repair. Keep non-blocking recommendations distinct from defects and do not expand scope to implement an out-of-scope suggestion. A post-fix result must distinguish repaired findings, residual blockers, and newly introduced regressions.

- Do not redesign route gates here.
- Do not redesign channel-selection policy here.
- Do not hide missing evidence families behind generic “looks good” language.
