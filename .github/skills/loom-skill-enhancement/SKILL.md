---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill for Loom Skill Orchestrator (`dotnet so.dll`).

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the Loom Skill Orchestrator package flow. Business scope is always target-skill delivery; runtime validation is supporting work only.

Every enhancement pass must run a fresh `dotnet so.dll --guide [--lang <language>]` from the current selected package runtime before editing or validating target-skill deliverables. If the target is already SO-enhanced, ask one user question with exactly two choices: update to latest released or update to latest beta.

## Read First

- Released package index: `reference/packages.released.md`
- Beta package index: `reference/packages.beta.md`
- Released guide: `reference/so-guide.released.md`
- Beta guide: `reference/so-guide.beta.md`
- Authority command: `dotnet so.dll --guide [--lang <language>]`

## Workflow Contract

### Inputs

- target skill path or repository path
- deterministic skill goal or upgrade request
- requested target-skill changes
- package channel: `released` or `beta`
- optional guide language flag
- optional JSON context file
- optional audit output root

### Self-Bootstrap Assets

- `assets/so-workflow/skill-plan.md`
- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`

### Defaults

- Keep Loom Skill Orchestrator-owned materials under `assets/so-workflow/`.
- Treat the checked-in workflow template as immutable; run/resume against an external runtime copy.
- Keep compile and audit artifacts outside the skill folder unless the user explicitly chooses otherwise.
- In SO-exclusive governance mode, only `dotnet so.dll run` and `dotnet so.dll resume` count as official runs.

### Workflow Baseline

- Enter plan mode before editing target-skill deliverables.
- Analyze inputs, outputs, branches, loops, seams, gates, and expected evidence.
- Generate the workflow template JSON first, then compile it.
- Keep the workflow template JSON as the authority.
- Repeat a user confirmation loop by updating the template or its source planning inputs and recompiling.
- For SO-governed target-skill templates, declare root `templateKind: so-governed-target-skill` and a root `validation` contract with `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`.
- Never author a node whose purpose says or implies `run a multistep plan`.

### Required Outputs

- package/channel confirmation
- package index links
- guide surface references
- workflow template path
- workflow analysis report
- compiled Mermaid
- node-to-file or node-to-artifact map
- package lock metadata
- runtime audit artifact links

### Governance

- SO-exclusive governance mode uses Loom Skill Orchestrator as the only official execution authority.
- AskUser seams may request only declared user-owned fields or decisions.
- Runtime-owned facts and artifact paths belong to runtime-owned seams such as `WaitResume`.
- Route-aware terminal and blocked business-output gates are required for governed routes.
- Completion requires requested target-skill deliverables to be created or modified.

## Runtime Flow

1. Classify governance state and lock the goal to target-skill delivery.
2. Confirm package channel and refresh the selected Loom Skill Orchestrator guide surface.
3. Enter plan mode and derive `skill-plan.md`.
4. Author or refresh the workflow template and package lock.
5. Compile the workflow template and review the analysis report.
6. Apply feedback, recompile if needed, then update the target `SKILL.md`.
7. Keep runtime workflow copies, event logs, and audit artifacts outside the skill folder.

## SO-Exclusive Completion

- The target skill states that it has been enhanced by Loom Skill Orchestrator and is now SO-exclusive governed.
- Direct CLI and direct MCP remain primitive paths only.
- Official run evidence comes only from Loom Skill Orchestrator workflow state, event log, and audit artifacts.
