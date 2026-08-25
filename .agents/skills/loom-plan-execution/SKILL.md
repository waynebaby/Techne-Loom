---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and Loom Agent Execution Orchestrator runtime surfaces.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the package and guide surface that matches the current CI/CD-managed skill package version block, then routes execution through the applicable Loom Agent Execution Orchestrator runtime surface.

Once the skill-bound package version or runtime source is chosen, this skill must first prove that the selected Loom Agent Execution Orchestrator runtime for that source is runnable and can execute the bare `dotnet ao.dll --guide` command successfully. The command installs the version-matched English docs bundle and returns JSON containing the actual `version`, `docs_root`, and `guide_path` paths. Before that proof exists, do not proceed to planning, authoring, validation, compile, `prompt-plan`, `prompt-replan`, run, resume, or any downstream input collection. Once the JSON result and readable `guide_path` exist, treat that guide as a hard governance handoff back onto the corresponding published AO package runtime surface for official execution. Do not let `--guide` become a detour that drifts back to repository builds, hand-assembled runtimes, or other non-governed paths.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local Loom Agent Execution Orchestrator repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill also enforces Loom Agent Execution Orchestrator-strong governance for official plan execution. In that governance model, Loom Agent Execution Orchestrator is the only official execution authority for this skill, only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs, and any direct non-Loom Agent Execution Orchestrator path stays outside official skill execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## Read This First

- Shared terminology authority: `../../../docs/en/architecture/workflow-terminology.md` (bilingual human-friendly status mapping; read it before any user-facing output).

<!-- skill-package-version-block:start -->
- Current published AO package runtime version: `0.3.245`.
- This block is refreshed by the publish workflows whenever AO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->






























Follow the current skill package version block first, then derive the matching package surface:

- When the current skill package version is stable, use released references: `reference/packages.released.md` and `reference/ao-guide.released.md`
- When the current skill package version is prerelease, use beta references: `reference/packages.beta.md` and `reference/ao-guide.beta.md`

- Workflow designer subagent: `assets/agents/loom-plan-execution-workflow-designer.agent.md`
- SO governance baseline assets for AO enhancement:
	- Per-run plan output: `<execution-output-root>/plan/skill-plan.md` (runtime-owned; not a stable skill asset)
	- `assets/so-workflow/so-template.json`
	- `assets/so-workflow/so-package-lock.json`
	- `assets/so-workflow/node-to-file-map.md`

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Runtime version authority: the current CI/CD-managed skill package version block; derive `released` versus `beta` from that bound version when needed
- Guide input: run bare `dotnet ao.dll --guide`; it is English-only and returns JSON with `version`, `docs_root`, and `guide_path` instead of accepting a language flag
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during Loom Agent Execution Orchestrator-based plan execution:

- Loom Agent Execution Orchestrator is the only official execution authority for this skill; only explicit `dotnet ao.dll run` and `dotnet ao.dll resume` count as official skill runs.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- Official AO runtime uses dual published channels: the default is the exact-RID published self-contained single-file executable package (`ao.exe` on Windows, or the matching executable on Unix); legacy framework/library `dotnet ao.dll` mode is opt-in only through `runtimeBinding` or an explicit bundle directory, and repository-source debug mode is opt-in only when explicitly requested.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- If runtime extraction, startup-contract checks, or guide execution fail, stop immediately and keep `runtime_preflight_result` and guide-refresh evidence in a failed state. Do not write success proof or treat failed command stderr as a guide; record only the successful JSON result and the readable `guide_path` returned by the runtime.
- In repo-src-debug mode, build and use the current repository Loom Agent Execution Orchestrator output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state under `session_dir` or explicit execution-output roots.
- After every `dotnet ao.dll` CLI call, report Mermaid continuity back to the user in-session: when the call emits fresh audit artifacts, report the fresh Mermaid/HTML paths plus a concise workflow-location summary; when it does not emit a fresh Mermaid, repeat the latest known Mermaid/HTML paths and state that the render is unchanged.

### Non-Negotiable Official Execution Gate

