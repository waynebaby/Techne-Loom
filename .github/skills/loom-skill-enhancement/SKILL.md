---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and Loom Skill Orchestrator package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill for Loom Skill Orchestrator (`dotnet so.dll`).

## Mission

This skill upgrades or creates a target skill so its deterministic execution is governed through the Loom Skill Orchestrator package flow. Business scope is always target-skill delivery; runtime validation is supporting work only.

Every enhancement pass must first prove that the skill-bound published Loom Skill Orchestrator runtime is runnable and can emit a fresh `dotnet so.dll --guide [--lang <language>]` result from that runtime. Before that proof exists, do not proceed to planning, authoring, validation, compile, run, resume, or any downstream input collection. Derive runtime channel from the bound package version when needed; do not ask the user to choose released versus beta during normal SO enhancement runs.

## Read First

<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.2.151`.
- This block is refreshed by the publish workflows whenever SO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->













- Released package index: `reference/packages.released.md`
- Beta package index: `reference/packages.beta.md`
- Released guide: `reference/so-guide.released.md`
- Beta guide: `reference/so-guide.beta.md`
- Workflow designer subagent: `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`
- Reusable weave-out subagents:
	- `assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md`
	- `assets/agents/loom-skill-enhancement-review-fix-loop.agent.md`
	- `assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md`
	- `assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md`
	- `assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md`
- Authority command: `dotnet so.dll --guide [--lang <language>]`

## Workflow Contract

### Inputs

- target skill root path that directly contains `SKILL.md` and `assets/so-workflow/`
- deterministic skill goal or upgrade request
- requested target-skill changes
- runtime version authority: the checked-in `assets/so-workflow/so-package-lock.json` plus the current CI/CD-managed skill package version block; derive channel from the bound version shape when needed instead of asking the user
- optional guide language flag
- optional JSON context file
- optional audit output root

### Self-Bootstrap Assets

- `assets/so-workflow/skill-plan.md`
- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`

### Defaults

- Keep Loom Skill Orchestrator-owned materials under `assets/so-workflow/`.
- For `/loom-skill-enhancement` itself and any Loom-governanced target skill, official workflow operations and package downloads must use the published Loom Skill Orchestrator package artifacts bound to the current CI/CD-managed skill version block and checked-in package lock, not repository source builds, ad hoc local project outputs, or hand-assembled runtime folders, unless the user explicitly approves a last-resort blocked-state workaround.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- Treat the checked-in workflow template as immutable; every new official SO run must start from a freshly copied external runtime workflow file derived from the template or current checked-in source workflow, and any later resume in that same execution chain must continue against that same persisted runtime copy rather than mutating the checked-in file in place.
- Keep compile and audit artifacts outside the skill folder unless the user explicitly chooses otherwise.
- In exclusive Loom Skill Orchestrator governance mode, only `dotnet so.dll run` and `dotnet so.dll resume` count as official runs.
- Normal enhancement governance for this skill and any Loom-governanced target skill must stay on the `dotnet so.dll --guide`, `dotnet so.dll compile`, `dotnet so.dll run`, and `dotnet so.dll resume` path. Do not treat direct workflow JSON edits as a routine control path.
- Direct edits to the running external workflow `.json` copy are allowed only when the current `dotnet so.dll` path is fully blocked, the user explicitly approves a minimal workaround, the edit is the smallest change needed to unblock the next SO command, and the very next step returns to `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume`.
- If runtime extraction, startup-contract checks, or guide execution fail, stop immediately and keep `runtime_preflight_result` and guide-refresh evidence in a failed state. Do not write success proof or exported guide files from failed commands.
- After every `dotnet so.dll` CLI call, report Mermaid continuity back to the user in-session: when the call emits fresh audit artifacts, report the fresh Mermaid/HTML/analysis paths plus a concise workflow-location summary; when it does not emit a fresh Mermaid, repeat the latest known Mermaid/HTML/analysis paths and state that the render is unchanged.

### Workflow Baseline

