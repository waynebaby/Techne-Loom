---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and AO package binaries.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the correct package channel and guide surface, then routes execution through the installed Techne Loom package binaries.

This skill also enforces AO-strong governance for official plan execution. In that governance model, AO is the only official execution authority for this skill, only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs, and any direct non-AO path stays outside official skill execution.

## Read This First

Choose package channel first:

- Released (main, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta (development, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

Then read the package guide:

- Released guide (English): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/ao-guide.md>
- Beta guide (English): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/ao-guide.md>
- Released guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/ao-guide.md>
- Beta guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/ao-guide.md>

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Optional input: language surface (`en` or `zh-cn`). If omitted, the current public guide surface defaults to `en`, so callers should pass `zh-cn` explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during AO-based plan execution:

- use the package index absolute URLs as the source of truth for acquisition guidance, with NuGet.org as the first-class latest package source and GitHub release assets as fallback downloads
- if this skill is later enhanced by Loom SO, its checked-in `SKILL.md` must explicitly reference `assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock, and any SO DLL restoration in that enhanced mode must resolve the exact locked version from NuGet first and freshly download it unless the local cache already holds that exact version
- require AO skills and any target product that adopts Loom-bin-based skills to preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- keep `dotnet ao.dll --guide [--lang <language>]` as the authoritative runtime surface instead of restating private templates in the skill
- treat AO as CLI-only in this project; do not rely on MCP hosts or MCP tools
- unless the user explicitly requests an output location, keep workflow-authoring intermediates, compile artifacts, audit artifacts, session directories, and other runtime temporary files under a runtime temporary root or repo-root temporary root, not under a skill path
- treat checked-in plan documents and any authored AO workflow snapshots as immutable source artifacts; let AO own mutable runtime state only through `session_dir` outputs such as `workflow_file` and event logs, not through files under a skill folder
- declare AO as the only official execution authority for this skill
- declare only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` as official skill runs
- treat `dotnet ao.dll compile` and `dotnet ao.dll --guide` as authority-supporting preparation or inspection surfaces, not official skill runs
- treat any direct non-AO path as outside official skill execution; it can explain or support execution, but it cannot count as an official run
- anchor skill-level history to AO workflow state, session state, event logs, and audit artifacts only
- anchor skill-level checklist authority to AO workflow nodes, frontiers, transitions, blocked states, and resume seams only
- anchor skill-level run-map authority to the AO runtime `workflow_file`, `next_frontier`, and blocked workflow state; any pre-authored AO workflow JSON is preparation evidence only
- anchor skill-level evidence authority to AO-owned runtime state and audit artifacts only
- require reporting honesty: prose flow, examples, or supporting shell steps are explanatory only unless they are explicit `dotnet ao.dll run` or `dotnet ao.dll resume` executions
- classify non-AO tests or helper-command tests as component or supporting tests only; they cannot count as official skill execution evidence

## DLL Interface Mapping

- `dotnet ao.dll --guide [--lang <language>]`: runtime authority and command surface source of truth
- agent-authored workflow JSON input file: authored by the skill-running agent to fit the AO snapshot schema as a preparation and validation artifact
- `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]`: validate workflow materialization when execution flow requires explicit compile
- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>]`: execute plan objective
- `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path> [--audit-output <path>]`: weave back with structured external result

Only `dotnet ao.dll run` and `dotnet ao.dll resume` can count as official runs for this skill. `--guide` and `compile` remain authority-supporting preparation or validation surfaces, not official skill runs.

## Runtime Flow

1. Confirm package channel from the package index.
2. Run `dotnet ao.dll --guide [--lang <language>]`.
3. When a reusable AO workflow snapshot is needed, have the skill-running agent author the AO workflow JSON input file so it fits the AO snapshot schema as a preparation artifact.
4. Run `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]`.
5. Run `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>]`.
6. When blocked, inspect the returned workflow JSON plus `next_frontier` and continue with `dotnet ao.dll resume`.

Do not let AO runtime-owned mutable state overwrite or live beside checked-in source plan files or checked-in snapshot artifacts.
Do not place `--session-dir` or `--audit-output` inside any skill-owned directory.
Every time AO reports progress, render the current workflow to Mermaid Markdown and HTML under the runtime temp root or explicit execution-output root, and surface those file paths in think-out-loud output.

Do not present helper shell steps, prose walkthroughs, or non-AO tooling as peer official execution modes for this skill.

If this skill is SO-enhanced later, the checked-in `SKILL.md` should say it explicitly, reference `assets/so-workflow/so-package-lock.json`, and use an explicit snippet such as:

```md
## SO-Enhanced Runtime Lock

This skill is enhanced by Loom SO.
Authoritative SO runtime version lock: `assets/so-workflow/so-package-lock.json`.
Routine SO DLL restoration must resolve the exact locked version from NuGet first and freshly download it unless the local cache already holds that exact version.
```

## Required Outputs

- chosen package index link
- package index link set for released/beta, including localized mirrors when they exist
- guide link
- DLL interface mapping used by this skill (`--guide`, agent-authored workflow JSON, `compile`, `run`, `resume`)
- optional agent-authored workflow JSON path used for preparation and compile validation
- runtime `workflow_file` / `event_log_file`
- explicit note that checked-in plan or snapshot artifacts remain immutable source files and AO runtime state is emitted under `session_dir` or an explicit execution output root
- when this skill is SO-enhanced, an explicit checked-in `SKILL.md` reference to `assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock for that enhanced mode
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
- current workflow Mermaid Markdown and HTML paths on every AO progress update, surfaced in think-out-loud output
- when the user does not explicitly choose a destination, the effective workflow-authoring, compile, and audit temporary-output root outside any skill path
- explicit execution authority declaration that AO is the only official execution authority for this skill
- official run definition that only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs
- history authority, checklist authority, run-map authority, and evidence authority statements anchored to AO workflow state, frontiers, event logs, workflow JSON, and audit artifacts
- reporting honesty and test classification constraints that reject non-AO output as official skill execution evidence
- explicit note that audit artifacts and intermediate outputs may be referenced in conversation or think-out-loud, but default to temp or explicit execution-output roots rather than skill folders
- explicit note that compile fails instead of overwriting existing artifact files

## Prohibited Results

Reject or mark invalid any execution result that says or implies any of these:

- AO is optional for official skill execution
- AO and another path are parallel official execution modes for this skill
- `compile`, `--guide`, or helper shell steps are normal skill run modes
- non-AO output can count as official skill execution history
- non-AO tests can count as official skill execution evidence
- prose flow or examples are official execution authority by themselves

## Completion Criteria

Do not treat execution as properly governed until all of these conditions hold:

- only explicit `dotnet ao.dll run` or `dotnet ao.dll resume` counts as an official skill run
- `dotnet ao.dll compile` and `--guide` are documented as preparation, validation, or authority-supporting surfaces only
- skill-level history only comes from AO workflow state, session state, event logs, or audit artifacts
- skill-level checklist only comes from AO workflow nodes, frontiers, transitions, blocked states, and resume points
- skill-level run map only comes from the AO runtime `workflow_file`, `next_frontier`, blocked state, and audit artifacts
- skill-level evidence only comes from AO-owned runtime state and audit artifacts
- non-AO tests do not count as official skill execution evidence
- prose flow and helper command examples are explanatory only, not execution authority
