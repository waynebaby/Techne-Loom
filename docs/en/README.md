# Techne Loom Documentation

[中文](../zh-cn/README.md) | [Root](README.md)

This is the English entry for the public documentation set.

## Start Here

- [Getting Started](getting-started/README.md)
- [Architecture](architecture/README.md)
- [Workflow Terminology](architecture/workflow-terminology.md)
- [Guides](guides/README.md)
- [Reference](reference/README.md)
- [Examples](examples/README.md)

## If You Are Continuing Implementation

- Read [Implementation Roadmap](architecture/implementation-roadmap.md) first.
- Read [Workflow Terminology](architecture/workflow-terminology.md) before rewriting workflow-explanation docs.
- Treat [Loom Agent Execution Orchestrator Guide Source](guides/ao-guide.md) and [SkillOrchestrator Guide Source](guides/so-guide.md) as the current product-contract handoff docs.
- Use [Architecture](architecture/README.md) and [Reference](reference/README.md) as the authority for current public contract surfaces.

## Current Baseline

- `Techne.Loom.Abstractions`, `Techne.Loom.Common`, and `Techne.Loom.SkillOrchestrator` now have public `.NET` slices in the repo.
- `Techne.Loom.AgentOrchestrator` now has a public `.NET` runtime slice in the repo.
- Node.js and Python roots remain reserved for later aligned ports.

## Product Guides

- [Using Techne Loom Skills](guides/skill-usage.md)
- [Loom Agent Execution Orchestrator Guide Source](guides/ao-guide.md)
- [SkillOrchestrator Guide Source](guides/so-guide.md)
- [Featured Example: Loom-Governanced Skill Run](examples/so-enhanced-skill-run.md)

## Documentation Rules

- Every paired page links to its counterpart at the top.
- English and Chinese trees keep mirrored relative paths.
- Root governance files stay at the repository root, while long-form content stays in `/docs`.