- Enter plan mode before editing target-skill deliverables.
- When creating or revising workflow templates, invoke the local workflow-designer subagent with relative-link context, not a freeform generic agent.
- When a route names a specific local or target-skill `.agent.md` file, treat that exact file as the only authoritative subagent contract. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders before use.
- If direct exact-name subagent resolution is available, invoke that exact subagent name while keeping the named `.agent.md` file as the authority. If direct resolution is unavailable, resolve the named `.agent.md` path from the current repository/workspace copy first and the corresponding global installed-skill copy second, then pass the resolved file path plus the full file content into the subagent-driving call.
- Do not replace a named `.agent.md` route with a freeform approximate role, a repository-global prompt, or an ad hoc summary of the intended subagent behavior.
- Analyze inputs, outputs, branches, loops, seams, gates, and expected evidence.
- Generate the workflow template JSON first, then compile it.
- Keep the workflow template JSON as the authority.
- Repeat a user confirmation loop by updating the template or its source planning inputs and recompiling.
- For target-skill templates that declare root `templateKind: so-governed-target-skill`, also declare a root `validation` contract with `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`.
- If a target-skill template with root `templateKind: so-governed-target-skill` is intended to become runnable execution authority, its materialized runtime workflow copy must execute on the current public `dotnet so.dll run` and `dotnet so.dll resume` path. Do not leave the runnable copy in `Drafting`, and do not depend on private or unavailable built-in tool names.
- If a checked-in workflow JSON is only a draft or compile-review source template, label it explicitly as source-only and do not present it as directly runnable.
- When `MemoryRead` inspects checked-in target-skill assets, it must load real file snapshots from an explicit target-skill asset root and must reject absolute paths or traversal outside that root.
- Never author a node whose purpose says or implies `run a multistep plan`.
- For weave-out design, prefer existing capable subagents whenever they can already complete the goal instead of emitting generic agent placeholders.
- If a target-skill enhancement weave-out is clearly reusable and benefits from a dedicated subagent, recommend creating a detailed `{target-skill-name}-{task-name}.agent.md` under `{skill-folder}/assets/`, route future workflow nodes to that subagent explicitly, add a relative-link reference to that file in the target `SKILL.md`, and reference the same relative path in the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.
- For any target-skill local subagent route introduced under `{skill-folder}/assets/`, the target skill must treat that exact `.agent.md` file as the subagent's authority source during both documentation handoff and runtime invocation. Resolution should test the target skill's repository/workspace copy first and the corresponding global installed-skill copy second before failing.

### Required Outputs

- bound runtime version confirmation plus derived channel evidence
- runtime-ready evidence for the selected published SO bundle before downstream work
- Windows package-channel runtime acquisition evidence when PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast proof when extraction or guide generation fails
- package index links
- guide surface references
- target `SKILL.md` governance wording that keeps ordinary workflow changes on the SO CLI path and limits direct workflow JSON edits to blocked-state, user-approved emergency workarounds
- target `SKILL.md` execution-status wording for both creation and update slices that distinguishes compile-ready governance integration from official run evidence, states that `dotnet so.dll compile` is validation only, and forbids claiming an official governed run before at least one public `dotnet so.dll run` chain exists and, when the route blocks, the matching public `dotnet so.dll resume` chain exists
- target `SKILL.md` runtime hardening wording that forbids pseudo-success preflight/guide records and requires ZIP-based `.nupkg` extraction on Windows PowerShell 5.1 package-channel restores
- workflow template path
- workflow-designer subagent dispatch record and relative-link context set used for workflow generation
- weave-out suitability review that checks whether every current weave-out should become a dedicated target-skill local `{skillname}-{taskname}.agent.md`
- target-skill local subagent definition paths created or refreshed under `assets/`
- target `SKILL.md` and target reference-doc relative-link updates for any newly required target-skill local weave-out subagents
- workflow analysis report
- compiled Mermaid
- node-to-file or node-to-artifact map
- package lock metadata split into:
	- checked-in lock reference target
	- resolved runtime bundle version/channel evidence
	- runtime-owned completion-manifest reference to the checked-in lock asset
