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
- Root bilingual files are required for `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `SECURITY.md`, and `AGENTS.md`.
- Root English files keep the default file name. Chinese mirrors use the `.zh-CN.md` suffix.
- Root bilingual files should include reciprocal header links.
- Keep `AGENTS.md` root-only. Do not duplicate it under `/docs`.
- Product guide source files live at `/docs/<lang>/reference/products/ao-guide.md` and `/docs/<lang>/reference/products/so-guide.md`.
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
- Guides should begin with version, build, and compatibility metadata.
- Guides should cover behavior, responsibilities, contracts, templates, examples, and anti-patterns.
- Keep guide content both human-readable and model-ingestible. Use stable fenced blocks such as `guide-contract`, `guide-template`, `guide-checklist`, and `guide-example` when extraction stability matters.
- Guide and reference content should enumerate MCP methods, CLI arguments, planner flows, audit artifact paths, and skill input/output payload shapes explicitly.

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
