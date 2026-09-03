# Execution Contract

Read this file before runtime acquisition, workflow authoring, compile, run, resume, or version migration. It contains the detailed payload and execution rules intentionally omitted from the compact skill entry.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.
## Workflow Identity

Every root `templateKind: so-governed-target-skill` workflow declares `taskType`, `workflowKind`, `caseId`, and `runId`. Use `skill_enhancement` with `so_self_bootstrap` for self-bootstrap or `target_skill_enhancement` for an outer enhancement run. Use a target-specific business task with `target_skill_business` for target business work. `caseId` remains the business-case link; a checked-in template may mark `runId` with `template:` and the first fresh materialization or `ReadyToStart` run replaces it with one generated `run-<guid>`. Compile, run, resume, audit, and completion evidence for that external copy must preserve the same runId.

## Workflow Contract

### Inputs

- target skill root path that directly contains `SKILL.md` and `assets/so-workflow/`
- deterministic skill goal or upgrade request
- requested target-skill changes
- runtime version authority: the checked-in `assets/so-workflow/so-package-lock.json` plus the current CI/CD-managed skill package version block; derive channel from the bound version shape when needed instead of asking the user
- Guide input: execute the selected runtime launch descriptor with the `--guide` operation; it is English-only and returns JSON with `version`, `docs_root`, and `guide_path`. The descriptor, not workflow prose, chooses the host and launch file
- optional JSON context file
- optional audit output root

### Self-Bootstrap Assets

- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`
- `assets/so-workflow/restore-so-runtime.ps1`

### Defaults

- The planning artifact is runtime-owned: write plan/skill-plan.md under the current exec-<timestamp>-loom-skill-enhancement-result/ output root and pass its path/hash through workflow context. Do not require or publish a checked-in assets/so-workflow/skill-plan.md.
- Keep stable Loom Skill Orchestrator-owned template, lock, reference, and agent materials under `assets/so-workflow/`; keep mutable plans and run checklists under the execution output root.
- For every Loom-governanced target skill, official workflow operations and package downloads must use the published Loom Skill Orchestrator package artifacts bound to the current CI/CD-managed skill version block and checked-in package lock, not repository source builds, ad hoc local project outputs, or hand-assembled runtime folders, unless the user explicitly approves a last-resort blocked-state workaround.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- In Windows PowerShell 5.1, add `-UseBasicParsing` to package-channel HTTP probes that use `Invoke-WebRequest` or `Invoke-RestMethod` so runtime acquisition does not stall on legacy browser-engine prompts.
- Treat the checked-in workflow template as immutable; every new official SO run must start from a freshly copied external runtime workflow file derived from the template or current checked-in source workflow, and any later resume in that same execution chain must continue against that same persisted runtime copy rather than mutating the checked-in file in place.
- Keep compile and audit artifacts outside the skill folder unless the user explicitly chooses otherwise.
- Write valid workflow, template, schema, demo, runtime-copy, audit-backup, analysis, dataflow, and compile-feedback JSON files as indented multi-line JSON. Keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Output targets may be outside the Git worktree or ignored by Git. Return the normalized real path for every output, verify that it exists and is readable, and use a verified workspace-relative mirror for direct editor opening when a workspace root is available; Git tracking is never a delivery condition.
- In exclusive Loom Skill Orchestrator governance mode, only `dotnet so.dll run` and `dotnet so.dll resume` count as official runs.
- Official SO runtime uses the exact version supplied by this skill and delegates channel, platform/RID, package identity, executable, cache location, and launch path to the platform-aware resolver. Self-contained is the resolver default; `.NET CLI mode` and repository-source debug mode are opt-in only. Every operation consumes the resolver-owned launch descriptor; workflow text never selects a DLL or EXE.
- Normal enhancement governance for this skill and any Loom-governanced target skill must use the selected runtime's resolver-owned launch descriptor. Generate MCP configuration and try local MCP first; use descriptor-driven `inspect-workflow-fragment` CLI backup only for an allowed pre-dispatch reason. Then use the same descriptor for `--guide`, compile, run, and resume. Do not treat direct workflow JSON edits as a routine control path.
- Direct edits to the running external workflow `.json` copy are allowed only when the current `dotnet so.dll` path is fully blocked, the user explicitly approves a minimal workaround, the edit is the smallest change needed to unblock the next SO command, and the very next step returns to `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume`.
- When unattended-mode execution is explicitly declared in-session, a minimal autonomous workaround may be used only after a structured trade-off evaluation pass confirms that expected benefit clearly exceeds risk and that the change is reversible in one rollback step. Always emit a decision-evidence report and then return immediately to the normal `dotnet so.dll` governed path.
- If runtime extraction, startup-contract checks, descriptor-driven MCP registration, descriptor-driven CLI backup, or guide execution fail, stop immediately and keep runtime, `mcp_registration_attempt_evidence`, `mcp_startup_evidence`, and guide-refresh evidence in a failed state. Do not write success proof or treat failed command stderr as a guide; record only successful transport evidence and the successful JSON result with its readable `guide_path`.
- After every SO CLI call or audit-producing step, including `dotnet so.dll` and self-contained `so.exe` (or `so`) calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file`, verify existence and readability, and never guess an audit path. If `--workspace-root` was supplied, use only the hash-verified `workspace_relative_mermaid_file` and `workspace_relative_html_file` values for clickable workspace links. If the card tool is available, pass `card_input_file` directly to it without asking another agent to return the file contents; a card is not proof of artifact delivery. Otherwise show the verified Mermaid link first and the HTML link second. When no fresh render exists, reuse only a previously verified link and state that the render is unchanged. On `delivery_failed`, report the failure and do not emit a guessed link.
- Do not infer unattended mode from prior turns. For each critical decision boundary, re-confirm current attended versus unattended status instead of reusing stale status assumptions.

