# Loom Skill Orchestrator Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-skill-enhancement/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-skill-enhancement` needs to create or revise workflow JSON:

- [../assets/agents/loom-skill-enhancement-workflow-designer.agent.md](../assets/agents/loom-skill-enhancement-workflow-designer.agent.md)

Pass relative links to the target `SKILL.md`, workflow template, package lock, guide file, package-index file, audit artifacts, and any blocked seam evidence so the subagent can run independently from repository-global docs.

That declared `.agent.md` file remains the only authoritative behavior contract for the subagent. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders. If the runtime supports direct exact-name resolution, invoke that exact subagent name while keeping the declared `.agent.md` file as the contract. If exact-name resolution is unavailable at runtime, resolve the same declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second before failing, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate role or repository-global substitute prompt.

The subagent must generate node-level granularity where each node owns one visible responsibility and every SO weave-out path has a detailed blocked-action hint, including file/path context when relevant.

If the enhancement flow introduces a target-skill local `.agent.md` file for a reusable weave-out, that file must also be linked by relative path from the target `SKILL.md` and from the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.

That target-skill local `.agent.md` file is also the authority source for the target-skill subagent. Resolve the target-skill repository/workspace copy first and the corresponding global installed-skill copy second before failing. Do not swap it for an ad hoc near-match role, repository-global prose, or a freeform summary during summary, review, or runtime invocation.

Current reusable local weave-out subagents owned by `/loom-skill-enhancement` are:

- [../assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md](../assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md)
- [../assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md](../assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md)
- [../assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md](../assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md)
- [../assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md](../assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md)
- [../assets/agents/loom-skill-enhancement-review-fix-loop.agent.md](../assets/agents/loom-skill-enhancement-review-fix-loop.agent.md)
- [../assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md](../assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md)
- [../assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md](../assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md)
- [../assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md](../assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md)
- [../assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md](../assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md)

When one of these subagents already matches the weave-out goal, prefer it over creating a new generic review node.

## External Result Projection And Gate Evidence

External `SubagentCall`, `AskUser`, and `WaitResume` transitions use one explicit dataflow contract:

- validate `requiredInputs` as payload-relative paths or already-present context inputs;
- extract `resumeOutputKey` relative to the resume payload;
- write the extracted value to `outputPath`;
- apply explicit `outputBindings` for every additional output family.

Governed templates must not rely on an implicit payload wrapper. Use `payload.result` with `resumeOutputKey: result` and a context `outputPath` for canonical projection. `satisfiesGateIds` and `publishesOutputFamilies` are declarations only; a required family must have a concrete producer through `outputPath` or an explicit binding. Gate contracts may declare `valueSemantics` (`present`, `nonEmptyString`, `nonEmptyArray`, `nonEmptyObject`, or `booleanTrue`) and `instanceBinding: current_workflow_instance`.

Every compile, run, and resume audit step may emit `workflow.dataflow.json` next to Mermaid, HTML, the workflow backup, and `workflow.analysis.json`. This report is the machine-readable source for transition payload paths, projections, produced context paths, published families, gate mappings, route names, and unresolved producer issues.

A failed workflow instance can recover to its previous state and resume when the request identifies the most recent failed transition belonging to that state. Missing failure history, previous-state, or transition-ownership evidence must fail closed. The runtime restores it to `Running`, retries from that state, and preserves the failed history, event log, and audit evidence. A succeeded workflow instance remains terminal for resume and requires a fresh external workflow copy.

## Enhancement Scope

- Enhancement business outcome is target-skill creation or modification.
- Runtime-only verification cannot be reported as final enhancement completion.
- Every enhancement pass must first prove that the selected published Loom Skill Orchestrator runtime is runnable, execute bare `dotnet so.dll --guide`, parse its JSON result, and read the returned `guide_path` and `docs_root` before editing, validating, compiling, running, resuming, or collecting downstream inputs for target-skill deliverables.
- When the target project does not already have its own dependencies installed, install only the minimum dependency set required for the requested target-skill changes and current guide-aligned validation work.

## Runtime Acquisition

