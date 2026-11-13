---
name: loom-skill-enhancement Scope Input Output Analysis
description: Analyze skill being enhanced inputs, outputs, and required business deliverables for skill enhancement workflow design.
---

# Mission

You analyze the the declared inputs, outputs, and required business deliverables before workflow template drafting.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../so-workflow/contract.json](../so-workflow/contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- Runtime documentation: use only the successful selected apphost's validated `guide_path` and English pages supplied in the parent's bounded reference context/manifest; never resolve docs relative to this installed skill.

Then read the run-specific target `SKILL.md`, the current `guide_path` returned by the successful guide JSON result, and any target deliverable notes passed in by the parent workflow.

## Scope And Review Plan

Use the [Review and Evidence Contract](../../reference/review-and-evidence-contract.md) as the authority for scope selection and evidence reuse. Before broad review or execution, derive the actual changed files and behavior boundaries from the request and current snapshot/diff, identify affected consumers/dependencies and risk, and preserve the active contract's mandatory approval, runtime, public run/resume, and terminal gates. Do not select checks merely because a subsystem, skill, node, or evidence family exists.

Return a bounded Markdown `review_plan_md` within `scope_analysis` that states: changed files and behavior boundaries; risk rationale; the minimum selected checks, their targets, reasons, evidence producers, and expected results; reusable evidence with its source snapshots, inputs, runtime/version where relevant, dependencies, and validity rationale; invalidation causes; excluded checks with reasons; and gates that remain mandatory. Keep complete source inventories and detailed evidence external; this field is a concise selection plan, not a full review report. When optional support digests are absent, use the actual snapshots and inputs; do not invent hash or revision gates.

## Required Analysis

Return explicit findings for:

- declared skill being enhanced inputs
- declared skill being enhanced outputs
- required business deliverables
- user-owned vs runtime-owned data boundaries
- which deliverables must be checked in versus runtime-owned

## What To Avoid

- Do not analyze route gates here.
- Do not analyze weave-out subagent reuse here.
- Do not hide multiple different output families in one vague statement.
