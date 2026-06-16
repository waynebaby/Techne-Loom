---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill for Loom Skill Orchestrator (`dotnet so.dll`).

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the Loom Skill Orchestrator package flow.

Business scope rule: this skill's business is always to create or modify the target skill deliverables. Runtime validation is supporting work only and can never be reported as the primary outcome.

When the target skill is already enhanced by Loom Skill Orchestrator (`SO-enhanced`), this skill must upgrade it all the way to a Loom Skill Orchestrator-exclusive governed skill in one pass (`SO-exclusive governance mode`). In that mode, Loom Skill Orchestrator becomes the only official execution authority, direct CLI or direct MCP remain runtime primitives only, and the target skill must state plainly that it has been enhanced by Loom Skill Orchestrator.
When that re-enhancement path triggers, ask one user question with exactly two choices: update to the latest released package or update to the latest beta package. Use the user-confirmed package channel, reacquire the latest Loom Skill Orchestrator package from that channel, run `dotnet so.dll --guide [--lang <language>]` from that selected package as mandatory authority input, and then continue the enhancement pass.

## Read This First

Choose package channel first:

- Released (local offline reference): `reference/packages.released.md`
- Beta (local offline reference): `reference/packages.beta.md`

Then read the Loom Skill Orchestrator package guide:

- Released guide (local offline reference): `reference/so-guide.released.md`
- Beta guide (local offline reference): `reference/so-guide.beta.md`

Then run the selected package guide surface:

- Mandatory authority input: `dotnet so.dll --guide [--lang <language>]`
- Re-enhancement rule: when the target is already SO-enhanced, always ask the user to choose latest released or latest beta for this pass before continuing, then run the selected package guide surface.

## Input Contract

- target skill path or repository path
- deterministic skill goal or upgrade request
- required target-skill changes to create or modify in this enhancement pass
- package channel: `released` or `beta`
- optional guide language flag (`--lang <language>`) when the runtime guide call needs explicit language selection
- optional JSON context file
- optional audit output root

## Loom Skill Orchestrator Re-Enhancement Upgrade Gate

When the target skill is already enhanced by Loom Skill Orchestrator (`SO-enhanced`):

- ask one user question with exactly two choices: `Update to latest released` or `Update to latest beta`
- do not silently reuse the old lock channel or old locked version as the upgrade decision
- reacquire the latest Loom Skill Orchestrator package from the user-confirmed channel and rewrite the package lock after the pass
- run `dotnet so.dll --guide [--lang <language>]` from that selected package before any new enhancement edits
- strongly recommend a subagent review that compares the current target skill and Loom Skill Orchestrator workflow assets against that latest guide result before editing

## Loom Skill Orchestrator-Exclusive Governance Mode

Enter `SO-exclusive governance mode` immediately when the target skill matches any of these signals:

- it is already declared as SO-enhanced
- the repo already contains SO workflow assets
- the skill or repo exposes `skill-plan`, `so-template`, or audit contracts
- docs describe Loom Skill Orchestrator as an execution authority candidate or official run surface

When this mode triggers, the enhancement target is fixed: upgrade the target skill into a Loom Skill Orchestrator-exclusive governed skill. Do not emit a second-layer target-skill plan. Emit one plan only: how to complete the SO-exclusive governance upgrade in the current enhancement pass.

## Default Assumptions

Apply these defaults during Loom Skill Orchestrator-based skill enhancement:

- Loom Skill Orchestrator enhancement business is always target-skill creation/modification; runtime-only verification cannot be reported as final enhancement outcome.
- In SO-exclusive governance mode, Loom Skill Orchestrator is the only official execution authority and only explicit `dotnet so.dll run` and `dotnet so.dll resume` count as official runs.
- In package-channel mode, restore the full Loom Skill Orchestrator runtime bundle into one unified runtime directory, enforce startup-contract preflight, and use explicit launch mode.
- Keep target-skill source templates and checked-in assets immutable; run/resume against external runtime copies.

