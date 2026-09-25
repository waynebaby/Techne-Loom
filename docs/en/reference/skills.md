# Skills Input/Output Reference

[中文](../../zh-cn/reference/skills.md) | [Root](../README.md)

For operator-facing usage, demos, and entrypoint selection, start with [Using Techne Loom Skills](../guides/skill-usage.md).

## Language policy

- skill-local reference documents under `.agents/skills/*/reference/` must be English only for deterministic offline execution and maintenance consistency
- repository docs under `docs/en` and `docs/zh-cn` must remain bilingual mirrors for public documentation surfaces
- when a skill needs localized explanations, keep localization in `docs/` bilingual pages instead of adding non-English variants under skill-local `reference/`

## Shared Loom-bin rule

- Loom Agent Plan-Execution Orchestrator skills, SO skills, and any target product that adopts Loom-bin-based skills must preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- Loom Agent Plan-Execution Orchestrator skills, SO skills, and any target product that adopts Loom-bin-based skills must treat NuGet.org as the first-class latest package source in their package-acquisition guidance, while preserving released and beta package index absolute URLs plus GitHub asset fallback links
- Released package index URL: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL: <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## Workflow File Language



Across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance, workflow definition files are canonical English information carriers. Use English for workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Runtime Selection

AO and SO publish only self-contained product+RID runtime packages. Use the owning skill's exact version; the host detects OS, architecture, and Linux libc and selects one supported RID. Package acquisition never probes for `dotnet` or selects a DLL mode.

- Reuse only a valid exact package in the standard NuGet global-packages cache; otherwise verify the exact NuGet registration `catalogEntry.packageHash`.
- A same-version GitHub Release fallback requires a valid `.sha512` sidecar. Do not use floating package aliases.
- Verify package ID, version, RID, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide assets before extraction.
- Safely extract outside the skill folder and directly run `ao.exe --guide`/`so.exe --guide` on Windows or `ao --guide`/`so --guide` on Unix before downstream work.
- MCP is optional later support and never blocks package acquisition, guide capture, or official CLI run/resume.

## `/loom-plan-execution`

### /loom-plan-execution Mission

Guide-first, environment-first entrypoint for plan execution using the plan-execution package flow.

It also uses Loom Agent Plan-Execution Orchestrator governance: AO is the only official execution authority for this skill, and official runs use direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix.

### /loom-plan-execution Inputs

- rich plan text, recommended at 10+ non-empty lines
- or a detailed plan file path
- package channel choice: released or beta
- guide surface: English-only; directly run `ao.exe --guide` on Windows or `ao --guide` on Unix, parse `version`, `docs_root`, and `guide_path`, and read the returned guide path
- optional runtime source mode: `package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output
- optional audit output path

### /loom-plan-execution Default assumptions
- use the absolute URL of the released or beta package index matching the current CI/CD-managed version block as package guidance
- before AO package acquisition, follow [Platform Detection Steps](runtime/platform-detection.md), detect one RID, and acquire only the exact `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package
- prefer a verified exact package in the standard NuGet cache; otherwise verify the exact NuGet registration hash or same-version GitHub `.sha512` sidecar before extraction
- when the caller explicitly requests `repo-src-debug` inside this repository, build and use the AO source project only for local debugging; do not treat it as official package execution
- require adopting products to preserve released and beta package-index absolute URLs, with localized mirrors when available
- treat the fresh direct apphost `--guide` result as the authority before planning or downstream work
- treat AO as the only official execution authority for this skill; AO workflows remain disk-backed and sessionless
- official skill runs use direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix
- direct apphost `compile`, `--guide`, `prompt-plan`, and `prompt-replan` are preparation or validation surfaces, not official runs
- keep plans, workflow copies, session state, audit artifacts, and intermediate files outside skill folders
- keep checked-in plan documents and authored workflow snapshots immutable
- preserve business-outcome-first behavior; AO runtime artifacts do not replace requested business deliverables
### /loom-plan-execution Output expectations
- bound AO package version and derived released/beta evidence
- absolute released/beta package-index links, including localized mirrors
- effective runtime source, including explicit `repo-src-debug` only when requested for repository debugging
- fresh guide surface references
- exact package ID/version/RID/hash, archive validation, extraction result, direct apphost path, and guide evidence
- optional externally authored workflow JSON and `WorkflowInstance` paths validated with direct AO apphost commands
- runtime result payloads, event logs, and audit artifact links
- external workflow, session, and audit roots outside skill paths
- explicit note that checked-in plans/snapshots remain immutable while AO runtime state and graph continuity are tracked in their external files
- think-out-loud package identity and verified Mermaid/HTML/Analysis/Dataflow artifacts
- history, checklist, run map, evidence, and completion reporting anchored to AO workflow and audit artifacts
### /loom-plan-execution Runtime handoff
- Treat `ao.exe --guide` on Windows or `ao --guide` on Unix as the authority; parse its JSON and read `guide_path` before downstream work.
- The explicit `repo-src-debug` override may build the AO source project only when the user is debugging this repository.
- For package-channel execution, acquire and validate the exact AO product+RID package, extract it safely, then use that same apphost for guide, compile, prompt-plan, prompt-replan, run, and resume.
- Write objective/context inputs first; `prompt-plan` supplies AO-owned planner text and typed blocks for WorkflowInstance authoring.
- Treat required prompt blocks as mandatory inputs and optional blocks as reference-only aids.
- Author WorkflowInstance JSON outside the skill folder, compile it with the apphost, and pass the same instance to run when appropriate.
- After a blocked action, use `prompt-replan` to obtain typed blocked-context and current-workflow blocks, update the same workflow instance, then resume it.
- Official skill runs use direct apphost `run`/`resume` only and preserve the same workflow and persisted state through completion.
- Blocked runs continue from the returned workflow frontier. Preserve failure, event, and audit evidence.
- Keep audit outputs outside skill paths and fail rather than overwrite existing artifacts.
- Render Mermaid and HTML after AO progress and display only verified paths.
## `/loom-skill-enhancement`

