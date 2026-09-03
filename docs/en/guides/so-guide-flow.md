# SkillOrchestrator Flow

[Hub](so-guide.md) | [Reference](so-guide-reference.md) | [Root](../README.md)

Version: 0.3.283-beta
Build: published package 0.3.283-beta

## Purpose

Use this page for the shortest governed execution path through SkillOrchestrator. The fixed `so-guide.md` page is the guide hub. Use [SO Guide Reference](so-guide-reference.md) for complete contracts, governance rules, examples, and anti-patterns.

## Flow

1. Bind the exact SO version from the owning skill's version block and package lock.
2. Restore and validate one complete published SO runtime bundle before downstream work.
3. Run the bare `dotnet so.dll --guide`, parse its JSON result, and read the returned guide.
4. Inspect the target skill's `SKILL.md`, package lock, workflow assets, and current guide deltas when this is an enhancement or re-enhancement.
5. Plan the target-skill inputs, outputs, routes, gates, seams, and evidence.
6. Use the required workflow designer to create or refresh the workflow template. Keep the workflow-owned information in English.
7. Compile the template and review Mermaid, HTML, analysis, and dataflow evidence. Ask for confirmation and repeat the template loop when needed.
8. Run the required review-fix loop, then copy one external runtime workflow instance.
9. Run `dotnet so.dll run` and use `dotnet so.dll resume` for every blocked seam on that same instance until final completion evidence exists.

## Runtime Checklist

- The exact published bundle passes startup and dependency-closure checks.
- The fresh `--guide` result is readable before planning or target-skill edits.
- The checked-in template remains immutable during official execution.
- Runtime copies and audit artifacts stay outside skill folders.
- `compile` is validation only; `run` and `resume` are the official execution path.
- The workflow file uses English for workflow-owned schema and control metadata.
- User and business payload values may keep their source language.

## CLI Quick Reference

```powershell
dotnet so.dll --guide
dotnet so.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
dotnet so.dll run --workflow-file <external-workflow.json> --context-file <context.json> --audit-output <external-audit-root>
dotnet so.dll resume --workflow-file <external-workflow.json> --result-file <result.json>
```

`--guide` and `compile` prepare or validate the route. Only public `run` and `resume` count as official SO workflow execution.

## Blocked Return

Read `current_step_kind`, `skill_hint`, `required_inputs`, `workflow_file`, `event_log_file`, and audit artifact links. For user-owned input, ask only for the declared decision or value. For runtime-owned facts, return structured data through the matching resume path. Preserve the same external workflow copy.

## Target-Skill Completion

A governed target-skill run is not complete at guide refresh, template authoring, compile, or a blocked return. It must have target-skill deliverable changes, review-fix evidence, route and gate evidence, and the same-copy public run/resume chain through final completion.

## Reference Chapters



The reference index is split into focused chapters so callers can load only the needed contract.



- [Contracts](so-guide-reference-contracts.md)

- [Behavior And Responsibilities](so-guide-reference-behavior.md)

- [Governance](so-guide-reference-governance.md)

- [Examples](so-guide-reference-examples.md)

- [Anti-Patterns](so-guide-reference-anti-patterns.md)




## Continue

- [SO Guide Hub](so-guide.md)
- [SO Complete Reference](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)
