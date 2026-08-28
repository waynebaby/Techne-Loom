# Loom Agent Execution Orchestrator Flow

[Hub](ao-guide.md) | [Reference](ao-guide-reference.md) | [Root](../README.md)

Version: 0.3.262
Build: published package 0.3.262

## Purpose

Use this page for the shortest operational path through Loom Agent Execution Orchestrator. The fixed `ao-guide.md` page is the guide hub. Use [AO Guide Reference](ao-guide-reference.md) when a contract, example, or anti-pattern needs full detail.

## Flow

1. Decide whether the request needs business deliverables or only runtime verification.
2. Bind the exact AO version from the owning skill or package source, then derive the package channel when needed.
3. Prepare one valid runtime: use the package resolver and exact bundle, or use the explicit repository debug mode only when requested for repository debugging.
4. Run the bare `dotnet ao.dll --guide`, parse `version`, `docs_root`, and `guide_path`, and read the returned guide.
5. Author or reuse one external workflow instance and keep its session, workflow, and audit paths outside skill folders.
6. Run `dotnet ao.dll compile` against that same external workflow before execution.
7. Run `dotnet ao.dll run` against the same instance. After a blocked return, execute the requested external action and resume the same instance with a structured result.
8. Continue until the AO runtime is complete and any requested business deliverables are verifiable.

## Runtime Checklist

- .NET host or the selected self-contained RID runtime passes startup preflight.
- `ao.dll`, `ao.deps.json`, and `ao.runtimeconfig.json` are present when `.NET CLI mode` is used.
- Package-channel extraction uses ZIP-safe handling on Windows PowerShell 5.1.
- The selected launch descriptor, version, and RID stay unchanged through `--guide`, `compile`, `run`, and `resume`.
- The workflow file is an English information carrier for workflow-owned schema and control metadata.
- User and business payload values may keep their source language.

## CLI Quick Reference

```powershell
dotnet ao.dll --guide
dotnet ao.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
dotnet ao.dll run --objective-file <objective.md> --session-dir <session-dir> --instance-file <external-workflow.json> --audit-output <external-audit-root>
dotnet ao.dll resume --session-dir <session-dir> --session-id <id> --result-file <result.json>
```

`--guide`, `compile`, `prompt-plan`, and `prompt-replan` support preparation or recovery. Only `run` and `resume` are official AO skill runs.

## Blocked Return

Read the structured blocked payload. Preserve `session_id`, `workflow_file`, `workflow_instance_file`, `event_log_file`, `current_node_id`, and the latest transition data. Use `prompt-plan` for the first authored graph and `prompt-replan` only when a later frontier or `tbr` path must be redesigned. Resume with `transition_id`, optional `correlation_key`, and a structured `payload`.

## Roles

- **Caller:** supplies the objective, performs external actions, preserves session continuity, and resumes with structured data.
- **Author:** keeps the workflow graph, control fields, evidence paths, and ownership explicit.
- **Outer agent:** evaluates the proposed frontier and preserves context across blocked returns.

## Reference Chapters



The reference index is split into focused chapters so callers can load only the needed contract.



- [Contracts](ao-guide-reference-contracts.md)

- [Plan And Replan](ao-guide-reference-plan-replan.md)

- [Behavior And Responsibilities](ao-guide-reference-behavior.md)

- [Examples](ao-guide-reference-examples.md)

- [Anti-Patterns](ao-guide-reference-anti-patterns.md)




## Continue

- [AO Guide Hub](ao-guide.md)
- [AO Complete Reference](ao-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)
