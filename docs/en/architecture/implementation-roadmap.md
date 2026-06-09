# Implementation Roadmap

[中文](../../zh-cn/architecture/implementation-roadmap.md)

This page is the approved repository handoff roadmap for Techne Loom.

It exists so another agent can continue work from the public docs alone, without needing hidden planning context.

## Status Snapshot

- Repository framing, root execution rules, and flagship bilingual README slices are complete.
- Public `.NET` slices exist for `Techne.Loom.Abstractions`, `Techne.Loom.Common`, and `Techne.Loom.SkillOrchestrator`.
- `SkillOrchestrator` now has a public CLI contract, runtime, tests, and aligned docs.
- `AgentOrchestrator` is now implemented in `.NET` with `ao host`, `ao run`, `ao resume`, and `ao --guide` commands.
- The broader `/docs` tree exists, but some pages are still being deepened from skeleton to handoff-grade detail.

## Source And Scope Rules

- Curated workflow-tracking material selected from the original private project is useful as historical input.
- Do not treat any private-source material as the canonical public product definition.
- Do not open-source `Clarios.*` projects verbatim.
- Keep the public core protocol-neutral and product-neutral at the `Abstractions` and `Common` layers.

## Product Split

| Product | Role | Current repository state |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | Public workflow/task-tracking contracts | Implemented in `.NET` |
| `Techne.Loom.Common` | Host-agnostic runtime helpers | Implemented in `.NET` |
| `Techne.Loom.SkillOrchestrator` | Deterministic skill execution and tracking | Implemented in `.NET` |
| `Techne.Loom.AgentOrchestrator` | Exploratory orchestration over `MCP/stdio` | Implemented in `.NET` |

AO and SO are separate products in different niches. They must not be reframed as one being the host or child runtime of the other.

## Approved Phase Map

1. Repo framing
   Public mono-repo skeleton, bilingual docs layout, source-provenance clarity, root execution rules.
2. Core contract extraction
   Public workflow model, engine/store/dispatcher contracts, namespace cleanup, dependency slimming.
3. Common runtime split
   Serialization, clocks, IDs, in-memory/file-backed stores, expression evaluation, visualization plumbing.
4. Skill executable
   Deterministic workflow execution, local tool execution, wait/resume handling, stable CLI contract.
5. Agent executable
   Exploratory orchestration over `MCP/stdio`, mutable workflow + append-only event/snapshot log, boundary-driven control payloads.
6. Protocol and cross-language preparation
   Canonical workflow/control contracts, transport-neutral boundaries, Node.js/Python alignment surfaces.
7. OSS hardening
   CI, packaging metadata, tests, examples, docs completion, release hygiene.

## Current And Next Slices

### Completed or substantially complete

- Root governance rules and bilingual README landing pages.
- Public `.NET` contract layer.
- Public common runtime layer.
- SO runtime, CLI output contract, sidecar JSON contract, and focused tests.
- AO runtime, MCP stdio host, CLI surface (`ao run`, `ao resume`, `ao host`, `ao --guide`), and control payload contract.

### Next recommended slice

- Expand solution-wide CI/build/test/pack behavior.
- Deepen docs still at skeleton level.
- Add broader visualization and workflow progression tests.
- Prepare Node.js/Python placeholder packages and schema-facing examples.

## Review And Commit Cadence

- Treat each major slice as a review boundary.
- Run `cto-review-and-commit` after every major slice before starting the next.
- As a default planning rule, keep each slice at or below 50 changed files when practical.
- Even below 50 files, still review immediately when the slice changes protocols, schemas, package boundaries, or runtime control behavior.

## Handoff Checklist For Another Agent

1. Read `AGENTS.md` and `AGENTS.zh-CN.md`.
2. Read this roadmap page.
3. Read `reference/products/ao-guide.md` and `reference/products/so-guide.md`.
4. Check `git status` and scope the next slice explicitly.
5. Keep the next slice small enough for evidence-based review.
6. Before moving beyond that slice, run `cto-review-and-commit`.

## Important Do-Not-Regress Rules

- Keep AO and SO independent in packaging, invocation, and mental model.
- Keep `Abstractions` and `Common` free of private cloud/AI product assumptions.
- Keep workflow file and CLI sidecar contracts explicit and machine-first.
- Keep public docs bilingual and mirrored by path.
