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
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during AO-based plan execution:

- use the package index absolute URLs as the source of truth for acquisition guidance, with NuGet.org as the first-class latest package source and GitHub release assets as fallback downloads
- when AO execution needs local NuGet acquisition, restore the AO runtime bundle instead of only the AO package: `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`, all at the same resolved channel/version
- when AO execution needs a local package runtime, resolve one exact bundle version first, acquire all three AO runtime-bundle packages in one pass, then extract them into one external unified runtime directory before running any AO CLI command; do not probe or execute from partial single-package extraction roots
- when exact-version NuGet acquisition fails, reacquire that same three-package AO runtime bundle from GitHub fallback assets for the same resolved version, rebuild the same unified runtime directory, and only then continue with `ao.dll`
- when the caller explicitly requests `repo-src-debug` while working inside this repository, build and use the current repo AO project output from `src/dotnet/Techne.Loom.AgentOrchestrator` instead of downloading package assets, while still treating package index links and guide surfaces as authority references
- if this skill is later enhanced by Loom SO, its checked-in `SKILL.md` must explicitly reference `assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock, and any SO DLL restoration in that enhanced mode must resolve the exact locked version from NuGet first and freshly download it unless the local cache already holds that exact version
- require AO skills and any target product that adopts Loom-bin-based skills to preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- keep `dotnet ao.dll --guide [--lang <language>]` as the authoritative runtime surface instead of restating private templates in the skill
- treat AO as CLI-only in this project; do not rely on MCP hosts or MCP tools
- unless the user explicitly requests an output location, keep workflow-authoring intermediates, compile artifacts, audit artifacts, session directories, and other runtime temporary files under a runtime temporary root or repo-root temporary root, not under a skill path
- treat checked-in plan documents and any authored AO workflow snapshots as immutable source artifacts; let AO own mutable runtime state only through `session_dir` outputs such as `workflow_file`, `workflow_instance_file`, runtime sidecars, and event logs, not through files under a skill folder
- when blocked seam replans depend on durable runtime facts or caller-managed reports, treat those facts as AO-relevant decision inputs that must re-enter AO through `prompt-replan` required blocks, WorkflowInstance seam edits, and stable resume payload keys rather than prose-only notes
- declare AO as the only official execution authority for this skill
- declare only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` as official skill runs
- treat `dotnet ao.dll compile`, `dotnet ao.dll --guide`, `dotnet ao.dll prompt-plan`, and `dotnet ao.dll prompt-replan` as authority-supporting preparation or inspection surfaces, not official skill runs
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
- `dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>]`: emit AO-owned planner prompt text for WorkflowInstance file generation
- `dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id>`: emit AO-owned replanner prompt text for WorkflowInstance node replacement around one selected TBR seam
- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--instance-file <path>] [--audit-output <path>]`: execute plan objective
- `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path> [--audit-output <path>]`: weave back with structured external result

Only `dotnet ao.dll run` and `dotnet ao.dll resume` can count as official runs for this skill. `--guide`, `compile`, `prompt-plan`, and `prompt-replan` remain authority-supporting preparation or validation surfaces, not official skill runs.

## Unified Runtime Template

When package-channel runtime acquisition is needed, reuse this external layout under the chosen temp root or explicit execution-output root:

```text
<execution-root>/
	runtime-bundle/
		ao-<resolved_runtime_version>/
			downloads/
			extracted/
			unified/
	authored-inputs/
	sessions/
	audit/
```

Restore order:

1. Resolve one exact AO bundle version.
2. Acquire `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` together at that version.
3. Save the original `.nupkg` assets under `downloads/`.
4. Extract each package under `extracted/<package-id>/`.
5. Materialize the runnable `lib/<tfm>/` payloads into `unified/`.
6. Verify `dotnet <execution-root>/runtime-bundle/ao-<resolved_runtime_version>/unified/ao.dll --guide` or `--help`.
7. Run every later `compile`, `prompt-plan`, `prompt-replan`, `run`, and `resume` command only from that unified runtime directory.

If exact-version NuGet acquisition fails, reacquire the same three-package AO bundle from GitHub fallback assets for that same resolved version and rebuild the same directory layout instead of switching to one-off package probing.

## Think-Out-Loud Output Contract

Think-out-loud output is incomplete unless it explicitly reports all of these runtime fields once the package runtime is prepared and again on every later AO progress update:

- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`

After every AO progress update, think-out-loud output must also explicitly report:

- `audit_markdown_file`
- `audit_html_file`

