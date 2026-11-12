## Deterministic Transition Contract (Required)

For every transition you design, specify enough detail that an operator can execute it without guessing.

Each transition must define all of these in the workflow JSON or in adjacent design notes:

- `id`, `source node`, and `targetNodeId`
- `stepKind` that matches the real execution surface (`ModelThink`, `ToolCall`, `SubagentCall`, `AskUser`, `WaitResume`, or equivalent current runtime kind)
- `guardExpression` written as a concrete boolean predicate over named context fields; avoid natural-language guards
- Every transition must explicitly declare a synchronous boolean `passExpression`; output-producing transitions test their actual produced output, while a route-only `ConditionBranch` may use a route-success predicate such as `true` without claiming evidence.
- Declare `outputPath` only when the transition actually produces a value to project or pass downstream; route-only `ConditionBranch` transitions may omit it and must not invent output/evidence paths.
- explicit input ownership: which required inputs are user-owned vs runtime-owned
- explicit produced evidence (artifact path, payload key, or both) only when the transition actually produces evidence; a route-only transition is not a producer
- explicit failure seam: where control moves when success conditions are not met

Reject any transition that uses vague descriptions like "analyze", "handle", "process", or "continue" without concrete guard/success predicates; require concrete output evidence only for transitions that actually produce or publish it.

## Canonical Resume Envelope (Required)

Before every resume, inspect the live wait's `requiredInputs`, the owning `context_fields` definitions, `resumeOutputKey`, `outputPath`, and `outputBindings`. Submit missing caller-owned inputs and the newly produced result required by the projection; do not fabricate or duplicate runtime-owned values already present in the same workflow context. Keep required sibling fields at the top level of `payload`; place only the declared projected value under `resumeOutputKey`.

Illustrative envelope only: these payload keys are valid only if the current wait declares them. Use the live contract for correlation nullability, identifiers, field placement, and required new results; never copy this sample blindly. Omit caller-owned inputs already satisfied in context unless the live contract explicitly requires a new value.

```json
{
  "transition_id": "transition.review",
  "correlation_key": null,
  "payload": {
    "target_scope": "...",
    "result": { "status": "complete" }
  }
}
```

For fan-out include the matching uncompleted `wait_id` from the persisted workflow, and refresh it for each retry; the example above is a non-fan-out result. Validate the complete envelope and field types locally before calling resume, preserve the same workflow/case/run lineage, then query persisted status and events. A command's nonzero exit does not by itself establish the resulting workflow state.

## Gate Failure Guidance Review (Required When Gates Exist)

When the workflow contains gates, every gate must include `failureGuidance.summary`, an immediately executable `failureGuidance.nextAction`, and one or more verified `evidenceReferences`. Each reference must use a target-relative or runtime-output-relative path, 1-based inclusive `startLine` and `endLine`, and an exact quote copied from the cited file. Before returning success, enumerate every gate id, reread each cited file, verify the quote is within the stated range, and emit `gate_failure_guidance_review` with one result per gate. If any review result is missing or failed, reject the candidate and do not return it as ready.

## Expression Producer Contract (Required)

When a workflow uses guards, success predicates, or gate predicates, preserve one root expression binding from the exact runtime. Each expression must be a complete synchronous `ExpressionDefinition` with `kind`, `source`, `entryPoint`, and `resultType`; boolean predicates must declare `resultType: bool`; context reads must use the approved read-only API such as `context.Get<T>("path")`; and per-node language overrides are forbidden. Before writing expressions, read the owning contract's `context_fields` catalog, declare every path, match each `Get<T>` type and output projection to its JSON type, and use `context.Has(path)` only for presence checks. Preserve structured `ExpressionCompileFeedback` in the static review and do not treat raw compiler text or a schema field as proof of semantic behavior.

## Gate Contract (Required)

When the workflow uses gates, each gate must be auditable and machine-checkable.

For every gate, define:

- `gate id` and gate type (`terminal` or `blocked-strongest-earned`)
- pass criteria as explicit predicates over context keys and/or required artifact existence
- required evidence references (exact artifact path, payload field path, or both)
- owning seam for missing data (`AskUser` only for user-owned fields; runtime facts must route to runtime-owned seams)
- route coverage mapping showing which routes require this gate

Reject gates that only say "reviewed", "done", or "approved" without explicit evidence shape and pass predicates.
