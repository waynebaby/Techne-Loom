# Loom Agent Execution Orchestrator Guide

[Root](../README.md)

Version: 0.3.283-beta
Build: published package 0.3.283-beta

## Guide Output

Run the bare `dotnet ao.dll --guide` command. It returns JSON with the actual `version`, `docs_root`, and `guide_path` paths for the version-matched English guide.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Information Hub

This fixed `guide_path` entry is intentionally short. Read it first, then follow the linked flow for execution and the reference for complete contracts and examples.

- [AO Flow](ao-guide-flow.md)
- [AO Complete Reference](ao-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)

## Product Role

Loom Agent Execution Orchestrator coordinates exploratory work under uncertainty. It keeps workflow state, returns structured blocked control data at external seams, and continues through structured resume results.

## Core Flow

1. Bind the exact AO version and prepare a valid published runtime.
2. Run the bare `dotnet ao.dll --guide` and read the returned guide path.
3. Author or reuse one external workflow instance and keep runtime state and audit output outside skill folders.
4. Compile that same external workflow, then run it.
5. After a blocked return, perform the required external action and resume the same instance with structured data.
6. Stop only when the runtime is complete and requested business deliverables are verifiable.

## Official Surface

- `dotnet ao.dll run` and `dotnet ao.dll resume` are the official AO skill runs.
- `--guide`, `compile`, `prompt-plan`, and `prompt-replan` support preparation or recovery.
- The selected version, launch descriptor, and workflow instance must remain stable across the run/resume chain.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language.

## Source Boundaries

The complete operational flow, contracts, responsibilities, examples, and anti-patterns live in [AO Complete Reference](ao-guide-reference.md). The hub path remains stable for `guide_path`; the documentation bundle carries the linked flow and reference pages.