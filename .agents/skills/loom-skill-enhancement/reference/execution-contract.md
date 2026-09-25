# Execution Contract

Read this file before runtime acquisition, workflow authoring, compile, run, resume, or version migration. It contains the detailed payload and execution rules intentionally omitted from the compact skill entry.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.

User-facing progress, blocked, error, and completion text must use plain words in the current interaction language. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the explanation; keep exact identifiers in technical details or evidence only.
## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.
## Workflow Identity

Every root `templateKind: so-governed-target-skill` workflow declares `taskType`, `workflowKind`, `caseId`, and `runId`. Use `skill_enhancement` with `so_self_bootstrap` for self-bootstrap or `target_skill_enhancement` for an outer enhancement run. Use a target-specific business task with `target_skill_business` for target business work. `caseId` remains the business-case link; a checked-in template may mark `runId` with `template:` and the first fresh materialization or `ReadyToStart` run replaces it with one generated `run-<guid>`. Compile, run, resume, audit, and completion evidence for that external copy must preserve the same runId.

## Workflow Contract

### Inputs

- root of the skill being enhanced path that directly contains `SKILL.md` and `assets/so-workflow/`
- deterministic skill goal or upgrade request
- requested changes to the skill being enhanced
- runtime version authority: the checked-in `assets/so-workflow/so-package-lock.json` plus the current CI/CD-managed skill package version block; derive channel from the bound version shape instead of asking the user
- Guide input: directly execute the extracted RID apphost (`so.exe` on Windows, `so` on Unix) with `--guide`; it returns JSON with `version`, `docs_root`, and `guide_path`
- optional JSON context file
- optional audit output root

### Self-Bootstrap Assets

- `assets/so-workflow/so-template.json`
- `assets/so-workflow/so-package-lock.json`
- `assets/agents/loom-skill-enhancement-mcp-startup.agent.md` (optional local MCP support for later operations; it is not part of package acquisition or guide capture)


### Defaults

- The planning artifact is runtime-owned: write `plan/skill-plan.md` under the current `exec-<timestamp>-loom-skill-enhancement-result/` output root and pass its path/hash through workflow context. Do not require or publish a checked-in `assets/so-workflow/skill-plan.md`.
- Keep stable Loom Skill Orchestrator-owned template, lock, reference, and agent materials under `assets/so-workflow/`; keep mutable plans and run checklists under the execution output root.
- Official workflow operations use only the exact published self-contained product+RID package bound by the skill version block and package lock. Keep repository builds as implementation/test evidence, not official skill execution.
- The host agent detects OS, architecture, and Linux libc and selects one supported RID. It uses an already verified exact package in the standard NuGet cache when available; otherwise it acquires the exact locked package using the configured exact-source policy. No installed `dotnet` host, resolver, DLL mode, fixed bootstrap script, or Loom-specific runtime cache is required.
- Treat `.nupkg` as ZIP content. Reject package identity/version/RID/hash/manifest mismatches, unsafe paths, and oversized archives before extraction. If the host lacks a required acquisition or verification capability, fail with a concrete diagnostic rather than lowering checks or asking which recovery mode to use.
- Extract the apphost and documentation to a per-run external runtime directory. Run the apphost directly: `so.exe` on Windows and `so` on Unix. A fresh readable `--guide` result is required before planning or workflow work. Continue compile/run/resume with that same apphost and exact external workflow copy.
- The host may use an already available shell or script engine to perform acquisition and extraction. Do not add a fixed checked-in runtime restore script or generate a resolver-owned launch descriptor.
- MCP is optional and is not a startup/guide gate. Use it only if a later step benefits from its tools; bind it to the current apphost identity. Do not require MCP registration or pre-guide fragment inspection.
- Treat the checked-in workflow template as immutable; every official run starts from a fresh external workflow copy and any resume continues against that same persisted copy.
- Keep compile and audit artifacts outside the skill folder unless the user explicitly chooses otherwise. Write JSON artifacts as indented multi-line JSON; keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Verify every output path exists and is readable; use verified workspace-relative links when available. Git tracking is never a delivery condition.
- In exclusive Loom Skill Orchestrator governance mode, direct apphost `so.exe run`/`resume` on Windows or `so run`/`resume` on Unix are the official workflow execution surfaces.
- Direct edits to a running external workflow copy remain blocked-state-only, user-approved emergency workarounds; immediately return to direct apphost `compile`, `run`, or `resume`.
- If package verification, extraction, apphost startup, or guide validation fails, stop and preserve failed evidence. Never turn stderr or a missing file into success proof.
- After every SO apphost audit-producing call, follow [Mermaid artifact delivery](mermaid-artifact-delivery.md): use actual artifact paths from the result, verify Mermaid/HTML/analysis/dataflow files, and do not guess missing paths.
- When unattended mode is explicitly declared in-session, use the existing reversible-workaround decision contract. Do not infer unattended mode from prior turns.

