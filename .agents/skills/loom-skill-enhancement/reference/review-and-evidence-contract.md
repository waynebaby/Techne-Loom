# Review And Evidence Contract

Read this file before planning, workflow-designer dispatch, batch review, repair, final evidence collection, or completion reporting.

User-facing progress, blocked, error, and completion text must use plain words in the current interaction language. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the explanation; keep exact identifiers in technical details or evidence only.

## Guide Hub Structure

The repository source SO guide pages live under `docs/en/guides/` and are packaged recursively. The source-tree path is for repository maintenance only; at runtime, use the absolute `docs_root` and `guide_path` returned by the exact apphost's successful `--guide` result, as described in `reference/so-skill-reference.md`. This skill publishes no SO guide files.
The exact SO package runtime version is the skill package version block in `SKILL.md`; the publish workflows refresh only that block.

























- Released package index: `reference/packages.released.md`
- Beta package index: `reference/packages.beta.md`
- Target-local SO reference copies: complete package guide pages at `assets/so-workflow/reference/so/runtime-contracts.md` and `assets/so-workflow/reference/so/runtime-governance.md`.
- Copy provenance: `assets/so-workflow/reference/document-copy-manifest.json`.
- Repository SO guide source: `docs/en/guides/so-guide.md`; the runtime entry is the exact absolute `guide_path` returned by the selected apphost.
- Do not add or publish any SO guide file under this skill; use `reference/so-skill-reference.md` for package acquisition and the fresh guide returned by direct `so.exe --guide` on Windows or `so --guide` on Unix for version-specific authority.
- Workflow designer subagent: `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`
- Optional MCP support: MCP is not used for package startup or guide retrieval. A later step may configure local MCP from the running apphost identity when that step benefits from MCP tools.
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
- Authority operation: directly run `so.exe --guide` on Windows or `so --guide` on Unix; parse its JSON result and read the returned `guide_path` first, then inspect `docs_root` only when necessary.

### Shared Context And Batch Verification

Build one bounded shared review context after exact package validation and fresh direct-apphost guide capture, before independent enhancement review. Include real checked-in snapshots, a source manifest, package/hash/RID/extraction evidence, guide/schema/runtime references, and the same external workflow-copy identity. A `context_hash` may be recorded only as optional generated metadata; consumers do not require, calculate, compare, re-attest, freshness-check, or gate on it. Independent subagents consume this context by reference without rebuilding or widening it.

Use an explicit `TransitionGroup` with `strategy: whenAllPass` for independent external `SubagentCall` reviews and validations that share one join state. The SO runtime registers every expected transition in one persisted batch and keeps the workflow at the current state until every result has been returned. A missing or duplicate result fails closed. Use this only for independent external transitions; keep synchronous mutations and ordered checks in separate serial groups. Legacy wire value `all` must be migrated and must not be authored.

After each parallel review batch, run one explicit aggregation step that preserves every finding, source, severity, and disposition (`accepted`, `rebutted`, or `needs_validation`). Run one coordinated batch repair against the complete aggregate. Do not launch one rewrite per finding. Then run the independent post-fix validators as a second parallel batch, aggregate their results, and finish with one serial validation stage for JSON, graph/dataflow, compile, and ordered runtime checks before official run/resume continuation.

This batching policy belongs to skill enhancement planning and delivery governance. It is not a generic AO/SO runtime Review engine and does not change AO behavior.

### Workflow Baseline

