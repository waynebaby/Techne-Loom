# Skills Input/Output Reference

[中文](../../zh-cn/reference/skills.md)

## Shared Loom-bin rule

- AO skills, SO skills, and any target product that adopts Loom-bin-based skills must preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- Released package index URL: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## `/loom-plan-execution`

### /loom-plan-execution Mission

Guide-first, environment-first entrypoint for plan execution using the plan-execution package flow.

It also uses AO-strong governance: AO is the only official execution authority for this skill, and only explicit `dotnet ao.dll run` / `resume` count as official skill runs.

### /loom-plan-execution Inputs

- rich plan text, recommended at 10+ non-empty lines
- or a detailed plan file path
- package channel choice: released or beta
- optional language surface: en or zh-cn; if omitted, the current public guide surface defaults to en, so callers should pass zh-cn explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional audit output path

### /loom-plan-execution Default assumptions

- use the absolute URL of the released or beta package index page that matches the chosen language surface as the source of truth for acquiring the AO package
- require target products that adopt Loom-bin-based skills to preserve released and beta package index absolute URLs in their own docs, using localized mirrors when the product exposes localized package index pages
- treat `dotnet ao.dll --guide [--lang <language>]` as the authoritative runtime surface instead of copying a private execution template
- treat AO as CLI-only in this project; do not rely on MCP hosts or MCP tools
- unless the user explicitly chooses an output location, keep planner, compile, audit, and other runtime temporary files under a runtime temporary root or repo-root temporary root, never under a skill path
- treat AO as the only official execution authority for this skill
- treat only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` as official skill runs
- treat `dotnet ao.dll planner`, `compile`, and `--guide` as authority-supporting preparation or validation surfaces, not official skill runs
- anchor skill-level history, checklist, run map, and evidence to AO workflow state, frontiers, workflow JSON, event logs, and audit artifacts only
- reject non-AO outputs or tests as official skill execution evidence

### /loom-plan-execution Output expectations

- package/channel choice confirmation
- absolute package index links
- released/beta package index link set, including localized mirrors when they exist
- guide surface references
- workflow JSON path produced by planner flow
- runtime return payload links, including audit artifacts
- when the user does not explicitly choose a destination, the effective planner, compile, and audit temporary-output root outside any skill path
- explicit execution authority and official run definitions for AO-only governance
- history, checklist, run-map, evidence, and reporting honesty outputs anchored to AO workflow and audit artifacts

### /loom-plan-execution Runtime handoff

- uses `dotnet ao.dll --guide [--lang <language>]` as the source of truth
- uses `dotnet ao.dll planner` to materialize workflow JSON
- uses `dotnet ao.dll run` / `resume` as the only official skill-run surface
- blocked runs continue from returned workflow JSON frontier

## `/loom-skill-enhancement`

### /loom-skill-enhancement Mission

Guide-first entrypoint for creating or upgrading deterministic skills around the SO package flow.

When the target skill is already SO-enhanced, this skill upgrades it in one pass into an SO-exclusive governed skill instead of stopping at generic SO support or documentation refresh.

### /loom-skill-enhancement Inputs

- target skill path or target skill repo path
- deterministic skill goal / upgrade request
- package channel choice: released or beta
- optional language surface: en or zh-cn; if omitted, the current public guide surface defaults to en, so callers should pass zh-cn explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional JSON context file
- optional audit output path

### /loom-skill-enhancement Default assumptions

- treat the absolute URL of the released or beta package index page that matches the chosen language surface as the source of truth for acquiring the SO package; if execution needs local binaries, install or unpack runtime assets from the selected package channel into an external temporary directory instead of the target repo
- require target products that adopt Loom-bin-based skills to preserve released and beta package index absolute URLs in their own docs, using localized mirrors when the product exposes localized package index pages
- keep SO-owned materials under `<target-skill-root>/assets/so-workflow/`
- generate `<target-skill-root>/assets/so-workflow/skill-plan.md` from the current `SKILL.md` when it exists, or from `goal` plus supporting references when creating a new skill
- when `references/*.md` exists, concatenate them into a temporary `merged-context.md` working note with clear section headers, then convert the needed content into a temporary JSON context file for planner context
- store the workflow template separately; unless the user explicitly picks an output destination, keep compile artifacts, audit artifacts, and other runtime temporary files under a runtime temporary root or repo-root temporary root instead of any skill path or `<target-skill-root>/assets/so-workflow/`
- force workflow-template correctness ahead of every other optimization: the generated workflow JSON template must be complete and detailed, must align with the selected channel guide, and must pass `dotnet so.dll compile --workflow-file <path>` before it can become the execution authority for the enhanced target skill
- when the target skill already exposes SO-enhanced signals such as SO workflow assets, `skill-plan` or `so-template` contracts, audit contracts, or SO authority wording, automatically enter SO-exclusive governance mode
- in SO-exclusive governance mode, treat SO as the only official execution authority for the target skill
- in SO-exclusive governance mode, treat only explicit `dotnet so.dll run` and `dotnet so.dll resume` as official skill runs
- in SO-exclusive governance mode, demote direct CLI and direct MCP to runtime primitive or component execution only; they are not official skill runs
- in SO-exclusive governance mode, anchor skill-level history, checklist, run map, and evidence to SO workflow state, event logs, workflow templates, guards, seams, and audit artifacts only
- in SO-exclusive governance mode, require the target skill to state that it has been enhanced by Loom SO and is now SO-exclusive governed
- compress the upgraded `SKILL.md` to roughly 80-100 lines while preserving high-level steps, guardrail headings, SO guidance, and the `## Workflow Contract` title
- mark released-channel wording as Beta Only when stable docs do not actually ship the same SO enhancement surface
- on weave-out, use structured blocked payload fields such as `current_step_kind` to classify the wait category, and consume `skill_hint` literally as the next external action instruction; ask the user only for mandatory human-input seams; treat waits on email, files, messages, or downstream script results as valid external wait states that either return the expected next input shape or pause until the external result arrives; continue automatically only when the structured payload plus literal `skill_hint` point to a non-human continuation
- treat these as skill-layer adaptation defaults rather than generic SO runtime guarantees; if the selected channel guide does not expose an equivalent surface, mark that behavior as Beta Only

### /loom-skill-enhancement Output expectations

- package/channel choice confirmation
- absolute package index links
- released/beta package index link set, including localized mirrors when they exist
- guide surface references
- deterministic workflow template path produced by the reviewed authoring flow, after guide-alignment review plus `dotnet so.dll compile` succeed; that validated template becomes the execution authority for the enhanced target skill
- runtime return payload links, including audit artifacts
- when the user does not explicitly choose a destination, the effective compile and audit temporary-output root outside the target skill path and outside `<target-skill-root>/assets/so-workflow/`
- when SO-exclusive governance mode applies, an explicit declaration that SO is the only official execution authority, that only `dotnet so.dll run` / `resume` count as official skill runs, and that direct CLI or direct MCP remain primitive paths only
- when SO-exclusive governance mode applies, explicit history, checklist, run-map, evidence, reporting honesty, and test classification outputs anchored to SO workflow and audit artifacts
- when SO-exclusive governance mode applies, explicit completion wording that the target skill has been enhanced by Loom SO and is now SO-exclusive governed

### /loom-skill-enhancement Runtime handoff

- uses `dotnet so.dll --guide [--lang <language>]` as the source of truth
- lets the AI agent execute `dotnet so.dll compile` / `run` / `resume` directly in the terminal
- uses a reviewed authoring flow to materialize workflow JSON under `<target-skill-root>/assets/so-workflow/`, then runs `dotnet so.dll compile --workflow-file <path>` with compile and audit temporary output routed to runtime temp or repo-root temp unless the user explicitly chooses another location
- validates that the resulting workflow template is complete and detailed against the selected channel guide, and also requires `dotnet so.dll compile` to succeed before treating it as the execution authority
- uses `dotnet so.dll run` / `resume` as the only official target-skill run surface when SO-exclusive governance mode applies
- target skills clone the stored template on each run and re-plan only when variance appears
