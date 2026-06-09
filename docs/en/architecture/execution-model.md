# Execution Model

[中文](../../zh-cn/architecture/execution-model.md)

Execution is centered on explicit workflow state, append-only history, and clear ownership seams.

Repo-wide explanatory terms such as **weave out**, **weave back**, **strand**, and **seam** are defined in [Workflow Terminology](workflow-terminology.md).

## Shared Runtime Semantics

- Start by materializing or loading a workflow instance.
- Advance from the current node through ordered transition groups.
- Record history for state changes, outputs, waits, expirations, and failures.
- Persist context updates instead of hiding state in prompts.

## AO Versus SO

- AO is decision-first: it should weave out at meaningful control seams so a top-level agent can decide the next move.
- SO is execution-first: it should continue through SO-owned deterministic steps until it reaches a terminal state or an external-participation seam.
- AO is allowed to rewrite the current workflow under uncertainty.
- SO should run only on fully materialized workflow state.

## Step Taxonomy

The public model already carries the step kinds that future AO/SO-compatible runtimes are expected to understand:

- `ModelThink`
- `ToolCall`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `ConditionBranch`
- `WaitResume`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

## Wait And Resume

- Waiting is a first-class state, not an implicit retry loop. A runtime that has woven out stays explicitly parked until a valid weave back arrives.
- Resume uses structured external input rather than freeform narrative. That structured return path is the weave-back surface.
- Expired waits produce history entries and deterministic follow-up behavior.
- In the current public SO runtime, timeout fallback can move execution to an explicit timeout target state when one is authored.

## Product-Specific Differences

- AO weaves out at major control seams so a top agent can decide the next move, and the protocol layer surfaces that seam as a blocked payload with explicit boundary fields.
- SO runs until it weaves out for external participation or reaches a terminal state.
- SO-owned memory reads and writes update workflow context directly and feed `memory_for_next_step`.

## Current Public Runtime Limits

- `FirstSuccess` is the fully supported transition-group strategy in the current public SO runtime.
- `FirstResponse` and `All` remain model-level values, but the current public runtime fails explicitly when multiple ready transitions would require those semantics.
- No-progress SO runs are treated as an explicit blocked condition rather than a silent success path.
- `memory_for_next_step` already avoids whole-context fallback when no memory-oriented keys are present.
