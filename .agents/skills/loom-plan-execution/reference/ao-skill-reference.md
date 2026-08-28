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

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.


## Caller File Preparation Contract

The calling agent must create the full input set on disk before one AO CLI call and pass only paths. Prepare every required script, JSON, workflow, objective, reference, patch, context, instance, and result input in one step. The CLI preflights all required files before reading or writing. Inline script, JSON, and replacement content is not a supported input form.
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

## Runtime Mode Separation

Resolve `self-contained` versus `.NET CLI mode` before checking the package cache. These are two independent paths.

- In `self-contained` mode, validate and acquire only the exact-RID `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package for the detected platform, then launch its direct `ao.exe` or `ao` entry point. Do not inspect, download, or assemble the .NET runtime bundle on this path.
- In explicit .NET CLI mode, validate and acquire the exact-version `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` bundle, including `ao.dll`, `ao.deps.json`, `ao.runtimeconfig.json`, Roslyn, and dependency closure.
- A failure in the selected mode fails closed. Never switch modes after selection, startup, or a command failure.
- Keep `runtime_mode`, `package_ids`, `rid`, and `launch_descriptor` in runtime evidence so the two paths cannot be mistaken for one another.

## Runtime Acquisition

- For `/loom-plan-execution`, package downloads must follow the current CI/CD-managed skill package version block. Derive `released` versus `beta` from that bound version only when the runtime flow needs a channel distinction.






- On Windows PowerShell 5.1, do not use `Expand-Archive` directly on `.nupkg`. Treat the package as ZIP content and extract it through ZIP-aware APIs or an equivalent ZIP-based flow.
- Resolve the selected mode before package lookup. In self-contained mode, use only the exact-RID executable package; in .NET CLI mode, use only the exact .NET runtime bundle.
- If you probe package URLs through `Invoke-WebRequest` or `Invoke-RestMethod` on Windows PowerShell 5.1, add `-UseBasicParsing` to avoid legacy security prompts that stall automation.

    ## Package Integrity Checks

    Validate the package before launch and fail closed on any mismatch:

    1. Read the exact runtime version from this skill's checked-in version block or package lock. Derive `released` or `beta` from that bound version; never float to `latest`.
    2. Download `Techne.Loom.AgentOrchestrator.Runtime.<rid>` for the detected RID and its `.nupkg.sha512` sidecar. Decode the sidecar and compare it with a locally computed SHA-512 digest before extraction.
    3. Open the `.nupkg` as ZIP content with a ZIP API. Do not use `Expand-Archive` on Windows PowerShell 5.1. Reject path traversal, duplicate paths, oversized entries, and unexpected files.
    4. Validate the root nuspec id and exact version, the RID tag, and the fixed `tools/<rid>/runtime.json` manifest. The manifest must match the product, version, RID, `ao.exe`, `docs_root: tools/<rid>/docs/en`, and `guide_path: guides/ao-guide.md`.
    5. Require `tools/<rid>/ao.exe` plus `tools/<rid>/docs/en/guides/ao-guide.md` and the complete English guide set. The executable does not contain guide pages; all guide content is direct package content.
    6. Run the unpacked `ao.exe --guide` from the complete `tools/<rid>` directory. Parse and read the returned absolute `guide_path`, confirm it is the unpacked `docs/en/guides/ao-guide.md`, and only then continue to `compile`, `run`, or `resume`.
    7. A failed checksum, nuspec, manifest, RID, entrypoint, dependency, extraction, or guide check is failed preflight evidence. Never turn stderr into guide evidence or cross from the selected runtime mode to another mode automatically.
## Extracted Package Guide Entry

This skill publishes no `ao-guide*.md` file. The authoritative guide is part of the English docs bundle in the selected runtime package.

1. Read the exact bound AO version from the skill version block and derive the channel when needed.
2. In the default self-contained mode, restore only `Techne.Loom.AgentOrchestrator.Runtime.<rid>` at that exact version. In explicit .NET CLI mode, restore the exact AO/Common/Abstractions bundle with `ao.dll`, `ao.deps.json`, `ao.runtimeconfig.json`, Roslyn, and its dependency closure.
3. On Windows PowerShell 5.1, treat the `.nupkg` as ZIP content and extract it with a ZIP-aware API. Do not use `Expand-Archive` directly on the package.
4. After extraction, the self-contained layout must contain `<extracted-root>/tools/<rid>/ao.exe` and `<extracted-root>/tools/<rid>/docs/en/guides/ao-guide.md`. The adjacent `runtime.json` must declare `"guide_path": "guides/ao-guide.md"`.
5. Run `.\ao.exe --guide` from the extracted `tools/<rid>` directory, or run the exact `dotnet exec --depsfile .\ao.deps.json --runtimeconfig .\ao.runtimeconfig.json .\ao.dll --guide` binding in .NET CLI mode.
6. Parse the JSON result and read its absolute `guide_path`. Use that extracted guide and its adjacent flow, reference index, and chapter pages as the version-specific authority. Never substitute a guide file copied into this skill.

## Startup Contract Preflight

Before AO command execution in package-channel mode, verify:

- `ao.dll`
- `ao.deps.json`
- `ao.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.
- If extraction fails or any startup-contract file is missing, stop immediately. Do not emit `runtime_preflight_result: passed`.

