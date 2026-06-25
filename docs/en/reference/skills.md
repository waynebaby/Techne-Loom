# Skills Input/Output Reference

[中文](../../zh-cn/reference/skills.md) | [Root](../README.md)

For operator-facing usage, demos, and entrypoint selection, start with [Using Techne Loom Skills](../guides/skill-usage.md).

## Language policy

- skill-local reference documents under `.agents/skills/*/reference/` must be English only for deterministic offline execution and maintenance consistency
- repository docs under `docs/en` and `docs/zh-cn` must remain bilingual mirrors for public documentation surfaces
- when a skill needs localized explanations, keep localization in `docs/` bilingual pages instead of adding non-English variants under skill-local `reference/`

## Shared Loom-bin rule

- Loom Agent Execution Orchestrator skills, SO skills, and any target product that adopts Loom-bin-based skills must preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- Loom Agent Execution Orchestrator skills, SO skills, and any target product that adopts Loom-bin-based skills must treat NuGet.org as the first-class latest package source in their package-acquisition guidance, while preserving released and beta package index absolute URLs plus GitHub asset fallback links
- Released package index URL: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## `/loom-plan-execution`

### /loom-plan-execution Mission

Guide-first, environment-first entrypoint for plan execution using the plan-execution package flow.

It also uses Loom Agent Execution Orchestrator-strong governance: Loom Agent Execution Orchestrator is the only official execution authority for this skill, and only explicit `dotnet ao.dll run` / `resume` count as official skill runs.

### /loom-plan-execution Inputs

- rich plan text, recommended at 10+ non-empty lines
- or a detailed plan file path
- package channel choice: released or beta
- optional language surface: en or zh-cn; if omitted, the current public guide surface defaults to en, so callers should pass zh-cn explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional runtime source mode: `package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output
- optional audit output path

### /loom-plan-execution Default assumptions

- use the absolute URL of the released or beta package index page that matches the chosen language surface and current CI/CD-managed skill version block as the source of truth for acquisition guidance, with NuGet.org as the first-class latest package source and GitHub assets as fallback links
- when Loom Agent Execution Orchestrator needs a local package runtime, follow the current CI/CD-managed skill package version block first, derive released versus beta from that bound version when needed, then acquire `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` together at that same version and extract them into one external unified runtime directory outside the skill path; do not probe or run `ao.dll` from a partial single-package extraction root
- when package-channel runtime acquisition is used, reuse a standard external layout such as `<execution-root>/runtime-bundle/ao-<resolved_runtime_version>/{downloads,extracted,unified}/`: keep original package assets under `downloads/`, unpack each package under `extracted/<package-id>/`, materialize the runnable `lib/<tfm>/` payloads under `unified/`, and run every later Loom Agent Execution Orchestrator command only from that unified runtime directory
- when the caller explicitly requests `repo-src-debug` while working inside this repository, build and use the current repo Loom Agent Execution Orchestrator project output from `src/dotnet/Techne.Loom.AgentOrchestrator` instead of downloading package assets, while still treating package index links and guide surfaces as authority references
- require target products that adopt Loom-bin-based skills to preserve released and beta package index absolute URLs in their own docs, using localized mirrors when the product exposes localized package index pages
- treat `dotnet ao.dll --guide [--lang <language>]` as the authoritative runtime surface instead of copying a private execution template
- treat Loom Agent Execution Orchestrator as CLI-only in this project; do not rely on MCP hosts or MCP tools
- unless the user explicitly chooses an output location, keep workflow-authoring intermediates, compile artifacts, audit artifacts, think-out-loud supporting outputs, and other runtime temporary files under a runtime temporary root or repo-root temporary root, never under a skill path
- treat checked-in plan documents and any authored Loom Agent Execution Orchestrator workflow snapshots as immutable source artifacts; Loom Agent Execution Orchestrator mutable runtime state belongs under `session_dir` outputs or an explicit execution output root, not in a skill folder
- treat Loom Agent Execution Orchestrator as the only official execution authority for this skill
- treat only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` as official skill runs
- treat `dotnet ao.dll compile`, `dotnet ao.dll --guide`, `dotnet ao.dll prompt-plan`, and `dotnet ao.dll prompt-replan` as authority-supporting preparation or validation surfaces, not official skill runs
- anchor skill-level history, checklist, run map, and evidence to Loom Agent Execution Orchestrator workflow state, frontiers, workflow JSON, event logs, and audit artifacts only
- reject non-Loom Agent Execution Orchestrator outputs or tests as official skill execution evidence

