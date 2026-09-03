---
name: loom-enhanced-research
description: Bounded research with material review, draft review, and a released 0.3.282 Loom-governanced workflow.
---

# /loom-enhanced-research

This released demo is a current, migrated target-skill snapshot for the exact Skill Orchestrator package version 0.3.282.

## Runtime Contract

- Read `assets/so-workflow/so-package-lock.json` before acquisition. The bound package is released 0.3.282.
- Use the resolver-selected exact-RID self-contained runtime by default. The explicit .NET CLI mode is a separate path and never a fallback after startup.
- Prove the selected runtime with a fresh `--guide` result, then use local stdio MCP and `so_inspect_workflow_fragment` against the same external workflow copy before downstream work.
- Run and resume only against a fresh external copy of `assets/so-workflow/so-template.json`. Keep the copy, event sidecar, Mermaid, HTML, and audit files outside this skill folder.

## 0.3.282 Semantic Rules

- Literal `updates` on plain `ToolCall` or `noop` do not write context. Use `StateUpdate` or `MemoryWrite` for literal writes.
- Treat `$result` by emitter: known-null and unproven emitters are not producers; real tools and proven external projections may use it.
- A declared output family is not evidence. A same-transition self-binding is not a prior producer, and a branch back edge is not a first-arrival producer.
- Before migrating repeated shapes, run the local dry-scan tools under `assets/so-workflow/scripts`, preserve source hashes, inspect candidates, validate with the exact runtime, and run the idempotence check.

## Workflow

1. Prove the exact published runtime, MCP fragment inspection, and fresh guide.
2. Collect research intake and initialize the external run artifacts.
3. Execute bounded research rounds that create evidence, then assemble and review the material inventory.
4. Generate a draft from existing evidence, collect draft review, and follow the selected continuation.
5. Publish the final report and completion manifest only after the terminal business-output gate passes.

AskUser seams request user-owned fields only. Runtime-owned paths, provenance, and audit evidence return through external runtime seams. A blocked result is a request for the next structured input, not completion.

## Files

- `assets/so-workflow/so-template.json` is the workflow authority.
- `assets/so-workflow/node-to-file-map.md` maps nodes to checked-in files and runtime evidence.
- `assets/so-workflow/reference/runtime-semantic-migration.md` records the 0.3.282 semantic matrix.
- `assets/so-workflow/reference/migration-script-playbook.md` defines dry-run, candidate, hash, rollback, and idempotence rules.
- `assets/so-workflow/scripts` contains the four migration entry points.
