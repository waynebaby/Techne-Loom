# Workspace Agent Rules

> `AGENTS.md` is the automation-facing source of repository execution rules.
>
> This file intentionally contains Level 1 rules only. Scope-specific details live in the linked instruction documents below and must be loaded when their file patterns match the current change.

<!-- shared-python-environment:begin -->
## Shared Python Environment

This workspace may use the shared virtual environment pointer from `.venv.path`.

- Resolve `.venv.path` with PowerShell on Windows or bash on Linux before invoking Python tooling.
- If the configured `.venv.path` target does not exist, initialize that virtual environment before invoking Python tooling.

## Run Output Naming

- When a skill creates a per-run output root, keep the skill-owned parent directory and name the run root `exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/`.
- Keep the timestamp immediately after `exec-` so runs remain sortable even when adjacent steps switch skills.
<!-- shared-python-environment:end -->

## Level 1 Rules

### Repository identity

- Techne Loom is a .NET-first multi-ecosystem mono-repo with parallel package families under `/src/dotnet`, `/src/nodejs`, and `/src/python`.
- Every source project remains buildable and testable; only self-contained product+RID runtime packages belong to the active .NET NuGet release set.
- `AgentOrchestrator` and `SkillOrchestrator` are independent products. They do not call each other and must not be framed as a parent/child runtime pair.
- Use `Loom Agent Plan-Execution Orchestrator` for AO user-facing narrative while preserving implementation identities such as `Techne.Loom.AgentOrchestrator`, `/loom-plan-execution`, and source/type names; runnable commands invoke the matching RID apphost directly.
- Use `/loom-ask-user` for agent-facing structured clarification with or without a workflow. It is a peer consumer of the shared XO Ask capability in existing AO/SO runtime binaries; the optional `AskUser` workflow node is a separate consumer. The selected runtime serves the browser form; the skill has no separate governance workflow or package lock and uses that exact runtime dependency.
- Use `enhancing skill` for `/loom-skill-enhancement` and `skill being enhanced` for the skill it creates or modifies; preserve exact `target_*`, `templateKind`, and workflow field literals in machine contracts.
- Workflow and process examples must include a complete Mermaid route with emoji, a nearby color legend, and readable labels; workflow JSON or `WorkflowInstance` examples must also include same-version direct-apphost `so compile` or `ao compile` Mermaid evidence, while explanatory diagrams must be labeled as such.

### Subagent model requirements

- Subagents engaged in autonomous work must use GPT-6 Luna or Claude Haiku 5.5 as the minimum model standard. Do not silently fall back to a lower-tier model; if neither model is available for a required subagent, pause that delegated work and ask the user before proceeding.
- This model requirement applies only to subagents. It does not restrict the primary agent performing work directly in the current session.

### Safe editing and Git hygiene

- GitHub Copilot must not use `apply_patch` in this repository. Use the checked-in external-file-driven range editor or another repository-approved editing mechanism; do not embed multiline replacement content in commands.
- Preserve user changes. Do not use destructive Git commands or rewrite shared history unless the user explicitly requests it.
- File-valued CLI inputs are path-only and must be complete on disk before a command starts. Inline scripts, JSON, patches, workflows, references, or replacement content are not valid inputs.
- Keep generated output, audit material, caches, and temporary validation files outside source and skill directories unless they are explicitly requested deliverables.

### Workflow invariants