### Exact-Version Runtime Cache

- Treat `assets/so-workflow/so-package-lock.json` as the only version authority for this skill. Read its `resolved_version` and derive the channel from that locked value; do not ask for or discover a newer version during the restore.
- Before any network request, inspect the local NuGet cache for all three locked packages: `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`.
- A cache hit is valid only when all three `.nupkg` files are present and their package ids, exact versions, and nuspec identities match the lock. A partial, corrupt, or mismatched cache is a miss for the bundle.
- Use `assets/so-workflow/restore-so-runtime.ps1` for the Windows-compatible check. A valid hit returns `cache_hit: true` and `downloaded_packages: []`; a miss downloads only the exact locked version through direct NuGet URLs. Latest package resolution and `*.latest.nupkg` aliases are forbidden on this path.
- Retain `cache_hit`, `downloaded_packages`, `cache_validation`, `resolved_runtime_version`, and `runtime_bundle_packages` as runtime evidence before the startup-contract and guide gates.

### Verified Audit-Step Reuse

- `dotnet so.dll copy-audit-step` is a supporting artifact command, not an official run or resume. It copies only a previously verified audit step to a new output position and writes `audit-reuse.json` with source paths, source hashes, verifier, reason, `artifact_origin: verified-copy`, and `official_execution_evidence: false`.
- The source must contain `workflow.mermaid.md`, `workflow.html`, and `workflow.json`; existing `workflow.analysis.json`, `workflow.dataflow.json`, and `summary.json` are copied when present. Source and destination files are hash-checked, and any non-empty destination step is rejected without overwrite.
- For `run` / `resume` reuse, compare the stable workflow graph/configuration projection and reject structural drift. Compare source Mermaid/HTML with the current render: copy exact matches and regenerate changed renders from the current instance. Always write the current runtime instance's `workflow.json` and fresh analysis/dataflow files when available; record copied and replaced file names in `audit-reuse.json`.
- Every actual `run` and `resume`, including state changes, external results, gate evaluation, and event logging, remains official and must not be skipped because an audit step was copied.
### Boundary Check And Approval Gate (Compulsory)

This gate applies to every Loom-governanced target skill that this skill enhances. Both the SO skill's own execution and its enhanced skills are compelled onto the Loom Skill Orchestrator-governanced route: no next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step.

