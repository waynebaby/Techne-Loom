# SkillOrchestrator Guide

[Root](../README.md)

Version: 0.3.270
Build: published package 0.3.270

## Guide Output

Run the bare `dotnet so.dll --guide` command. It returns JSON with the actual `version`, `docs_root`, and `guide_path` paths for the version-matched English guide.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Information Hub

This fixed `guide_path` entry is intentionally short. Read it first, then follow the linked flow for governed execution and the reference for complete contracts, governance rules, and examples.

- [SO Flow](so-guide-flow.md)
- [SO Complete Reference](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)

## Product Role

SkillOrchestrator executes deterministic workflow steps and returns only when the workflow completes or reaches a seam that requires external participation. It is the official execution authority for Loom-governanced target skills.

## Core Flow

1. Bind the exact SO version and restore one complete published runtime bundle.
2. Run the bare `dotnet so.dll --guide` and read the returned guide path.
3. Inspect the target skill and plan its inputs, outputs, routes, gates, seams, and evidence.
4. Use the required workflow designer to create or refresh the template.
5. Compile, review, and confirm the template and its audit artifacts.
6. Copy one external runtime workflow instance, then run and resume that same instance until final completion evidence exists.

## Official Surface

- `dotnet so.dll run` and `dotnet so.dll resume` are the official SO workflow runs.
- `--guide`, `compile`, `status`, and inspection commands support preparation or validation.
- A guide refresh, template authoring, compile result, or blocked return is not governed completion by itself.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language.

## Source Boundaries

The complete governed flow, target-skill rules, contracts, examples, and anti-patterns live in [SO Complete Reference](so-guide-reference.md). The hub path remains stable for `guide_path`; the documentation bundle carries the linked flow and reference pages.