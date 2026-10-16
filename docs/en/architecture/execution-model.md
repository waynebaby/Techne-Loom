# Execution Model

[中文](../../zh-cn/architecture/execution-model.md) | [Root](../README.md)

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
- Every fan-out resume, successful or failed, must include the pending attempt's `wait_id` from the persisted workflow's `activeWaitGroups`; stale IDs are rejected. `execution_error` reports a failed external attempt without a non-empty result payload and is valid only for fan-out.
- Each retry creates a fresh `wait_id`. Retry counts survive wait ticks and reset only when a new batch starts. An exhausted branch error is routed after the strategy's required sibling results settle.
- When a winner or early join completes, the runtime persists cancellation signals for pending sibling waits. `cancelLosers` controls whether physical cancellation is requested; late results with the exact canceled `wait_id` are ignored without mutation, while unknown or stale IDs are rejected.
- Expired waits produce history entries and deterministic follow-up behavior.
- In the current public SO runtime, timeout fallback can move execution to an explicit timeout target state when one is authored.

## Product-Specific Differences

- AO weaves out at major control seams so a top agent can decide the next move, and the protocol layer surfaces that seam as a blocked payload with explicit boundary fields.
- SO runs until it weaves out for external participation or reaches a terminal state.
- SO-owned memory reads and writes update workflow context directly and feed `memory_for_next_step`.

## Current Public Runtime Behavior

- `firstPass` evaluates guarded transitions sequentially by priority. A false guard skips to the next eligible transition; a command result that fails `passExpression` fails that transition and does not fall through. `noPassTargetStateId` is not supported for `firstPass`.
- `whenAnyResponse`, `whenAnyPass`, `whenAllPass`, and `whenExpression` start only external steps concurrently; compile rejects synchronous fan-out members and fan-out branches that declare `contractRefs`.
- `whenAnyResponse` selects the first returned branch regardless of its pass predicate. `whenAnyPass` selects the first passing branch even if a sibling exhausted retries; if none pass, it waits for every branch and routes exhausted errors before `noPassTargetStateId`.
- `whenAllPass` waits for every branch, routes exhausted execution errors first, routes no-pass next, and joins only when every branch passes.
- `whenExpression` joins early when its predicate becomes true over completed outputs. Exhausted errors wait for all expected branches; compile rejects statically overlapping `outputPath`/`outputBindings`, and runtime preflights before wait registration without publishing partial context.
- Legacy wire values `firstSuccess`, `firstResponse`, and `all` require explicit migration to their canonical strategy values; they are not silently upgraded.
- An expired fan-out wait uses `timeoutTargetStateId` or the nested `timeoutTransition.targetNodeId` when either points to a valid state.
- No-progress SO runs are treated as an explicit blocked condition rather than a silent success path.
- `memory_for_next_step` already avoids whole-context fallback when no memory-oriented keys are present.
