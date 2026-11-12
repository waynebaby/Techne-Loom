# Execution And Reporting Contract

Before execution, progress reporting, recovery, or completion, read this complete contract. Paths in backticks are relative to the skill root unless stated otherwise. Return to the [skill entrypoint](../SKILL.md) for the exact version and execution route.

## Plain-Language Feedback For Every Language

All user-facing progress, blocked, error, and completion updates from this skill must be understandable to a high-school reader with no workflow background, in the language requested by the user. English is not automatically plain language, and the rule applies equally to every supported language.

Use short sentences, familiar words, and direct verbs. Say four things in order: what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and exactly what will happen next.

Do not make the reader translate status values, step kinds, node IDs, gate names, handoff terms, runtime details, or audit jargon. Explain a necessary technical word in ordinary language before showing its exact name. Keep commands, paths, IDs, and payload fields in a separate `Technical details` line only when they help the user act or verify the result. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the user-facing explanation; keep exact identifiers in technical details or evidence only. The same rule and the term examples below apply to any skill being enhanced feedback reported through AO; for the skill being enhanced instructions should carry a compact version of them.

## Artifact Address Usability

Use the complete local file URL for every artifact address in the standard three-slash form (`file://` plus an empty authority plus the absolute path): Windows `file:///C:/...`; Linux/macOS `file:///...`. Drive-letter case is insignificant; never use the two-slash `file://C:/...` form, which places the drive letter in the URL authority and does not open. Verify that the URL resolves to an existing readable file in the active host. If the file URL cannot be opened, create and verify a mirror in the active workspace and use the mirror URL instead. Encode every space in the URL as `%20`; use that identical encoded URL in the clickable Markdown link and following text fence. Never return a literal-space URL, bare path, truncated or stale URL, or code fence without a link. If no URL opens, report the delivery blocker.

## Per-Execution Progress Log

For each AO post-execution progress report, write the complete Markdown entry to a separate external file. Include ISO 8601 UTC and local timestamps with numeric UTC offset and OS timezone ID. Resolve this skill's root to an absolute path and invoke its bundled helper under `assets/scripts/`: Windows uses `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <active-skill-root>/assets/scripts/AppendProgressReport.ps1 -TargetFile <execution-output-root>/progress-report.md -EntryFile <entry-file>`; Linux/macOS uses `bash <active-skill-root>/assets/scripts/AppendProgressReport.sh --target-file <execution-output-root>/progress-report.md --entry-file <entry-file>`. The helper reads only the entry file and appends to the target. It returns the target's full file URL; reuse that exact URL if reporting the log location. Never read, open, or inspect the existing `progress-report.md`, and never use manual append or read-modify-write. If the helper fails, report the failure and pause later business-step progression without changing workflow state.

## Plain-Language Term Examples

The left column is for machine records. The right column shows the meaning to express to a user. These English sentences are examples, not fixed output: translate the meaning into the user's requested language. English is not a substitute for simple language in another locale.

| Internal wording | Say this in the user's language |
| --- | --- |
| `Done` | "The requested work is complete." |
| `WaitResume` | "I need your information or confirmation before I can continue." |
| `SubagentCall` | "A specialist is checking this part." |
| `gate` | "A required check has not passed yet." |
| `transition` | "The next step is to ..." |
| `seam` or `boundary` | "The work is waiting at a handoff point." |
| `frontier` | "There are a few possible next actions." |
| `runtime` | "the program that is running the task" |
| `render unchanged` | "The earlier diagram is still valid; no new diagram was needed." |

### Example: output folder already has a record

Internal note: `step-0008-compiled` already exists; the render is unchanged.

User-facing update: "The task itself is fine. The output folder already has the earlier record, so this run did not overwrite it. The earlier diagram and report are still valid. I will use a new output folder and continue the same saved run."

### Example: the review found unresolved problems

Internal note: the review returned four findings, but the clean-review check was claimed before classification and repair.

User-facing update: "The review found four problems. The process stopped too early because it treated the review as finished instead of checking whether the problems were fixed. I will sort and fix the four problems, then check again."

### Example: waiting for a decision

Internal note: the task is waiting at a user-input step.

User-facing update: "I need one decision from you before I can continue: [state the decision in one short sentence]."

Do not copy the internal note into the user-facing update. Keep exact commands, paths, IDs, and evidence fields in a separate `Technical details` section when they are needed for action or verification.

## Default Assumptions

Apply these defaults during Loom Agent Plan-Execution Orchestrator-based plan execution:

- Loom Agent Plan-Execution Orchestrator is the only official execution authority for this skill; official runs use direct `loom.exe agent run`/`loom.exe agent resume` on Windows or `loom agent run`/`loom agent resume` on Unix.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- Official execution uses the exact unified Loom runtime version bound by this skill and the single published `Techne.Loom.Runtime.<rid>` package selected by the host. No host probe, DLL closure, second runtime mode, or cross-mode fallback is supported.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- Apply the [L0-L6 acquisition policy](./runtime-and-dispatch.md#dependency-acquisition-fallback-policy) to generic network CLIs. PowerShell web commands, `curl`, `gh`, `wget`, BITS and equivalent downloaders are L6 and require explicit per-operation approval after L0-L5. A failed request must not automatically trigger a same-source transport switch; a nonblocked source may be retried only through explicit L6 approval after managed options are exhausted. A blocked or denied source is a stop across every transport.
- If package identity, version, hash, manifest, archive, or bundled guide validation fails, discard that artifact and continue same-version acquisition/retry under the Exact NuGet Acquisition Completion Rule; if no approved source remains, preserve the run and pause resumably. If extraction, apphost startup, or `--guide` execution fails after package verification, preserve failed evidence and stop. Never record success from stderr; keep only the successful JSON result and readable `guide_path` returned by the direct apphost.
- In repo-src-debug mode, build and use the current repository Loom Agent Plan-Execution Orchestrator output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state in the same external workflow file or explicit execution-output roots.
- Write valid workflow, template, schema, demo, runtime-copy, audit-backup, and compile-feedback JSON outputs as indented multi-line JSON. Keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Output targets may be outside the Git worktree or ignored by Git. Return normalized real paths, verify each output exists and is readable, and use a verified workspace-relative mirror for direct editor opening when `--workspace-root` is available; Git tracking is never a delivery condition.
- After every Agent-route apphost call or audit-producing step, including `loom.exe agent` and `loom agent` calls, follow [Mermaid artifact delivery](./mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, each using the same complete absolute verified runtime address normalized with `/`. Do not substitute a workspace-relative path, shorten the address, or alter the existing paired format. If an artifact is missing or unverified, follow delivery-failure/continuity guidance and do not invent a link. Follow with localized `##` headings for execution confidence and estimated overall progress in that order, each with one short reason. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress measures approximate completion of the whole request. Base both on verified evidence; no fixed formula or threshold is prescribed, and neither percentage replaces required completion checks. Use only current or latest verified paths; on `not_emitted`, say the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. Keep user-facing text plain and in the active interaction language.
- For `runtime_path_only`, keep verified absolute paths as technical evidence; use workspace-relative links only when `link_resolvable=true`.
Agent is an independent implementation module behind the unified CLI. Execute the extracted `loom` apphost through `loom agent`; dynamic MCP registration, a runtime descriptor, and an MCP preflight are not required for guide, compile, run, or resume.

- For every full-delivery execution of this skill itself, direct apphost compile is validation only; it is not an end state.
- After guide handoff and compile validation, use one fresh external runtime workflow copy. Run the same apphost against that copy, then use that apphost and persisted state for every resume until terminal completion.
- If the runtime returns a blocked state, preserve its evidence and resume the same workflow copy. Continue until terminal completion or a documented unrecoverable failure; a blocked payload alone is never completion.
- Never claim governed completion from local orchestration, direct scripts, repository-debug output, guide success, prompt planning, compile success, or an unresumed block.
- If the published apphost cannot start, preserve failed package/startup evidence and stop. Do not substitute a local or helper execution path.

## Autonomous End-to-End Execution And Bounded Recovery

When the caller requests an end-to-end outcome and has not limited the task to planning or analysis, proceed through the required plan, authorized actions, AO continuation, and verifiable business deliverables. Derive routine facts from repository contracts, exact package locks, fresh guide evidence, and the current workflow instead of asking the user.

- Ask only for genuinely missing user-owned business input or required human approval; combine independent decisions into one structured question where possible. Respect scope and approval boundaries, never fabricate a decision, and continue safe independent work.
- Before adopting a generated prompt, script, or workflow candidate, verify intended targets, portable paths, exact branch/channel version references, requested scope, stale active references, and unexpected deletions. Candidate generation or compilation does not write source or prove execution completion.
- When a failure or blocked result occurs, diagnose its cause before repair. Do not repeat an unchanged operation. Make one bounded, evidence-backed repair pass and rerun only checks affected by changed inputs, while retaining required compile, run/resume, and repository validation.
- Report candidate generation, compile success, source update, and AO completion as distinct states. Mark execution complete only when AO reaches its terminal completed state and requested business deliverables are verifiable; otherwise preserve completed work and report the concrete blocker.

## Required Outputs

- bound runtime version confirmation with derived released/beta evidence and matching canonical links
- runtime source selection and version-derived channel resolution metadata
- package-channel runtime evidence: exact package ID/version/RID, source, verified SHA-512, nuspec/manifest/archive checks, extraction result, apphost path, and fresh guide result
- package-channel runtime acquisition facts when Windows PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast evidence when extraction or guide generation fails
- workflow/session/event paths and audit artifact links
- named-agent dispatch record for every subagent call: exact frontmatter name, `.agent.md` link, route (`direct` or `driver-fallback`), delivery mode (`inline` or `path-only`), fallback trigger evidence, and the driver's name echo
- `workflow.compile-feedback.json` and the shared `workflow.compile-feedback.v1` result, including parse/validation status, counts, phase blockers, candidate path, optional generated candidate-hash metadata, and AO runtime identity/version. The candidate hash is not a consumer freshness gate.
- Verified real paths for every output artifact, including workflow, feedback, audit, Mermaid, and HTML files outside the Git worktree or under ignored workspace mirrors.
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- required think-out-loud fields for runtime and audit updates
- After every Agent-route apphost call or audit-producing step, including `loom.exe agent` and `loom agent` calls, follow [Mermaid artifact delivery](./mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, each using the same complete absolute verified runtime address normalized with `/`. Do not substitute a workspace-relative path, shorten the address, or alter the existing paired format. If an artifact is missing or unverified, follow delivery-failure/continuity guidance and do not invent a link. Follow with localized `##` headings for execution confidence and estimated overall progress in that order, each with one short reason. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress measures approximate completion of the whole request. Base both on verified evidence; no fixed formula or threshold is prescribed, and neither percentage replaces required completion checks. Use only current or latest verified paths; on `not_emitted`, say the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. Keep user-facing text plain and in the active interaction language.
- business deliverable verification summary when business-first mode applies
- For `runtime_path_only`, keep verified absolute paths as technical evidence; use workspace-relative links only when `link_resolvable=true`.

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

- only direct `loom.exe agent run`/`loom.exe agent resume` on Windows or `loom agent run`/`loom agent resume` on Unix counts as an official skill run
- direct apphost `compile`, `--guide`, `prompt-plan`, and `prompt-replan` are preparation or validation surfaces only
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
