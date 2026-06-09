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
- `ao --guide` and `so --guide` must emit version-matched, offline guide surfaces derived from curated docs sources.

## README Positioning

- Treat `README.md` and `README.zh-CN.md` as flagship landing pages, not only technical indexes.
- Use GitHub-supported rich Markdown intentionally: badges, alerts/callouts, comparison tables, Mermaid diagrams, architecture visuals, and strong use-case framing.
- Marketing language can be ambitious, but claims must remain defensible against the current implementation and docs.
- When reframing terminology or ecosystem positioning, use bounded research, including `cto-web-research` when appropriate, before rewriting the landing-page narrative.

## Guide Surface Rules

- `so --guide` and `ao --guide` should emit full Markdown by default, support section filtering, support `--lang zh-cn|en`, and support `--export <path>`.
- Guides should begin with version, build, and compatibility metadata.
- Guides should cover behavior, responsibilities, contracts, templates, examples, and anti-patterns.
- Keep guide content both human-readable and model-ingestible. Use stable fenced blocks such as `guide-contract`, `guide-template`, `guide-checklist`, and `guide-example` when extraction stability matters.

## Execution Order And Review Cadence

- Before broader implementation, update `AGENTS.md` and `AGENTS.zh-CN.md` with the current language, documentation, and execution rules.
- After every major implementation slice, run a reasonable `cto-review-and-commit` review/fix/validate/commit workflow before starting the next slice.
- Treat that cadence as a hard default boundary, not a soft suggestion: do not let work continue across multiple major slices and then review later in one large batch unless the user explicitly overrides it.
- Keep each review-and-commit slice small enough to be reviewed with evidence. As a default planning rule, a slice should usually stay at or below 50 changed files; if the pending scope is approaching that size, stop and run `cto-review-and-commit` before adding more.
- Even when a slice is smaller than 50 files, still run `cto-review-and-commit` immediately when the slice changes protocol contracts, schemas, package boundaries, or runtime control behavior.
- Major slices include work such as root AGENTS rules, flagship README landing pages, docs skeletons, package scaffolding, protocol/schema changes, and code implementation.
- Do not continue into the next major slice with unreviewed or uncommitted work unless the user explicitly overrides that cadence.