Do not collapse those paths into a generic audit summary when concrete Mermaid Markdown and HTML paths are available.

## Runtime Flow

1. Confirm package channel from the package index, or record an explicit `repo-src-debug` override when the caller is debugging this skill inside the current repository.
2. When `repo-src-debug` is active, build the current repo AO project at `src/dotnet/Techne.Loom.AgentOrchestrator` and use the produced `ao.dll` for the remaining steps.
3. Otherwise resolve one exact AO bundle version from the selected channel, acquire `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` together at that same version, and extract them into one external unified runtime directory before any AO CLI call.
4. If exact-version NuGet acquisition fails, reacquire that same three-package AO bundle from GitHub fallback assets for the same version and rebuild the unified runtime directory instead of switching to one-off package probing.
5. Run `dotnet ao.dll --guide [--lang <language>]` from the current-repo-src build output or from that unified runtime directory.
6. Write the objective file and optional context file outside the skill folder.
7. When the next planning move needs AO-managed planner wording, run `dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>]` and capture the returned `<ao_property type="prompt">` payload.
8. Treat any returned prompt block with `consumption_requirement = required` as mandatory authoring input, and treat blocks marked `optional` as reference-only examples.
9. Use the returned `prompt`, `blocks[*].block_id = workflow.output-schema`, `workflow.root-field-contract`, `workflow.example-projection`, `prompt.plan.runtime-context`, plus `allowed_node_kinds` and `allowed_command_kinds`, to author a WorkflowInstance JSON file outside the skill folder.
10. Run `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]` on that authored WorkflowInstance file.
11. Run `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--instance-file <path>] [--audit-output <path>]`, and pass the authored WorkflowInstance file when graph continuity from compile into the first blocked runtime step matters.
12. When blocked, inspect `boundary_reason`, `pending_requirements`, `next_frontier`, `human_or_agent_hint`, `workflow_file`, `workflow_instance_file`, `event_log_file`, and `last_transition_id` from AO payloads.
13. If the blocked seam depends on refreshed runtime facts or caller-managed reports, materialize or refresh those artifacts outside the skill folder before replanning.
14. Select one frontier action to continue, and if the continuation needs graph surgery, prepare or refresh the current runtime WorkflowInstance file anchored by `workflow_instance_file` for replan.
15. Run `dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id>` and capture the returned `<ao_property type="prompt">` payload.
16. Treat any returned prompt block with `consumption_requirement = required` as mandatory replan input, and treat blocks marked `optional` as reference-only examples.
17. Treat `prompt.replan.runtime-context` as a mandatory AO decision re-entry surface, so durable runtime facts, caller-managed report keys, and `payload.plan_meta.selected_frontier_action` are carried back into the seam instead of being left in prose only.
18. Use the returned `prompt`, `blocks[*].block_id = prompt.replan.runtime-context`, `prompt.replan.blocked-boundary-context`, `prompt.replan.selected-tbr-projection`, `prompt.replan.current-workflow-projection`, `prompt.replan.current-workflow-instance`, and `workflow.output-schema` to modify that WorkflowInstance file by replacing the selected `tbr` seam while preserving one or more remaining `tbr` nodes.
19. Make the chosen route explicit in node ids, names, target choices, or `designNotes`, and keep stable resume payload keys for runtime facts or caller-managed reports instead of renaming them into generic prose-only notes.
20. Execute the minimum external action set for the selected frontier and write a structured resume envelope with `transition_id`, optional `correlation_key`, and structured `payload`.
21. When richer operator guidance is needed, carry it as caller convention metadata inside `payload` (for example `payload.plan_meta.unsolved_target_id`, `payload.plan_meta.determined_path_ids`, `payload.plan_meta.unresolved_bridge_ids`, and `payload.plan_meta.next_step_prompt`) without adding new AO top-level fields.
22. Run `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path> [--audit-output <path>]`.
23. If AO returns blocked again, refresh from the latest payload and repeat replan from step 12 using the latest `last_transition_id`.
24. Treat AO as completed only when AO returns `status: completed` and `current_node_id: state.completed`.