- In package-channel mode, restore the Loom Skill Orchestrator runtime bundle together at one resolved version:
  - `Techne.Loom.SkillOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- For `/loom-skill-enhancement` itself and any Loom-governanced target skill, official workflow operations and package downloads must use the published SO package artifacts restored from the current CI/CD-managed skill version block, checked-in lock, and derived channel. Do not treat repository source builds, local debug outputs, or hand-assembled runtime folders as the normal workflow-operation path.
- Build one unified runtime directory and execute Loom Skill Orchestrator commands from that directory only.
- Do not execute from partial single-package extraction roots.
- On Windows PowerShell 5.1, do not use `Expand-Archive` directly on `.nupkg`. Treat the package as ZIP content and extract it through ZIP-aware APIs or an equivalent ZIP-based flow.
- If you probe package URLs through `Invoke-WebRequest` or `Invoke-RestMethod` on Windows PowerShell 5.1, add `-UseBasicParsing` to avoid legacy security prompts that stall automation.
- Every new official SO run must begin from a freshly copied runtime workflow file outside the skill folder. Resume in that same execution chain must continue against the same persisted runtime copy. Do not reuse the checked-in template itself as the mutable execution file.
- Before package-channel network access, inspect the local NuGet cache for the complete three-package bundle at the exact version in `assets/so-workflow/so-package-lock.json`. Reuse only after package id, exact version, nuspec identity, and bundle completeness checks pass. If any member is missing or invalid, download only that exact version; never float to latest.
- The checked-in `assets/so-workflow/restore-so-runtime.ps1` helper emits cache-hit/download and validation evidence and uses ZIP-aware `.nupkg` inspection plus `Invoke-WebRequest -UseBasicParsing` on Windows PowerShell 5.1.

## Re-Enhancement Upgrade Gate

When the target skill already shows Loom Skill Orchestrator governance signals:

- do not ask the user to choose released versus beta during normal re-enhancement
- use the exact version already bound in the checked-in `so-package-lock.json` and current skill build metadata
- derive the package channel from that bound version shape only when a released-versus-beta distinction is needed operationally
- reacquire that exact published Loom Skill Orchestrator package bundle before any new enhancement edits or downstream steps
- prove the bound published Loom Skill Orchestrator runtime is runnable, run bare `dotnet so.dll --guide` from that exact runtime, parse the JSON result, and read `guide_path` before editing
- strongly recommend a subagent review that compares the current target skill and Loom Skill Orchestrator workflow assets against that bound-version guide result before editing

## Verified Audit-Step Reuse

`dotnet so.dll copy-audit-step` is an explicit supporting operation for a verified unchanged audit step:

```powershell
dotnet so.dll copy-audit-step `
  --source-step <existing-step-directory> `
  --workflow-id <workflow-id> `
  --sequence <n> `
  --action <action> `
  --audit-output <external-audit-root> `
  --reason <verification-reason> `
  --verified-by <verifier-id>
```

The command requires `workflow.mermaid.md`, `workflow.html`, and `workflow.json`, copies optional analysis/dataflow/summary files when present, verifies source/destination SHA-256 values, rejects destination collisions, and writes `audit-reuse.json`. This provenance is audit presentation continuity only: it has `artifact_origin: verified-copy` and `official_execution_evidence: false`, and cannot replace official `run`/`resume`, event-log entries, gate evaluation, or guide evidence.


For `run` / `resume` reuse, SO compares a stable workflow graph/configuration projection and rejects structural drift. It also compares source Mermaid/HTML with the current render: exact matches are copied; changed renders are regenerated from the current instance. The step always writes the current runtime instance's `workflow.json`, and fresh analysis/dataflow files when available. The `audit-reuse.json` manifest records copied and replaced file names so an older runtime state cannot replace the current workflow backup.

## Re-Enhancement Template Strategy

Every re-enhancement pass must make the template-change strategy explicit after the current skill, package lock, and workflow-governance gap reviews:

- `local_patch` is limited to wording, links, descriptions, metadata, and other changes that do not alter workflow topology, guards, gates, seams, output families, route coverage, or the workflow instance contract.
- `structural_refactor` is used when a bounded node, branch, loop, gate, output family, or ownership boundary changes while the existing workflow goal and most validated structure remain reusable through an explicit mapping.
- `full_regeneration` is required when the old template conflicts with current requirements, concept documents, or the fresh guide; several structural areas change together; or the old shape would hide the requested behavior.

For `structural_refactor` and `full_regeneration`, use the old checked-in template as a baseline input, not as a patch target. Combine it with the current requirements, concept documents, target-skill assets, gap-review evidence, and the fresh guide to generate a new candidate template. The same policy applies when `/loom-skill-enhancement` re-enhances itself; self-bootstrap does not bypass the strategy judgment or recursively start another enhancement run.

This self-bootstrap scope is repository and skill-reference policy. Keep it out of generic published `SKILL.md` and `assets/agents/*.agent.md` bodies; those files should describe reusable behavior and receive the target context as inputs.

## Workflow Template Governance Baseline