### /loom-plan-execution Output expectations

- bound runtime version confirmation with derived released/beta evidence
- absolute package index links
- released/beta package index link set, including localized mirrors when they exist
- effective runtime source selection, including explicit `current-repo-src` / `repo-src-debug` when that override is active
- guide surface references
- exact resolved AO bundle version, runtime bundle package list, and unified runtime directory when package-channel runtime acquisition was needed
- reusable unified runtime layout template with the required restore order when package-channel runtime acquisition was used
- optional externally authored workflow JSON snapshot path validated by AO compile
- optional authored `WorkflowInstance` path that continues into `dotnet ao.dll run --instance-file <path>` so the first blocked runtime audit stays on the same graph
- runtime return payload links, including audit artifacts
- when the user does not explicitly choose a destination, the effective workflow-authoring, compile, and audit temporary-output root outside any skill path
- explicit note that checked-in plan or snapshot artifacts remain immutable source files and AO runtime state is emitted under `session_dir` or an explicit execution output root
- think-out-loud output that explicitly reports `resolved_runtime_version`, `runtime_bundle_packages`, and `unified_runtime_directory` once the package runtime is prepared and again on every AO progress update
- think-out-loud output that includes current workflow Mermaid Markdown and HTML paths on every AO progress update as explicit `audit_markdown_file` and `audit_html_file` entries
- explicit execution authority and official run definitions for AO-only governance
- history, checklist, run-map, evidence, and reporting honesty outputs anchored to AO workflow and audit artifacts

### /loom-plan-execution Runtime handoff

- uses `dotnet ao.dll --guide [--lang <language>]` as the source of truth
- when `repo-src-debug` is explicitly active inside the current repository, build `src/dotnet/Techne.Loom.AgentOrchestrator` and use the produced `ao.dll` for the same AO CLI surface instead of downloading package assets
- when package-channel runtime execution is used, acquire the full AO three-package bundle in one pass, extract it into one external unified runtime directory, and run `--guide`, `compile`, `prompt-plan`, `prompt-replan`, `run`, and `resume` only from that unified runtime directory
- writes objective/context inputs first, then can use `dotnet ao.dll prompt-plan` to obtain AO-owned planner prompt text plus typed prompt blocks for WorkflowInstance file generation
- treats prompt blocks with `consumption_requirement = required` as mandatory input contracts and blocks with `consumption_requirement = optional` as reference-only shape aids
- uses those `prompt-plan` outputs to author a WorkflowInstance JSON file outside the skill folder, then uses `dotnet ao.dll compile` to validate that authored workflow JSON
- can then pass that same authored WorkflowInstance file to `dotnet ao.dll run --instance-file <path>` so runtime starts from the same graph instead of a minimal sidecar-only graph
- after AO blocks, can use `dotnet ao.dll prompt-replan` to obtain AO-owned replanner prompt text plus typed blocked-context and current-workflow blocks for WorkflowInstance TBR seam replacement after a blocked frontier action fails to converge
- uses those `prompt-replan` outputs to modify the current `workflow_instance_file` before the next resume cycle
- uses `dotnet ao.dll run` / `resume` as the only official skill-run surface
- blocked runs continue from returned workflow JSON frontier
- audit artifacts and intermediate outputs may be referenced in conversation or think-out-loud, but default to runtime temp, repo-root temp, or an explicit execution output root rather than a skill folder
- compile and audit flows must fail rather than overwrite an existing artifact file
- checked-in plan files and authored snapshot artifacts stay clean; AO runtime-owned mutable control state is tracked through `workflow_file`, while runtime graph continuity is tracked through `workflow_instance_file`, the runtime sidecar, and the optional pointer file outside the skill folder
- every AO progress update should render the current workflow to Mermaid Markdown and HTML under runtime temp or explicit execution-output roots, then cite those paths in think-out-loud output

