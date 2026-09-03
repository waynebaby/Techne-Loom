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

## Workflow Designer Reference Pack And Evidence

Before dispatching the local SO workflow designer, create one bounded reference manifest. It must point to the fresh guide JSON and the actual returned guide file, the same exact-runtime `workflow.schema.json`, `workflow.demo.json`, and successful demo compile audit, the target `SKILL.md`, applicable `AGENTS.md`, current requirements, current workflow source, current package lock, and the latest compile feedback when this is a revision.

Each entry records a normalized path, SHA-256, exact runtime version, `authorityRole`, read status, and validation result. Use these roles: `authority` for exact-runtime guide/schema/demo/demo-audit and version-matched runtime contract docs; `current_contract` for the current target contract, requirements, applicable rules, and workflow source; `diagnostic_evidence` for compile feedback and prior probe reports; `previous_runnable_reference` for an older runnable workflow only; and `supplemental` for generated C# shape files, small fixtures, and source excerpts. The schema, demo, and demo audit must share one generation-set identity.

The exact-runtime guide/schema/demo/demo-audit set is authoritative. Current requirements and target contracts define business scope. An old runnable workflow is a comparison reference, never a schema authority or copy template. If it is supplied, record `previousRunnableReferenceDisposition` with source version, source hash, copy time, reusable shapes, differences from the current schema and requirements, and rejected or deprecated items.

The designer writes three runtime-owned records under `<execution-output-root>/workflow-design/`: `reference-manifest.json` (`workflow-designer.reference-manifest.v1`), `static-contract-review.json` (`workflow-designer.static-contract-review.v1`), and `semantic-probe-report.json` (`workflow-designer.semantic-probe-report.v1`). Return descriptors containing normalized path, SHA-256, schemaVersion, verdict, and exact runtime version. Do not write these records into the skill bundle or treat them as a second workflow state.

## Layered Design Evidence Protocol

Use the same ordered validation layers for every workflow-design dispatch: `runtime`, `JSON`, `graph`, `enum`, `expression`, `projection`, `dataflow`, `gate`, `ownership`, and `semantic`. Stop at the first failed or required-unknown layer; after repair, rerun all earlier layers before continuing. Compile is a validation checkpoint after the static layers, not proof of semantic readiness.

Return three runtime-owned JSON records under `<execution-output-root>/workflow-design/`:

- `reference-manifest.json` with schemaVersion `workflow-designer.reference-manifest.v1` and one entry per bounded input. Every entry records normalized path, SHA-256, exact runtime version, `authorityRole`, read status, and validation result. The exact-runtime guide/schema/demo/demo-audit set shares one generation-set identity.
- `static-contract-review.json` with schemaVersion `workflow-designer.static-contract-review.v1`. It records each layer result, schema coverage, expression audit, projection matrix, gate-producer-route matrix, ownership review, plain-language review, and gate failure-guidance review.
- `semantic-probe-report.json` with schemaVersion `workflow-designer.semantic-probe-report.v1`. It records stable `probeId` values, same-runtime fixture and command evidence, expected/observed paths and types, artifact and source/copy/case/run identity evidence, and the verdict.

A required probe is any semantic behavior used by the candidate that involves an external or canonical projection, an emitter, a gate-consumed family, or source/copy/case/run identity. A required `failed` or `unknown` probe blocks readiness. Optional behavior not used by the candidate may remain `unknown`, but it cannot support a readiness claim. A previous workflow is `previous_runnable_reference` only and must have a source/version/hash/copy-time disposition with reusable shapes, current differences, and rejected items.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.


## Caller File Preparation Contract

The calling agent must create the full input set on disk before one SO CLI call and pass only paths. Prepare every required script, JSON, workflow, reference, patch, context, and result input in one step. The CLI preflights all required files before reading or writing. Inline script, JSON, and replacement content is not a supported input form.
## Workflow Identity

For `templateKind: so-governed-target-skill`, require root `taskType`, `workflowKind`, `caseId`, and `runId`. Valid enhancement pairs are `skill_enhancement` with `so_self_bootstrap` or `target_skill_enhancement`; target business workflows use a target-specific task type with `target_skill_business`. A `template:` run marker is replaced with a generated `run-<guid>` on fresh materialization or the first new run, and the resulting runId remains stable through compile, run, resume, audit, and completion evidence.
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
- Every enhancement pass must first prove that the selected published Loom Skill Orchestrator runtime is runnable and preserve its resolver-owned launch descriptor. Before guide capture or downstream work, generate the requested MCP configuration files through that descriptor and try MCP registration, handshake, and bounded fragment inspection. If MCP cannot be provided before successful command dispatch, use the same descriptor for the bounded CLI fragment backup with one allowed fallback reason. Persist mcp_registration_attempt_evidence and mcp_startup_evidence; only then execute the selected descriptor's fresh guide operation. This applies equally to ordinary target-skill enhancement and /loom-skill-enhancement self-bootstrap.
- When the target project does not already have its own dependencies installed, install only the minimum dependency set required for the requested target-skill changes and current guide-aligned validation work.

