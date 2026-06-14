---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and SO package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill.

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the SO package flow.

Business scope rule: this skill's business is always to create or modify the target skill deliverables. Runtime validation is supporting work only and can never be reported as the primary outcome.

When the target skill is already SO-enhanced, this skill must upgrade it all the way to an SO-exclusive governed skill in one pass. In that mode, SO becomes the only official execution authority, direct CLI or direct MCP remain runtime primitives only, and the target skill must state plainly that it has been enhanced by Loom SO.

## Read This First

Choose package channel first:

- Released (local offline reference): `reference/packages.released.md`
- Beta (local offline reference): `reference/packages.beta.md`

Then read the package guide:

- Released guide (local offline reference): `reference/so-guide.released.md`
- Beta guide (local offline reference): `reference/so-guide.beta.md`

## Input Contract

- target skill path or repository path
- deterministic skill goal or upgrade request
- optional guide language flag (`--lang <language>`) when the runtime guide call needs explicit language selection
- optional JSON context file
- optional audit output root

## SO-Exclusive Governance Mode

Enter `SO-exclusive governance mode` immediately when the target skill matches any of these signals:

- it is already declared as SO-enhanced
- the repo already contains SO workflow assets
- the skill or repo exposes `skill-plan`, `so-template`, or audit contracts
- docs describe SO as an execution authority candidate or official run surface

When this mode triggers, the enhancement target is fixed: upgrade the target skill into an SO-exclusive governed skill. Do not emit a second-layer target-skill plan. Emit one plan only: how to complete the SO-exclusive governance upgrade in the current enhancement pass.

## Default Assumptions

Apply these defaults during SO-based skill enhancement:

- SO enhancement business is always target-skill creation/modification; runtime-only verification cannot be reported as final enhancement outcome.
- In SO-exclusive governance mode, SO is the only official execution authority and only explicit `dotnet so.dll run` and `dotnet so.dll resume` count as official runs.
- In package-channel mode, restore the full SO runtime bundle into one unified runtime directory, enforce startup-contract preflight, and use explicit launch mode.
- Keep target-skill source templates and checked-in assets immutable; run/resume against external runtime copies.

Detailed assumptions, interface mappings, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/so-skill-reference.md`

## Runtime Flow

1. Confirm package channel and verify command surface (`dotnet so.dll --help` / `parameter --help`).
2. Classify target skill governance state and lock enhancement goal to target-skill delivery.
3. Prepare runtime:
	- restore full SO bundle into one unified runtime directory in package-channel mode,
	- run startup-contract preflight,
	- apply explicit launch mode when required.
4. Prepare enhancement inputs (`skill-plan`, references merge/context conversion, lock metadata).
5. Author or refresh workflow template and run `compile` before any execution authority claim.
6. Enforce target-skill lock reference and runtime restoration policy in the enhanced target `SKILL.md`.
7. Execute run/resume only against external runtime workflow copies; keep checked-in templates immutable.
8. On variance, inspect status/workflow/events, update source template, and re-compile.
9. Report completion only after requested target-skill deliverables are created/modified and governance wording is aligned.

Detailed runtime command forms, payload conventions, and progress-report field contracts are maintained in reference docs.

## Required Outputs

- package/channel confirmation and released/beta English canonical links
- runtime source/channel and package lock metadata for enhancement pass
- package-channel runtime facts: version, bundle list, unified runtime directory, preflight result, and launch mode
- workflow template and runtime workflow-copy/event/audit artifact links
- explicit target-skill governance updates and lock reference evidence
- target-skill delivery evidence proving requested skill changes were created or modified

For the full field-level output contract, use reference docs.

## Prohibited Results

Reject or mark invalid any enhancement result that says or implies any of these:

- SO is optional
- CLI and SO are parallel official execution modes
- CLI mode is a normal skill run mode
- MCP mode is a normal skill run mode
- workflow assets merely imply SO execution authority without an explicit declaration
- runtime-only or meta-only validation is reported as completed enhancement without producing requested target-skill changes
- direct CLI or direct MCP output can count as skill execution history
- direct CLI or direct MCP tests can count as official skill execution evidence
- prose flow, CLI examples, or MCP examples are official execution authority by themselves
- a qualifying target skill may remain under weak dual-track governance after this enhancement pass

## Completion Criteria

Do not treat the enhancement as complete until the target skill satisfies all of these conditions:

- only explicit `dotnet so.dll run` or `dotnet so.dll resume` counts as an official skill run
- direct CLI and direct MCP are documented as primitive or component execution only
- skill-level history only comes from SO workflow state, event log, or audit artifacts
- skill-level checklist only comes from SO nodes, transitions, guards, and blocked or resume points
- skill-level run map only comes from the SO workflow template
- skill-level evidence only comes from SO-owned runtime state and audit artifacts
- direct CLI or direct MCP tests do not count as official skill execution evidence
- prose flow and CLI examples are explanatory only, not execution authority
- the target skill states that it has been enhanced by Loom SO and is now SO-exclusive governed
- requested target-skill deliverables are created or modified; runtime-only validation artifacts are not used as the sole completion evidence

Detailed weak/invalid result examples are maintained in reference docs.

## Allowed And Forbidden Result Examples

Allowed result:

- `execution authority: SO only`
- `official run: only dotnet so.dll run or dotnet so.dll resume`
- `primitive path: direct CLI and direct MCP are component execution only`
- `history/checklist/run map/evidence: anchored to SO workflow, event log, guards, seams, template, and audit artifacts`
- `completion: this target skill has been enhanced by Loom SO and is now SO-exclusive governed`

Forbidden weak result:

- `SO is recommended, but direct CLI is still a normal skill run mode`
- `SO and CLI are both official execution surfaces`
- `direct MCP results can be counted as skill history`
- `existing workflow assets imply SO authority even without an explicit declaration`
