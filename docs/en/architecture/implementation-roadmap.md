# Implementation Roadmap

[中文](../../zh-cn/architecture/implementation-roadmap.md) | [Root](../README.md)

This page is the approved repository handoff roadmap for Techne Loom.

It exists so another agent can continue work from the public docs alone, without needing hidden planning context.

## Status Snapshot

- The public framing now positions Techne Loom as a verifiable semantic and execution interoperability layer above Agent Skills.
- Public `.NET` slices exist for `Techne.Loom.Abstractions`, `Techne.Loom.Common`, and `Techne.Loom.SkillOrchestrator`.
- `SkillOrchestrator` has a public CLI contract, runtime, tests, and aligned docs.
- `AgentOrchestrator` is implemented in `.NET` with `dotnet ao.dll compile`, `dotnet ao.dll run`, `dotnet ao.dll resume`, and `dotnet ao.dll --guide` commands.
- Workflow IR, compile/validation feedback, runtime binding, wait/resume, local MCP governance, provenance, and audit evidence are current public foundations.
- [Skill Interoperability](skill-interoperability.md) records the evidence, current product surface, and limits of the interoperability claim.
- Cross-host target profiles, adapters, loss accounting, dependency/environment portability, and host-matrix conformance remain staged follow-up work.

## Source And Scope Rules

- Historical workflow-tracking material may inform comparisons, but it is not part of the public product contract.
- Define public behavior only from repository code, tests, package contracts, and authored docs.
- Do not copy implementation or documentation from any non-public source into this repository.
- Keep the public core protocol-neutral and product-neutral at the `Abstractions` and `Common` layers.

## Product Split

| Product | Role | Current repository state |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | Public workflow/task-tracking contracts | Implemented in `.NET` |
| `Techne.Loom.Common` | Host-agnostic runtime helpers | Implemented in `.NET` |
| `Techne.Loom.SkillOrchestrator` | Deterministic skill execution and tracking | Implemented in `.NET` |
| `Techne.Loom.AgentOrchestrator` | Exploratory orchestration over a CLI/package contract | Implemented in `.NET` |

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
   Exploratory orchestration over a CLI/package contract, mutable workflow + append-only event/snapshot log, control seams that weave out and are surfaced through blocked protocol payloads.
6. Protocol and cross-language preparation
   Canonical workflow/control contracts, transport-neutral boundaries, Node.js/Python alignment surfaces.
7. OSS hardening
   CI, packaging metadata, tests, examples, docs completion, release hygiene.

## Expression Runtime Roadmap

- Current .NET route: C# expressions compiled by Roslyn through the canonical root `runtimeBinding` and `expressionBinding` contract. All predicate compilation emits `detailedCompileFeedbackV1`.
- Adapter route: Node.js and Python may provide ecosystem adapters, but neither host language becomes an expression language automatically. Each future adapter must implement the same structured compile-feedback contract first.
- Future fourth route: Rust implementation of a cross-platform Loom Runtime Core with CEL as the canonical expression language. It is not Rust code execution and must reuse `ExpressionDefinition`, `requiredExpressionCapabilities`, `compileFeedbackContract`, and `ExpressionCompileFeedback`.
- Rust+CEL milestones: (1) documentation first, (2) prototype validation, (3) contract freeze, (4) runtime implementation, (5) CLI release, and (6) .NET adapter integration. Cross-language translation remains skill-owned and must preserve source, translated source, tool, review, and compile evidence.

## Current And Next Slices

### Completed or substantially complete

- Root governance rules and bilingual README landing pages.
- Public `.NET` contract and common runtime layers.
- SO runtime, CLI output contract, sidecar JSON contract, and focused tests.
- AO runtime, CLI surface (`dotnet ao.dll compile`, `dotnet ao.dll run`, `dotnet ao.dll resume`, and `dotnet ao.dll --guide`), and control payload contract.
- Workflow IR with explicit states, transitions, routes, seams, gates, ownership, and output evidence.
- Disk-backed run/resume, local MCP descriptor binding, provenance, and audit artifact continuity.
- Bilingual interoperability architecture and community evidence pages.

### Next recommended slice

- Define `loom-target-profile` and host capability fields for paths, frontmatter, tools, hooks, permissions, and context mode.
- Build read-only host adapters and machine-readable semantic loss reports before attempting write-back conversion.
- Expand activation, script, MCP, permission, runtime, and resume probes into a fixed cross-host conformance corpus.
- Add dependency/environment contracts and a policy IR that can fail closed when a host cannot express a requirement.
- Prepare signed package, SBOM, publisher trust, and revocation evidence rather than relying on prose provenance alone.
- Continue solution-wide CI/build/test/pack hardening and Node.js/Python schema-facing preparation.

## Review And Commit Cadence

- Treat each major slice as a review gate.
- Run a review, validation, and commit loop supported by the execution environment or active agent after every major slice before starting the next.
- As a default planning rule, keep each slice at or below 50 changed files when practical.
- Even below 50 files, still review immediately when the slice changes protocols, schemas, package seams, or runtime control behavior.

## Handoff Checklist For Another Agent

1. Read `AGENTS.md`.
2. Read this roadmap page.
3. Read `guides/ao-guide.md` and `guides/so-guide.md`.
4. Check `git status` and scope the next slice explicitly.
5. Keep the next slice small enough for evidence-based review.
6. Before moving beyond that slice, complete a review, validation, and commit loop supported by the execution environment or active agent.

## Important Do-Not-Regress Rules

- Keep AO and SO independent in packaging, invocation, and mental model.
- Keep `Abstractions` and `Common` free of private cloud/AI product assumptions.
- Keep workflow file and CLI sidecar contracts explicit and machine-first.
- Keep public docs bilingual and mirrored by path.
