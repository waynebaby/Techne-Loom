# Review And Evidence Contract

Read this file before planning, workflow-designer dispatch, batch review, repair, final evidence collection, or completion reporting.

## Guide Hub Structure

The authoritative SO guide pages live under `../../../docs/en/guides/` and are packaged recursively into the runtime docs bundle. The extracted package uses `guides/so-guide.md` as the `--guide` entry, with adjacent `so-guide-flow.md`, `so-guide-reference.md`, and `so-guide-reference-<chapter>.md` pages. This skill publishes no SO guide files; use `reference/so-skill-reference.md` for runtime acquisition and the fresh extracted guide for version-specific authority.
<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.3.282`.
- This block is refreshed by the publish workflows whenever SO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->

























- Released package index: `reference/packages.released.md`
- Beta package index: `reference/packages.beta.md`
- Target-local SO reference copies: complete package guide pages at `assets/so-workflow/reference/so/runtime-contracts.md` and `assets/so-workflow/reference/so/runtime-governance.md`.
- Copy provenance: `assets/so-workflow/reference/document-copy-manifest.json`.
- Authoritative SO guide source: `../../../docs/en/guides/so-guide.md`; the extracted runtime entry is `guides/so-guide.md`.
- Do not add or publish any SO guide file under this skill; use `reference/so-skill-reference.md` for runtime acquisition and the fresh guide returned by the selected runtime launch descriptor's `--guide` operation for version-specific authority.
- Workflow designer subagent: `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`
- MCP-preferred governance-entry subagent: `assets/agents/loom-skill-enhancement-mcp-startup.agent.md`; it generates MCP configuration from the resolver-owned descriptor and owns the CLI backup decision
- Reusable weave-out subagents:
	- `assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md`
	- `assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md`
	- Re-enhancement strategy reviewer: `assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md`
	- `assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md`
	- `assets/agents/loom-skill-enhancement-review-fix-loop.agent.md`
- `assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md`
	- `assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md`
	- `assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md`
	- `assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md`
- Authority operation: execute the selected runtime launch descriptor with `--guide`; parse its JSON result and read the returned `guide_path` first, then inspect `docs_root` only when necessary

### Shared Context And Batch Verification

Build one bounded shared review context after governance-entry fragment proof (MCP preferred or descriptor-driven CLI backup) and fresh guide capture, before any independent enhancement review. The context is produced once from real checked-in asset snapshots and runtime inputs. It carries a source manifest, bounded snapshots, guide/schema/runtime references, a deterministic `context_hash`, and the identity of the same external workflow copy. Independent subagents read this context by reference; they do not rebuild or silently widen it.

Use `ConcurrencyStrategy.All` for independent external `SubagentCall` reviews and validations. The SO runtime registers every expected transition in one persisted batch and keeps the workflow at the current state until every result has been returned. A missing or duplicate result fails closed. Use this only for independent external transitions with one shared target state; keep synchronous mutations and ordered checks in separate serial groups.

After each parallel review batch, run one explicit aggregation step that preserves every finding, source, severity, and disposition (`accepted`, `rebutted`, or `needs_validation`). Run one coordinated batch repair against the complete aggregate. Do not launch one rewrite per finding. Then run the independent post-fix validators as a second parallel batch, aggregate their results, and finish with one serial validation stage for JSON, graph/dataflow, compile, and ordered runtime checks before official run/resume continuation.

This batching policy belongs to SO enhancement planning and delivery governance. It is not a generic AO/SO runtime Review engine and does not change AO behavior.

### Workflow Baseline

- Enter plan mode before editing target-skill deliverables.
- When creating or revising workflow templates, invoke the local workflow-designer subagent with relative-link context, not a freeform generic agent.
- When a route names a specific local or target-skill `.agent.md` file, treat that exact file as the only authoritative subagent contract. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders before use.
- If direct exact-name subagent resolution is available, invoke that exact subagent name while keeping the named `.agent.md` file as the authority. If direct resolution is unavailable, resolve the named `.agent.md` path from the current repository/workspace copy first and the corresponding global installed-skill copy second, then pass the resolved file path plus the full file content into the subagent-driving call.
- Do not replace a named `.agent.md` route with a freeform approximate role, a repository-global prompt, or an ad hoc summary of the intended subagent behavior.
- Analyze inputs, outputs, branches, loops, seams, gates, and expected evidence.
- Generate the workflow template JSON first, then compile it.
- Keep the workflow template JSON as the authority.
- Repeat a user confirmation loop by updating the template or its source planning inputs and recompiling.
- For target-skill templates that declare root `templateKind: so-governed-target-skill`, also declare a root `validation` contract with `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`.
- For full-delivery target-skill templates with root `templateKind: so-governed-target-skill`, the materialized runtime workflow copy must execute on the current public `dotnet so.dll run` and `dotnet so.dll resume` path until final `Done`. Do not leave the runnable copy in `Drafting`, and do not depend on private or unavailable built-in tool names.
- If a checked-in workflow JSON is only a draft or compile-review source template, label it explicitly as source-only and do not present it as directly runnable.
- When `MemoryRead` inspects checked-in target-skill assets, it must load real file snapshots from an explicit target-skill asset root and must reject absolute paths or traversal outside that root.
- Never author a node whose purpose says or implies `run a multistep plan`.
- For weave-out design, prefer existing capable subagents whenever they can already complete the goal instead of emitting generic agent placeholders.
- If a target-skill enhancement weave-out is clearly reusable and benefits from a dedicated subagent, recommend creating a detailed `{target-skill-name}-{task-name}.agent.md` under `{skill-folder}/assets/`, route future workflow nodes to that subagent explicitly, add a relative-link reference to that file in the target `SKILL.md`, and reference the same relative path in the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.
- For any target-skill local subagent route introduced under `{skill-folder}/assets/`, the target skill must treat that exact `.agent.md` file as the subagent's authority source during both documentation handoff and runtime invocation. Resolution should test the target skill's repository/workspace copy first and the corresponding global installed-skill copy second before failing.

### Required Outputs

- bound runtime version confirmation plus derived channel evidence
- runtime-ready evidence for the selected published SO bundle before downstream work
- MCP registration-attempt evidence and governance-entry evidence from the same external workflow copy, including configuration generation, initialize, notifications/initialized, bounded so_inspect_workflow_fragment or its descriptor-driven CLI backup, descriptor identity, hashes, and fallback reason
- Windows package-channel runtime acquisition evidence when PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast proof when extraction or guide generation fails
- package index links
- guide surface references
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- target `SKILL.md` workflow-file language wording that requires English as the canonical information carrier for workflow-owned schema and control metadata, while preserving source/request-language user and business payloads
- target `SKILL.md` governance wording that keeps ordinary workflow changes on the SO CLI path and limits direct workflow JSON edits to blocked-state, user-approved emergency workarounds
- target `SKILL.md` execution-status wording for both creation and update slices that states `dotnet so.dll compile` is validation only, requires the default governed success path to continue on public `dotnet so.dll run` and `dotnet so.dll resume` until final `Done`, and forbids claiming governed completion before that chain has reached final `Done`
- target `SKILL.md` runtime hardening wording that forbids pseudo-success preflight/guide records and requires ZIP-based `.nupkg` extraction on Windows PowerShell 5.1 package-channel restores
- per-run plan output path and hash (runtime-owned; not a stable target-skill asset)
- workflow template path
- workflow-designer subagent dispatch record and relative-link context set used for workflow generation
- weave-out suitability review that checks whether every current weave-out should become a dedicated target-skill local `{skillname}-{taskname}.agent.md`
- target-skill local subagent definition paths created or refreshed under `assets/`
- target `SKILL.md` and target reference-doc relative-link updates for any newly required target-skill local weave-out subagents
- re-enhancement template-change strategy and conflict evidence, including the selected strategy and old-template/current-requirements input mapping
- `shared_review_context`, `aggregated_reenhancement_findings`, and `aggregated_plan_findings` produced from one bounded context and complete independent review batches
- `aggregated_review_findings` and `batch_repair_evidence` proving that every pre-fix finding was considered and repaired in one coordinated pass
- `aggregated_post_fix_validation` and `serial_validation_evidence` proving that all post-fix validators returned before the final ordered validation stage
- workflow analysis report
- `workflow.compile-feedback.json` and the `workflow.compile-feedback.v1` payload, including parse/validation status, counts, phase blockers, candidate path/hash, and runtime identity/version.
- Verified real output paths for workflow, feedback, analysis, dataflow, Mermaid, and HTML artifacts, including paths outside the Git worktree or ignored workspace mirrors.
- compiled Mermaid
- node-to-file or node-to-artifact map
- package lock metadata split into:
	- checked-in lock reference target
	- resolved runtime bundle version/channel evidence
	- runtime-owned completion-manifest reference to the checked-in lock asset
- checked-in skill-markdown governance outcome split into:
	- checked-in skill-markdown target path
	- governed evidence that the checked-in skill markdown is the source deliverable for this slice
	- runtime-owned completion-manifest reference to that checked-in source asset
- boundary-check/approval-gate trail covering every governed transition on the same external runtime copy, including the gate predicates checked (bound to concrete output fields and instance), seam ownership verified, route coverage confirmed, and the explicit approval or structured non-human continuation that allowed each next step
- target-deliverable-change evidence for terminal completion: `completion_by_target_skill_changes` or file/diff evidence showing the requested checked-in deliverables were created or modified, not merely present as paths
- fixed governance verdict and evidence checklist surface carried by the runtime-owned completion manifest, including the verdict rule, current status and node, whether final `Done` was reached, any missing evidence, the next action, and explicit mappings back to the existing runtime-owned evidence families instead of a parallel completion schema or a terminal self-certification surface
- runtime audit artifact links
- After every SO CLI call or audit-producing step, including `dotnet so.dll` and self-contained `so.exe` (or `so`) calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file`, verify existence and readability, and never guess an audit path. If `--workspace-root` was supplied, use only the hash-verified `workspace_relative_mermaid_file` and `workspace_relative_html_file` values for clickable workspace links. If the card tool is available, pass `card_input_file` directly to it without asking another agent to return the file contents; a card is not proof of artifact delivery. Otherwise show the verified Mermaid link first and the HTML link second. When no fresh render exists, reuse only a previously verified link and state that the render is unchanged. On `delivery_failed`, report the failure and do not emit a guessed link.