## Governance-Entry Transport

For every Loom Skill Orchestrator-governanced target-skill verification, including `/loom-skill-enhancement` self-bootstrap, the exact published runtime must first produce a resolver-owned launch descriptor for the same external workflow copy.

1. Generate the requested VS Code `mcp.json` and Claude `.mcp.json` files through the selected runtime and descriptor. The resolver chooses whether the configuration starts a self-contained executable or a framework-dependent DLL; workflow text must not choose either one.
2. Try to register the generated configuration, complete `initialize` and `notifications/initialized`, and call `so_inspect_workflow_fragment` with bounded limits.
3. On success, persist `mcp_registration_attempt_evidence.status=ready`, set `governance_entry_transport=mcp_stdio`, and return `mcp_startup_evidence` with the descriptor and workflow identities.
4. If MCP cannot be provided before successful command dispatch, persist `mcp_registration_attempt_evidence.status=failed`, `mcp_attempted=true`, and exactly one allowed reason: `mcp_transport_unavailable`, `mcp_handshake_unsupported`, or `mcp_tool_unavailable`. Then use the same descriptor for `inspect-workflow-fragment` CLI backup and set `governance_entry_transport=cli`.
5. An MCP application or command failure after startup is not a backup trigger. Keep the workflow at the boundary and fail closed.
6. Only after one transport has produced `mcp_startup_evidence` may the pass capture `--guide` and continue to planning, authoring, validation, compile, run, or resume.

The unified evidence must include the transport, exact runtime version, descriptor/preparation identity, workflow path and hash, bounded limits, command or tool identity, result hash, configuration paths and hashes, and fallback reason. The MCP and CLI branches must converge on the same next state and every later external step must be dominated by their shared gate.

## Runtime Mode Separation

Resolve `self-contained` versus .NET CLI mode before checking the package cache. These are two independent paths.

- In `self-contained` mode, validate and acquire only the exact-RID `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package for the detected platform, then launch its direct `so.exe` or `so` entry point. Do not inspect, download, or assemble the .NET runtime bundle on this path.
- In explicit .NET CLI mode, validate and acquire the exact-version `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions` bundle, including `so.dll`, `so.deps.json`, `so.runtimeconfig.json`, Roslyn, and dependency closure.
- A failure in the selected mode fails closed. Never switch modes after selection, startup, or a command failure.
- Keep `runtime_mode`, `package_ids`, `rid`, and `launch_descriptor` in runtime evidence so the two paths cannot be mistaken for one another.

## Runtime Acquisition





- For `/loom-skill-enhancement` itself and any Loom-governanced target skill, official workflow operations and package downloads must use the published SO package artifacts restored from the current CI/CD-managed skill version block, checked-in lock, and derived channel. Do not treat repository source builds, local debug outputs, or hand-assembled runtime folders as the normal workflow-operation path.


- On Windows PowerShell 5.1, do not use `Expand-Archive` directly on `.nupkg`. Treat the package as ZIP content and extract it through ZIP-aware APIs or an equivalent ZIP-based flow.
- If you probe package URLs through `Invoke-WebRequest` or `Invoke-RestMethod` on Windows PowerShell 5.1, add `-UseBasicParsing` to avoid legacy security prompts that stall automation.
- Every new official SO run must begin from a freshly copied runtime workflow file outside the skill folder. Resume in that same execution chain must continue against the same persisted runtime copy. Do not reuse the checked-in template itself as the mutable execution file.
- Before package-channel network access, inspect the local NuGet cache for the complete .NET runtime bundle at the exact version in `assets/so-workflow/so-package-lock.json`. Reuse only after package id, exact version, nuspec identity, and bundle completeness checks pass. If any member is missing or invalid, download only that exact version; never float to latest.
- Resolve the selected mode before package lookup. In self-contained mode, use only the exact-RID executable package; in .NET CLI mode, use only the exact .NET runtime bundle.
- The checked-in `assets/so-workflow/restore-so-runtime.ps1` helper emits cache-hit/download and validation evidence and uses ZIP-aware `.nupkg` inspection plus `Invoke-WebRequest -UseBasicParsing` on Windows PowerShell 5.1.

    ## Package Integrity Checks



    Validate the package before launch and fail closed on any mismatch:



    1. Read the exact runtime version from this skill's checked-in version block or package lock. Derive `released` or `beta` from that bound version; never float to `latest`.

    2. Download `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for the detected RID and its `.nupkg.sha512` sidecar. Decode the sidecar and compare it with a locally computed SHA-512 digest before extraction.

    3. Open the `.nupkg` as ZIP content with a ZIP API. Do not use `Expand-Archive` on Windows PowerShell 5.1. Reject path traversal, duplicate paths, oversized entries, and unexpected files.

    4. Validate the root nuspec id and exact version, the RID tag, and the fixed `tools/<rid>/runtime.json` manifest. The manifest must match the product, version, RID, `so.exe`, `docs_root: tools/<rid>/docs/en`, and `guide_path: guides/so-guide.md`.

    5. Require `tools/<rid>/so.exe` plus `tools/<rid>/docs/en/guides/so-guide.md` and the complete English guide set. The executable does not contain guide pages; all guide content is direct package content.

    6. Use the resolver-owned launch descriptor to generate MCP configuration and try local registration, the initialize handshake, and the bounded fragment call against the same external workflow copy. If MCP cannot be provided before successful dispatch, use the same descriptor for the bounded CLI fragment backup with one allowed reason; preserve both transport-attempt records.

    7. Run the unpacked `so.exe --guide` from the complete `tools/<rid>` directory. Parse and read the returned absolute `guide_path`, confirm it is the unpacked `docs/en/guides/so-guide.md`, and only then continue to `compile`, `run`, or `resume`.
    7. A failed checksum, nuspec, manifest, RID, entrypoint, dependency, extraction, or guide check is failed preflight evidence. Never turn stderr into guide evidence or cross from the selected runtime mode to another mode automatically.

