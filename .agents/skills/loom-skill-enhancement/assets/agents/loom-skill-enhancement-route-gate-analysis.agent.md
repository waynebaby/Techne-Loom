---
name: loom-skill-enhancement Route Gate Analysis
description: Analyze branches, loops, seams, routes, and gate contracts for SO enhancement workflow design.
---

# Mission

You analyze workflow route structure, branch/loop structure, seam ownership, and business-output gate contracts before template drafting.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../../contract.json](../../contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the run-specific target `SKILL.md`, the current `guide_path` returned by the successful guide JSON result, and any current workflow draft or notes passed in by the parent workflow.

## Required Analysis

Return explicit findings for:

- branches and loops
- user seams vs runtime seams
- route structure
- terminal gate requirements
- blocked gate requirements
- where weave-out should be explicit

## What To Avoid

- Do not analyze package-lock field policy here.
- Do not analyze node-to-file evidence mapping here.
- Do not hide multiple route problems in one vague sentence.