### Exact-Version Runtime Package Acquisition

- Treat `assets/so-workflow/so-package-lock.json` as the only version authority. Read `resolved_version` and derive the release channel from it; never ask the user to choose a channel or float to `latest`.
- Detect exactly one supported RID from the current host: OS, architecture, and Linux libc must all match. Stop with a concrete error when detection is unsupported or ambiguous.
- Before network access, inspect the standard NuGet global-packages cache for exactly `Techne.Loom.SkillOrchestrator.Runtime.<rid>` at the locked version. Reuse only a package whose hash, package ID, version, nuspec, manifest, and RID all validate.
- On a cache miss, fetch the exact NuGet flat-container package and compare its bytes to the exact registration `catalogEntry.packageHash`. The same-version GitHub Release fallback is allowed only when its `.sha512` sidecar validates. Never use a floating version or latest alias.
- Enforce archive and entry-size limits, duplicate/path-traversal checks, manifest identity, exact apphost path, and documentation presence before extraction. Extract into the external execution output root and set Unix execute permission where needed.
- Record exact package ID/version/RID, source, hash, manifest/archive validation, extraction path/result, and direct apphost path as runtime-owned evidence. Do not create a Loom cache/lock subsystem or serialize these facts into a launch descriptor.
- Run `so.exe --guide` on Windows or `so --guide` on Unix immediately after extraction. Validate the returned exact version, absolute docs/guide paths, containment, and readability before any downstream work.


### Verified Audit-Step Reuse

- The direct SO apphost `so.exe copy-audit-step` on Windows or `so copy-audit-step` on Unix is a supporting artifact command, not an official run or resume. It copies only a previously verified audit step and records `artifact_origin: verified-copy` with `official_execution_evidence: false`.
- The source must contain `workflow.mermaid.md`, `workflow.html`, and `workflow.json`; existing `workflow.analysis.json`, `workflow.dataflow.json`, and `summary.json` are copied when present. Source and destination files are hash-checked, and any non-empty destination step is rejected without overwrite.
- For `run` / `resume` reuse, compare the stable workflow graph/configuration projection and reject structural drift. Compare source Mermaid/HTML with the current render: copy exact matches and regenerate changed renders from the current instance. Always write the current runtime instance's `workflow.json` and fresh analysis/dataflow files when available; record copied and replaced file names in `audit-reuse.json`.
- Every actual `run` and `resume`, including state changes, external results, gate evaluation, and event logging, remains official and must not be skipped because an audit step was copied.
### Boundary Check And Approval Gate (Compulsory)

This gate applies to every skill being enhanced under Loom Skill Orchestrator governance that this skill enhances. Both the SO skill's own execution and the skills being enhanced are compelled onto the route under Loom Skill Orchestrator governance: no next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step.

