# Released 0.3.282 Demo

[中文](Readme.zh-CN.md) | [Demo Index](../README.md) | [Repository Root](../../../README.md)

> [!IMPORTANT]
> This is the current released demo snapshot for `loom-enhanced-research`. It is a migrated target-skill copy, not a historical timeline. Its workflow authority is checked against the exact released Skill Orchestrator 0.3.282 contract.

## At A Glance

| Area | Summary |
| --- | --- |
| Goal | Carry the research skill from its prior governed snapshot onto the released 0.3.282 runtime contract |
| Runtime | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` at exact `0.3.282` |
| Main outcome | Emitter-aware workflow, MCP-first entry, canonical resume projections, and migration tooling |
| Completion rule | The same external workflow copy must pass public `run` and `resume` until final `Done` |

## Migration Record

1. The prior governed target-skill sample was migrated into this release-scoped directory.
2. The workflow identity was changed to target business task `research_generation` with workflow kind `target_skill_business`.
3. The entry path was rebuilt around exact runtime preflight, local stdio MCP, bounded fragment inspection, and fresh guide capture.
4. Plain `ToolCall/noop` literal updates were removed from the producer contract; literal writes use `StateUpdate` semantics.
5. External results use canonical top-level `result` projection, with required sibling fields kept at the payload top level.
6. The four migration tools record dry-run candidates, hashes, ambiguity findings, rollback points, producer audits, and idempotence.

## Inspect

- [loom-enhanced-research/SKILL.md](loom-enhanced-research/SKILL.md)
- [loom-enhanced-research/contract.json](loom-enhanced-research/contract.json)
- [loom-enhanced-research/assets/so-workflow/so-template.json](loom-enhanced-research/assets/so-workflow/so-template.json)
- [loom-enhanced-research/assets/so-workflow/so-package-lock.json](loom-enhanced-research/assets/so-workflow/so-package-lock.json)
- [loom-enhanced-research/assets/so-workflow/reference/runtime-semantic-migration.md](loom-enhanced-research/assets/so-workflow/reference/runtime-semantic-migration.md)
- [loom-enhanced-research/assets/so-workflow/reference/migration-script-playbook.md](loom-enhanced-research/assets/so-workflow/reference/migration-script-playbook.md)
- [loom-enhanced-research/assets/so-workflow/scripts](loom-enhanced-research/assets/so-workflow/scripts/)

## Verification Shape

The release demo is considered valid only when all of these are available:

- exact 0.3.282 guide and runtime evidence;
- MCP startup evidence for the same external workflow copy;
- compile-clean workflow and readable analysis/dataflow artifacts;
- non-empty research, review, draft, migration, and decision evidence;
- one unchanged workflow file across public `run` and every `resume`;
- a terminal business-output gate and final `Done` state.

For the operational contract, use [the SO guide](../../../docs/en/guides/so-guide.md) and [the migration reference](loom-enhanced-research/assets/so-workflow/reference/runtime-semantic-migration.md).