## `/loom-skill-enhancement`

### /loom-skill-enhancement Mission

Guide-first entrypoint for creating or upgrading deterministic skills around the Loom Skill Orchestrator package flow.

When the target skill already shows Loom Skill Orchestrator governance signals, this skill upgrades it in one pass into a Loom-governanced skill under exclusive Loom Skill Orchestrator governance instead of stopping at generic Loom Skill Orchestrator support or documentation refresh.

### /loom-skill-enhancement Inputs

- target skill path or target skill repo path
- deterministic skill goal / upgrade request
- requested target-skill changes to create or modify in this enhancement pass
- runtime version authority: reuse the checked-in `assets/so-workflow/so-package-lock.json` plus the current skill package version block, and derive released versus beta from that bound version when needed
- optional language surface: en or zh-cn; if omitted, the current public guide surface defaults to en, so callers should pass zh-cn explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional JSON context file
- optional audit output path

### /loom-skill-enhancement Default assumptions

- treat the absolute URL of the package index page that matches the chosen language surface and bound runtime version as the source of truth for acquiring the Loom Skill Orchestrator package; if execution needs local binaries, install or unpack runtime assets from the derived channel into an external temporary directory instead of the target repo
- run a fresh `dotnet so.dll --guide [--lang <language>]` from the current selected package runtime on every enhancement pass before authoring, editing, or validating target-skill deliverables; do not reuse stale guide output from an earlier session or older package version
- when the target project does not already have its own dependencies installed, install only the minimum dependency set required for the requested target-skill changes and current guide-aligned validation path; do not widen into unrelated package restore or optional toolchain installation
- when Loom Skill Orchestrator execution or day-to-day target-skill runtime restoration needs a local package runtime, first resolve one exact version, then acquire `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` together at that same version and extract them into one external unified runtime directory outside the target repo; do not probe or run `so.dll` from a partial single-package extraction root
- require target products that adopt Loom-bin-based skills to preserve released and beta package index absolute URLs in their own docs, using localized mirrors when the product exposes localized package index pages
- keep Loom Skill Orchestrator-owned materials under `<target-skill-root>/assets/so-workflow/`
- generate `<target-skill-root>/assets/so-workflow/skill-plan.md` from the current `SKILL.md` when it exists, or from `goal` plus supporting references when creating a new skill
- write `<target-skill-root>/assets/so-workflow/so-package-lock.json` with the exact Loom Skill Orchestrator NuGet package version, chosen channel, and runtime bundle members used for the enhancement pass, following the standard example at `.agents/skills/loom-skill-enhancement/examples/so-package-lock.example.json`
- when `references/*.md` exists, concatenate them into a temporary `merged-context.md` working note with clear section headers, then convert the needed content into a temporary JSON context file for the Loom Skill Orchestrator `--context-file` flow
- store the workflow template separately; unless the user explicitly picks an output destination, keep compile artifacts, audit artifacts, intermediate working files, and other runtime temporary files under a runtime temporary root or repo-root temporary root instead of any skill path or `<target-skill-root>/assets/so-workflow/`
- treat the checked-in workflow template under `<target-skill-root>/assets/so-workflow/` as immutable; before `dotnet so.dll run` or `resume`, clone it to an external runtime workflow copy and keep the mutable copy plus its event sidecars outside the target skill path unless the user explicitly chooses another execution output root
- after enhancement, burn a machine-readable Loom Skill Orchestrator package lock that records `package_id`, chosen `released` or `beta` channel, and the exact resolved NuGet version used for that enhancement pass
- the enhanced target `SKILL.md` must explicitly reference `<target-skill-root>/assets/so-workflow/so-package-lock.json` as the authoritative Loom Skill Orchestrator runtime version lock, and must state that routine Loom Skill Orchestrator runtime bundle restoration resolves the exact locked bundle from NuGet first and freshly downloads it unless the local cache already holds that exact version bundle
- when the enhanced target skill is used later, restore that exact locked Loom Skill Orchestrator runtime bundle instead of silently floating to a newer one or omitting `Common` / `Abstractions`
- when the target skill needs another enhancement pass, do not ask the user to choose a channel during normal SO re-enhancement; reuse the bound runtime version from the checked-in lock and current skill build metadata, derive `released` versus `beta` only when operationally needed, and then rewrite the lock file only if the bound version changes
- force workflow-template correctness ahead of every other optimization: the generated workflow JSON template must be complete and detailed, must align with the guide captured from the current bound runtime version, and must pass `dotnet so.dll compile --workflow-file <path>` before it can become the execution authority for the enhanced target skill
- for target-skill templates that use root `templateKind: so-governed-target-skill`, write a root `validation` contract with `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`
- require governed routes to declare terminal business-output gates and strongest-earned blocked-output gates so compile can reject governance-only done paths or empty blocked pauses
- keep `AskUser` seams limited to declared user-owned fields or decisions; runtime-owned facts and artifact paths belong to runtime-owned seams such as `WaitResume`
- when the target skill already exposes Loom Skill Orchestrator governance signals such as workflow assets, `skill-plan` or `so-template` contracts, audit contracts, or Loom Skill Orchestrator authority wording, automatically enter exclusive Loom Skill Orchestrator governance mode
- in exclusive Loom Skill Orchestrator governance mode, treat Loom Skill Orchestrator as the only official execution authority for the target skill
- in exclusive Loom Skill Orchestrator governance mode, treat only explicit `dotnet so.dll run` and `dotnet so.dll resume` as official skill runs
- in exclusive Loom Skill Orchestrator governance mode, demote direct CLI and direct MCP to runtime primitive or component execution only; they are not official skill runs
- in exclusive Loom Skill Orchestrator governance mode, anchor skill-level history, checklist, run map, and evidence to Loom Skill Orchestrator workflow state, event logs, workflow templates, guards, seams, and audit artifacts only
- in exclusive Loom Skill Orchestrator governance mode, require the target skill to state that it has switched into Loom-governanced execution
- workflow templates must use explicit governed steps, guards, seams, and reviewable outputs; never author or keep a node whose purpose says or implies `run a multistep plan`
- review workflow templates for any node instruction that embeds a multistep plan or a broad prompt to an agent, then break that intent into smaller governed nodes when possible
- compress the upgraded `SKILL.md` to roughly 80-100 lines while preserving high-level steps, guardrail headings, Loom Skill Orchestrator guidance, and the `## Workflow Contract` title
- mark released-channel wording as Beta Only when stable docs do not actually ship the same Loom Skill Orchestrator enhancement surface
- on weave-out, use structured blocked payload fields such as `current_step_kind` to classify the wait category, and consume `skill_hint` literally as the next external action instruction; ask the user only for mandatory human-input seams; treat waits on email, files, messages, or downstream script results as valid external wait states that either return the expected next input shape or pause until the external result arrives; continue automatically only when the structured payload plus literal `skill_hint` point to a non-human continuation
- treat these as skill-layer adaptation defaults rather than generic Loom Skill Orchestrator runtime guarantees; if the bound-version guide does not expose an equivalent surface, mark that behavior as Beta Only

