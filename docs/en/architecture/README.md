# Architecture

[中文](../../zh-cn/architecture/README.md)

Techne Loom is built as a package-first mono-repo with a deliberate product split.

This section is the handoff-grade architecture source for the public repository direction. Another agent should be able to continue work from these pages without relying on hidden conversation state.

## Architecture Map

- `package-layout.md` explains the cross-ecosystem package matrix.
- `workflow-model.md` defines the shared workflow vocabulary.
- `execution-model.md` explains progression, waits, resume, and eventing.
- `cli-and-hosts.md` defines AO and SO host boundaries.
- `json-contract.md` outlines the canonical workflow and control payload direction.
- `implementation-roadmap.md` records the approved multi-slice plan, current repository status, and next recommended slices.

## Source Authority

- Curated workflow-tracking material from an earlier private codebase is useful for extraction and comparison, but it is not the public source of truth.
- The public source of truth is the combination of repository code, tests, and authored docs under `/docs`.
- AO and SO share low-level vocabulary where useful, but they do not share one runtime hierarchy.

## Current Implementation Status

- `.NET` is the only implemented runtime family in v1.
- `Abstractions`, `Common`, and `SkillOrchestrator` have active public code.
- `AgentOrchestrator` is still primarily a documented target and scaffold.
- Node.js and Python roots remain reserved for future aligned packages.

The goal is not to make every product identical. The goal is to keep shared contracts low-level and reusable while preserving separate product identities.
