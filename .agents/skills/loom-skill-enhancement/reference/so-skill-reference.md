# Loom Skill Orchestrator Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-skill-enhancement/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-skill-enhancement` needs to create or revise workflow JSON:

- [../assets/agents/loom-skill-enhancement-workflow-designer.agent.md](../assets/agents/loom-skill-enhancement-workflow-designer.agent.md)

Pass relative links to the target `SKILL.md`, workflow template, package lock, guide file, package-index file, audit artifacts, and any blocked seam evidence so the subagent can run independently from repository-global docs.

That declared `.agent.md` file remains the only authoritative behavior contract for the subagent. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders. If the runtime supports direct exact-name resolution, invoke that exact subagent name while keeping the declared `.agent.md` file as the contract. If exact-name resolution is unavailable at runtime, resolve the same declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second before failing, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate role or repository-global substitute prompt.

The subagent must generate node-level granularity where each node owns one visible responsibility and every SO weave-out path has a detailed blocked-action hint, including file/path context when relevant.

If the enhancement flow introduces a local `.agent.md` file for a reusable weave-out, that file must also be linked by relative path from the target `SKILL.md` and from the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.

That local `.agent.md` file is also the authority source for the skill being enhanced subagent. Resolve the repository/workspace copy of the skill being enhanced first and the corresponding global installed-skill copy second before failing. Do not swap it for an ad hoc near-match role, repository-global prose, or a freeform summary during summary, review, or runtime invocation.

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

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.


## Caller File Preparation Contract

The calling agent must create the full input set on disk before one SO CLI call and pass only paths. Prepare every required script, JSON, workflow, reference, patch, context, and result input in one step. The CLI preflights all required files before reading or writing. Inline script, JSON, and replacement content is not a supported input form.
## Workflow Identity

For `templateKind: so-governed-target-skill`, require root `taskType`, `workflowKind`, `caseId`, and `runId`. Valid enhancement pairs are `skill_enhancement` with `so_self_bootstrap` or `target_skill_enhancement`; business workflows for the skill being enhanced use a target-specific task type with `target_skill_business`. A `template:` run marker is replaced with a generated `run-<guid>` on fresh materialization or the first new run, and the resulting runId remains stable through compile, run, resume, audit, and completion evidence.
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

