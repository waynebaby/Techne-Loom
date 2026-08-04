# Loom Plan Execution Governance Enhancement Plan

## Scope

This plan governs the Phase B target where `/loom-plan-execution` is enhanced through Loom Skill Orchestrator governance.

## Goal

Upgrade AO skill governance assets so workflow-authoring quality is deterministic, auditable, and runnable through SO-governed routes.

## Runtime Entry Gate

1. Reacquire the bound published SO runtime bundle.
2. Prove runtime preflight success.
3. Capture a fresh `dotnet so.dll --guide` result before downstream edits.

## Compile-Review Stage

1. Analyze AO `SKILL.md` governance wording.
2. Analyze AO workflow-designer transition/gate contract quality.
3. Refresh `so-template.json` with explicit validation gates, seams, and route evidence.
4. Compile template and collect Mermaid, HTML, and workflow analysis artifacts.
5. Review findings and apply revisions until compile-review approval is reached.

## Official Runnable Stage

1. Materialize an external runtime workflow copy.
2. Execute public `dotnet so.dll run`.
3. If blocked, continue with matching public `dotnet so.dll resume` until final `Done`.
4. Emit completion evidence that references checked-in deliverables without replacing them.

## Completion Criteria

- Compile-only is insufficient.
- Official governed evidence requires public `run` and conditional `resume` to final `Done`.
- Checked-in source deliverables and runtime-owned evidence remain explicitly separated.