### Governance

- Exclusive Loom Skill Orchestrator governance mode uses Loom Skill Orchestrator as the only official execution authority.
- Every Loom-governanced target skill is forced onto the Loom Skill Orchestrator-governanced route: no transition may advance without passing a boundary check on the exact external runtime copy, then receiving explicit approval or structured continuation instruction for that next step. There is no autonomous shortcut off this route.
- For any Loom-governanced target skill, execution authority must come from published SO package artifacts for the chosen channel. Do not normalize repository-source builds or manually assembled binaries into the default workflow-operation path.
- For both new target-skill creation slices and update or re-enhancement slices, do not present guide refresh, checked-in asset creation, workflow-template authoring, or `dotnet so.dll compile` success as governed completion or official governed run evidence by themselves.
- AskUser seams may request only declared user-owned fields or decisions.
- Runtime-owned facts and artifact paths belong to runtime-owned seams such as `WaitResume`.
- Route-aware terminal and blocked business-output gates are required for governed routes.
- Gate predicates must bind the declared required output fields explicitly — non-empty values, success/passed state, and belonging to the current workflow instance — not a single aggregate flag such as `gate_outputs_present == true`. A gate that only checks an aggregate boolean cannot prove its listed evidence exists.
- Official runnable route guards after review-fix must require both `review_fix_loop_evidence != null` AND `commit_report_ready.status == 'ready'`, with explicit blocked/needs-validation stop or wait paths when readiness is not proven. A non-empty evidence object alone must never authorize the official run chain.
- Terminal business-output gates before final `Done` must include both a boundary-check/approval-gate trail covering every transition on the same external runtime copy and concrete target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence for the requested checked-in deliverables). Checked-in asset path existence alone cannot satisfy a business-output gate.
- For target-skill modifications, runtime-ready evidence, successful MCP startup/use evidence, and fresh-guide evidence must exist before downstream planning, authoring, validation, compile, run, or resume work starts.
- If a governed workflow is presented as runnable execution authority, its materialized runtime copy must actually execute on the current public `dotnet so.dll run` and `dotnet so.dll resume` path rather than being only compile-clean.
- Full-delivery governed slices must continue from compile-review approval onto the public `dotnet so.dll run` path, then weave back through any blocked business-intake or `AskUser` seams with public `dotnet so.dll resume` until final `Done`, passing a boundary check and explicit approval at every transition.
- Do not keep `compile-only`, `compile-ready governance integration`, or `official run evidence pending` as supported completion outcomes for full-delivery governed enhancement slices unless the user has explicitly changed the slice contract before implementation begins.
- File-backed checked-in asset inspection must stay rooted under the declared target-skill asset root and must not degrade into placeholder context-copy review.
- Before completion, review every current weave-out and decide whether it should be implemented as a dedicated target-skill local subagent under `assets/{skillname}-{taskname}.agent.md`; when the answer is yes, create or refresh that subagent file and add the relative-link reference in the target `SKILL.md` and target reference docs before the workflow can complete.
- Before completion, the review-fix loop must build one shared bounded context, run independent reviews as a complete `ConcurrencyStrategy.All` batch, aggregate every finding before one coordinated repair, run a complete parallel post-fix validation batch, and finish with serial validation; compile-clean or one returned subagent result is insufficient.
- Before completion, run an explicit review-skill -> fix-skill loop on the target skill, then prepare commit-and-report-ready evidence for the final handoff instead of stopping immediately after template compile or first-pass edits.
- When a slice still uses checked-in source assets as the authoritative business deliverables, the governed route must name those checked-in assets explicitly as done outputs and must emit a runtime-owned completion manifest that references them instead of pretending runtime-owned temporary files replaced them.
- The runtime-owned completion manifest is the fixed governance verdict and evidence checklist surface for final completion handoff. Reuse the existing runtime-owned evidence families for that verdict surface instead of introducing a parallel completion schema, and do not let the terminal manifest step invent its own replacement route-proof evidence.
- Completion requires requested target-skill deliverables to be created or modified.
- Post-run workaround reminders are non-blocking by default: highlight the decision report path and key risk summary, request explicit user acknowledgement, and keep execution continuity unless the user overrides with a blocking policy.

## Workflow Designer Reference Pack

Before dispatching `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`, the caller must provide a bounded `referencePackManifest` and fresh `schemaDemoInput` from the exact SO runtime after the required governance-entry transport proof. The pack must include the successful guide JSON and returned guide file, same-runtime schema/demo/demo compile audit, target contract and requirements, current workflow source, current package lock, applicable `AGENTS.md`, and latest compile feedback when revising. Every entry carries a normalized path, SHA-256, exact runtime version, authority role, read status, and validation result. An older workflow is `previous_runnable_reference` only and requires a version/hash/difference/rejected-item disposition.

The designer must return runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` with schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Keep descriptors with path, SHA-256, schemaVersion, verdict, and exact runtime version. A required semantic probe that is failed or unknown prevents readiness; compile success alone is not semantic evidence. SO governance wrappers must hand off to the owning domain orchestrator instead of copying its business steps.