- Enhancement business outcome is skill-being-enhanced creation or modification; runtime-only checks are supporting evidence, never the final deliverable.
- Bind one exact published `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package version from the checked-in lock and current version block. Derive the channel from the locked version.
- The host agent detects the current OS, architecture, and Linux libc and selects exactly one supported RID. Package acquisition may use a shell or scripting engine already available in that host. Do not add a fixed bootstrap script, require an installed `dotnet` host, or create a resolver-owned descriptor.
- Before extraction, verify exact package ID/version/RID, nuspec, SHA-512, fixed `runtime.json` manifest, archive size, duplicate entries, safe paths, apphost, and English guide tree. NuGet.org package bytes must match exact registration `catalogEntry.packageHash`; same-version GitHub Release fallback requires a matching `.sha512` sidecar. Fail closed when a required check cannot be performed.
- Prefer only the standard NuGet global-packages cache for exact package reuse. Do not create a Loom-specific runtime cache or lock. Extract the verified package into an external per-run runtime directory and record package/hash/extraction evidence there.
- Directly run `so.exe --guide` on Windows or `so --guide` on Unix immediately after extraction. Validate the returned version, absolute `docs_root` and `guide_path`, path containment, and readability before planning or workflow work.
- MCP is optional and is not a package-startup or guide gate. Do not perform a required pre-guide fragment inspection. A later workflow step may use local MCP when useful; its launch binding comes from the running apphost identity and a dispatched MCP application error is not silently retried.

## Package Integrity Checks

Validate the exact RID package and fail closed on any mismatch:

1. Read `resolved_version` from the checked-in lock and derive its channel. Never use a floating version or latest alias.
2. Detect one supported RID from the host. Reject an unsupported or ambiguous OS/architecture/libc result.
3. Reuse only a standard NuGet cache entry whose exact package bytes, ID, version, nuspec, RID and manifest validate. On a cache miss, fetch the exact NuGet package and verify against exact registration metadata; use only the same-version GitHub asset with its valid `.sha512` sidecar as fallback.
4. Open the `.nupkg` as ZIP content. Reject traversal, absolute/non-canonical paths, duplicates, oversized entries, unexpected payloads, and a mismatched root nuspec.
5. Require `tools/<rid>/runtime.json`, the product apphost (`so.exe` on Windows or `so` on Unix), and `tools/<rid>/docs/en/guides/so-guide.md`. Verify product, exact version, RID, entrypoint, docs root, and guide path against the manifest.
6. Extract only after the archive passes validation. Keep the runtime directory outside the skill bundle; set executable permission on Unix as needed.
7. Directly run the extracted apphost with `--guide`. Read successful stdout JSON only; verify exact version, absolute guide/docs paths, containment and file readability. Failed stderr is not guide evidence.

## Extracted Package Guide Entry

This skill publishes no `so-guide*.md` file. The authoritative guide is the English documentation tree carried by the exact RID package.

1. Read the locked version and derive its package channel.
2. Acquire and validate one `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package, then safely extract it.
3. Run `so.exe --guide` from the extracted package on Windows or `so --guide` on Unix. No DLL, `.deps.json`, `.runtimeconfig.json`, or .NET CLI invocation is part of the runtime contract.
4. Parse the JSON result and read the absolute `guide_path`; verify that it is the extracted `docs/en/guides/so-guide.md`. Use this fresh guide as the version-specific authority before downstream work.
5. Continue compile/run/resume with that same extracted apphost and one external workflow copy.

## Re-Enhancement Upgrade Gate

When the skill being enhanced already shows Loom Skill Orchestrator governance signals:

- use the exact version in `so-package-lock.json` and derive the channel from it
- reacquire and validate the matching self-contained RID package before new enhancement work
- run its apphost `--guide`, parse the result, and read `guide_path` before editing
- compare the current skill and workflow assets against that fresh guide

## Verified Audit-Step Reuse

The direct SO apphost command `so.exe copy-audit-step` on Windows or `so copy-audit-step` on Unix copies only a previously verified audit step. It writes `audit-reuse.json` with source paths/hashes, verifier, reason, `artifact_origin: verified-copy`, and `official_execution_evidence: false`.

The source must contain `workflow.mermaid.md`, `workflow.html`, and `workflow.json`; optional analysis/dataflow/summary files are copied when present. Source and destination hashes are verified and destination collisions fail. Reuse preserves audit presentation only; it never replaces official direct-apphost `run`/`resume`, event logs, gate evaluation, package validation, or guide evidence.

## Re-Enhancement Template Strategy

Every re-enhancement pass records whether the template change is `local_patch`, `structural_refactor`, or `full_regeneration`. Use the old template as a comparison input only; current requirements, governance rules, and the fresh direct-apphost guide control the new candidate.

Optional MCP support for later workflow operations lives in `../assets/agents/loom-skill-enhancement-mcp-startup.agent.md`; it cannot acquire the runtime, capture the guide, or gate startup.

## Workflow Template Governance Baseline

- Plan before editing the skill being enhanced; analyze inputs, outputs, routes, ownership, gates, and expected evidence.
- Keep workflow JSON canonical; regenerate Mermaid and audit views after template changes.
- Start from one fresh external workflow copy and preserve its case/run identity through compile, run, resume, and audit.
- Use direct `so.exe`/`so` for compile, run, resume, inspection, and audit commands. Compile is validation only; full-delivery governed completion requires run/resume to final `Done`.
- Boundary checks, owner-specific approvals, route coverage, business-output gates, and strongest-earned blocked outputs remain required. Removing the startup MCP gate does not weaken those business checks.
- `AskUser` requests only user-owned choices. Runtime-owned package facts, host paths, guide results, and provenance are returned through runtime-owned evidence.
- Direct edits to a running workflow remain blocked-state-only emergency workarounds approved by the user; immediately return to direct apphost compile/run/resume.