- A workflow's business steps must match its declared business intent. Governed workflow instances declare `taskType` and `workflowKind`; enhancement and target-business workflows must not be interchanged.
- Plan, replan, compile, run, resume, and audit are disk-backed and sessionless. Each product owns one canonical `WorkflowInstance`; events, logs, audits, envelopes, and large artifacts are companion evidence, not a second mutable execution truth.
- `caseId` and `runId` stay on the same external workflow copy through the full execution chain. They identify one business execution and do not replace business outputs.
- AO and SO remain independent runtimes with independent package, CLI, release, and product-facing boundaries.
- XO Ask is the workflow-independent shared ask capability exposed by existing AO/SO runtime binaries; `XO` is shorthand for AO or SO, not a third product. `/loom-ask-user` and the `AskUser` workflow node are peer consumers and neither depends on the other.
- The shared Common XO Ask service owns ask-scoped questions, validation, drafts, answers, and receipts only. Standalone sessions do not require or mutate a `WorkflowInstance`; for the optional node adapter, the owning AO/SO path validates the receipt, maps answers, and performs its existing resume. Workers never lock or mutate a workflow and never resume it.
- `requiredInputs` and SO `validation.declaredUserOwnedFields` apply only to the workflow-node adapter; standalone question contracts have no workflow context-path requirement.
- Every skill-owned contract defines the versioned `context_fields` catalog as the sole authority for business context paths, JSON types, nullability, and container item/value types; workflow definitions and resume descriptions consume derived views instead of duplicating this catalog.
- AO/SO compile and run/resume validate the current owning catalog. For statically identifiable expression reads such as `context.Get<T>("path")`, compile verifies that the consumer type matches the catalog type; incompatible or conflicting consumers fail with actionable diagnostics. `Has(path)` checks presence only and does not establish a type. No implicit string-to-boolean/number conversion may hide a mismatch.
- External `resumeOutputKey`, `outputPath`, and `outputBindings` destinations must agree with the same catalog. Runtime validates actual payload/context values before mutation, preserves missing/null/false distinctions, and rejects writes to runtime-owned fields.
- Every weave-out includes catalog-backed payload type metadata, selectable typed suggestions or an explicit empty-with-reason result, bounded current/direct-successor consumer expressions, and optional host guidance for a decision model/tool/skill/MCP. Suggestions and type validity never prove evidence or replace human approval; Loom never invokes model APIs.
- The published `0.3.334-beta` CLI does not expose standalone `ask`; do not claim the workflow-free route is shipped until a published AO/SO apphost exposes it.
- Governed routes use bounded workflow-fragment access when a workflow step needs it, but runtime bootstrap proceeds from self-contained package extraction directly to fresh `--guide` without resolver descriptors or a required MCP/fragment startup gate.
- AO/SO skills share verified runtime packages in a current-user OS-temp cache. Each immutable generation is identified by product/package ID, exact version, RID, and full `.nupkg` SHA-512; short hash prefixes only abbreviate the path. Verify package bytes before reuse or extraction, stage and validate repairs before atomic publication, preserve generations used by active apphosts, and retain valid entries for the OS-temp lifecycle.
- AO/SO artifact delivery covers Mermaid, HTML, Analysis, and Dataflow when generated. Link only verified workspace mirrors and include complete native paths; if a mirror cannot be verified, show the native path without claiming clickability.
- Progress reports for `/loom-skill-enhancement`, `/loom-plan-execution`, skills created or updated by `/loom-skill-enhancement`, and `/loom-ask-user` when it emits a report are appended by skill/agent delivery to per-execution `<execution-output-root>/progress-report.md`. Serialize complete Markdown entries with ISO 8601 UTC and local timestamps, including the local offset and OS timezone ID. A failed append preserves the previous log, is reported, and pauses later business-step progression without changing workflow execution state.
- Workflow designers use version-matched English technical documentation as authority; translated public documentation does not substitute for the English runtime docs.
- Prefer fan-out only when branches are logically independent, the host can dispatch them concurrently, and each branch writes to an isolated output path; keep dependent or shared-write work sequential.
- Keep transition/group pass results distinct from workflow terminal status. `firstPass`, `whenAnyResponse`, and `whenAnyPass` route to the selected transition target; aggregation strategies require an explicit join target, while no-pass and execution-error routes remain distinct.
- If the host cannot dispatch concurrently, execute branches sequentially without changing the selected strategy semantics. Request cancellation when a strategy finishes early, persist logical cancellation when physical cancellation is unavailable, and reject late results.

### Phase-sliced workflow design

- Enable phase slicing at 40 or more authored state nodes; use 25 as a soft cap per phase. Count authored state nodes only, excluding transitions and synthetic harness nodes. Existing oversized workflows receive warnings, not automatic rejection.
- Freeze stable phase IDs and typed ports before phase authoring. The owning versioned `context_fields` catalog remains the sole type and ownership authority; each shared business field has one producing phase, and cross-phase routes and backedges are declared.
- Phase-local compilation is static only. Synthetic harness inputs and nodes cannot perform business side effects or satisfy business gates. Merge creates a new candidate and must pass the complete owning AO or SO compile.
- Common owns shared design contracts and checks; AO and SO retain independent commands and compilers. Rework follows the declared impact closure. No live `WorkflowInstance`, history, or pending-result migration is in scope.

### Documentation and public contracts

- Public documentation is bilingual by default under mirrored `/docs/en` and `/docs/zh-cn` trees. Root public docs require their Chinese mirrors; `AGENTS.md` and other agent configuration files remain English-only.
- Workflow definition files are the canonical English carrier for schema keys and control semantics. Localization belongs in the presentation layer and must not change wire names or workflow behavior.
- State node names pair checkpoint identifiers with concise business wording; descriptions state the business purpose, and workflow phases include a stable stage identifier plus a readable phase name.
- Successful compile Mermaid output is derived from the workflow. Compile audit HTML may summarize only recorded feedback, analysis, and dataflow evidence; failed compile must not emit placeholder renders.
- Keep `AGENTS.md` at the repository root. Do not create a second agent-rules source under `docs`.

### Branch and package channel locks