### /loom-skill-enhancement Output expectations

- package/channel choice confirmation
- absolute package index links
- released/beta package index link set, including localized mirrors when they exist
- guide surface references
- deterministic workflow template path produced by the reviewed authoring flow, after guide-alignment review plus `dotnet so.dll compile` succeed; that validated template becomes the execution authority for the enhanced target skill
- governed-template validation contract evidence for future target-skill workflows, including route-aware gate declarations and seam ownership declarations
- route-aware business-output gate evidence for both terminal and blocked governed paths
- locked Loom Skill Orchestrator package metadata path plus the exact resolved package version, chosen channel, and runtime bundle members used for the enhancement pass
- locked Loom Skill Orchestrator package metadata should be represented in two layers when source deliverables remain checked in: the checked-in `so-package-lock.json` source asset and the runtime-owned completion/reference artifact that cites that checked-in source asset for the current slice
- runtime return payload links, including audit artifacts
- when the user does not explicitly choose a destination, the effective compile and audit temporary-output root outside the target skill path and outside `<target-skill-root>/assets/so-workflow/`
- intermediate outputs and think-out-loud support files may be referenced in conversation, but they still default outside the target skill path and outside `<target-skill-root>/assets/so-workflow/`
- runtime workflow-copy path plus event-log path, separate from the checked-in source template path
- think-out-loud output that includes current workflow Mermaid Markdown and HTML paths on every Loom Skill Orchestrator progress update for the enhanced target skill
- when exclusive Loom Skill Orchestrator governance mode applies, an explicit declaration that Loom Skill Orchestrator is the only official execution authority, that only `dotnet so.dll run` / `resume` count as official skill runs, and that direct CLI or direct MCP remain primitive paths only
- when exclusive Loom Skill Orchestrator governance mode applies, explicit history, checklist, run-map, evidence, reporting honesty, and test classification outputs anchored to Loom Skill Orchestrator workflow and audit artifacts
- when exclusive Loom Skill Orchestrator governance mode applies, explicit completion wording that the target skill has switched into Loom-governanced execution
- when exclusive Loom Skill Orchestrator governance mode applies and checked-in source assets remain authoritative, explicit completion wording must also distinguish checked-in source deliverables from runtime-owned completion manifests instead of implying that the runtime-owned manifest replaced the source deliverables
- workflow-template governance evidence that no node purpose or node intention says or implies `run a multistep plan`