- Before editing target-skill deliverables, first prove the selected published Loom Skill Orchestrator runtime is runnable and capture a fresh guide result from that runtime, then run a plan-first pass when the platform supports it.
- The plan-first pass must analyze inputs, outputs, state nodes, transition groups, guards, branches, loops, user seams, runtime seams, validation gates, and expected output evidence.
- The workflow template JSON is the authority. Mermaid, HTML, localized prose, and review plans are presentation surfaces and must be regenerated or kept aligned after template feedback.
- For `/loom-skill-enhancement` and any Loom-governanced target skill, ordinary workflow governance must remain on the `dotnet so.dll --guide`, `compile`, `run`, and `resume` path. Do not treat checked-in workflow JSON as a freeform direct-edit surface.
- For `/loom-skill-enhancement` and any Loom-governanced target skill, every new official SO run must recopy the execution workflow from checked-in source assets into an external runtime file before execution begins, while resume must continue against the same persisted runtime file produced by that run chain.
- Direct edits to the running external workflow `.json` copy are allowed only when the current SO path is fully blocked, the user explicitly approves a narrow workaround, the change is the smallest one that unblocks the next `dotnet so.dll` step, and the operator immediately returns to `dotnet so.dll compile`, `run`, or `resume`.
- When unattended mode is explicitly declared in-session, autonomous workaround execution is allowed only after a structured evaluation pass confirms that expected benefit clearly exceeds risk and the chosen change is reversible within one rollback step.
- Do not infer unattended mode from earlier rounds. Re-confirm current attended versus unattended status at each critical decision boundary.
- User feedback during planning must update the workflow template or its source planning inputs. Do not accept a Mermaid-only change as a real workflow change.
- `dotnet so.dll compile` emits Mermaid Markdown, HTML, workflow JSON backup, and `workflow.analysis.json` under the audit root. The analysis report is evidence for the plan review.
- Mermaid node backgrounds should use stable light color families derived from step kind semantics plus owned-input metadata: AI/model/subagent green, code/tool blue, user-owned optional branch choices yellow, required user input red, generic conditional branches amber/yellow, and gate/governance states white or very light gray.
- Enhancement outputs should include a node-to-file or node-to-artifact map from workflow node ids to the target files, generated artifacts, or audit evidence they govern.
- When the slice keeps checked-in source assets as the authoritative business deliverables, the workflow should separate those checked-in assets from runtime-owned completion artifacts. A runtime-owned completion manifest may reference checked-in source assets, but that does not by itself replace the checked-in deliverable.
- Workflow templates must model explicit governed steps, guards, seams, and reviewable outputs.
- Target-skill templates that use root `templateKind: so-governed-target-skill` should also declare `validation.gates`, `validation.routes`, `validation.declaredUserOwnedFields`, and `validation.reservedRuntimeOwnedFields`.
- Terminal governed routes must name the business-output gates that must be satisfied before `done`.
- Blocked governed routes must name the strongest-earned business-output gates that must be satisfied before a runtime-owned wait boundary.
- `AskUser` may request only user-owned inputs or decisions. Runtime-owned facts, runtime provenance, and system-generated artifact paths belong to runtime-owned seams such as `WaitResume`.
- Never author or keep any node whose purpose says or implies `run a multistep plan`.
- Split open-ended work into explicit deterministic steps instead of hiding it behind a generic planner node.
- Review workflow templates for any node whose instruction embeds a multistep plan or a broad prompt to an agent, then decompose that node into smaller governed nodes when possible.

## Governed Validation Enforcement

- `dotnet so.dll compile` and workflow-load paths reject target-skill templates with root `templateKind: so-governed-target-skill` when they omit the root validation contract.
- `dotnet so.dll compile` and workflow-load paths reject `AskUser` seams that request reserved runtime-owned fields such as `workflow_file`, `event_log_file`, audit artifact paths, or other system-generated provenance.
- `dotnet so.dll compile` and workflow-load paths reject terminal paths that can reach `done` without satisfying the route's declared business-output gates.
- `dotnet so.dll compile` and workflow-load paths reject blocked routes that pause without declaring and publishing the strongest-earned blocked business outputs.

## Startup Contract Preflight

Before Loom Skill Orchestrator command execution in package-channel mode, verify:

- `so.dll`
- `so.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.
- `so.deps.json` is mandatory; keep it with the runtime bundle and use it for explicit dependency binding.
- If extraction fails, `so.dll` is missing, `so.runtimeconfig.json` is missing, dependency closure is broken, or the co-located runtime bundle cannot actually start, stop immediately. Do not emit `runtime_preflight_result: passed`.

## Launch Mode

Default package-channel launch uses the exact-RID published self-contained executable package: run `.\so.exe` on Windows or `./so` on Unix. The framework-dependent `dotnet exec ... so.dll` path below is only for explicit legacy framework/library mode.

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --runtimeconfig <so.runtimeconfig.json> <so.dll> ...`
  - with the mandatory `so.deps.json` for deterministic binding, `dotnet exec --depsfile <so.deps.json> --runtimeconfig <so.runtimeconfig.json> <so.dll> ...`

## Boundary Check And Approval Gate (Compulsory)

