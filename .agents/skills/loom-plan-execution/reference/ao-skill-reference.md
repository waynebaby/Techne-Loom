# AO Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-plan-execution/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-plan-execution` needs to create or revise workflow JSON:

- [../assets/agents/loom-plan-execution-workflow-designer.agent.md](../assets/agents/loom-plan-execution-workflow-designer.agent.md)

Pass relative links to the plan file, guide file, workflow file, audit artifacts, and any blocked payload evidence so the subagent runs with explicit local context instead of relying on repository-global discovery.

That declared `.agent.md` file is the authoritative behavior contract for the workflow-designer subagent. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders. If the runtime supports direct exact-name resolution, invoke that exact subagent name while keeping the declared `.agent.md` file as the contract. If exact-name resolution is unavailable, resolve the same declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second before failing, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate role or repository-global substitute prompt.

The subagent must generate node-level granularity where each node owns one visible responsibility and where every AO weave-out path has a detailed blocked-action hint.

The subagent must also enforce deterministic transition/gate contracts and fail-closed anti-hallucination behavior:

- transition contracts must use executable boolean predicates for `guardExpression` (pre-execution eligibility) and `succeedExpression` (post-execution output acceptance)
- transition contracts must include explicit output evidence and explicit ownership of required inputs
- gate contracts must include machine-checkable pass predicates, required evidence references, and route coverage mapping
- workflow output must include preflight checklists for transitions, gates, and `AskUser` ownership before final JSON is emitted
- reject vague prose-only transition/gate wording when predicates or evidence paths are missing

## Blocked-Route History And Replanning

When AO confirms that the current route cannot progress, do not send only the latest blocked payload to the planner. Persist and pass a structured `replan_history` containing:

- the current workflow and blocked node identifiers
- the blocker reason and the exact unmet requirement
- ordered attempted actions, outcomes, and evidence references
- the latest event log and audit artifact references
- the selected replan anchor and strategy

The planner must choose one explicit strategy:

- `continue_from_current`: continue from the current state with a new viable bridge
- `rollback_to_unconfirmed`: return to the latest unconfirmed or not-yet-designed node and design forward from there
- `redesign_from_current`: preserve completed history but replace the failing continuation
- `full_redesign`: discard the current route design while retaining historical evidence and the terminal business objective
- `reversible_workaround`: apply the smallest reversible workaround, with one-step rollback evidence

Every strategy must produce a candidate path that can reach the terminal business outcome. A workaround must additionally provide a rollback plan. Do not silently erase failed attempts, blocker history, or previous route decisions when generating `prompt-replan` input.

## Runtime Acquisition

- For `/loom-plan-execution`, package downloads must follow the current CI/CD-managed skill package version block. Derive `released` versus `beta` from that bound version only when the runtime flow needs a channel distinction.
- In package-channel mode, restore the AO runtime bundle together at one resolved version:
  - `Techne.Loom.AgentOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- Build one unified runtime directory and execute AO commands from that directory only.
- Do not execute from partial single-package extraction roots.
- On Windows PowerShell 5.1, do not use `Expand-Archive` directly on `.nupkg`. Treat the package as ZIP content and extract it through ZIP-aware APIs or an equivalent ZIP-based flow.
- If you probe package URLs through `Invoke-WebRequest` or `Invoke-RestMethod` on Windows PowerShell 5.1, add `-UseBasicParsing` to avoid legacy security prompts that stall automation.

## Startup Contract Preflight

Before AO command execution in package-channel mode, verify:

- `ao.dll`
- `ao.deps.json`
- `ao.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.
- If extraction fails or any startup-contract file is missing, stop immediately. Do not emit `runtime_preflight_result: passed`.

## Launch Mode

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --depsfile <ao.deps.json> --runtimeconfig <ao.runtimeconfig.json> <ao.dll> ...`

## Runtime Flow Details

- After skill-bound version and runtime-source selection, the next hard gate is proving that the selected AO runtime for that source is runnable and can emit a fresh `dotnet ao.dll --guide [--lang <language>]` result from that runtime.
- Do not proceed to planning, authoring, validation, `compile`, `prompt-plan`, `prompt-replan`, `run`, `resume`, or downstream input collection before that guide result exists.
- Once that guide result exists, official governed execution must return to the corresponding published AO package runtime surface that the guide describes. Reading `--guide` does not allow official execution to keep drifting on repository builds, hand-assembled runtimes, or other non-governed paths.
- Failed stderr output from `dotnet ao.dll --guide` or `dotnet exec ... ao.dll --guide` is not a guide artifact. Save exported guide files only after the guide command succeeds and the startup-contract files are present.
- Use guide and prompt surfaces for preparation:
  - `dotnet ao.dll --guide`
  - `dotnet ao.dll prompt-plan`
  - `dotnet ao.dll prompt-replan`
  - `dotnet ao.dll compile`
- Official skill runs remain only:
  - `dotnet ao.dll run`
  - `dotnet ao.dll resume`

## Think-Out-Loud Required Fields

Report runtime fields once runtime is prepared, after every `dotnet ao.dll` CLI call, and on each progress update:

- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

Report audit fields after every `dotnet ao.dll` CLI call and on each progress update:

- `audit_markdown_file`
- `audit_html_file`
- `must_show_to_user_files`
- `workflow_location_summary`

If a specific `dotnet ao.dll` call did not emit a fresh Mermaid render, repeat the latest known `audit_markdown_file` and `audit_html_file` anyway and say that the render is unchanged, then add a concise workflow-location summary so the user can still tell where the active workflow currently is in this session.

`must_show_to_user_files` should contain the ordered file list that the user-facing update must cite or surface for that call. In this skill it normally contains the current Mermaid Markdown and HTML artifact paths.

## Business-Outcome-First Gate

- If objective/plan clearly requests business outputs, completion requires business deliverables plus AO completed state.
- Runtime-only or meta-only reporting cannot replace business delivery completion.
