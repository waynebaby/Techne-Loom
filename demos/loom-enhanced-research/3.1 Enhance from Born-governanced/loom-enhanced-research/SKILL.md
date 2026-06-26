---
name: loom-enhanced-research
description: End-to-end enhanced research skill with bounded rounds, material review, draft review, and Loom-governanced continuation.
---

# /loom-enhanced-research

Guide-first enhanced research skill.

## Mission

This skill runs end-to-end enhanced research with bounded evidence-building rounds, material review, draft generation, draft review, and approval-driven continuation.

This skill is Loom-governanced under Loom Skill Orchestrator. Ordinary workflow changes stay on the `dotnet so.dll --guide`, `dotnet so.dll compile`, `dotnet so.dll run`, and `dotnet so.dll resume` path. Direct workflow JSON edits are blocked-state-only emergency workarounds that require explicit user approval and an immediate return to the Loom-governanced path. Historical demo timelines for this skill may record earlier compile-ready or blocked states, but those records do not redefine the current completion criteria.

Before any target-skill planning, authoring, validation, compile, run, resume, or downstream input collection, prove that the bound published Loom Skill Orchestrator runtime is runnable and can emit a fresh `dotnet so.dll --guide [--lang <language>]` result from that runtime. If package-channel extraction, startup-contract checks, or guide execution fail, stop immediately and keep runtime proof in a failed state. Do not record pseudo-success proof or export guide artifacts from failed commands.

## Read This First

- Authoritative SO runtime version lock: `assets/so-workflow/so-package-lock.json`
- Workflow authority source template: `assets/so-workflow/so-template.json`
- Workflow plan: `assets/so-workflow/skill-plan.md`
- Node-to-file map: `assets/so-workflow/node-to-file-map.md`
- Released package index reference: `../loom-skill-enhancement/reference/packages.released.md`
- Beta package index reference: `../loom-skill-enhancement/reference/packages.beta.md`
- Released guide surface reference: `../loom-skill-enhancement/reference/so-guide.released.md`
- Beta guide surface reference: `../loom-skill-enhancement/reference/so-guide.beta.md`
- Weave-out subagents:
	- `assets/loom-enhanced-research-research-round.agent.md`
	- `assets/loom-enhanced-research-report-draft.agent.md`

Routine SO runtime restoration must resolve the exact locked runtime bundle from NuGet first. Restore `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` at the same locked version and channel into one unified runtime directory outside the skill folder. On Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; when using `Invoke-WebRequest` or `Invoke-RestMethod`, add `-UseBasicParsing`.

Every new official SO run must start from a freshly copied external runtime workflow file derived from the checked-in source template. Resume in that same execution chain must continue against the same persisted runtime copy. Do not run against the checked-in workflow file in place, and keep runtime workflow copies, event sidecars, and audit artifacts outside the skill folder.

## Input Contract

- research goal
- optional seed query
- optional seed URLs
- max depth
- max rounds
- output root
- evidence policy
- demo mode
- user language
- mandatory freeform intake comments

Material review requires structured decision plus mandatory freeform comments. Draft review requires structured decision plus mandatory freeform comments.

## Workflow Contract

This skill preserves the canonical node map from the planning slice and keeps prose synchronized with the governed workflow template.

### Intake And Setup

- `A` enters the workflow with the user research goal.
- `B`, `B1`, and `B2` collect the structured intake contract plus mandatory native-language freeform comments.
- `C` and `C1` initialize the run root, ledgers, materials, notes, UI, and report targets.

### Research Loop

- `D`, `D1`, `D2`, `D3`, `D4`, and `E` define the bounded research loop.
- Only the research loop may publish net-new evidence.
- Every round records trigger, working hypothesis, selected action, evidence captured, round summary, and continue/stop rationale.

### Material Review

- `F`, `G`, and `G1` assemble and present the full material inventory.
- `H`, `H1`, `H2`, and `H3` collect structured material selections plus mandatory native-language freeform comments.
- `I` and `J` decide whether the next step is another evidence-creating research pass or draft generation.

### Drafting And Review

- `K`, `K1`, and `K2` generate the report draft from the existing evidence chain only.
- `L`, `L1`, and `L2` review the written draft, not the raw material set.
- `M` branches to finalization, more research, or material reselection.
- `N` publishes the final Markdown report.

### Rules

- `B2`, `H2`, and `L2` are mandatory, not optional.
- Freeform text from intake, material review, and draft review is first-class workflow input.
- `O -> D` re-enters bounded research and must preserve explicit round rationale and budget semantics.
- `P -> G` re-enters material review without claiming net-new evidence creation.
- AskUser seams may request only user-owned fields or decisions.
- Runtime-owned facts and artifact paths belong to runtime-owned seams.

## Runtime Flow

1. Resolve the exact SO runtime bundle from `assets/so-workflow/so-package-lock.json` and derive channel from the locked version.
2. Prove the selected published runtime can run and export a fresh `dotnet so.dll --guide [--lang <language>]` result.
3. If package-channel startup preflight fails, stop unless an explicit blocked-state workaround is approved.
4. Compile the checked-in workflow template with `dotnet so.dll compile` before any official run. If the published-package path is blocked and an emergency workaround is explicitly approved, run that compile through the same approved workaround runtime and keep the compile-validation audit reference in runtime-owned artifacts.
5. Start official governed runs only with `dotnet so.dll run` against a fresh external runtime workflow copy.
6. Continue blocked runs only with `dotnet so.dll resume` against that same runtime copy.

## Completion Criteria

- Official run evidence comes only from Loom Skill Orchestrator workflow state, event logs, audit artifacts, and the runtime-owned completion manifest that summarizes the final governed completion verdict for this target skill.
- Completion requires the final report plus the runtime-owned completion manifest outputs declared by the governed route, and that manifest must remain a runtime-owned verdict surface rather than a replacement for the checked-in governed source assets.
- Direct CLI and direct MCP remain primitive paths only and do not replace the Loom-governanced route.