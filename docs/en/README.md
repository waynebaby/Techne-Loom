# Techne Loom Documentation

[中文](../zh-cn/README.md) | [Root](README.md)

This is the English entry for the public documentation set.

## Start Here

- [Getting Started](getting-started/README.md)
- [Architecture](architecture/README.md)
- [Skill Interoperability](architecture/skill-interoperability.md)
- [Workflow Terminology](architecture/workflow-terminology.md)
- [Guides](guides/README.md)
- [Reference](reference/README.md)
- [Examples](examples/README.md)
- [Articles & Blogs](articles/README.md)

## If You Are Continuing Implementation

- Read [Implementation Roadmap](architecture/implementation-roadmap.md) first.
- Read [Workflow Terminology](architecture/workflow-terminology.md) before rewriting workflow-explanation docs.
- Treat the [Loom Agent Plan-Execution Orchestrator Guide](guides/ao-guide.md) and [Loom Skill Orchestrator Guide](guides/so-guide.md) as module-specific contract references for the unified `loom` runtime.
- Use [Architecture](architecture/README.md) and [Reference](reference/README.md) as the authority for current public contract surfaces.

## Current Baseline

- The active .NET release publishes eight self-contained `Techne.Loom.Runtime.<rid>` packages and the no-RID `Techne.Loom.Abstractions` SDK at one exact version and channel.
- `Techne.Loom.AgentOrchestrator` and `Techne.Loom.SkillOrchestrator` remain independent implementation modules exposed through `loom agent` and `loom skill`.
- Node.js and Python roots remain reserved for later aligned ports.

## Product Guides

- [Using Techne Loom Skills](guides/skill-usage.md)
- [Loom Agent Plan-Execution Orchestrator Guide Source](guides/ao-guide.md)
- [Loom Skill Orchestrator Guide](guides/so-guide.md)
- [Featured Example: Skill Under Loom Skill Orchestrator Governance Run](examples/so-enhanced-skill-run.md)

## Documentation Rules

- Every paired page links to its counterpart at the top.
- English and Chinese trees keep mirrored relative paths.
- Root governance files stay at the repository root, while long-form content stays in `/docs`.