- A **boundary check** is the machine-readable validation of every transition before advancing. It must confirm `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state it must satisfy gate predicates (`passExpression` / `succeedExpression`) over runtime evidence, plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) an explicit approval/instruction from the user at `AskUser` seams for declared user-owned fields or decisions, or (b) a structured non-human continuation payload whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, an unapproved draft copy, local orchestration, or direct workflow JSON edits; however, runtime bootstrap does not require an MCP startup or fragment call. No transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Every governed enhancement of the skill being enhanced slice must apply this gate at every transition on the same external runtime copy until final `Done`.

### Non-Negotiable Official Execution Gate

- For every full-delivery enhancement or re-enhancement under Loom Skill Orchestrator governance, direct apphost `so.exe compile`/`so compile` is a validation checkpoint, not an end state.
- After fresh package acquisition and guide validation, create or reuse one external runtime workflow copy and record its immutable instance identity, workflow-file path, and persisted runtime-state/session path. Compile that exact copy with the same apphost, pass the compile-boundary check, then immediately dispatch the public `so.exe run`/`so run` command against that copy. Every later `resume` reuses the same apphost, workflow copy, and persisted state.
- If `run` returns a runtime-owned block, pass its boundary check and continue with direct apphost `resume` against that same copy. Preserve recoverable failure history and evidence; continue until final `Done`. A blocked payload alone is never completion.
- Never claim governed completion from local orchestration, direct scripts, compile success, guide success, a materialized workflow copy, or an unresumed block. The completion report must state the direct apphost command chain, final runtime status/node, boundary-check/approval trail, event log, and audit evidence paths.
- When the official apphost cannot start, preserve failed preflight evidence and do not substitute repository DLL execution.

### Runtime-Version Semantic Drift Gate (Compulsory)

Treat every bound SO runtime version change as both a schema-contract change and an execution-semantics change. A clean `compile` result proves only that the candidate is structurally accepted; it never proves that emitters, projections, context writes, gates, or terminal outputs behave as the previous runtime did.

- Before batch enhancement or skill being enhanced edits, create a minimal three-node fixture (`seed -> behavior under test -> terminal gate`) and run two variants on the exact selected runtime: the inherited pattern and the proposed replacement. Compile and run both, inspect the terminal context and event log, and record expected versus observed values. Continue only when the replacement reaches final `Done` with the required non-empty output.
- The `0.3.282` behavior probe established that literal `updates` on `noop` and `ToolCall` do not create context values. Use `StateUpdate` or `MemoryWrite` for literal context writes. Treat `$result` as untrusted during upgrades; remove it or replace it with an explicit `outputPath`/`outputBindings` projection unless a same-version probe proves the exact result shape and target value.
- When several skills being enhanced share an obsolete pattern, prepare reusable, idempotent migration scripts before the first target run. The standard migration set covers `noop` to `stateUpdate`, missing output-family bindings, and unsupported `$result` projections. Run a dry scan across the full declared batch, preserve the candidate-file manifest and hashes, apply the scripts consistently, and then validate each changed workflow. Do not wait for each target to fail independently.
- Persist every assumption, probe fixture, command/result summary, migration manifest, decision and superseded decision under `<execution-output-root>/evidence/`. Keep `events.jsonl` and audit paths in the evidence index. Conversation text is not recoverable execution evidence.
- Final validation must return non-empty `runtime_semantic_probe_evidence`, `batch_migration_evidence`, and `decision_evidence_manifest`. The probe evidence identifies the exact runtime and both variants; batch evidence records scanned, changed, unchanged, and failed targets plus script hashes; the decision manifest records timestamps, inputs, conclusions, superseded conclusions, and artifact links.

## Runtime Mode Separation

There is one runtime mode: one exact-version self-contained package for the current product and detected RID. The host validates package identity/hash/manifest/archive safety, extracts the apphost, and executes it directly. There is no .NET host probe, framework-dependent DLL closure, `runtime resolve` command, or launch descriptor.

A fresh `so.exe --guide`/`so --guide` result is the first runtime operation after extraction. Validate its exact version and readable paths. After that gate, use the same apphost for schema/demo, compile, run, and resume. Any integrity, startup, or guide failure fails closed.

Runtime evidence records exact package ID/version/RID, hash, archive validation, extraction outcome, apphost path for the current run, guide JSON, and failure category. It does not record a runtime mode or launch descriptor.


## Runtime Flow

1. Classify governance state and lock the goal to skill-being-enhanced delivery.
2. Read the exact package version from the lock and derived channel; detect the current host RID.
3. Use the host's available shell or script engine to acquire, verify, and safely extract the one exact RID runtime package. No fixed restore script, installed `dotnet` host, resolver, or descriptor is required.
4. Directly run the extracted apphost (`so.exe` on Windows, `so` on Unix) with `--guide`. Parse its JSON and verify the version, absolute paths, containment, and guide readability; only then enter plan mode and write the per-run plan.
5. Author or refresh the workflow template using the exact package guide/schema/demo authority and the bounded reference manifest.
6. Materialize one fresh external `WorkflowInstance` copy outside the skill folder, recording its case/run identity and persisted runtime-state path.
7. Compile the exact copy using the same apphost and review its analysis, projection, dataflow, ownership, and gate evidence.
8. Run and resume the same workflow copy with that apphost until final `Done`, passing boundary checks and owner-specific approval/continuation at each required seam.
9. Keep workflow copies, event logs, and audit artifacts outside the skill folder. MCP may support later workflow steps but is never a runtime-startup prerequisite or a second execution authority.


## Exclusive Loom Governance Completion

- The skill being enhanced states that it has switched into execution under Loom Skill Orchestrator governance.
- The skill being enhanced states in its own `SKILL.md` that ordinary workflow changes stay on the Loom Skill Orchestrator governance CLI path and that direct workflow JSON edits are blocked-state-only emergency workarounds.
- The skill being enhanced states in its own `SKILL.md` that it is forced onto the route under Loom Skill Orchestrator governance: every transition must pass a boundary check on the exact external runtime copy, then receive explicit approval or structured continuation instruction before advancing; no step may proceed on inferred intent, compile success alone, prose, or direct JSON edits.
- The skill being enhanced states in its own `SKILL.md` that direct apphost `so.exe compile`/`so compile` is validation evidence only and full-delivery governed completion requires the public direct apphost `run`/`resume` chain to reach final `Done` on the same runtime workflow copy.
- MCP is optional and does not block package acquisition, guide retrieval, or official execution. If a later workflow step uses MCP, bind it to the running self-contained apphost identity; MCP does not replace official direct apphost `run`/`resume`, and Web or remote transports are unsupported.
- Official run evidence comes only from Loom Skill Orchestrator workflow state, event log, and audit artifacts. The runtime-owned completion manifest may summarize that evidence for final handoff, but it does not replace or self-certify the underlying runtime evidence families.
