# Techne Loom Documentation

[中文](../zh-cn/README.md)

This is the English entry for the public documentation set.

## Start Here

- [Getting Started](getting-started/README.md)
- [Architecture](architecture/README.md)
- [Guides](guides/README.md)
- [Reference](reference/README.md)
- [Examples](examples/README.md)

## If You Are Continuing Implementation

- Read [Implementation Roadmap](architecture/implementation-roadmap.md) first.
- Treat [AgentOrchestrator Guide Source](reference/products/ao-guide.md) and [SkillOrchestrator Guide Source](reference/products/so-guide.md) as the current product-contract handoff docs.
- Use [Architecture](architecture/README.md) and [Reference](reference/README.md) as the authority for current public boundaries.

## Current Baseline

- `Techne.Loom.Abstractions`, `Techne.Loom.Common`, and `Techne.Loom.SkillOrchestrator` now have public `.NET` slices in the repo.
- `Techne.Loom.AgentOrchestrator` remains a documented target with only scaffold-level code.
- Node.js and Python roots remain reserved for later aligned ports.

## Product Guides

- [AgentOrchestrator Guide Source](reference/products/ao-guide.md)
- [SkillOrchestrator Guide Source](reference/products/so-guide.md)

## Documentation Rules

- Every paired page links to its counterpart at the top.
- English and Chinese trees keep mirrored relative paths.
- Root governance files stay at the repository root, while long-form content stays in `/docs`.