## Governed Validation Enforcement

- Direct `so.exe compile`/`so compile` and workflow-load paths reject governed templates that omit the root validation contract.
- Compile/load rejects `AskUser` seams that request reserved runtime-owned fields such as workflow paths, event logs, audit paths, or system-generated provenance.
- Terminal routes must satisfy their declared business-output gates; blocked routes must publish the strongest-earned business outputs before a runtime-owned wait.

## Self-Contained Apphost Preflight

Before any governed work, verify the locked product+RID package identity and hash, archive safety, manifest, apphost and guide assets; extract safely; then run and validate a fresh `--guide` result. No package bundle assembly, descriptor file, DLL startup contract, or mandatory MCP registration/fragment-inspection step is required.
## Boundary Check And Approval Gate (Compulsory)

This gate applies with equal force to `/loom-skill-enhancement` self-bootstrap runs **and** to every skill being enhanced under Loom Skill Orchestrator governance. Both the SO skill's own execution and the skills being enhanced are forced onto the route under Loom Skill Orchestrator governance: no next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step.

- A **boundary check** is the machine-readable validation of every transition before advancing. It must confirm `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state it must satisfy gate predicates (`passExpression` / `succeedExpression`) over runtime evidence, plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) an explicit approval/instruction from the user at `AskUser` seams for declared user-owned fields or decisions, or (b) a structured non-human continuation payload whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, compile success, an unapproved draft copy, local orchestration, or direct workflow JSON edits — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Both self-bootstrap runs and governed enhancement of the skill being enhanced slices must apply this gate at every transition on the same external runtime copy until final `Done`.

## Shared Context And Parallel Enhancement Batches

Build one bounded `shared_review_context` after exact package validation and a fresh direct-apphost guide result. Include real checked-in snapshots, a source manifest, guide/schema/runtime references, `context_hash`, package evidence, and the same external workflow-copy identity. Independent external subagents consume that context by reference.

Model independent review or validation transitions in one `ConcurrencyStrategy.All` group with one shared target state. The SO runtime must persist every expected external wait and join only after all results return. Aggregate every finding before one coordinated repair. After repair, run a second complete parallel validation batch, aggregate it, and finish with one serial validation transition for JSON, graph/dataflow, compile, schema/demo, and ordered runtime checks. Partial or duplicate batches fail closed. This policy belongs to enhancement governance and does not add a generic runtime Review engine.

## Governance and Official Run Surface

In exclusive Loom Skill Orchestrator governance mode:

- Loom Skill Orchestrator is the only official execution authority.
- Official skill runs are only:
  - `so.exe run` on Windows
  - `so run` on Unix
- Official workflow operations for `/loom-skill-enhancement` and any skill being enhanced under Loom Skill Orchestrator governance must be executed from published SO package artifacts for the bound version and derived channel unless a blocked-state emergency exception was explicitly approved.
- Gate predicates must bind declared required output fields explicitly — non-empty values, success/passed state, belonging to the current workflow instance — not a single aggregate flag such as `gate_outputs_present == true`.
- Official runnable route guards after review-fix must require both `review_fix_loop_evidence != null` AND `commit_report_ready.status == 'ready'`, with explicit blocked/needs-validation stop or wait paths when readiness is not proven.
- Terminal business-output gates before final `Done` must include a boundary-check/approval-gate trail covering every transition on the same external runtime copy and concrete target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence). Checked-in asset path existence alone cannot satisfy a business-output gate.
- Enhanced target `SKILL.md` files must say that ordinary workflow changes stay on the SO CLI path and that direct workflow JSON edits are blocked-state-only, user-approved emergency workarounds.
- Enhanced target `SKILL.md` files must also say that Windows PowerShell 5.1 package-channel restores use ZIP-based `.nupkg` extraction, that HTTP probes add `-UseBasicParsing` when those PowerShell web cmdlets are used, and that failed extraction or guide commands cannot be recorded as success proof.
- MCP is optional after the fresh guide step. Use a local MCP server only when a later operation benefits from it; bind it to the current apphost identity. Do not make MCP registration or fragment inspection a startup or guide gate. Web and remote transports remain unsupported.

## Think-Out-Loud Required Fields

Report runtime package fields once prepared, after every SO apphost execution, and on each progress update:

- `resolved_runtime_version`
- `runtime_package_id`
- `runtime_rid`
- `runtime_package_sha512`
- `runtime_preflight_result`
- `runtime_extraction_directory`
- `runtime_apphost_path`

Report audit fields after every SO binary execution and on each progress update:

- `mermaid_file`
- `html_file`
- `analysis_file`
- `dataflow_file`
- `must_show_to_user_files`
- `workflow_location_summary`
- `execution_confidence`
- `estimated_overall_progress`

Every direct apphost execution (`so.exe` on Windows or `so` on Unix) must begin its think-out-loud update with the current verified Mermaid, HTML, Analysis, and Dataflow artifacts in that order. Each artifact is shown as a Markdown link immediately followed by a `text` fence containing the same normalized path. After the four pairs, print localized headings in this order: `## 执行信心: x%`, one short reason, `## 预计整体进度: x%`, and one brief progress sentence. For English interaction, use `## Execution confidence: x%` and `## Estimated overall progress: x%`. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress describes approximate completion of the whole request. Use only paths verified by the current call or the latest verified continuity set. For `not_emitted`, say that the render is unchanged; for `runtime_path_only`, preserve verified paths as technical evidence; for `delivery_failed`, report the failure and next action without a guessed link. Prefer verified workspace-relative paths when a workspace mirror is available.

