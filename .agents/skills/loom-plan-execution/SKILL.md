---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and Loom Agent Execution Orchestrator runtime surfaces.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the package and guide surface that matches the current CI/CD-managed skill package version block, then routes execution through the applicable Loom Agent Execution Orchestrator runtime surface.

Once the skill-bound package version or runtime source is chosen, this skill must first prove that the selected Loom Agent Execution Orchestrator runtime for that source is runnable and can emit a fresh `dotnet ao.dll --guide [--lang <language>]` result from that runtime. Before that proof exists, do not proceed to planning, authoring, validation, compile, `prompt-plan`, `prompt-replan`, run, resume, or any downstream input collection. Once that guide result exists, treat it as a hard governance handoff back onto the corresponding published AO package runtime surface for official execution. Do not let `--guide` become a detour that drifts back to repository builds, hand-assembled runtimes, or other non-governed paths.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local Loom Agent Execution Orchestrator repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill also enforces Loom Agent Execution Orchestrator-strong governance for official plan execution. In that governance model, Loom Agent Execution Orchestrator is the only official execution authority for this skill, only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs, and any direct non-Loom Agent Execution Orchestrator path stays outside official skill execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## Read This First

<!-- skill-package-version-block:start -->
- Current published AO package runtime version: `0.2.170`.
- This block is refreshed by the publish workflows whenever AO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->















Follow the current skill package version block first, then derive the matching package surface:

- When the current skill package version is stable, use released references: `reference/packages.released.md` and `reference/ao-guide.released.md`
- When the current skill package version is prerelease, use beta references: `reference/packages.beta.md` and `reference/ao-guide.beta.md`

- Workflow designer subagent: `assets/agents/loom-plan-execution-workflow-designer.agent.md`

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Runtime version authority: the current CI/CD-managed skill package version block; derive `released` versus `beta` from that bound version when needed
- Optional input: guide language flag (`--lang <language>`) when the runtime guide call needs explicit language selection
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during Loom Agent Execution Orchestrator-based plan execution:

- Loom Agent Execution Orchestrator is the only official execution authority for this skill; only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- In package-channel mode, restore the full Loom Agent Execution Orchestrator runtime bundle that matches the current skill package version block into one unified runtime directory, enforce startup-contract preflight, and use explicit launch mode for deterministic host binding.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- If runtime extraction, startup-contract checks, or guide execution fail, stop immediately and keep `runtime_preflight_result` and guide-refresh evidence in a failed state. Do not write success proof or exported guide files from failed commands.
- In repo-src-debug mode, build and use the current repository Loom Agent Execution Orchestrator output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state under `session_dir` or explicit execution-output roots.
- After every `dotnet ao.dll` CLI call, report Mermaid continuity back to the user in-session: when the call emits fresh audit artifacts, report the fresh Mermaid/HTML paths plus a concise workflow-location summary; when it does not emit a fresh Mermaid, repeat the latest known Mermaid/HTML paths and state that the render is unchanged.

Detailed assumptions, startup contracts, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/ao-skill-reference.md`

Workflow generation or revision for this skill must use the local workflow-designer subagent with context-rich relative links, not a freeform generic agent call:

- `assets/agents/loom-plan-execution-workflow-designer.agent.md`

That exact `.agent.md` file is the authoritative behavior source for the workflow-designer subagent. Do not require it to be mirrored into `.github/agents/`, a user-profile agent folder, or any other discoverable agent root before use. If the runtime can resolve the exact subagent name directly, invoke that name directly while keeping the declared `.agent.md` file as the contract. If direct name resolution is unavailable, resolve the declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate agent role.

## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm the current skill-bound package version, derive channel from its version shape when needed, and confirm runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build Loom Agent Execution Orchestrator from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: restore the full Loom Agent Execution Orchestrator bundle into one unified runtime, use ZIP-based extraction for `.nupkg` on Windows PowerShell 5.1, run startup-contract preflight, and use explicit launch mode.
3. Prove the selected runtime can run and capture a fresh `--guide` result from that runtime.
4. Only after that guide result exists, run planning surfaces (`prompt-plan`) and capture required prompt blocks.
5. When creating or revising a workflow, invoke the local workflow-designer subagent and give it the relevant skill files, guide files, plan files, and audit artifacts through relative links.
6. Author a WorkflowInstance outside skill paths, then run `compile`.
7. Run Loom Agent Execution Orchestrator with that WorkflowInstance when graph continuity matters.
8. On blocked state, use payload signals plus `prompt-replan` to update seam nodes, then `resume` with structured envelope payload.
9. Repeat replan/resume until Loom Agent Execution Orchestrator reaches completed state.
10. Report completion only when Loom Agent Execution Orchestrator is completed and requested business deliverables are verifiable.

For AO workflow design and AO weave-out planning, prefer existing capable subagents whenever they can already complete the weave-out goal instead of emitting generic agent placeholders.

Operational details for prompt blocks, payload conventions, and blocked-state handling are defined in reference docs.

## Required Outputs

- bound runtime version confirmation with derived released/beta evidence and matching canonical links
- runtime source selection and version-derived channel resolution metadata
- package-channel runtime facts: version, bundle list, unified runtime directory, preflight result, and launch mode
- package-channel runtime acquisition facts when Windows PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast evidence when extraction or guide generation fails
- workflow/session/event paths and audit artifact links
- required think-out-loud fields for runtime and audit updates
- session-level Mermaid continuity after every `dotnet ao.dll` call, including fresh-or-latest Mermaid/HTML paths and a concise workflow-location summary
- business deliverable verification summary when business-first mode applies

For the full output matrix and field-level contracts, use reference docs.

## Prohibited Results

Reject or mark invalid any execution result that says or implies any of these:

- AO is optional for official skill execution
- AO and another path are parallel official execution modes for this skill
- AO runtime artifacts alone are accepted as final completion when the caller explicitly requested business outputs
- business-output requests are silently downgraded to runtime/meta-only execution without explicit user approval
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
- when caller objectives explicitly request business outputs, AO completion state alone is insufficient without the corresponding business deliverables

Detailed prohibited/acceptance examples are maintained in reference docs.
