# Skills Input/Output Reference

[中文](../../zh-cn/reference/skills.md)

## Shared Loom-bin rule

- AO skills, SO skills, and any target product that adopts Loom-bin-based skills must preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- Released package index URL: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## `/loom-plan-execution`

### Mission

Guide-first, environment-first entrypoint for plan execution using the plan-execution package flow.

### Inputs

- rich plan text, recommended at 10+ non-empty lines
- or a detailed plan file path
- package channel choice: released or beta
- optional language surface: en or zh-cn; if omitted, the current public guide surface defaults to en, so callers should pass zh-cn explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional audit output path

### Default assumptions

- use the absolute URL of the released or beta package index page that matches the chosen language surface as the source of truth for acquiring the AO package
- require target products that adopt Loom-bin-based skills to preserve released and beta package index absolute URLs in their own docs, using localized mirrors when the product exposes localized package index pages
- treat `dotnet ao.dll --guide [--lang <language>]` as the authoritative runtime surface instead of copying a private execution template
- treat AO as CLI-only in this project; do not rely on MCP hosts or MCP tools

### Output expectations

- package/channel choice confirmation
- absolute package index links
- released/beta package index link set, including localized mirrors when they exist
- guide surface references
- workflow JSON path produced by planner flow
- runtime return payload links, including audit artifacts

### Runtime handoff

- uses `dotnet ao.dll --guide [--lang <language>]` as the source of truth
- uses `dotnet ao.dll planner` to materialize workflow JSON
- uses `dotnet ao.dll run` / `resume` for execution
- blocked runs continue from returned workflow JSON frontier

## `/loom-skill-enhancement`

### Mission

Guide-first entrypoint for creating or upgrading deterministic skills around the SO package flow.

### Inputs

- target skill path or target skill repo path
- deterministic skill goal / upgrade request
- package channel choice: released or beta
- optional language surface: en or zh-cn; if omitted, the current public guide surface defaults to en, so callers should pass zh-cn explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional JSON context file
- optional audit output path

### Default assumptions

- treat the absolute URL of the released or beta package index page that matches the chosen language surface as the source of truth for acquiring the SO package; if execution needs local binaries, install or unpack runtime assets from the selected package channel into an external temporary directory instead of the target repo
- require target products that adopt Loom-bin-based skills to preserve released and beta package index absolute URLs in their own docs, using localized mirrors when the product exposes localized package index pages
- keep SO-owned materials under `<target-skill-root>/assets/so-workflow/`
- generate `<target-skill-root>/assets/so-workflow/skill-plan.md` from the current `SKILL.md` when it exists, or from `goal` plus supporting references when creating a new skill
- when `references/*.md` exists, concatenate them into a temporary `merged-context.md` working note with clear section headers, then convert the needed content into a temporary JSON context file for planner context
- store the workflow template separately; unless the user explicitly picks an audit destination, keep runtime artifacts under a user-level temporary output root instead of `<target-skill-root>/assets/so-workflow/`
- force workflow-template correctness ahead of every other optimization: the generated workflow JSON template must be complete and detailed, must align with the selected channel guide, and must pass `dotnet so.dll compile --workflow-file <path>` before it can become the execution authority for the enhanced target skill
- compress the upgraded `SKILL.md` to roughly 80-100 lines while preserving high-level steps, guardrail headings, SO guidance, and the `## Workflow Contract` title
- mark released-channel wording as Beta Only when stable docs do not actually ship the same SO enhancement surface
- on weave-out, use structured blocked payload fields such as `current_step_kind` to classify the wait category, and consume `skill_hint` literally as the next external action instruction; ask the user only for mandatory human-input seams; treat waits on email, files, messages, or downstream script results as valid external wait states that either return the expected next input shape or pause until the external result arrives; continue automatically only when the structured payload plus literal `skill_hint` point to a non-human continuation
- treat these as skill-layer adaptation defaults rather than generic SO runtime guarantees; if the selected channel guide does not expose an equivalent surface, mark that behavior as Beta Only

### Output expectations

- package/channel choice confirmation
- absolute package index links
- released/beta package index link set, including localized mirrors when they exist
- guide surface references
- deterministic workflow template path produced by the reviewed authoring flow, after guide-alignment review plus `dotnet so.dll compile` succeed; that validated template becomes the execution authority for the enhanced target skill
- runtime return payload links, including audit artifacts

### Runtime handoff

- uses `dotnet so.dll --guide [--lang <language>]` as the source of truth
- lets the AI agent execute `dotnet so.dll compile` / `run` / `resume` directly in the terminal
- uses a reviewed authoring flow to materialize workflow JSON under `<target-skill-root>/assets/so-workflow/`, then runs `dotnet so.dll compile --workflow-file <path>` before execution
- validates that the resulting workflow template is complete and detailed against the selected channel guide, and also requires `dotnet so.dll compile` to succeed before treating it as the execution authority
- uses `dotnet so.dll run` / `resume` to execute deterministic steps
- target skills clone the stored template on each run and re-plan only when variance appears
