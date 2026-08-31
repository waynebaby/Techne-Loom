# Workflow Model

[中文](../../zh-cn/architecture/workflow-model.md) | [Root](../README.md)

The shared workflow model is the language-neutral core that both AO and SO can align on at a low level.

For repo-wide explanatory prose such as **weave out**, **weave back**, **strand**, and **seam**, read [Workflow Terminology](workflow-terminology.md). This page keeps the neutral model terms.

## Core Concepts

- `WorkflowInstance`: the persisted state of one execution.
- `StateNode`: a named node that owns ordered transition groups.
- `TransitionGroup`: a set of transitions evaluated together with one concurrency policy.
- `TransitionBase`: the common metadata for executable, waiting, branching, or placeholder steps.
- `WorkflowHistoryEntry`: the append-only event trail for progression, waits, outputs, and failures.
- Governed workflow identity: `taskType` declares business intent, `workflowKind` declares the route family, `caseId` identifies one business case, and `runId` identifies one execution chain.

## Product Interpretation

- SO executes a fully materialized workflow.
- AO may refine the current workflow over time, but still persists explicit nodes, artifacts, and decisions.
- Shared terms do not imply shared top-level behavior.
- For `templateKind: so-governed-target-skill`, the valid combinations are `skill_enhancement` with `so_self_bootstrap` or `target_skill_enhancement`, and a target-specific task type such as `requirement_generation` or `model_generation` with `target_skill_business`. Compile rejects enhancement/business mismatches, enhancement output families, and enhancement subagents inside target business workflows.
- Checked-in templates may use a `runId` beginning with `template:`. The first fresh materialization or `ReadyToStart` run replaces that marker with a generated `run-<guid>` value. The same `caseId` and `runId` then remain on the external workflow copy through compile, run, resume, audit, and completion evidence.
- In the current public SO runtime, `FirstSuccess` is the fully supported transition-group strategy. `FirstResponse` and `All` remain part of the model surface but are rejected explicitly when multiple ready transitions would require those semantics.

## Design Direction

The public model is meant to preserve the useful parts of the reference task-tracking design without carrying private product dependencies into the open-source core.
