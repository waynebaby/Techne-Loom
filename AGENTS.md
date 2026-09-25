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
- Use `enhancing skill` for `/loom-skill-enhancement` and `skill being enhanced` for the skill it creates or modifies; preserve exact `target_*`, `templateKind`, and workflow field literals in machine contracts.
- Workflow and process examples must include a complete Mermaid route with emoji, a nearby color legend, and readable labels; workflow JSON or `WorkflowInstance` examples must also include same-version direct-apphost `so compile` or `ao compile` Mermaid evidence, while explanatory diagrams must be labeled as such.

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
- Governed routes use bounded workflow-fragment access when a workflow step needs it, but runtime bootstrap proceeds from self-contained package extraction directly to fresh `--guide` without resolver descriptors or a required MCP/fragment startup gate.

### Documentation and public contracts

- Public documentation is bilingual by default under mirrored `/docs/en` and `/docs/zh-cn` trees. Root public docs require their Chinese mirrors; `AGENTS.md` and other agent configuration files remain English-only.
- Workflow definition files are the canonical English carrier for schema keys and control semantics. Localization belongs in the presentation layer and must not change wire names or workflow behavior.
- State node names pair checkpoint identifiers with concise business wording; descriptions state the business purpose, and workflow phases include a stable stage identifier plus a readable phase name.
- Successful compile Mermaid output is derived from the workflow. Compile audit HTML may summarize only recorded feedback, analysis, and dataflow evidence; failed compile must not emit placeholder renders.
- Keep `AGENTS.md` at the repository root. Do not create a second agent-rules source under `docs`.

### Branch and package channel locks

- For checked-in release surfaces, treat `main` as the release branch: resolve the latest published stable (`release`) NuGet version and use that exact version for release-channel runtime locks, package references, guide metadata, and provenance surfaces.
- For checked-in beta surfaces, treat `development` as the beta branch: resolve the latest published prerelease (`beta`) NuGet version and use that exact version for beta-channel runtime locks, package references, guide metadata, and provenance surfaces.
- When refreshing a checked-in lock, select the channel from the current branch and use only a version actually published on NuGet. Keep the complete 16-package AO/SO RID runtime release closure and its matching runtime locks on one compatible exact version; never mix `release` and `beta`, copy a version from the other branch, or use a floating `latest` alias.
- This lock-refresh rule is separate from release version calculation: `release-set.json` and the shared version job may select the next monotonic version after the published high-water mark for a new release. Do not write that unpublished candidate into an existing runtime lock until it is published.

### Validation and delivery

- For both `development` and `main`, start Windows and WSL restore, build, and test jobs in parallel. Keep their build/intermediate outputs isolated, collect results separately, and wait for both before declaring validation complete.
- Tests that exercise intentionally long-running tasks must not set a test timeout or add timeout-based cancellation merely to shorten the run. Let the task complete naturally and distinguish genuine failures from runner or environment interruption.
- Before code check-in, generate AO/SO schema and demo evidence through their matching self-contained RID apphosts, then run focused tests and platform validation for the affected scope. Detailed validation commands and artifact rules are in the validation instruction document.
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