- Enter plan mode before editing deliverables of the skill being enhanced.
- When creating or revising workflow templates, invoke the local workflow-designer subagent with relative-link context, not a freeform generic agent.
- When a route names a specific local or skill being enhanced `.agent.md` file, treat that exact file as the only authoritative subagent contract. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders before use.
- Every dispatch must show both the exact agent name declared in that file's frontmatter and a clickable Markdown link to the same `.agent.md`; names or paths alone are insufficient.
- Before dispatch, compare the exact frontmatter name with the host's registered agent list. Treat `agent not found`, an unknown agent, a subagent tool with no `agentName` parameter, or a silent default-agent response as not registered. Do not stop or ask for approval merely because registration is missing; driver fallback does not require user approval. Resolve the file from the current skill/workspace copy first and its matching global installed-skill copy second, then dispatch a capable non-read-only registered general-purpose agent as driver with either the complete file content inline or a path-only reference (no hash) that the driver must read in full and confirm by echoing the frontmatter name. Pass all required inputs/reference context, large ones by path. A 408, request-size error, or timeout is a transport failure: retry once in path-only mode. Stop only when the file is absent/ambiguous or no capable driver exists.
- Require every skill being enhanced to state this fallback in its own `SKILL.md` and use name-plus-link on every subagent invocation in its instructions and workflow dispatches.
- If the exact name is registered, invoke it directly while keeping the named `.agent.md` file as the authority; otherwise use the driver route above in the same step.
- Do not replace a named `.agent.md` route with a freeform approximate role, a repository-global prompt, or an ad hoc summary of the intended subagent behavior.
- Analyze inputs, outputs, branches, loops, seams, gates, and expected evidence.
- Generate the workflow template JSON first, then compile it.
- Keep the workflow template JSON as the authority.
- Repeat a user confirmation loop by updating the template or its source planning inputs and recompiling.
- For templates for the skill being enhanced that declare root `templateKind: so-governed-target-skill`, also declare a root `validation` contract with `gates`, `routes`, `declaredUserOwnedFields`, and `reservedRuntimeOwnedFields`.
- For full-delivery templates for the skill being enhanced with root `templateKind: so-governed-target-skill`, the materialized workflow copy must execute through the same published apphost (`so.exe run` on Windows or `so run` on Unix) and corresponding `resume` command until final `Done`. Do not leave the runnable copy in `Drafting`, and do not depend on private or unavailable built-in tool names.
- If a checked-in workflow JSON is only a draft or compile-review source template, label it explicitly as source-only and do not present it as directly runnable.
- When `MemoryRead` inspects checked-in assets of the skill being enhanced, it must load real file snapshots from an explicit asset root of the skill being enhanced and must reject absolute paths or traversal outside that root.
- Never author a node whose purpose says or implies `run a multistep plan`.
- For weave-out design, prefer existing capable subagents whenever they can already complete the goal instead of emitting generic agent placeholders.
- If an enhancement of the skill being enhanced weave-out is clearly reusable and benefits from a dedicated subagent, recommend creating a detailed `{target-skill-name}-{task-name}.agent.md` under `{skill-folder}/assets/`, route future workflow nodes to that subagent explicitly, add a relative-link reference to that file in the target `SKILL.md`, and reference the same relative path in the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.
- For any local subagent for the skill being enhanced route introduced under `{skill-folder}/assets/`, the skill being enhanced must treat that exact `.agent.md` file as the subagent's authority source during both documentation handoff and runtime invocation. Resolution should test the the repository of the skill being enhanced/workspace copy first and the corresponding global installed-skill copy second before failing.

### Required Outputs

- bound runtime version confirmation plus derived channel evidence
- runtime-ready evidence for the selected published SO bundle before downstream work
- Runtime evidence from the same external workflow copy: exact package ID/version/RID, source and verified SHA-512, nuspec/manifest/archive checks, extraction path/result, direct apphost path, and fresh readable guide result. MCP registration and handshake evidence are optional and only apply to a later step that actually uses MCP.
- Windows package-channel runtime acquisition evidence when PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast proof when extraction or guide generation fails
- package index links
- guide surface references
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- target `SKILL.md` workflow-file language wording that requires English as the canonical information carrier for workflow-owned schema and control metadata, while preserving source/request-language user and business payloads
- target `SKILL.md` governance wording that keeps ordinary workflow changes on the SO CLI path and limits direct workflow JSON edits to blocked-state, user-approved emergency workarounds
- target `SKILL.md` execution-status wording for both creation and update slices stating that direct apphost compile is validation only, the governed success path uses `so.exe run`/`resume` on Windows or `so run`/`resume` on Unix until final `Done`, and completion cannot be claimed before that same-copy chain finishes
- target `SKILL.md` progress-report wording requiring localized execution-confidence and estimated-overall-progress percentage headings after each post-execution update, in that order and each with a short reason; define confidence as likelihood of successful completion and progress as approximate completion of the whole request, ground both in verified evidence, and state that neither replaces required completion evidence or the final `Done` state
- target `SKILL.md` per-execution progress-log wording requiring a complete Markdown entry file and the append helper bundled in the target skill at `assets/scripts/AppendProgressReport.ps1` or `assets/scripts/AppendProgressReport.sh`; resolve the target skill root to an absolute path before invocation. Require UTC and local timestamps with numeric offset and OS timezone ID, state that the helper reads only the entry file and append-writes the target, returns its full file URL, forbid reading or inspecting the existing target, and require failure reporting plus a pause without runtime-state mutation.
- target `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints requiring the AO/SO report as four Markdown link-plus-`text`-fence pairs (Mermaid, HTML, Analysis, Dataflow) in that order. Each title must link to a complete local file URL the active host has verified it can open, in the standard three-slash form (`file://` plus an empty authority plus the absolute path): Windows `file:///C:/...`, Linux/macOS `file:///...`. Drive-letter case is insignificant and the two-slash `file://C:/...` form is invalid; encode spaces as %20 and repeat the identical encoded URL in the following fence. Require a verified mirror and its encoded file URL only when the original URL cannot be opened. Never include literal spaces in the URL, omit the scheme, truncate the URL, use a bare code fence instead of a link, or reuse an unverified stale address; if no URL opens, require delivery-failure guidance without links.
- target `SKILL.md` runtime hardening wording that forbids pseudo-success preflight/guide records and requires ZIP-based `.nupkg` extraction on Windows PowerShell 5.1 package-channel restores
- per-run plan output path; an optional generated hash may accompany it as metadata, but consumers do not require or compare it as a validity, freshness, or revision gate (runtime-owned; not a stable skill being enhanced asset)
- workflow template path
- workflow-designer subagent dispatch record with exact declared agent name, clickable `.agent.md` link, dispatch route used (`direct` or `driver-fallback`), delivery mode (`inline` or `path-only`), fallback trigger evidence, the driver's name echo, and relative-link context set
- evidence that the skill being enhanced's `SKILL.md` includes the named-agent fallback and every subagent invocation supplies the exact name plus the matching `.agent.md` link
- weave-out suitability review that checks whether every current weave-out should become a dedicated local `{skillname}-{taskname}.agent.md`
- local subagent for the skill being enhanced definition paths created or refreshed under `assets/`
- `SKILL.md` and reference-doc relative-link updates for any newly required local weave-out subagent for the skill being enhanced
- re-enhancement template-change strategy and conflict evidence, including the selected strategy and old-template/current-requirements input mapping
- `shared_review_context`, `aggregated_reenhancement_findings`, and `aggregated_plan_findings` produced from one bounded context and complete independent review batches
- `aggregated_review_findings` and `batch_repair_evidence` proving that every pre-fix finding was considered and repaired in one coordinated pass
- `aggregated_post_fix_validation` and `serial_validation_evidence` proving that all post-fix validators returned before the final ordered validation stage
- workflow analysis report
- `workflow.compile-feedback.json` and the `workflow.compile-feedback.v1` payload, including parse/validation status, counts, phase blockers, candidate path, and runtime identity/version. Candidate hashes may be recorded only as optional development-support metadata and are not compared or used as freshness or revision gates.
- Verified real output paths for workflow, feedback, analysis, dataflow, Mermaid, and HTML artifacts, including paths outside the Git worktree or ignored workspace mirrors.
- compiled Mermaid
- node-to-file or node-to-artifact map
- package lock metadata split into:
	- checked-in lock reference target
    - exact package version, channel, product/RID identity, and integrity evidence
	- runtime-owned completion-manifest reference to the checked-in lock asset