## Extracted Package Guide Entry

This skill publishes no `so-guide*.md` file. The authoritative guide is part of the English docs bundle in the selected runtime package.

1. Read the exact `resolved_version` from the checked-in package lock and derive the channel from that version.
2. In the default self-contained mode, restore only `Techne.Loom.SkillOrchestrator.Runtime.<rid>` at that exact version. In explicit .NET CLI mode, restore the exact SO/Common/Abstractions bundle with `so.dll`, `so.deps.json`, `so.runtimeconfig.json`, Roslyn, and its dependency closure.
3. On Windows PowerShell 5.1, treat the `.nupkg` as ZIP content and extract it with a ZIP-aware API. Do not use `Expand-Archive` directly on the package.
4. After extraction, the self-contained layout must contain `<extracted-root>/tools/<rid>/so.exe` and `<extracted-root>/tools/<rid>/docs/en/guides/so-guide.md`. The adjacent `runtime.json` must declare `"guide_path": "guides/so-guide.md"`.
5. Run `.\so.exe --guide` from the extracted `tools/<rid>` directory, or run the exact `dotnet exec --depsfile .\so.deps.json --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide` binding in .NET CLI mode.
6. Parse the JSON result and read its absolute `guide_path`. Use that extracted guide and its adjacent flow, reference index, and chapter pages as the version-specific authority. Never substitute a guide file copied into this skill.

## Re-Enhancement Upgrade Gate

When the target skill already shows Loom Skill Orchestrator governance signals:

- do not ask the user to choose released versus beta during normal re-enhancement
- use the exact version already bound in the checked-in `so-package-lock.json` and current skill build metadata
- derive the package channel from that bound version shape only when a released-versus-beta distinction is needed operationally
- reacquire that exact published Loom Skill Orchestrator package bundle before any new enhancement edits or downstream steps
- prove the bound published Loom Skill Orchestrator runtime is runnable, execute the selected runtime launch descriptor with `--guide`, parse the JSON result, and read `guide_path` before editing
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
- The reusable MCP-preferred startup agent is `../assets/agents/loom-skill-enhancement-mcp-startup.agent.md`; it generates configuration from the resolver-owned descriptor, tries MCP first, and uses the same descriptor for the bounded CLI backup only when allowed.

For `structural_refactor` and `full_regeneration`, use the old checked-in template as a baseline input, not as a patch target. Combine it with the current requirements, concept documents, target-skill assets, gap-review evidence, and the fresh guide to generate a new candidate template. The same policy applies when `/loom-skill-enhancement` re-enhances itself; self-bootstrap does not bypass the strategy judgment or recursively start another enhancement run.

This self-bootstrap scope is repository and skill-reference policy. Keep it out of generic published `SKILL.md` and `assets/agents/*.agent.md` bodies; those files should describe reusable behavior and receive the target context as inputs.

## Workflow Template Governance Baseline

