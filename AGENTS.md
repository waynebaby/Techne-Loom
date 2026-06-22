# Workspace Agent Rules

[中文](AGENTS.zh-CN.md)

> `AGENTS.md` is the automation-facing source of repository execution rules.
> `AGENTS.zh-CN.md` is the Chinese mirror and must stay aligned with this file.

<!-- cto-skills-manager-managed:begin -->
## Shared Python Environment

This workspace uses the shared virtual environment pointer from `.venv.path`.

- Managed by `cto-skills-manager`.
- Windows: resolve `.venv.path` with PowerShell before invoking Python-based tooling.
- If a Python runtime is available but the `.venv.path` target does not exist yet, initialize that virtual environment first and then use the new environment.
- Linux: resolve `.venv.path` with bash before invoking Python-based tooling.

## Run Output Naming

- When a skill creates a per-run output root, keep the skill-owned parent directory and name the run root `exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/`.
- Keep the timestamp immediately after `exec-` so runs remain sortable even when adjacent steps switch skills.
<!-- cto-skills-manager-managed:end -->

## Repository Direction

- Techne Loom is a .NET-first multi-ecosystem mono-repo with parallel package families across .NET, Node.js, and Python.
- `AgentOrchestrator` and `SkillOrchestrator` are separate products in different niches. They do not call each other and must not be framed as a parent/child runtime pair.
- The user-facing product name for AO narrative, landing-page copy, and guide positioning is `Loom Agent Execution Orchestrator`.
- That user-facing name does not rename implementation identity. Keep `Techne.Loom.AgentOrchestrator`, `dotnet ao.dll`, `/loom-plan-execution`, source paths, and type names unchanged unless a task explicitly calls for a code/package rename.
- Shared abstractions may align at a low level, but packaging, release identity, and product-facing contracts stay independent.

## Packaging And Layout

- Organize source by language root from the start: `/src/dotnet`, `/src/nodejs`, and `/src/python`.
- Every project unit is a publishable package.
- In `.NET`, each `.csproj` maps to one NuGet package or tool package.
- In Node.js, each package directory with its own `package.json` maps to one npm package.
- In Python, each package/build target with its own `pyproject.toml` maps to one PyPI distribution or wheel.
- Keep package families parallel by role: `abstractions`, `common`, `agent-orchestrator`, and `skill-orchestrator`.

## Documentation And Language Rules

