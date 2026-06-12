---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and SO package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill.

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the SO package flow.

When the target skill is already SO-enhanced, this skill must upgrade it all the way to an SO-exclusive governed skill in one pass. In that mode, SO becomes the only official execution authority, direct CLI or direct MCP remain runtime primitives only, and the target skill must state plainly that it has been enhanced by Loom SO.

## Read This First

Choose package channel first:

- Released (main, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta (development, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

Then read the package guide:

- Released guide (English): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md>
- Beta guide (English): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md>
- Released guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/so-guide.md>
- Beta guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/so-guide.md>

## Input Contract

- target skill path or repository path
- deterministic skill goal or upgrade request
- optional language surface (`en` or `zh-cn`). If omitted, the current public guide surface defaults to `en`, so callers should pass `zh-cn` explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
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

- use the absolute URL of the released or beta package index page that matches the chosen language surface as the source of truth for acquisition guidance, with NuGet.org as the first-class latest package source; if execution needs a local runtime, resolve the exact package version from NuGet first for each enhancement pass or target-skill runtime restoration, freshly download it unless the local cache already holds that exact version, then install or unpack the runtime from the selected package channel into an external temporary directory instead of the target repo, and use GitHub asset links only as fallback downloads when NuGet.org is unavailable
- require SO skills and any target product that adopts Loom-bin-based skills to preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- let the AI agent execute `dotnet so.dll compile` / `run` / `resume` / `status` / `inspect-workflow` / `inspect-events` directly in the terminal
- keep SO-owned files under `<target-skill-root>/assets/so-workflow/`
- write the workflow description file to `<target-skill-root>/assets/so-workflow/skill-plan.md`
- write the SO package lock file to `<target-skill-root>/assets/so-workflow/so-package-lock.json`
- keep the standard SO package lock template example at `.github/skills/loom-skill-enhancement/examples/so-package-lock.example.json`, and use the same field shape when burning a real target-skill lock
- derive that description file at fine granularity from the current `SKILL.md` decision tree when it exists, or from `goal` plus supporting references when creating a greenfield skill, then let the maintainer review it
- when `references/` Markdown sources exist, concatenate them with clear section headers into a temporary `merged-context.md` working note, then convert the needed context into a temporary JSON context file for `--context-file`
- store the deterministic workflow template as its own JSON file; unless the user explicitly chooses an output destination, keep compile artifacts, audit artifacts, intermediate working files, and other runtime temporary files under a runtime temporary root or repo-root temporary root instead of the target skill directory or `assets/so-workflow/`
- treat the checked-in workflow template under `<target-skill-root>/assets/so-workflow/` as an immutable source template; before `dotnet so.dll run` or `dotnet so.dll resume`, clone it to an external runtime workflow copy under a runtime temp root, repo-root temp root, or explicit execution output root, and keep `--audit-output` outside the target skill directory as well
- after enhancement, burn a machine-readable SO package lock under `<target-skill-root>/assets/so-workflow/so-package-lock.json` with at least `package_id`, `channel`, and the exact resolved NuGet version that was used for the enhancement pass
- the enhanced target skill `SKILL.md` must explicitly reference `<target-skill-root>/assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock, and it must say that day-to-day SO DLL restoration resolves the exact locked version from NuGet first and freshly downloads it unless the local cache already holds that exact version
- when the enhanced target skill is later used, restore the SO runtime from that locked package version instead of silently floating to a newer version within the same channel
- when the target skill needs another enhancement pass, ignore the old lock for upgrade selection and reacquire the latest package version from the user-chosen `released` or `beta` channel, then rewrite the lock file to the new resolved version
- force workflow-template correctness ahead of every other optimization: the generated workflow JSON template must be complete and detailed, must align with the selected channel guide, and must pass `dotnet so.dll compile --workflow-file <path>` before it can become the execution authority for the enhanced target skill
- when `SO-exclusive governance mode` is active, declare SO as the only official execution authority for the target skill
- when `SO-exclusive governance mode` is active, declare only explicit `dotnet so.dll run` and `dotnet so.dll resume` as official skill runs
- when `SO-exclusive governance mode` is active, demote direct CLI and direct MCP to runtime primitive or component execution only; they are never official skill runs
- when `SO-exclusive governance mode` is active, anchor skill-level history to SO workflow state, event log, and audit artifacts only
- when `SO-exclusive governance mode` is active, anchor skill-level checklist authority to SO nodes, transitions, guards, and blocked or resume seams only
- when `SO-exclusive governance mode` is active, anchor skill-level run-map authority to the SO workflow template only
- when `SO-exclusive governance mode` is active, anchor skill-level evidence authority to SO audit artifacts and SO-owned runtime state only
- when `SO-exclusive governance mode` is active, require reporting honesty: prose flow, CLI snippets, and MCP examples are explanatory only unless they are explicit `dotnet so.dll run` or `dotnet so.dll resume` executions
- when `SO-exclusive governance mode` is active, classify direct CLI or direct MCP tests as primitive or component tests only; they cannot count as official skill execution evidence
- when `SO-exclusive governance mode` is active, require the upgraded target skill to state that it has been enhanced by Loom SO and is now SO-exclusive governed
- keep `SKILL.md` compressed to about 80-100 lines, preserving high-level steps, guardrail headings, SO guidance, and the `## Workflow Contract` section title
- when released-channel docs do not actually ship the same SO enhancement asset shape, mark that surface as Beta Only instead of implying parity
- when SO weaves out, use the structured blocked payload such as `current_step_kind` to classify the wait category, and consume `skill_hint` literally as the next external action instruction: ask the user for mandatory human-input seams, treat waits on email, files, messages, or downstream script results as valid external wait states that either return the expected next input shape or pause until the external result arrives, and continue automatically only when the structured payload plus literal `skill_hint` point to a non-human continuation
- treat these as skill-layer adaptation defaults rather than generic SO runtime guarantees; if the selected channel guide does not expose an equivalent surface, mark that behavior as Beta Only
- audit artifacts and intermediate outputs may be referenced in conversation or think-out-loud, but default them to runtime temp, repo-root temp, or an explicit user-chosen execution output root rather than any skill folder
- compile and audit flows must fail rather than overwrite an existing artifact file, and should report the conflicting path set on failure

## DLL Interface Mapping

- `dotnet so.dll parameter --help`: print the authoritative parameter surface and verify the command set before execution
- `dotnet so.dll --guide [--lang <language>]`: runtime authority and command surface source of truth
- `dotnet so.dll compile --workflow-file <path> [--audit-output <path>]`: validate workflow template and emit compile-time audit artifacts
- `dotnet so.dll run --workflow-file <runtime-copy-path> [--context-file <path>] [--audit-output <path>]`: execute deterministic workflow from a cloned runtime copy, not the checked-in source template
- `dotnet so.dll resume --workflow-file <runtime-copy-path> --result-file <path>`: weave back with structured external result against that mutable runtime copy
- `dotnet so.dll status --workflow-file <path>`: inspect current runtime state for blocked/in-progress/completed transitions
- `dotnet so.dll inspect-workflow --workflow-file <path>`: inspect effective workflow shape during troubleshooting
- `dotnet so.dll inspect-events --event-log-file <path>`: inspect event log stream for transition-level diagnosis

Only `dotnet so.dll run` and `dotnet so.dll resume` can count as official target-skill runs in `SO-exclusive governance mode`. `compile`, `status`, `inspect-workflow`, and `inspect-events` remain authority-supporting runtime primitives and inspection surfaces, not official skill runs.

## Runtime Flow

1. Confirm package channel from the package index.
2. Run `dotnet so.dll parameter --help` (invoked as `dotnet so.dll --help`) and confirm the real command surface before continuing.
3. Run `dotnet so.dll --guide [--lang <language>]`.
4. Classify the target skill against the `SO-exclusive governance mode` triggers before producing any enhancement output.
5. When `SO-exclusive governance mode` is active, first rewrite the target-skill governance contract so SO is the only official execution authority, only explicit `dotnet so.dll run` and `dotnet so.dll resume` count as official skill runs, and direct CLI or direct MCP remain primitive paths only.
6. When `SO-exclusive governance mode` is active, rewrite the target-skill history, checklist, run-map, evidence, reporting honesty, and test-classification language so all official skill-level authority is anchored to SO workflow state, events, templates, guards, seams, and audit artifacts.
7. Create or refresh `<target-skill-root>/assets/so-workflow/skill-plan.md` from the target `SKILL.md` when it exists, or from `goal` plus supporting references when creating a new skill.
8. When `references/` Markdown files exist, concatenate them with clear section headers into a temporary `merged-context.md` working note, then convert the needed context into a temporary JSON context file.
9. Resolve the latest SO package version from the user-chosen `released` or `beta` channel for the current enhancement pass, regardless of any older lock file already stored by the target skill.
10. Record that resolved package version in `<target-skill-root>/assets/so-workflow/so-package-lock.json` together with the chosen channel and package identity.
11. Update the enhanced target skill `SKILL.md` so it explicitly references `<target-skill-root>/assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock, and so it states that routine SO DLL restoration resolves the exact locked version from NuGet first and freshly downloads it unless the local cache already holds that exact version.
12. Author or refresh the deterministic workflow JSON template under `<target-skill-root>/assets/so-workflow/` from the reviewed plan and supporting references.
13. Unless the user explicitly chooses another destination, point compile and audit temporary output to a runtime temporary root or repo-root temporary root, not to the target skill path or `assets/so-workflow/`.
14. Run `dotnet so.dll compile --workflow-file <path> [--audit-output <path>]`.
15. Validate that the workflow JSON template is complete and detailed against the selected channel guide, then require `dotnet so.dll compile` to succeed before treating that workflow template as the execution authority for the enhanced target skill.
16. When the enhanced target skill is used later, restore the SO runtime from the locked version in `so-package-lock.json`, resolving that exact version from NuGet first and freshly downloading it unless the local cache already holds that exact version, then clone the checked-in source template to an external runtime workflow copy before any `dotnet so.dll run` or `resume` call.
17. Keep that mutable runtime copy plus its `.events.jsonl` sidecar outside the target skill path unless the user explicitly chooses another execution output root.
18. Keep `--audit-output` outside the target skill path too; runtime workflow copies, event sidecars, and audit artifacts do not belong in the skill folder.
19. Run `dotnet so.dll run` / `resume` against that runtime copy. When variance appears, use `status` plus `inspect-workflow` / `inspect-events` to locate drift, then update the source workflow JSON through the same authoring flow and re-run `compile`.
20. Use the structured blocked payload such as `current_step_kind` to classify whether a weave-out is waiting for mandatory user input, waiting for external asynchronous results, or explicitly allowing non-human continuation, and then consume `skill_hint` literally as the next action instruction.
21. Every time SO reports progress for the enhanced target skill, render the current workflow to Mermaid Markdown and HTML under runtime temp or explicit execution-output roots, and surface those file paths in think-out-loud output.

## Required Outputs

- chosen package index link
- package index link set for released/beta, including localized mirrors when they exist
- guide link
- DLL interface mapping used by this skill (`parameter --help`, `--guide`, `compile`, `run`, `resume`, `status`, `inspect-workflow`, `inspect-events`)
- standard SO package lock template example path `.github/skills/loom-skill-enhancement/examples/so-package-lock.example.json`
- locked SO package metadata path plus the exact resolved package version and chosen `released` or `beta` channel used for the enhancement pass
- explicit target-skill `SKILL.md` reference to `<target-skill-root>/assets/so-workflow/so-package-lock.json` as the authoritative SO runtime version lock
- deterministic workflow template path, after guide-alignment review plus `dotnet so.dll compile` succeed; that validated template becomes the execution authority for the enhanced target skill
- runtime workflow-copy path plus `event_log_file`
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
- when the user does not explicitly choose a destination, the effective compile and audit temporary-output root outside the target skill path and outside `assets/so-workflow/`
- explicit note that checked-in source templates remain clean and that `run` / `resume` target only the external runtime copy
- explicit note that day-to-day target-skill execution restores the locked SO package version from NuGet and freshly downloads it unless the local cache already holds that exact version, while a new enhancement pass always resolves the latest package from the user-chosen channel and freshly downloads it unless that exact version is already present in local cache, then rewrites the lock
- current workflow Mermaid Markdown and HTML paths on every SO progress update, surfaced in think-out-loud output for the enhanced target skill
- when `SO-exclusive governance mode` is active, an explicit execution authority declaration that SO is the only official execution authority for the target skill
- when `SO-exclusive governance mode` is active, an official run definition that only explicit `dotnet so.dll run` and `dotnet so.dll resume` count as official skill runs
- when `SO-exclusive governance mode` is active, a primitive path definition that direct CLI and direct MCP are runtime primitive or component execution only
- when `SO-exclusive governance mode` is active, history authority, checklist authority, run-map authority, and evidence authority statements anchored to SO workflow state, events, templates, seams, and audit artifacts
- when `SO-exclusive governance mode` is active, reporting honesty and test classification constraints that reject direct CLI or direct MCP output as official skill execution evidence
- when `SO-exclusive governance mode` is active, explicit completion language stating that the target skill has been enhanced by Loom SO and is now SO-exclusive governed

## Prohibited Results

Reject or mark invalid any enhancement result that says or implies any of these:

- SO is optional
- CLI and SO are parallel official execution modes
- CLI mode is a normal skill run mode
- MCP mode is a normal skill run mode
- workflow assets merely imply SO execution authority without an explicit declaration
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
