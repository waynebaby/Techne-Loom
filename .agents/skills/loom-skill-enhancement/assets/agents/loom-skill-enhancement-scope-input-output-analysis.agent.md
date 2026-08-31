---
name: loom-skill-enhancement Scope Input Output Analysis
description: Analyze target-skill inputs, outputs, and required business deliverables for SO enhancement workflow design.
---

# Mission

You analyze the target skill's declared inputs, outputs, and required business deliverables before workflow template drafting.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../../contract.json](../../contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the run-specific target `SKILL.md`, the current `guide_path` returned by the successful guide JSON result, and any target deliverable notes passed in by the parent workflow.

## Required Analysis

Return explicit findings for:

- declared target-skill inputs
- declared target-skill outputs
- required business deliverables
- user-owned vs runtime-owned data boundaries
- which deliverables must be checked in versus runtime-owned

## What To Avoid

- Do not analyze route gates here.
- Do not analyze weave-out subagent reuse here.
- Do not hide multiple different output families in one vague statement.