- Before editing target-skill deliverables, first prove the selected published Loom Skill Orchestrator runtime is runnable, start and use its local `mcp stdio` server for the bounded fragment check, capture a fresh guide result from that same runtime, and only then run a plan-first pass when the platform supports it.
- The plan-first pass must analyze inputs, outputs, state nodes, transition groups, guards, branches, loops, user seams, runtime seams, validation gates, and expected output evidence.
- The workflow template JSON is the authority. Mermaid, HTML, localized prose, and review plans are presentation surfaces and must be regenerated or kept aligned after template feedback.
- For `/loom-skill-enhancement` and any Loom-governanced target skill, ordinary workflow governance must remain on the selected runtime launch descriptor's `--guide`, compile, run, and resume operations. Do not treat checked-in workflow JSON as a freeform direct-edit surface.
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

Default package-channel launch uses the exact-RID published self-contained executable package: run `.\so.exe` on Windows or `./so` on Unix. The framework-dependent `dotnet exec ... so.dll` path below is only for explicit .NET CLI mode.

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

## Shared Context And Parallel Enhancement Batches

Build one bounded `shared_review_context` after governance-entry fragment proof (MCP preferred or descriptor-driven CLI backup) and fresh guide proof and before independent review. The producer must include real checked-in snapshots, a source manifest, guide/schema/runtime references, `context_hash`, and the same external workflow-copy identity. Independent external subagents consume that context by reference.

Model independent review or validation transitions in one `ConcurrencyStrategy.All` group with one shared target state. The SO runtime must persist every expected external wait and join only after all results return. Aggregate every finding before one coordinated repair. After repair, run a second complete parallel validation batch, aggregate it, and finish with one serial validation transition for JSON, graph/dataflow, compile, schema/demo, and ordered runtime checks. Partial or duplicate batches fail closed. This policy belongs to enhancement governance and does not add a generic runtime Review engine.

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
- Direct CLI remains a primitive/component path only. Local stdio MCP is the mandatory first external interface for governed SO verification of this skill and every Loom-governanced target skill after runtime preflight, including self-bootstrap; it must be started and used for a bounded fragment check, while Web and remote transport remain unsupported.

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

If a specific `dotnet so.dll` call did not emit a fresh Mermaid render, repeat the latest known `mermaid_file`, `html_file`, and `analysis_file` as direct clickable Markdown file links, say that the render is unchanged, and add a concise workflow-location summary so the user can still tell where the active workflow currently is in this session. Never expose only a bare Mermaid path. If the chat agent provides a Mermaid card-display tool, pass the existing Mermaid file path directly to it instead; do not read or return the file contents again solely to display the card.

`must_show_to_user_files` should contain the ordered file list that the user-facing update must cite or surface for that call. If the chat agent provides a Mermaid card-display tool, pass the existing Mermaid file path directly to it without reading or returning its contents again solely for display. Otherwise render the Mermaid path in the user-facing update as a direct clickable Markdown file link, using a workspace-relative link when the artifact is inside the workspace; a bare path alone is insufficient.

## Plain-Language Feedback For Every Language

Write every user-facing progress, blocked, error, and completion update in the user's requested language for a high-school reader with no workflow background. English is not automatically plain language. Use short sentences and everyday words; state what happened, whether the user's work or data is still safe, why it happened, and the next action, in that order. Translate internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon before exposing exact technical details. Keep commands, paths, IDs, and evidence fields in a separate technical-details section only when needed. When creating or updating a target skill, copy this rule into its `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints.

## Delivery Completion Gate

- Completion requires requested target-skill deliverables to exist and governance wording to be aligned.
- Runtime validation artifacts alone cannot serve as sole completion evidence.
- Failed stderr output from the selected runtime descriptor's `--guide` operation cannot be saved as the guide artifact for completion evidence.
- For target-skill templates with root `templateKind: so-governed-target-skill`, completion also requires the governed validation contract, route-aware business-output gates, and seam ownership declarations to be present and compile-clean.
- Completion evidence for enhanced skills should cite the final workflow template, compiled Mermaid, workflow analysis report, confirmation-loop result, node-to-file or node-to-artifact map, and the boundary-check/approval-gate trail covering every governed transition on the same external runtime copy.
- Terminal completion must also include target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence showing the requested checked-in deliverables were created or modified), not just their path existence.
- Completion evidence should also distinguish three categories explicitly when they differ: checked-in source deliverables, runtime-owned temporary artifacts, and runtime-owned completion manifests that reference checked-in source deliverables.
- Post-run workaround reporting should include decision trigger, alternatives considered, risk justification, rollback plan, and follow-up acknowledgement request. The default acknowledgement reminder is non-blocking unless the user explicitly requests blocking behavior.