- Public docs are bilingual by default.
- Keep mirrored trees under `/docs/zh-cn` and `/docs/en`.
- Every paired page must include a reciprocal header link to the counterpart page.
- Skill-local references under `.github/skills/*/reference/` must be English only so skills remain deterministic and runnable offline without multilingual drift.
- Localized narrative for skills belongs in bilingual docs under `/docs/en` and `/docs/zh-cn`, not in multilingual variants under skill-local `reference/` directories.
- Root bilingual files are required for `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `SECURITY.md`, and `AGENTS.md`.
- Root English files keep the default file name. Chinese mirrors use the `.zh-CN.md` suffix.
- Root bilingual files should include reciprocal header links.
- Keep `AGENTS.md` root-only. Do not duplicate it under `/docs`.
- Product guide source files live at `/docs/<lang>/reference/products/ao-guide.md` and `/docs/<lang>/reference/products/so-guide.md`.
- For AO-facing user docs, prefer the user-facing name `Loom Agent Execution Orchestrator` in titles, intros, README positioning, and guide navigation, while preserving `ao-guide.md`, `dotnet ao.dll`, and package identifiers as implementation-facing names.
- `dotnet ao.dll --guide` and `dotnet so.dll --guide` must emit version-matched, offline guide surfaces derived from curated docs sources.
- Root package acquisition indexes live at `packages.released.md`, `packages.released.zh-CN.md`, `packages.beta.md`, and `packages.beta.zh-CN.md`, and skills should reference them with absolute GitHub URLs.
- Treat NuGet.org as the first-class latest package source for released and beta package acquisition guidance; GitHub-hosted package assets remain fallback download paths when NuGet.org access is unavailable or when the user explicitly requests asset URLs.
- Those package acquisition indexes must also expose GitHub-hosted latest release fallback links for stable and beta package assets, not only package-manager install commands.
- MCP, CLI, and skill input/output contract docs are first-class deliverables; do not leave them implicit in README prose.

## Workflow Terminology Rules

- The repo-wide workflow vocabulary root lives at `/docs/en/architecture/workflow-terminology.md` and `/docs/zh-cn/architecture/workflow-terminology.md`.
- Use that glossary for explanatory prose across AO and SO docs, guides, READMEs, and future schema explanations.
- Prefer **weave out** and **weave back** when explaining outward control transfer and structured continuation.
- Prefer **strand** over **thread** in repo docs to avoid collision with `.NET` threading terminology.
- Use **seam** for conceptual ownership joins, and keep **boundary** for explicit wire/protocol surfaces such as `boundary_reason` and the `type: "boundary"` envelope inside `<so_property>` blocks.
- When explanatory terminology and current wire names differ, mention both on first use and keep implemented field names explicit.
- Do not introduce new workflow metaphors in one product doc without updating the glossary and its bilingual mirror first.

## README Positioning

- Treat `README.md` and `README.zh-CN.md` as flagship landing pages, not only technical indexes.
- Use GitHub-supported rich Markdown intentionally: badges, alerts/callouts, comparison tables, Mermaid diagrams, architecture visuals, and strong use-case framing.
- Marketing language can be ambitious, but claims must remain defensible against the current implementation and docs.
- When reframing terminology or ecosystem positioning, use bounded research, including `cto-web-research` when appropriate, before rewriting the landing-page narrative.

## Guide Surface Rules

- `dotnet so.dll --guide` and `dotnet ao.dll --guide` should emit full Markdown by default, support section filtering, support `--lang zh-cn|en`, and support `--export <path>`.
- For AO- and SO-routed skills, once the package channel or runtime source is chosen, the next hard gate is proving that the selected Loom runtime is actually runnable and can produce a fresh `--guide` result from that runtime. Do not continue to planning, authoring, validation, compile, run, resume, or downstream input collection until that `--guide` result exists.
- Guides should begin with version, build, and compatibility metadata.
- Guides should cover behavior, responsibilities, contracts, templates, examples, and anti-patterns.
- Keep guide content both human-readable and model-ingestible. Use stable fenced blocks such as `guide-contract`, `guide-template`, `guide-checklist`, and `guide-example` when extraction stability matters.
- Guide and reference content should enumerate MCP methods, CLI arguments, planner flows, audit artifact paths, and skill input/output payload shapes explicitly.

## SO Workflow Validation Rules

- For SO-governed target-skill templates, `dotnet so.dll compile` and workflow-load paths must enforce more than structural validity; they must also reject missing business-output gates, seam-ownership violations, and done paths that can complete with governance-only evidence.
- `AskUser` seams may request only user-owned inputs or decisions. Runtime-owned facts, runtime provenance, and system-generated artifact paths belong to runtime-owned seams such as `WaitResume` or blocked-resume payloads, not to user prompts.
- Route-aware workflow templates should declare the business-output gates and strongest-earned blocked outputs needed for each governed route so compile/load validation can prove that meaningful business artifacts exist before `done` or before a runtime-owned wait boundary.

## Loom Skill Enhancement Governance

- `/loom-skill-enhancement` must plan before it edits a target skill: analyze the target skill inputs, outputs, nodes, guards, branches, loops, user seams, runtime seams, gates, and output evidence before authoring target-skill deliverables.
- The workflow template JSON is the authority for review and execution. Mermaid, HTML, and localized plan text are display layers generated from or kept aligned with the template; user feedback must update the workflow template or its source plan inputs, not only the rendered Mermaid.
- Workflow visualizations should carry stable node-type semantics. Use light color families consistently: AI/model/subagent work in green, code/tool work in blue, optional user choices in yellow, mandatory mid-run user input in red, and required gate/governance states in white or very light gray.
- Skill-enhancement completion evidence must include the final workflow template, generated Mermaid, node-to-file or node-to-artifact mapping, actual implementation/audit evidence, and the target-skill deliverables changed. Runtime-only validation is not enough.
- Step 1 of the loom-skill-enhancement upgrade is the reusable foundation: plan mode, workflow analysis, template generation, compile-generated Mermaid, confirmation loop, node-to-file mapping, final evidence reporting, and the existing latest-package behavior for normal target skills.
- Step 2 is self-bootstrap: after Step 1 has its own review/fix/validate/commit, `/loom-skill-enhancement` may consume that foundation to become SO-governed. The self-bootstrap execution may use the current repository `src` build result and record that local runtime manifest only under the audit root, while the resulting future official skill behavior must still restore the latest package/channel runtime and package-lock semantics.
- Self-bootstrap backups are taken after the Step 1 commit and before Step 2 edits. Back up only loom-skill-enhancement skill-local files to the audit root unless the user explicitly asks for a wider snapshot.

## Audit Artifact Rules

- Workflow audit outputs are not optional display helpers; treat them as per-step audit records.
- Unless the user explicitly requests an audit destination, use a temporary output root.
- Do not default compile-time artifacts, audit artifacts, or other runtime temporary files under a skill directory or under `assets/so-workflow/`; keep them under a runtime temporary root or a repo-root temporary root unless the user explicitly chooses another destination.
- Audit artifacts, intermediate workflow materializations, and think-out-loud or conversation-referenceable run outputs may be cited during the conversation, but they still default to a runtime temporary root, repo-root temporary root, or an explicit user-chosen execution output root; do not default them into any skill folder.
- Persist audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`.
- Each step directory must include the point-in-time Mermaid Markdown, HTML, and workflow JSON backup.
- Compile and audit flows must never overwrite an existing artifact file in place; fail with a rich error that reports the conflicting path set and tells the caller to choose a different output root or clean the destination.

## Execution Order And Review Cadence

- Before broader implementation, update `AGENTS.md` and `AGENTS.zh-CN.md` with the current language, documentation, and execution rules.
- After every major implementation slice, run a reasonable `cto-review-and-commit` review/fix/validate/commit workflow before starting the next slice.
- Treat that cadence as a hard default gate, not a soft suggestion: do not let work continue across multiple major slices and then review later in one large batch unless the user explicitly overrides it.
- Keep each review-and-commit slice small enough to be reviewed with evidence. As a default planning rule, a slice should usually stay at or below 50 changed files; if the pending scope is approaching that size, stop and run `cto-review-and-commit` before adding more.
- Even when a slice is smaller than 50 files, still run `cto-review-and-commit` immediately when the slice changes protocol contracts, schemas, package seams, or runtime control behavior.
- Major slices include work such as root AGENTS rules, flagship README landing pages, docs skeletons, package scaffolding, protocol/schema changes, and code implementation.
- Do not continue into the next major slice with unreviewed or uncommitted work unless the user explicitly overrides that cadence.