## Launch Mode

Default package-channel launch uses the exact-RID published self-contained executable package: run `.\ao.exe` on Windows or `./ao` on Unix. The framework-dependent `dotnet exec ... ao.dll` path below is only for explicit .NET CLI mode.

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --depsfile <ao.deps.json> --runtimeconfig <ao.runtimeconfig.json> <ao.dll> ...`

## Runtime Flow Details

- After skill-bound version and runtime-source selection, the next hard gate is proving that the selected AO runtime is runnable, executing the bare `dotnet ao.dll --guide`, parsing its JSON result, and reading the returned `guide_path` and `docs_root`.
- Do not proceed to planning, authoring, validation, `compile`, `prompt-plan`, `prompt-replan`, `run`, `resume`, or downstream input collection before that guide result exists.
- Once that guide result exists, official governed execution must return to the corresponding published AO package runtime surface that the guide describes. Reading `--guide` does not allow official execution to keep drifting on repository builds, hand-assembled runtimes, or other non-governed paths.
- Failed stderr output from `dotnet ao.dll --guide` or `dotnet exec ... ao.dll --guide` is not a guide artifact. Record guide evidence only after the command succeeds, returns JSON, and the returned `guide_path` and startup-contract files are readable.
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

If a specific `dotnet ao.dll` call did not emit a fresh Mermaid render, repeat the latest known `audit_markdown_file` and `audit_html_file` as direct clickable Markdown file links, say that the render is unchanged, and add a concise workflow-location summary so the user can still tell where the active workflow currently is in this session. Never expose only a bare Mermaid path. If the chat agent provides a Mermaid card-display tool, pass the existing Mermaid file path directly to it instead; do not read or return the file contents again solely to display the card.

`must_show_to_user_files` should contain the ordered file list that the user-facing update must cite or surface for that call. If the chat agent provides a Mermaid card-display tool, pass the existing Mermaid file path directly to it without reading or returning its contents again solely for display. Otherwise render every Mermaid path in the user-facing update as a direct clickable Markdown file link, using a workspace-relative link when the artifact is inside the workspace; a bare path alone is insufficient.

## Plain-Language Feedback For Every Language

Write every user-facing progress, blocked, error, and completion update in the user's requested language for a high-school reader with no workflow background. English is not automatically plain language. Use short sentences and everyday words; state what happened, whether the user's work or data is still safe, why it happened, and the next action, in that order. Translate internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon before exposing exact technical details. Keep commands, paths, IDs, and evidence fields in a separate technical-details section only when needed. This rule also applies to target-skill feedback reported through AO.

## Business-Outcome-First Gate

- If objective/plan clearly requests business outputs, completion requires business deliverables plus AO completed state.
- Runtime-only or meta-only reporting cannot replace business delivery completion.