### /loom-skill-enhancement Runtime handoff

- uses `dotnet so.dll --guide [--lang <language>]` as the Loom Skill Orchestrator source of truth
- requires that `dotnet so.dll --guide [--lang <language>]` come from the current selected package runtime for the current enhancement pass, not from a stale prior run
- lets the AI agent execute `dotnet so.dll compile` / `run` / `resume` directly in the terminal
- uses a reviewed authoring flow to materialize workflow JSON under `<target-skill-root>/assets/so-workflow/`, then runs `dotnet so.dll compile --workflow-file <path>` with compile and audit temporary output routed to runtime temp or repo-root temp unless the user explicitly chooses another location
- validates that the resulting workflow template is complete and detailed against the guide captured from the bound runtime version, and also requires `dotnet so.dll compile` to succeed before treating it as the execution authority
- for target-skill templates that use root `templateKind: so-governed-target-skill`, `dotnet so.dll compile` and workflow load also reject missing root validation contracts, invalid `AskUser` seam ownership, governance-only done paths, and blocked routes that do not publish the strongest-earned business outputs
- reuses the exact Loom Skill Orchestrator package version already bound by the current skill build and checked-in `so-package-lock.json`, derives the channel from that bound version when needed, and later restores that locked runtime bundle from NuGet first, freshly downloading it unless the local cache already holds that exact version bundle, when the enhanced target skill runs
- later target-skill execution restores that locked Loom Skill Orchestrator three-package runtime bundle in one pass and rebuilds one external unified runtime directory before any `so.dll` invocation, instead of falling back to one-off package probing
- clones the stored template to an external runtime workflow copy before every `dotnet so.dll run` or `resume`, so the checked-in source template stays clean
- uses `dotnet so.dll run` / `resume` as the only official target-skill run surface when exclusive Loom Skill Orchestrator governance mode applies, and those calls target only the external runtime copy
- target skills re-plan the source template only when variance appears
- compile and audit flows must fail rather than overwrite an existing artifact file, and should report the conflicting path set when they fail
- every Loom Skill Orchestrator progress update should render the current workflow to Mermaid Markdown and HTML under runtime temp or explicit execution-output roots, then cite those paths in think-out-loud output