This gate applies with equal force to `/loom-skill-enhancement` self-bootstrap runs **and** to every Loom-governanced target skill. Both the SO skill's own execution and its enhanced skills are forced onto the Loom Skill Orchestrator-governanced route: no next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step.

- A **boundary check** is the machine-readable validation of every transition before advancing. It must confirm `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state it must satisfy gate predicates (`passExpression` / `succeedExpression`) over runtime evidence, plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) an explicit approval/instruction from the user at `AskUser` seams for declared user-owned fields or decisions, or (b) a structured non-human continuation payload whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, compile success, an unapproved draft copy, local orchestration, or direct workflow JSON edits — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Both self-bootstrap runs and governed target-skill enhancement slices must apply this gate at every transition on the same external runtime copy until final `Done`.

## Governance and Official Run Surface

In exclusive Loom Skill Orchestrator governance mode:

- Loom Skill Orchestrator is the only official execution authority.
- Official skill runs are only:
  - `dotnet so.dll run`
  - `dotnet so.dll resume`
- Official workflow operations for `/loom-skill-enhancement` and any Loom-governanced target skill must be executed from published SO package artifacts for the bound version and derived channel unless a blocked-state emergency exception was explicitly approved.
- Gate predicates must bind declared required output fields explicitly — non-empty values, success/passed state, belonging to the current workflow instance — not a single aggregate flag such as `gate_outputs_present == true`.
- Official runnable route guards after review-fix must require both `review_fix_loop_evidence != null` AND `commit_report_ready.status == 'ready'`, with explicit blocked/needs-validation stop or wait paths when readiness is not proven.
- Terminal business-output gates before final `Done` must include a boundary-check/approval-gate trail covering every transition on the same external runtime copy and concrete target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence). Checked-in asset path existence alone cannot satisfy a business-output gate.
- Enhanced target `SKILL.md` files must say that ordinary workflow changes stay on the SO CLI path and that direct workflow JSON edits are blocked-state-only, user-approved emergency workarounds.
- Enhanced target `SKILL.md` files must also say that Windows PowerShell 5.1 package-channel restores use ZIP-based `.nupkg` extraction, that HTTP probes add `-UseBasicParsing` when those PowerShell web cmdlets are used, and that failed extraction or guide commands cannot be recorded as success proof.
- Direct CLI and MCP are primitive/component paths only.

## Think-Out-Loud Required Fields

Report runtime fields once runtime is prepared, after every `dotnet so.dll` CLI call, and on each progress update:

- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

Report audit fields after every `dotnet so.dll` CLI call and on each progress update:

- `mermaid_file`
- `html_file`
- `analysis_file` when present
- `must_show_to_user_files`
- `workflow_location_summary`

If a specific `dotnet so.dll` call did not emit a fresh Mermaid render, repeat the latest known `mermaid_file`, `html_file`, and `analysis_file` anyway and say that the render is unchanged, then add a concise workflow-location summary so the user can still tell where the active workflow currently is in this session.

`must_show_to_user_files` should contain the ordered file list that the user-facing update must cite or surface for that call. In this skill it normally contains the current Mermaid Markdown, HTML, and analysis artifact paths.

## Plain-Language Feedback For Every Language

Write every user-facing progress, blocked, error, and completion update in the user's requested language for a high-school reader with no workflow background. English is not automatically plain language. Use short sentences and everyday words; state what happened, whether the user's work or data is still safe, why it happened, and the next action, in that order. Translate internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon before exposing exact technical details. Keep commands, paths, IDs, and evidence fields in a separate technical-details section only when needed. When creating or updating a target skill, copy this rule into its `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints.

## Delivery Completion Gate

- Completion requires requested target-skill deliverables to exist and governance wording to be aligned.
- Runtime validation artifacts alone cannot serve as sole completion evidence.
- Failed stderr output from `dotnet so.dll --guide` or `dotnet exec ... so.dll --guide` cannot be saved as the guide artifact for completion evidence.
- For target-skill templates with root `templateKind: so-governed-target-skill`, completion also requires the governed validation contract, route-aware business-output gates, and seam ownership declarations to be present and compile-clean.
- Completion evidence for enhanced skills should cite the final workflow template, compiled Mermaid, workflow analysis report, confirmation-loop result, node-to-file or node-to-artifact map, and the boundary-check/approval-gate trail covering every governed transition on the same external runtime copy.
- Terminal completion must also include target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence showing the requested checked-in deliverables were created or modified), not just their path existence.
- Completion evidence should also distinguish three categories explicitly when they differ: checked-in source deliverables, runtime-owned temporary artifacts, and runtime-owned completion manifests that reference checked-in source deliverables.
- Post-run workaround reporting should include decision trigger, alternatives considered, risk justification, rollback plan, and follow-up acknowledgement request. The default acknowledgement reminder is non-blocking unless the user explicitly requests blocking behavior.
