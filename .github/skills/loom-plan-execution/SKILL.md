---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and AO runtime surfaces.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the correct package channel and guide surface, then routes execution through the applicable Techne Loom AO runtime surface.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local AO repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill also enforces AO-strong governance for official plan execution. In that governance model, AO is the only official execution authority for this skill, only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs, and any direct non-AO path stays outside official skill execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## When To Use

- The user wants plan execution routed through AO runtime surfaces.
- The request needs blocked-seam handling with `run` and `resume`.
- The request requires governed, auditable execution evidence.

## When Not To Use

- The user only wants brainstorming, not executable plan flow.
- The user only asks for AO runtime diagnostics and explicitly says no business delivery.
- The task is purely non-AO tool usage and does not require AO-governed execution.

## Read This First

Choose package channel first:

- Released (local offline reference): `reference/packages.released.md`
- Beta (local offline reference): `reference/packages.beta.md`

Then read the package guide:

- Released guide (local offline reference): `reference/ao-guide.released.md`
- Beta guide (local offline reference): `reference/ao-guide.beta.md`

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Optional input: guide language flag (`--lang <language>`) when the runtime guide call needs explicit language selection
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Preconditions

- Package channel is chosen (`released` or `beta`).
- Runtime source mode is chosen (`package-channel` or explicit `repo-src-debug`).
- Offline references under `reference/` are available.

## Default Assumptions

Apply these defaults during AO-based plan execution:

- AO is the only official execution authority for this skill; only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- In package-channel mode, restore the full AO runtime bundle into one unified runtime directory, enforce startup-contract preflight, and use explicit launch mode for deterministic host binding.
- In repo-src-debug mode, build and use the current repository AO output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state under `session_dir` or explicit execution-output roots.

Detailed assumptions, startup contracts, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/ao-skill-reference.md`

## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm channel and runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build AO from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: restore full AO bundle into one unified runtime, run startup-contract preflight, and use explicit launch mode.
3. Run guide and planning surfaces (`--guide`, `prompt-plan`) and capture required prompt blocks.
4. Author a WorkflowInstance outside skill paths, then run `compile`.
5. Run AO with that WorkflowInstance when graph continuity matters.
6. On blocked state, use payload signals plus `prompt-replan` to update seam nodes, then `resume` with structured envelope payload.
7. Repeat replan/resume until AO reaches completed state.
8. Report completion only when AO is completed and requested business deliverables are verifiable.

Operational details for prompt blocks, payload conventions, and blocked-state handling are defined in reference docs.

## Failure Handling

- If runtime preflight fails, stop and report the missing startup-contract items.
- If package acquisition fails, do not switch to partial-package execution; rebuild a valid unified runtime first.
- If AO blocks repeatedly with no deliverable progress, return to seam-specific replan instead of declaring completion.
- If business outcomes were requested, reject completion claims that only show runtime artifacts.

## Required Outputs

- package/channel confirmation with released/beta English canonical links
- runtime source selection and channel resolution metadata
- package-channel runtime facts: version, bundle list, unified runtime directory, preflight result, and launch mode
- workflow/session/event paths and audit artifact links
- required think-out-loud fields for runtime and audit updates
- business deliverable verification summary when business-first mode applies

For the full output matrix and field-level contracts, use reference docs.

## Output Quality Bar

- Outputs must be specific, path-addressable, and auditable.
- Each progress update must include required runtime and audit fields.
- Completion output must explicitly distinguish runtime completion vs business delivery completion.

## Prohibited Results

Reject or mark invalid any execution result that says or implies any of these:

- AO is optional for official skill execution
- AO and another path are parallel official execution modes for this skill
- AO runtime artifacts alone are accepted as final completion when the caller explicitly requested business outputs
- business-output requests are silently downgraded to runtime/meta-only execution without explicit user approval
- `compile`, `--guide`, or helper shell steps are normal skill run modes
- `compile`, `--guide`, `prompt-plan`, `prompt-replan`, or helper shell steps are normal skill run modes
- non-AO output can count as official skill execution history
- non-AO tests can count as official skill execution evidence
- prose flow or examples are official execution authority by themselves

## Completion Criteria

Do not treat execution as properly governed until all of these conditions hold:

- only explicit `dotnet ao.dll run` or `dotnet ao.dll resume` counts as an official skill run
- `dotnet ao.dll compile`, `dotnet ao.dll --guide`, `dotnet ao.dll prompt-plan`, and `dotnet ao.dll prompt-replan` are documented as preparation, validation, or authority-supporting surfaces only
- skill-level history only comes from AO workflow state, session state, event logs, or audit artifacts
- skill-level checklist only comes from AO workflow nodes, frontiers, transitions, blocked states, and resume points
- skill-level run map only comes from the AO runtime `workflow_file`, `next_frontier`, blocked state, and audit artifacts
- skill-level evidence only comes from AO-owned runtime state and audit artifacts
- non-AO tests do not count as official skill execution evidence
- prose flow and helper command examples are explanatory only, not execution authority
- when caller objectives explicitly request business outputs, AO completion state alone is insufficient without the corresponding business deliverables

Detailed prohibited/acceptance examples are maintained in reference docs.

## Quick Acceptance Check

- Official runs cited: only `dotnet ao.dll run` and `dotnet ao.dll resume`.
- Runtime preflight and launch mode were reported when package-channel was used.
- Workflow/session/event/audit artifact paths are present.
- Business deliverables are verifiable when business-first mode applied.