- checked-in skill-markdown governance outcome split into:
	- checked-in skill-markdown target path
	- governed evidence that the checked-in skill markdown is the source deliverable for this slice
	- runtime-owned completion-manifest reference to that checked-in source asset
- runtime audit artifact links
- session-level Mermaid continuity after every `dotnet so.dll` call, including fresh-or-latest Mermaid/HTML/analysis paths and a concise workflow-location summary

### Governance

- Exclusive Loom Skill Orchestrator governance mode uses Loom Skill Orchestrator as the only official execution authority.
- For `/loom-skill-enhancement` itself and any Loom-governanced target skill, that execution authority must come from published SO package artifacts for the chosen channel. Do not normalize repository-source builds or manually assembled binaries into the default workflow-operation path.
- For both new target-skill creation slices and update or re-enhancement slices, do not present guide refresh, checked-in asset creation, workflow-template authoring, or `dotnet so.dll compile` success as official governed run evidence by themselves.
- AskUser seams may request only declared user-owned fields or decisions.
- Runtime-owned facts and artifact paths belong to runtime-owned seams such as `WaitResume`.
- Route-aware terminal and blocked business-output gates are required for governed routes.
- For target-skill modifications, runtime-ready evidence and fresh-guide evidence must exist before downstream planning, authoring, validation, compile, run, or resume work starts.
- If a governed workflow is presented as runnable execution authority, its materialized runtime copy must actually execute on the current public `dotnet so.dll run` and `dotnet so.dll resume` path rather than being only compile-clean.
- If a slice stops after guide refresh, checked-in asset creation, and compile validation without a real public `run` or `resume` chain, the target `SKILL.md`, completion evidence, and final report must label the result as governance integration complete or compile-ready, with official run evidence still pending.
- File-backed checked-in asset inspection must stay rooted under the declared target-skill asset root and must not degrade into placeholder context-copy review.
- Before completion, review every current weave-out and decide whether it should be implemented as a dedicated target-skill local subagent under `assets/{skillname}-{taskname}.agent.md`; when the answer is yes, create or refresh that subagent file and add the relative-link reference in the target `SKILL.md` and target reference docs before the workflow can complete.
- Before completion, run an explicit review-skill -> fix-skill loop on the target skill, then prepare commit-and-report-ready evidence for the final handoff instead of stopping immediately after template compile or first-pass edits.
- When a slice still uses checked-in source assets as the authoritative business deliverables, the governed route must name those checked-in assets explicitly as done outputs and must emit a runtime-owned completion manifest that references them instead of pretending runtime-owned temporary files replaced them.
- Completion requires requested target-skill deliverables to be created or modified.

## Runtime Flow

1. Classify governance state and lock the goal to target-skill delivery.
2. Confirm the skill-bound package version and derived channel, prove the corresponding published Loom Skill Orchestrator runtime can run, and capture a fresh guide surface from that runtime.
3. Only after that guide result exists, enter plan mode and derive `skill-plan.md`.
4. Author or refresh the workflow template and package lock.
5. Compile the workflow template and review the analysis report.
6. Apply feedback, recompile if needed, then update the target `SKILL.md` with the correct execution-status wording for the current slice.
7. If the slice needs to claim runnable execution authority or official governed run evidence, materialize an external runtime workflow copy and execute the public `dotnet so.dll run` path, then continue with `dotnet so.dll resume` when the route blocks.
8. Keep runtime workflow copies, event logs, and audit artifacts outside the skill folder.

## Exclusive Loom Governance Completion

- The target skill states that it has switched into Loom-governanced execution under Loom Skill Orchestrator.
- The target skill states in its own `SKILL.md` that ordinary workflow changes stay on the Loom-governanced CLI path and that direct workflow JSON edits are blocked-state-only emergency workarounds.
- The target skill states in its own `SKILL.md` whether the current slice produced only compile-ready governance integration or also produced official governed run evidence, and it must not claim an official run without at least one public `dotnet so.dll run` chain and the matching public `dotnet so.dll resume` chain when the route blocks.
- Direct CLI and direct MCP remain primitive paths only.
- Official run evidence comes only from Loom Skill Orchestrator workflow state, event log, and audit artifacts.
