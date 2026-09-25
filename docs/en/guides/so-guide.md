# SkillOrchestrator Guide

[中文](../../zh-cn/guides/so-guide.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.323-beta
Build: published package 0.3.323-beta
<!-- guide-version:end -->





## Guide Output

Run `so.exe --guide` on Windows or `so --guide` on Unix. It returns JSON with the actual `version`, `docs_root`, and `guide_path` for the version-matched English guide.

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Information Hub

This fixed `guide_path` entry is intentionally short. Read it first, then follow the linked flow for governed execution and the reference for complete contracts, governance rules, examples, and anti-patterns.

- [SO Flow](so-guide-flow.md)
- [SO Complete Reference](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)

## Product Role

SkillOrchestrator executes deterministic workflow steps and returns only when the workflow completes or reaches a seam that requires external participation. It is the execution authority for skills under Loom Skill Orchestrator governance.

## Core Flow

1. Bind the exact SO version and acquire the self-contained package for one supported RID.
2. Run `so.exe --guide` on Windows or `so --guide` on Unix and read the returned guide path.
3. Inspect the skill being enhanced and plan its inputs, outputs, routes, gates, seams, and evidence.
4. Use the required workflow designer to create or refresh the template.
5. Compile, review, and confirm the template and its audit artifacts.
6. Copy one external runtime workflow instance, then run and resume that same instance until final completion evidence exists.

## Official Surface

- `so.exe run` / `so.exe resume` on Windows and `so run` / `so resume` on Unix are the official SO workflow runs.
- `--guide`, `compile`, `status`, and inspection commands support preparation or validation.
- A guide refresh, template authoring, compile result, or blocked return is not governed completion by itself.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills under Loom Skill Orchestrator governance. Keep workflow-owned schema and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language.

## Source Boundaries

The complete governed flow, rules for the skill being enhanced, contracts, examples, and anti-patterns live in [SO Complete Reference](so-guide-reference.md). The hub path remains stable for `guide_path`; the documentation bundle carries the linked flow and reference pages.
