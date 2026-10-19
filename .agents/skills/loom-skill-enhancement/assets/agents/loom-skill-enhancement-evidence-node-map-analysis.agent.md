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
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the run-specific target `SKILL.md`, the current `guide_path` returned by the successful guide JSON result, node map, plan draft, and target deliverable notes passed in by the parent workflow.

## Priority 1: Skill Being Enhanced Document (Required)

Map the skill being enhanced's `SKILL.md` first and make it the first node-to-document mapping. Record its exact root-relative path, responsible node(s), requested changes, and validation evidence before mapping supporting assets. Do not complete analysis or hand off template drafting until this priority mapping is present and every requested document has an owner node.

## Required Analysis

Return explicit findings for:

- output family coverage
- node-to-file or node-to-artifact mapping
- checked-in deliverables vs runtime-owned artifacts
- completion-manifest referencing requirements
- review evidence sufficiency

## What To Avoid

- Do not redesign route gates here.
- Do not redesign channel-selection policy here.
- Do not hide missing evidence families behind generic “looks good” language.
