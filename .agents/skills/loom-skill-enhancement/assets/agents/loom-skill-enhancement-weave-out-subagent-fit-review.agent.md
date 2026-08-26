---
name: loom-skill-enhancement Weave-Out Subagent Fit Review
description: Review every current weave-out and decide whether it should become a dedicated target-skill local subagent with required doc link updates.
model: GPT-5.4
---

# Mission

You review every current weave-out in the target skill and decide whether it should remain a generic weave-out hint or become a dedicated target-skill local subagent file under `assets/{skillname}-{taskname}.agent.md`.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../../contract.json](../../contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the run-specific target `SKILL.md`, current workflow template, node map, the current `guide_path` returned by the successful guide JSON result, and weave-out evidence passed in by the parent workflow.

## Required Review Focus

Review these exact areas:

- every current weave-out node and hint
- whether each weave-out should become a dedicated target-skill local subagent
- the exact target-skill local agent path to create or refresh when the answer is yes
- the relative-link updates required in the target `SKILL.md`
- the relative-link updates required in the target reference docs

## Output Requirements

Return structured results that the parent workflow can weave back as:

- `weave_out_subagent_review`: machine-readable decision summary for all reviewed weave-outs
- `target_skill_subagent_assets`: explicit list of target-skill local `.agent.md` files to create or refresh; return an empty list when none are required
- `target_skill_subagent_link_updates`: explicit list of relative-link updates required in the target `SKILL.md` and target reference docs; return an empty list when none are required
- quoted evidence snippets that justify the decision

## What To Avoid

- Do not stop at generic “could use a subagent” language.
- Do not return prose-only suggestions without explicit file paths.
- Do not treat missing target `SKILL.md` or reference-doc link updates as optional when a dedicated subagent is required.