All progress, blocked, error, and completion prose must use the current interaction language and plain words. Do not use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the user-facing explanation. Keep exact identifiers in a separate technical-details or evidence section.

`must_show_to_user_files` should contain the same ordered file list for the current SO binary execution. This list is an audit list, not a link guarantee. A host card or notification can supplement the fixed link-and-fence block but cannot replace it.

## Plain-Language Feedback For Every Language

Write every user-facing progress, blocked, error, and completion update in the user's requested language for a high-school reader with no workflow background. English is not automatically plain language. Use short sentences and everyday words; state what happened, whether the user's work or data is still safe, why it happened, and the next action, in that order. Translate internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon before exposing exact technical details. Keep commands, paths, IDs, and evidence fields in a separate technical-details section only when needed. When creating or updating a skill being enhanced, copy this rule into its `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints.

## Delivery Completion Gate

- Completion requires requested deliverables of the skill being enhanced to exist and governance wording to be aligned.
- Runtime validation artifacts alone cannot serve as sole completion evidence.
- Failed stderr output from the direct apphost `--guide` operation cannot be saved as guide evidence.
- For templates for the skill being enhanced with root `templateKind: so-governed-target-skill`, completion also requires the governed validation contract, route-aware business-output gates, and seam ownership declarations to be present and compile-clean.
- Completion evidence for skills being enhanced should cite the final workflow template, compiled Mermaid, workflow analysis report, confirmation-loop result, node-to-file or node-to-artifact map, and the boundary-check/approval-gate trail covering every governed transition on the same external runtime copy.
- Terminal completion must also include target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence showing the requested checked-in deliverables were created or modified), not just their path existence.
- Completion evidence should also distinguish three categories explicitly when they differ: checked-in source deliverables, runtime-owned temporary artifacts, and runtime-owned completion manifests that reference checked-in source deliverables.
- Post-run workaround reporting should include decision trigger, alternatives considered, risk justification, rollback plan, and follow-up acknowledgement request. The default acknowledgement reminder is non-blocking unless the user explicitly requests blocking behavior.
