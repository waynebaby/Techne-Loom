# Architecture

[中文](../../zh-cn/architecture/README.md) | [Root](../README.md)

Techne Loom is built as a package-first mono-repo with a deliberate product split.

This section is the handoff-grade architecture source for the public repository direction. Another agent should be able to continue work from these pages without relying on hidden conversation state.

## Architecture Map

- `workflow-terminology.md` defines the repo-wide workflow vocabulary root.
- `package-layout.md` explains the cross-ecosystem package matrix.
- `workflow-model.md` defines the shared neutral workflow model terms and schema semantics.
- `execution-model.md` explains progression, waits, resume, and eventing.
- `skill-interoperability.md` explains the Agent Skills interoperability layer, current evidence, and product boundary.
- `cli-and-hosts.md` defines AO and SO host surfaces.
- `json-contract.md` outlines the canonical workflow and control payload direction.
- `contract-context-reference.md` defines B+ contract binding, bounded fragment injection, runtime metadata, and manual-edit behavior.
- `implementation-roadmap.md` records the current foundation and next interoperability slices.

## Source Authority

- Historical workflow-tracking material may inform extraction and comparison, but repository code, tests, and authored docs are the public source of truth.
- The public source of truth is the combination of repository code, tests, and authored docs under `/docs`.
- AO and SO share low-level vocabulary where useful, but they do not share one runtime hierarchy.

## Current Implementation Status

- `.NET` is the only implemented runtime family in v1.
- `Abstractions`, `Common`, and `SkillOrchestrator` have active public code.
- `AgentOrchestrator` now has an implemented public `.NET` runtime slice.
- Node.js and Python roots remain reserved for future aligned packages.

The goal is not to make every product identical. The goal is to keep shared contracts low-level and reusable while preserving separate product identities.