- For every full-delivery execution of this skill itself, `dotnet ao.dll compile` is never an end state. It is only a validation checkpoint; `--guide`, `prompt-plan`, `prompt-replan`, and helper scripts are preparation or recovery surfaces only.
- After the guide handoff, create or reuse one fresh external runtime workflow instance copy and record its immutable instance identity, workflow-file path, and persisted runtime-state/session path. Run `dotnet ao.dll compile` against that exact external copy, then immediately dispatch the public `dotnet ao.dll run` against the same copy. Every later `dotnet ao.dll resume` must reference the same persisted instance and state; never switch to a new workflow copy between compile, run, or a block. Do not stop at a preflight explanation, local-source debug output, planning output, compile output, or a blocked-state description when the user has required execution.
- If `run` returns a runtime-owned block, continue with `dotnet ao.dll resume` against that exact workflow instance and persisted state. If the runtime reports a recoverable failure, preserve its evidence and resume from the previous state on the same persisted instance. Repeat until the AO runtime reaches its terminal completed state; a blocked payload alone is never a terminal outcome. Stop only when the failed instance has no recoverable previous state or the official runtime cannot start, and preserve that failure evidence.
- Never claim AO-governed completion from local orchestration, direct scripts, repo-source debug execution, compile success, guide success, prompt planning, prompt replanning, or an unresumed block. The completion report must state the official command chain, final runtime status/frontier, and the event-log and audit evidence paths.
- When the official runtime cannot be started, the result is failed preflight, not governed completion. Preserve the failure evidence and do not substitute a local or helper execution path.

Detailed assumptions, startup contracts, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/ao-skill-reference.md`

Workflow generation or revision for this skill must use the local workflow-designer subagent with context-rich relative links, not a freeform generic agent call:

- `assets/agents/loom-plan-execution-workflow-designer.agent.md`

When using that workflow-designer route, enforce deterministic workflow authoring contracts instead of descriptive-only prose:

- each transition must declare executable `guardExpression` and `succeedExpression` predicates, explicit evidence outputs, and explicit seam ownership
- each gate must declare machine-checkable pass predicates, required evidence references, and route coverage mapping
- weave-out hints must preserve resume continuity contract fields and expected payload/evidence shapes
- final workflow proposals must include transition, gate, and ownership preflight checklists before JSON output is accepted
- fail closed: reject vague transition/gate wording that lacks concrete predicates or evidence paths

That exact `.agent.md` file is the authoritative behavior source for the workflow-designer subagent. Do not require it to be mirrored into `.github/agents/`, a user-profile agent folder, or any other discoverable agent root before use. If the runtime can resolve the exact subagent name directly, invoke that name directly while keeping the declared `.agent.md` file as the contract. If direct name resolution is unavailable, resolve the declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate agent role.

## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm the current skill-bound package version, derive channel from its version shape when needed, and confirm runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build Loom Agent Execution Orchestrator from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: restore the full Loom Agent Execution Orchestrator bundle into one unified runtime, use ZIP-based extraction for `.nupkg` on Windows PowerShell 5.1, run startup-contract preflight, and use explicit launch mode.
3. Prove the selected runtime can run the bare `--guide` command, parse its JSON result, and read the returned `guide_path` and `docs_root` before proceeding.
4. Only after that guide result exists, run planning surfaces (`prompt-plan`) and capture required prompt blocks.
5. When creating or revising a workflow, invoke the local workflow-designer subagent and give it the relevant skill files, guide files, plan files, and audit artifacts through relative links.
6. Materialize one external WorkflowInstance copy outside skill paths, record its immutable instance identity and persisted runtime-state/session path, then run `compile` against that exact copy.
7. Run Loom Agent Execution Orchestrator with that same external WorkflowInstance copy; every later `resume` must reuse its persisted runtime state.
8. On blocked state, use payload signals plus `prompt-replan` to update seam nodes, then `resume` with structured envelope payload.
9. When the current route is confirmed blocked, persist the current workflow state, blocker report, all attempted remedies and their outcomes, and the relevant event/audit references before asking the AO planner to replan.
10. Replan from the retained history by selecting an explicit strategy: continue from the current state, roll back to an unconfirmed design node, redesign from the current state, replace the whole plan, or apply a smallest reversible workaround.
11. Require the planner to return a viable path to the terminal business outcome, including a rollback or workaround path when selected, before resuming execution.
12. Repeat replan/resume until Loom Agent Execution Orchestrator reaches completed state.
13. Report completion only when Loom Agent Execution Orchestrator is completed and requested business deliverables are verifiable.

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
- blocker history must retain the blocked node, blocker reason, attempted actions, outcomes, evidence references, and the selected replan anchor/strategy
- the planner must receive that retained history as input and must not silently discard failed attempts or prior route decisions
- a replan is invalid unless it declares a path from its selected anchor to the terminal business outcome and preserves a one-step rollback plan for any workaround
- skill-level checklist only comes from AO workflow nodes, frontiers, transitions, blocked states, and resume points
- skill-level run map only comes from the AO runtime `workflow_file`, `next_frontier`, blocked state, and audit artifacts
- skill-level evidence only comes from AO-owned runtime state and audit artifacts
- non-AO tests do not count as official skill execution evidence
- prose flow and helper command examples are explanatory only, not execution authority
- when caller objectives explicitly request business outputs, AO completion state alone is insufficient without the corresponding business deliverables

Detailed prohibited/acceptance examples are maintained in reference docs.