- A **boundary check** is the machine-readable validation of every transition before advancing. It must confirm `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state it must satisfy gate predicates (`passExpression` / `succeedExpression`) over runtime evidence, plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) an explicit approval/instruction from the user at `AskUser` seams for declared user-owned fields or decisions, or (b) a structured non-human continuation payload whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, an unapproved draft copy, local orchestration, direct workflow JSON edits, or an MCP startup without a real fragment call — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Every governed target-skill enhancement slice must apply this gate at every transition on the same external runtime copy until final `Done`.

### Non-Negotiable Official Execution Gate

- For every full-delivery enhancement or re-enhancement of a Loom-governanced target skill, `dotnet so.dll compile` is never an end state. It is only a validation checkpoint and one boundary-check precondition.
- After governance-entry transport proof and guide handoff, create or reuse one fresh external runtime workflow instance copy and record its immutable instance identity, workflow-file path, and persisted runtime-state/session path. Run `dotnet so.dll compile` against that exact external copy, pass the compile-boundary check, then immediately dispatch the public `dotnet so.dll run` against the same copy. Every later `dotnet so.dll resume` must reference the same persisted instance and state; never switch to a new workflow copy between compile, run, or a block. Do not stop at a preflight explanation, local geometry/tool validation, a draft workflow, compile output, or a blocked-state description when the user has required execution.
- If `run` returns a runtime-owned block, pass that boundary check and continue with `dotnet so.dll resume` against that exact workflow copy and persisted state. If the runtime reports a recoverable failure, preserve its evidence and resume from the previous state on the same persisted copy. Repeat until final `Done`; a blocked payload alone is never a terminal outcome and never approval to skip the next gate. Stop only when the failed instance has no recoverable previous state or the official runtime cannot start, and preserve that failure evidence.
- Never claim Loom-governanced completion from local orchestration, direct scripts, compile success, a guide result, a materialized workflow copy, or an unresumed block. The completion report must state the official command chain, final runtime status/node, the boundary-check/approval trail at each transition, and the event-log and audit evidence paths.
- When the official runtime cannot be started, the result is failed preflight, not governed completion. Preserve the failure evidence and do not substitute a local workflow execution.
### Runtime-Version Semantic Drift Gate (Compulsory)

Treat every bound SO runtime version change as both a schema-contract change and an execution-semantics change. A clean `compile` result proves only that the candidate is structurally accepted; it never proves that emitters, projections, context writes, gates, or terminal outputs behave as the previous runtime did.

- Before batch enhancement or target-skill edits, create a minimal three-node fixture (`seed -> behavior under test -> terminal gate`) and run two variants on the exact selected runtime: the inherited pattern and the proposed replacement. Compile and run both, inspect the terminal context and event log, and record expected versus observed values. Continue only when the replacement reaches final `Done` with the required non-empty output.
- The `0.3.282` behavior probe established that literal `updates` on `noop` and `ToolCall` do not create context values. Use `StateUpdate` or `MemoryWrite` for literal context writes. Treat `$result` as untrusted during upgrades; remove it or replace it with an explicit `outputPath`/`outputBindings` projection unless a same-version probe proves the exact result shape and target value.
- When several target skills share an obsolete pattern, prepare reusable, idempotent migration scripts before the first target run. The standard migration set covers `noop` to `stateUpdate`, missing output-family bindings, and unsupported `$result` projections. Run a dry scan across the full declared batch, preserve the candidate-file manifest and hashes, apply the scripts consistently, and then validate each changed workflow. Do not wait for each target to fail independently.
- Persist every assumption, probe fixture, command/result summary, migration manifest, decision and superseded decision under `<execution-output-root>/evidence/`. Keep `events.jsonl` and audit paths in the evidence index. Conversation text is not recoverable execution evidence.
- Final validation must return non-empty `runtime_semantic_probe_evidence`, `batch_migration_evidence`, and `decision_evidence_manifest`. The probe evidence identifies the exact runtime and both variants; batch evidence records scanned, changed, unchanged, and failed targets plus script hashes; the decision manifest records timestamps, inputs, conclusions, superseded conclusions, and artifact links.

## Runtime Mode Separation

Resolve the runtime mode before any package-cache lookup or network request. The two package paths are independent and must not be combined.

- `self-contained` mode is the default package-channel path. It validates and acquires only one exact-RID package for the selected product and platform: `Techne.Loom.AgentOrchestrator.Runtime.<rid>` for AO or `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for SO. It launches the validated `ao.exe` or `so.exe` directly. It must not download, validate, extract, or assemble the `.NET CLI mode` .NET runtime bundle.
- `.NET CLI mode` is explicit. Only this mode validates and acquires the same exact-version .NET runtime bundle (a NuGet restore set that includes the embedded Roslyn compiler assemblies used by the C# expression evaluator), checks the `.dll`, `.deps.json`, `.runtimeconfig.json`, Roslyn, and dependency closure, then launches through the shared .NET host.
- Once a mode is selected, a failure stays in that mode and fails closed. Do not fall back from `.NET CLI mode` to self-contained or from self-contained to `.NET CLI mode` after startup or package acquisition begins.
- Runtime evidence must identify `runtime_mode`, exact version, package ids, RID, cache validation, launch descriptor, and failure category. Never report a self-contained RID package as a .NET runtime bundle.

## Runtime Flow

1. Classify governance state and lock the goal to target-skill delivery.
2. Confirm the skill-bound package version and derived channel, prove the corresponding published Loom Skill Orchestrator runtime can run, and preserve its resolver-owned launch descriptor.
3. Generate MCP configuration and try MCP registration, handshake, and bounded fragment inspection with that descriptor. If MCP cannot be provided before successful dispatch, use the same descriptor for the bounded CLI fragment backup with one allowed reason. Persist `mcp_registration_attempt_evidence` and `mcp_startup_evidence`.
4. Use the same descriptor for the selected runtime's `--guide` operation, parse its JSON result, and read the returned `guide_path` and `docs_root`; only then enter plan mode and write the per-run plan to `<execution-output-root>/plan/skill-plan.md`.
5. Author or refresh the workflow template and package lock.
6. Apply feedback and materialize one fresh external runtime workflow copy outside the skill folder, recording its immutable instance identity, workflow-file path, and persisted runtime-state/session path.
7. Run the selected descriptor's compile operation against that exact external workflow copy, review the analysis report, pass the compile-boundary check, and update the target `SKILL.md` with the correct execution-status wording for the current slice.
8. Execute the selected descriptor's public run operation against that same external copy after passing its boundary-check/approval gate; then continue with its resume operation whenever the route blocks or requires the next instruction, reusing the same persisted instance and state and weaving back through every business-intake or `AskUser` seam until final `Done`.
9. Keep runtime workflow copies, event logs, and audit artifacts outside the skill folder.

## Exclusive Loom Governance Completion

- The target skill states that it has switched into Loom-governanced execution under Loom Skill Orchestrator.
- The target skill states in its own `SKILL.md` that ordinary workflow changes stay on the Loom-governanced CLI path and that direct workflow JSON edits are blocked-state-only emergency workarounds.
- The target skill states in its own `SKILL.md` that it is forced onto the Loom Skill Orchestrator-governanced route: every transition must pass a boundary check on the exact external runtime copy, then receive explicit approval or structured continuation instruction before advancing; no step may proceed on inferred intent, compile success alone, prose, or direct JSON edits.
- The target skill states in its own `SKILL.md` that `dotnet so.dll compile` is validation evidence only and that full-delivery governed completion requires an official public `dotnet so.dll run` / `dotnet so.dll resume` chain that reaches final `Done` on the runtime workflow copy.
- Direct CLI remains a primitive path only. The local stdio MCP is the mandatory first external interface for governed SO verification of this skill and every Loom-governanced target skill after runtime preflight; it must be started and used for a bounded fragment check, including during self-bootstrap. MCP does not replace official SO `run`/`resume`, and Web or remote transport is not supported.
- Official run evidence comes only from Loom Skill Orchestrator workflow state, event log, and audit artifacts. The runtime-owned completion manifest may summarize that evidence for final handoff, but it does not replace or self-certify the underlying runtime evidence families.