- checked-in skill-markdown governance outcome split into:
	- checked-in skill-markdown target path
	- governed evidence that the checked-in skill markdown is the source deliverable for this slice
	- runtime-owned completion-manifest reference to that checked-in source asset
- boundary-check/approval-gate trail covering every governed transition on the same external runtime copy, including the gate predicates checked (bound to concrete output fields and instance), seam ownership verified, route coverage confirmed, and the explicit approval or structured non-human continuation that allowed each next step
- target-deliverable-change evidence for terminal completion: `completion_by_target_skill_changes` or file/diff evidence showing the requested checked-in deliverables were created or modified, not merely present as paths
- fixed governance verdict and evidence checklist surface carried by the runtime-owned completion manifest, including the verdict rule, current status and node, whether final `Done` was reached, any missing evidence, the next action, and explicit mappings back to the existing runtime-owned evidence families instead of a parallel completion schema or a terminal self-certification surface
- runtime audit artifact links
- After every SO apphost call or audit-producing step, including `so.exe` and `so` calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file`, verify existence and readability, and never guess an audit path. If `--workspace-root` was supplied, use only the hash-verified `workspace_relative_mermaid_file` and `workspace_relative_html_file` values for clickable workspace links. If the card tool is available, pass `card_input_file` directly to it without asking another agent to return the file contents; a card is not proof of artifact delivery. Otherwise show the verified Mermaid link first and the HTML link second. When no fresh render exists, reuse only a previously verified link and state that the render is unchanged. On `delivery_failed`, report the failure and do not emit a guessed link.

### Governance

- Exclusive Loom Skill Orchestrator governance mode uses Loom Skill Orchestrator as the only official execution authority.
- Every skill being enhanced under Loom Skill Orchestrator governance is forced onto the route under Loom Skill Orchestrator governance: no transition may advance without passing a boundary check on the exact external runtime copy, then receiving explicit approval or structured continuation instruction for that next step. There is no autonomous shortcut off this route.
- For any skill being enhanced under Loom Skill Orchestrator governance, execution authority must come from published SO package artifacts for the chosen channel. Do not normalize repository-source builds or manually assembled binaries into the default workflow-operation path.
- For both new skill-being-enhanced creation slices and update or re-enhancement slices, do not present guide refresh, checked-in asset creation, workflow-template authoring, or direct apphost compile success as governed completion or official governed run evidence by themselves.
- AskUser seams may request only declared user-owned fields or decisions.
- Runtime-owned facts and artifact paths belong to runtime-owned seams such as `WaitResume`.
- Route-aware terminal and blocked business-output gates are required for governed routes.
- Gate predicates must bind the declared required output fields explicitly — non-empty values, success/passed state, and belonging to the current workflow instance — not a single aggregate flag such as `gate_outputs_present == true`. A gate that only checks an aggregate boolean cannot prove its listed evidence exists.
- Official runnable route guards after review-fix must require both `review_fix_loop_evidence != null` AND `commit_report_ready.status == 'ready'`, with explicit blocked/needs-validation stop or wait paths when readiness is not proven. A non-empty evidence object alone must never authorize the official run chain.
- Terminal business-output gates before final `Done` must include both a boundary-check/approval-gate trail covering every transition on the same external runtime copy and concrete target-deliverable-change evidence (`completion_by_target_skill_changes` or file/diff evidence for the requested checked-in deliverables). Checked-in asset path existence alone cannot satisfy a business-output gate.
- Before downstream planning, authoring, validation, compile, run, or resume, require exact package identity/hash/manifest/archive validation, safe extraction, apphost readiness, and a fresh readable guide result. No bounded MCP or CLI fragment inspection is a bootstrap prerequisite.
- If a governed workflow is presented as runnable execution authority, its materialized runtime copy must execute with the exact published apphost's direct `run` and `resume` commands rather than being only compile-clean.
- Full-delivery governed slices continue from compile review to direct apphost `run`, then use direct apphost `resume` for blocked business-intake or `AskUser` seams until final `Done`, passing a boundary check and explicit approval at every transition.
- Do not keep `compile-only`, `compile-ready governance integration`, or `official run evidence pending` as supported completion outcomes for full-delivery governed enhancement slices unless the user has explicitly changed the slice contract before implementation begins.
- File-backed checked-in asset inspection must stay rooted under the declared asset root of the skill being enhanced and must not degrade into placeholder context-copy review.
- Before completion, review every current weave-out and decide whether it should be implemented as a dedicated local subagent for the skill being enhanced under `assets/{skillname}-{taskname}.agent.md`; when the answer is yes, create or refresh that subagent file and add the relative-link reference in the target `SKILL.md` and reference docs for the skill being enhanced before the workflow can complete.
- Before completion, the review-fix loop must build one shared bounded context, run independent reviews as a complete `whenAllPass` batch, aggregate every finding before one coordinated repair, run a complete external `whenAllPass` post-fix validation batch, and finish with serial validation; compile-clean or one returned subagent result is insufficient.
- Before completion, run an explicit review-skill -> fix-skill loop on the skill being enhanced, then prepare commit-and-report-ready evidence for the final handoff instead of stopping immediately after template compile or first-pass edits.
- When a slice still uses checked-in source assets as the authoritative business deliverables, the governed route must name those checked-in assets explicitly as done outputs and must emit a runtime-owned completion manifest that references them instead of pretending runtime-owned temporary files replaced them.
- The runtime-owned completion manifest is the fixed governance verdict and evidence checklist surface for final completion handoff. Reuse the existing runtime-owned evidence families for that verdict surface instead of introducing a parallel completion schema, and do not let the terminal manifest step invent its own replacement route-proof evidence.
- Completion requires requested deliverables of the skill being enhanced to be created or modified.
- Post-run workaround reminders are non-blocking by default: highlight the decision report path and key risk summary, request explicit user acknowledgement, and keep execution continuity unless the user overrides with a blocking policy.

## Workflow Designer Reference Pack

Before dispatching `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`, the caller must provide a bounded `referencePackManifest` and fresh `schemaDemoInput` from the exact SO runtime after exact package validation and fresh direct-apphost guide capture. The pack must include the successful guide JSON and returned guide file, same-runtime schema/demo/demo compile audit, target contract and requirements, current workflow source, current package lock, applicable `AGENTS.md`, and latest compile feedback when revising. Every entry carries a normalized path, exact runtime version, authority role, read status, and validation result; `sha256` may be recorded only as optional generated metadata. During consumption, do not calculate, compare, re-attest, freshness-check, or gate on development-support hashes. An older workflow is `previous_runnable_reference` only and requires a version/path/copy-time/difference/rejected-item disposition; its source hash is optional metadata, not a gate.

The designer must return runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` with schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Keep descriptors with path, schemaVersion, verdict, and exact runtime version; `sha256` may be recorded only as optional generated metadata and is not required or compared as a freshness/validity gate. A required semantic probe that is failed or unknown prevents readiness; compile success alone is not semantic evidence. Loom Skill Orchestrator governance wrappers must hand off to the owning domain orchestrator instead of copying its business steps.