Do not let AO runtime-owned mutable state overwrite or live beside checked-in source plan files or checked-in snapshot artifacts.
Do not place `--session-dir` or `--audit-output` inside any skill-owned directory.
Every time AO reports progress, render the current workflow to Mermaid Markdown and HTML under the runtime temp root or explicit execution-output root, and force-report `resolved_runtime_version`, `runtime_bundle_packages`, `unified_runtime_directory`, `audit_markdown_file`, and `audit_html_file` in think-out-loud output.

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
- exact resolved AO bundle version when local package runtime acquisition was needed
- AO runtime bundle package list used for local acquisition: `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`
- unified runtime directory path used for local AO CLI execution when package-channel runtime acquisition was used
- reusable unified runtime layout template with standard `downloads/`, `extracted/`, and `unified/` naming plus the required restore order
- effective runtime source selection, including explicit `current-repo-src` / `repo-src-debug` when that override is active
- guide link
- DLL interface mapping used by this skill (`--guide`, agent-authored workflow JSON, `compile`, `prompt-plan`, `prompt-replan`, `run`, `resume`)
- optional agent-authored workflow JSON path used for preparation and compile validation
- optional AO-owned planner prompt output from `prompt-plan` for WorkflowInstance file generation
- optional AO-owned replanner prompt output from `prompt-replan` for WorkflowInstance node replacement around a selected `tbr` seam
- optional caller-managed runtime fact or report path or summary that must enter AO replan
- optional prompt payload block set from `prompt-plan` / `prompt-replan`, including stable `block_id`, `block_kind`, `semantic_role`, and JSON content used for downstream file authoring or editing, plus `prompt.replan.runtime-context` when a blocked seam depends on runtime facts
- runtime `workflow_file` / `workflow_instance_file` / `event_log_file`
- boundary planning signals from AO payloads: `boundary_reason`, `pending_requirements`, `next_frontier`, and `human_or_agent_hint`
- explicit `transition_id_source` note showing `workflow_file.last_transition_id` used for resume envelope generation
- explicit note that `workflow_file` remains the snapshot control file while `workflow_instance_file` is the graph continuity surface for runtime audits and replan edits
- selected frontier action and the minimal external action slice executed for that frontier
- explicit note that durable runtime facts and caller-managed report keys re-entered AO through WorkflowInstance seam edits plus stable payload keys such as `payload.plan_meta.*`, not through prose-only commentary
- structured resume envelope path with `transition_id`, optional `correlation_key`, and payload summary
- per-cycle plan and replan metadata under payload convention keys (for example `payload.plan_meta.plan_phase`, `payload.plan_meta.unsolved_target_id`, `payload.plan_meta.selected_frontier_action`, and `payload.plan_meta.next_step_prompt`)
- per-cycle deterministic-path and unresolved-bridge metadata under payload convention keys (for example `payload.plan_meta.determined_path_ids` and `payload.plan_meta.unresolved_bridge_ids`) when such decomposition is required by the objective
- per-cycle resume result marker: `blocked` or `completed`, including the next action decision
- explicit note that checked-in plan or snapshot artifacts remain immutable source files and AO runtime state is emitted under `session_dir` or an explicit execution output root
- when this skill is SO-enhanced, an explicit checked-in `SKILL.md` reference to `assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock for that enhanced mode
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
- think-out-loud runtime fields on every AO progress update: `resolved_runtime_version`, `runtime_bundle_packages`, and `unified_runtime_directory`
- current workflow Mermaid Markdown and HTML paths on every AO progress update, surfaced as explicit `audit_markdown_file` and `audit_html_file` entries in think-out-loud output
- when the user does not explicitly choose a destination, the effective workflow-authoring, compile, and audit temporary-output root outside any skill path
- explicit execution authority declaration that AO is the only official execution authority for this skill
- official run definition that only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs
- history authority, checklist authority, run-map authority, and evidence authority statements anchored to AO workflow state, frontiers, event logs, workflow JSON, and audit artifacts
- reporting honesty and test classification constraints that reject non-AO output as official skill execution evidence
- explicit note that audit artifacts and intermediate outputs may be referenced in conversation or think-out-loud, but default to temp or explicit execution-output roots rather than skill folders
- explicit note that compile fails instead of overwriting existing artifact files
- explicit note that this skill must not invent undocumented AO top-level schema fields; advanced dispatch details belong to resume `payload` conventions
- explicit note that `prompt-plan` should require file generation with at least one `tbr` path that can still reach the terminal path, and that `prompt-replan` should require replacing one selected `tbr` node after the most recent selected frontier action failed to converge while preserving one or more remaining `tbr` seams in the graph
- explicit note that package-channel AO execution acquired the full three-package bundle in one pass and ran `ao.dll` only from a unified runtime directory, not from a partial package extraction root

## Prohibited Results

Reject or mark invalid any execution result that says or implies any of these:

- AO is optional for official skill execution
- AO and another path are parallel official execution modes for this skill
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