### /loom-skill-enhancement Mission

Guide-first entrypoint for creating or upgrading deterministic skills around the Loom Skill Orchestrator package flow.

When the skill being enhanced already shows Loom Skill Orchestrator governance signals, this skill upgrades it in one pass into a skill under exclusive Loom Skill Orchestrator governance instead of stopping at generic Loom Skill Orchestrator support or documentation refresh.

### /loom-skill-enhancement Inputs

- path of the skill being enhanced or repository path of the skill being enhanced
- deterministic skill goal / upgrade request
- requested changes to the skill being enhanced to create or modify in this enhancement pass
- runtime version authority: reuse the checked-in `assets/so-workflow/so-package-lock.json` plus the current skill package version block, and derive released versus beta from that bound version when needed
- guide surface: English-only; directly run `so.exe --guide` on Windows or `so --guide` on Unix, parse its JSON `version`, `docs_root`, and `guide_path`, and read the returned guide path
- optional JSON context file
- optional audit output path

### /loom-skill-enhancement Default assumptions
- use the absolute released/beta package-index URL matching the bound SO version as acquisition guidance
- on every enhancement pass, read the exact version from `assets/so-workflow/so-package-lock.json`, detect one host RID, acquire the exact SO package, verify it, extract it safely, and run its direct apphost `--guide` before editing or collecting downstream inputs
- prefer a verified exact package from the standard NuGet cache; otherwise verify NuGet registration SHA-512 or the same-version GitHub `.sha512` sidecar. Do not add a fixed bootstrap script or Loom-specific cache
- keep stable SO-owned templates, locks, references, and maps under `<target-skill-root>/assets/so-workflow/`; keep plans and mutable run files under the external execution output root
- keep the checked-in workflow template immutable; copy it to one external runtime workflow file before compile/run, and keep every resume on that same workflow copy and persisted state
- write the package lock with the exact SO version and active package identity; the host supplies transient RID, apphost, hash, and extraction facts as runtime evidence
- do not omit `Common` or `Abstractions` by attempting to acquire them as runtime packages; the published self-contained SO RID package already contains its runtime closure
- use bounded context files only when a later workflow step needs them; fragment inspection and MCP are not package or guide prerequisites
- allow a later workflow step to use local MCP when useful, bound to the current apphost identity; keep official compile/run/resume on the direct apphost path
- keep authoring inputs complete, closed, and path-only; keep mutable outputs outside the skill folder
- require guide-aligned workflow design, route-aware business-output gates, explicit seam ownership, and per-transition boundary checks
- keep `AskUser` limited to user-owned choices and runtime facts in runtime-owned outputs
- preserve the re-enhancement strategy choice (`local_patch`, `structural_refactor`, or `full_regeneration`) and compare against the fresh package guide
- use the same exact published apphost for schema/demo, compile, run, and resume; compile is validation, not completion
- in exclusive SO governance, only direct `so.exe run`/`so.exe resume` on Windows or `so run`/`so resume` on Unix are official skill runs
- if package verification, extraction, startup, or guide validation fails, stop with failed evidence; do not substitute a repository build or another package
- never author a node whose purpose says or implies `run a multistep plan`; split visible work into explicit governed nodes
### /loom-skill-enhancement Output expectations
- package-index links for released and beta channels, including localized mirrors
- fresh guide result and returned guide path for the exact locked SO version
- exact package ID/version/RID, verified hash, archive checks, extraction result, and apphost path
- checked-in package-lock path and runtime-owned evidence that cites the lock used
- workflow template path, governed validation contract, route-aware business-output gates, and seam ownership evidence
- runtime-owned compile feedback, Mermaid, HTML, analysis, and dataflow outputs
- external workflow-copy path, event log, resume chain, boundary-check trail, and audit links
- review/repair/post-fix validation evidence and completion manifest
- explicit distinction between checked-in source deliverables and runtime-owned temporary artifacts or manifests
- direct apphost command chain and final workflow status for official completion
- optional later-step MCP evidence only when that workflow step actually uses MCP
- workflow-template evidence that no node hides a multistep plan
### /loom-skill-enhancement Runtime handoff
- Directly run `so.exe --guide` on Windows or `so --guide` on Unix as the first SO runtime operation; verify the fresh JSON and readable guide path before downstream work.
- Let the agent acquire and verify the exact locked SO product+RID package using host-native tools; no runtime resolver or fixed bootstrap script is required.
- Execute `so.exe`/`so` compile, run, and resume directly from that extracted package. Keep the same apphost, external workflow copy, and persisted state across the full chain.
- Clone the checked-in template to a fresh external workflow copy before each new run; never mutate the checked-in source during execution.
- Compile verifies the template but does not complete a full-delivery slice. Continue direct apphost run/resume through final `Done` on the same workflow copy.
- When a later step benefits from MCP, use optional local MCP bound to the current apphost; do not gate guide or official execution on registration or fragment inspection.
- Preserve route-aware business outputs, user/runtime seam ownership, boundary checks, failure history, event logs, and verified audit links.
- Stop on package, hash, manifest, archive, extraction, apphost, or guide failure. Never convert failure into success evidence or switch to another runtime.