Detailed assumptions, interface mappings, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/so-skill-reference.md`

## Workflow Template Governance Baseline

When authoring or refreshing a workflow template:

- model explicit governed steps, guards, seams, and reviewable outputs
- never use a workflow node, node purpose, or node intention that says or implies `run a multistep plan`
- do not hide open-ended execution behind a generic planner node; split the route into reviewable deterministic steps instead
- review the workflow template for any node whose instruction embeds a multistep plan or a broad prompt to an agent, then break that intent into smaller governed nodes when possible

## Runtime Flow

1. Classify target skill governance state and lock enhancement goal to target-skill delivery.
2. If the target is already enhanced by Loom Skill Orchestrator (`SO-enhanced`), ask one user question with exactly two choices: latest released or latest beta; use that confirmed answer as the upgrade channel for this pass even when the caller suggested a channel up front.
3. Confirm package channel, reacquire the latest Loom Skill Orchestrator package from that channel, and verify command surface with `dotnet so.dll --help` plus the selected public subcommands you will actually use.
4. Run `dotnet so.dll --guide [--lang <language>]` from the selected package runtime and treat that guide result as mandatory authority input for the enhancement pass.
5. Strongly recommend a subagent review of the current target skill and Loom Skill Orchestrator workflow assets against that latest guide result before editing.
6. Prepare runtime:
	- restore full Loom Skill Orchestrator bundle into one unified runtime directory in package-channel mode,
	- run startup-contract preflight,
	- apply explicit launch mode when required.
7. Prepare enhancement inputs (`skill-plan`, references merge/context conversion, lock metadata).
8. Author or refresh the workflow template with explicit governed steps only, review it for any node instruction that embeds a multistep plan or a broad prompt to an agent, break that intent into smaller nodes when possible, and run `compile` before any execution authority claim.
9. Enforce target-skill lock reference and runtime restoration policy in the enhanced target `SKILL.md`.
10. Execute run/resume only against external runtime workflow copies; keep checked-in templates immutable.
11. On variance, inspect status/workflow/events, update source template, and re-compile.
12. Report completion only after requested target-skill deliverables are created/modified and governance wording is aligned.

Detailed runtime command forms, payload conventions, and progress-report field contracts are maintained in reference docs.

## Required Outputs

- package/channel confirmation and released/beta English canonical links
- when re-enhancing an already SO-enhanced skill: the recorded two-choice latest-version user confirmation and selected latest channel/version
- runtime source/channel and package lock metadata for enhancement pass
- package-channel runtime facts: version, bundle list, unified runtime directory, preflight result, and launch mode
- mandatory `dotnet so.dll --guide [--lang <language>]` invocation evidence for the selected package runtime
- workflow template and runtime workflow-copy/event/audit artifact links
- explicit target-skill governance updates and lock reference evidence
- target-skill delivery evidence proving requested skill changes were created or modified

For the full field-level output contract, use reference docs.

## Prohibited Results

Reject or mark invalid any enhancement result that says or implies any of these:

- Loom Skill Orchestrator is optional
- CLI and Loom Skill Orchestrator are parallel official execution modes
- CLI mode is a normal skill run mode
- MCP mode is a normal skill run mode
- workflow assets merely imply Loom Skill Orchestrator execution authority without an explicit declaration
- runtime-only or meta-only validation is reported as completed enhancement without producing requested target-skill changes
- direct CLI or direct MCP output can count as skill execution history
- direct CLI or direct MCP tests can count as official skill execution evidence
- prose flow, CLI examples, or MCP examples are official execution authority by themselves
- a qualifying target skill may remain under weak dual-track governance after this enhancement pass
- an already SO-enhanced target is re-enhanced by silently reusing the old lock channel/version without asking the user to choose latest released or latest beta for this pass
- re-enhancement proceeds without a fresh `dotnet so.dll --guide [--lang <language>]` run from the user-confirmed package channel
- a workflow template contains or implies any node whose purpose is to `run a multistep plan` instead of enumerating explicit governed steps, guards, and seams

## Completion Criteria

Do not treat the enhancement as complete until the target skill satisfies all of these conditions:

- only explicit `dotnet so.dll run` or `dotnet so.dll resume` counts as an official skill run
- direct CLI and direct MCP are documented as primitive or component execution only
- skill-level history only comes from Loom Skill Orchestrator workflow state, event log, or audit artifacts
- skill-level checklist only comes from Loom Skill Orchestrator nodes, transitions, guards, and blocked or resume points
- skill-level run map only comes from the Loom Skill Orchestrator workflow template
- skill-level evidence only comes from Loom Skill Orchestrator-owned runtime state and audit artifacts
- direct CLI or direct MCP tests do not count as official skill execution evidence
- prose flow and CLI examples are explanatory only, not execution authority
- the target skill states that it has been enhanced by Loom Skill Orchestrator and is now SO-exclusive governed
- when the target was already SO-enhanced, this pass used a user-confirmed latest released or latest beta package selection and a fresh selected-package `dotnet so.dll --guide [--lang <language>]` run before the enhancement edits
- the workflow template uses only explicit governed steps, guards, seams, and reviewable outputs, with no node intention that says or implies `run a multistep plan`
- requested target-skill deliverables are created or modified; runtime-only validation artifacts are not used as the sole completion evidence

Detailed weak/invalid result examples are maintained in reference docs.

## Allowed And Forbidden Result Examples

Allowed result:

- `execution authority: Loom Skill Orchestrator only`
- `official run: only dotnet so.dll run or dotnet so.dll resume`
- `primitive path: direct CLI and direct MCP are component execution only`
- `history/checklist/run map/evidence: anchored to Loom Skill Orchestrator workflow, event log, guards, seams, template, and audit artifacts`
- `workflow template: explicit governed steps only, never a hidden multistep-plan node intent`
- `completion: this target skill has been enhanced by Loom Skill Orchestrator and is now SO-exclusive governed`

Forbidden weak result:

- `Loom Skill Orchestrator is recommended, but direct CLI is still a normal skill run mode`
- `Loom Skill Orchestrator and CLI are both official execution surfaces`
- `direct MCP results can be counted as skill history`
- `existing workflow assets imply Loom Skill Orchestrator authority even without an explicit declaration`
- `workflow node intention: run a multistep plan`