- Classify each activity by purpose, not branch name or output file type: authoring, debugging, or improving a skill/workflow is development; invoking a skill to produce requested domain code, projects, or documents is business execution.
- Development may use a compatible current-source or candidate self-contained AO/SO apphost without an exact published lock or a full candidate run/resume chain for every phase iteration. Prepare runtime references once per development run, record product/version/RID/provenance, and label evidence development-only. Never refresh a checked-in lock from an unpublished candidate.
- Development support references, shared context, schema/demo descriptors, skeletons, phase candidates, plans, scripts, and support reports do not require hashes, re-attestation, or substitute revision/fingerprint gates. They must still be readable and pass actual schema, type, interface, ownership, path, security, and compile checks. If a published package is used, package identity, SHA-512, and safe-archive checks remain mandatory.
- Target business outputs exercised during development remain production-validation objects. Compute and verify every hash required by the target business contract against the actual artifact using its declared algorithm and canonicalization. Missing, invalid, or mismatched required hashes block acceptance, delivery, and production-ready claims; preserve the failure and require fresh passing validation after repair.
- Development allowances do not waive formal check-in, CI, package, or release gates. Official channel execution and publication use published exact versions and locks. Source check-in still requires matching AO/SO self-contained schema/demo exports, same-apphost demo compiles, focused tests, and platform gates; current-source evidence must be labeled development-only.

- For checked-in release surfaces, treat `main` as the release branch: resolve the latest published stable (`release`) NuGet version and use that exact version for release-channel runtime locks, package references, guide metadata, and provenance surfaces.
- For checked-in beta surfaces, treat `development` as the beta branch: resolve the latest published prerelease (`beta`) NuGet version and use that exact version for beta-channel runtime locks, package references, guide metadata, and provenance surfaces.
- When refreshing a checked-in lock, select the channel from the current branch and use only a version actually published on NuGet. Keep the complete 16-package AO/SO RID runtime release closure and its matching runtime locks on one compatible exact version; never mix `release` and `beta`, copy a version from the other branch, or use a floating `latest` alias.
- This lock-refresh rule is separate from release version calculation: `release-set.json` and the shared version job may select the next monotonic version after the published high-water mark for a new release. Do not write that unpublished candidate into an existing runtime lock until it is published.
- When a workflow-template migration introduces fields or enum values newer than its current locked runtime, compile with both the exact locked apphost and the candidate apphost. Record a locked-runtime failure as a version-compatibility finding; candidate success does not prove the current lock is compatible. Never put an unpublished candidate in a checked-in lock. If a push is intentionally used to trigger publication, verify the exact package and version after CI/CD succeeds, then refresh the lock to that published version before treating the template as officially usable.

### Validation and delivery

- For source, test, workflow, package, or build-input changes on `development` and `main`, start Windows and WSL restore, build, and test jobs in parallel. Keep their build/intermediate outputs isolated, collect results separately, and wait for both before declaring validation complete.
- Pure article-publication pushes that touch only `docs/en/articles/**`, `docs/zh-cn/articles/**`, `docs/assets/images/**`, or `docs/assets/attachments/**` are documentation-only: the package-publishing workflows must skip restore/build/test, schema-demo generation, and package publication for them. Keep matching `paths-ignore` filters in both `publish-development.yml` and `publish-main.yml`; retain `workflow_dispatch` as an explicit full validation/release route. Any push that also changes a path outside these exclusions follows the normal workflow.
- Tests that exercise intentionally long-running tasks must not set a test timeout or add timeout-based cancellation merely to shorten the run. Let the task complete naturally and distinguish genuine failures from runner or environment interruption.
- Before source check-in, generate both AO/SO schema and demo files through their matching self-contained RID apphosts in separate external directories, then compile each demo with that same apphost. Development may use a compatible current-source or candidate apphost and must label its provenance and evidence development-only; official package execution/publication uses the published channel version. Run focused tests and platform validation for the affected scope. Detailed commands and artifact rules are in the validation instruction document.
- Review and validate each major implementation slice before starting the next. Do not carry unreviewed or uncommitted major work across slices unless the user explicitly overrides the cadence.

## Scoped Instruction Documents

Read the applicable document before changing files in its scope. These documents expand the L1 rules; they do not replace them.

| Scope | Instruction document | Main contents |
| --- | --- | --- |
| Runtime, packages, release metadata, CI publishing, package indexes | [loom-runtime.instructions.md](.github/instructions/loom-runtime.instructions.md) | Runtime resolution, mode separation, exact versions, release-set closure, package validation, and guide acquisition |
| Workflow templates, runtime state, dataflow, validation, C# expressions | [loom-workflow.instructions.md](.github/instructions/loom-workflow.instructions.md) | Workflow identity, canonical state, projection evidence, SO checks, and Roslyn capability policy |
| Skill authoring, enhancement, workflow design, subagents | [loom-skill-governance.instructions.md](.github/instructions/loom-skill-governance.instructions.md) | Reference packs, layered validation, subagent authority, enhancement routes, and batch review |
| Public docs, guides, READMEs, demos, user-facing status text | [loom-documentation.instructions.md](.github/instructions/loom-documentation.instructions.md) | Bilingual docs, Mermaid, plain-language feedback, workflow terminology, and README positioning |
| Source/tests/scripts/workflows and validation handoff | [loom-validation.instructions.md](.github/instructions/loom-validation.instructions.md) | Range editing, file inputs, schema/demo checks, audit artifacts, Windows/WSL execution, and review cadence |

When more than one scope matches, load all matching instruction documents. If a detailed instruction conflicts with a Level 1 rule, the Level 1 rule wins.