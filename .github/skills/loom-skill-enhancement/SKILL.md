---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill for Loom Skill Orchestrator (`dotnet so.dll`).

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the Loom Skill Orchestrator package flow.

Business scope rule: this skill's business is always to create or modify the target skill deliverables. Runtime validation is supporting work only and can never be reported as the primary outcome.

Guide freshness rule: every enhancement pass must run a fresh `dotnet so.dll --guide [--lang <language>]` from the current selected package runtime before authoring, editing, or validating target-skill deliverables. Do not reuse a stale guide result from an earlier session or an older package version.

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
- Freshness gate: run it from the current selected package runtime for this pass before any enhancement edits or validation work.
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
- When the target project does not already have its own dependencies installed, install only the minimum dependency set required to complete the requested target-skill changes and current guide-aligned validation work. Do not widen into unrelated package restore or optional toolchain installation.
- Keep target-skill source templates and checked-in assets immutable; run/resume against external runtime copies.

Detailed assumptions, interface mappings, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/so-skill-reference.md`

## Workflow Template Governance Baseline

When authoring or refreshing a workflow template:

- enter plan mode before target-skill deliverables are edited whenever the platform supports it
- analyze target-skill inputs, outputs, state nodes, transition groups, guards, branches, loops, user seams, runtime seams, validation gates, and expected output evidence
- generate the workflow template JSON first, then compile it to produce Mermaid, HTML, workflow backup, and workflow analysis artifacts
- keep the workflow template JSON as the authority; Mermaid, HTML, and localized plan prose are display layers only
- repeat a user confirmation loop by applying feedback to the template or its source planning inputs, then recompile before asking for approval again
- model explicit governed steps, guards, seams, and reviewable outputs
- for SO-governed target-skill templates, declare root `templateKind: so-governed-target-skill` plus a root `validation` contract that defines `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`
- require each governed route to name the business-output gates that must be satisfied before `done`, plus the strongest-earned blocked outputs that must exist before a runtime-owned wait boundary
- keep `AskUser` seams limited to user-owned inputs or decisions; runtime-owned facts, runtime provenance, and system-generated artifact paths belong to `WaitResume` or blocked-resume payloads instead
- never use a workflow node, node purpose, or node intention that says or implies `run a multistep plan`
- do not hide open-ended execution behind a generic planner node; split the route into reviewable deterministic steps instead
- review the workflow template for any node whose instruction embeds a multistep plan or a broad prompt to an agent, then break that intent into smaller governed nodes when possible

## Runtime Flow

1. Classify target skill governance state and lock enhancement goal to target-skill delivery.
2. If the target is already enhanced by Loom Skill Orchestrator (`SO-enhanced`), ask one user question with exactly two choices: latest released or latest beta; use that confirmed answer as the upgrade channel for this pass even when the caller suggested a channel up front.
3. Confirm package channel, reacquire the latest Loom Skill Orchestrator package from that channel, and verify command surface with `dotnet so.dll --help` plus the selected public subcommands you will actually use.
4. Run `dotnet so.dll --guide [--lang <language>]` from the selected package runtime and treat that guide result as mandatory authority input for the enhancement pass.
5. Enter plan mode before editing target-skill deliverables: analyze inputs, outputs, branches, loops, seams, gates, and expected evidence, then draft the workflow template and localized review plan.
6. If the target project does not already have its own dependencies installed, install only the minimum dependency set required for the requested target-skill changes plus the current guide-aligned validation path.
7. Strongly recommend a subagent review of the current target skill and Loom Skill Orchestrator workflow assets against that latest guide result before editing.
8. Prepare runtime:
	- restore full Loom Skill Orchestrator bundle into one unified runtime directory in package-channel mode,
	- run startup-contract preflight,
	- apply explicit launch mode when required.
9. Prepare enhancement inputs (`skill-plan`, references merge/context conversion, lock metadata, node-to-file map).
10. Author or refresh the workflow template with explicit governed steps only, write the root governed-template validation contract when SO-exclusive governance applies, review it for any node instruction that embeds a multistep plan or a broad prompt to an agent, break that intent into smaller nodes when possible, and run `compile` before any execution authority claim.
11. Present the compiled Mermaid, workflow analysis report, localized review plan, gates/routes summary, and node-to-file map to the user; apply feedback to the template and recompile until confirmed.
12. Enforce target-skill lock reference and runtime restoration policy in the enhanced target `SKILL.md`.
13. Execute run/resume only against external runtime workflow copies; keep checked-in templates immutable.
14. On variance, inspect status/workflow/events, update source template, and re-compile.
15. Report completion only after requested target-skill deliverables are created/modified and governance wording is aligned.

Detailed runtime command forms, payload conventions, and progress-report field contracts are maintained in reference docs.

## Required Outputs

- package/channel confirmation and released/beta English canonical links
- when re-enhancing an already SO-enhanced skill: the recorded two-choice latest-version user confirmation and selected latest channel/version
- runtime source/channel and package lock metadata for enhancement pass
- package-channel runtime facts: version, bundle list, unified runtime directory, preflight result, and launch mode
- mandatory `dotnet so.dll --guide [--lang <language>]` invocation evidence for the selected package runtime
- workflow template and runtime workflow-copy/event/audit artifact links
- workflow analysis report (`workflow.analysis.json`) for inputs, outputs, branches, loops, seams, gates, and Turing-complete control risk
- compiled Mermaid with node-type coloring derived from workflow step kinds
- localized review plan when the user-facing language is not English
- confirmation-loop transcript showing how user feedback changed the workflow template before execution
- node-to-file or node-to-artifact map for every governed node in the target-skill workflow
- root governed-template validation contract evidence for future target-skill workflows: `templateKind`, `validation.gates`, `validation.routes`, `validation.declaredUserOwnedFields`, and `validation.reservedRuntimeOwnedFields`
- route-aware business-output gate evidence showing what must exist before `done` and what strongest-earned business artifacts must exist before any runtime-owned blocked boundary
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
- dependency installation widens beyond the minimum set required for the requested target-skill changes and current guide-aligned validation path
- direct CLI or direct MCP output can count as skill execution history
- direct CLI or direct MCP tests can count as official skill execution evidence
- prose flow, CLI examples, or MCP examples are official execution authority by themselves
- a qualifying target skill may remain under weak dual-track governance after this enhancement pass
- an already SO-enhanced target is re-enhanced by silently reusing the old lock channel/version without asking the user to choose latest released or latest beta for this pass
- re-enhancement proceeds without a fresh `dotnet so.dll --guide [--lang <language>]` run from the user-confirmed package channel
- a workflow template contains or implies any node whose purpose is to `run a multistep plan` instead of enumerating explicit governed steps, guards, and seams
- a workflow template can reach `done` with governance-only outputs and without satisfying route-aware business-output gates
- an `AskUser` seam requests runtime-owned fields, runtime provenance, compile or audit paths, `workflow_file`, `event_log_file`, or system-generated artifact locations
- a blocked route pauses without publishing the strongest-earned business artifacts declared for that route
- an SO-governed target-skill workflow omits the root governed-template validation contract

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
- for SO-governed target-skill templates, the root `templateKind: so-governed-target-skill` and root `validation` contract are present and compile-clean
- route-aware business-output gates are declared for terminal routes, strongest-earned blocked outputs are declared for blocked routes, and compile success proves both gate validity and seam ownership validity
- `AskUser` seams request only declared user-owned fields or decisions; runtime-owned facts and artifact paths are carried by runtime-owned seams such as `WaitResume`
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
