## Deterministic Transition Contract (Required)

### Expression Producer Contract (Required)

Every workflow-designer dispatch must receive and preserve one root expression contract. The machine-readable dispatch payload must include `runtimeBinding`, `expressionBinding.language`, `languageVersion`, `contractId`, `contractVersion`, `requiredExpressionCapabilities`, `compileFeedbackContract`, and `allowedExpressionForms`. The current supported combination is `dotnet-so` with C# and `detailedCompileFeedbackV1`.

Allowed C# forms are `predicate`, `lambda`, and `method`; each generated `ExpressionDefinition` must include `kind`, `source`, `entryPoint`, and `resultType`, with boolean result type for guards, success predicates, and gate pass expressions. The designer must use the read-only contract API (`context.Get<T>("path")` or an equivalent approved context read) and must not emit legacy non-C# syntax, implicit bare context identifiers, or per-node language overrides. Before writing expressions, read the owning contract's versioned `context_fields` catalog; declare every path, match each `Get<T>` type and output projection to its JSON type, and use `context.Has(path)` only for presence checks.

The dispatch must also include the contract code fragment used to author expressions. `skillHint` may explain the skill being enhanced context, but it is not a substitute for these machine-readable fields. Every expression compile response must preserve `ExpressionCompileFeedback` fields and the `detailedCompileFeedbackV1` contract; raw compiler text alone is insufficient.

### Gate Failure Guidance Hard Gate (Required)

Every `validation.gates` entry is incomplete unless it contains a `failureGuidance` object with `summary`, an immediately executable `nextAction`, and one or more verified `evidenceReferences`. Each evidence reference must contain a target-relative `path`, 1-based inclusive `startLine`, 1-based inclusive `endLine`, and an exact `quote` copied from that file.

The references must point to the specific contract, guide, target `SKILL.md`, workflow source, or artifact instruction that explains how to repair this gate. Absolute paths, estimated line numbers, directory-only citations, bare filenames, and paraphrases in place of quoted source are invalid. A failed gate must leave the next agent with enough information to act without rediscovering the governing rule.

Before returning a workflow template or dispatch success, enumerate every key under `validation.gates` and self-review every gate. Verify the summary, nextAction, and evidence references; reread each cited file; verify that every line range is 1-based and that the exact quote occurs within that range; and verify that the guidance is specific to the gate's required outputs and route. Emit a machine-readable `gate_failure_guidance_review` listing every reviewed gate and its verification status. If any gate fails this self-review, reject the template as incomplete and do not weave it back as successful evidence.

For every transition, require an operator-executable contract instead of descriptive prose.

Each transition must include:

- `id`, source node, and `targetNodeId`
- `stepKind` aligned to real runtime behavior (`ModelThink`, `SubagentCall`, `AskUser`, `WaitResume`, `ToolCall`, or current equivalent)
- concrete `guardExpression` over named context fields (boolean predicate only; no natural-language guards)
- structured `passExpression` over produced output fields, declared as a concrete boolean predicate
- `outputPath` and explicit produced evidence keys/artifact paths
- explicit seam-ownership declaration for required inputs (`user-owned` vs `runtime-owned`)
- explicit fallback transition or blocked seam when success predicates fail

Reject transitions that contain only verbs such as "analyze", "review", "handle", or "continue" without predicates, ownership, and evidence outputs.

## Deterministic Gate Contract (Required)

For `validation.gates` and route gate usage, require machine-checkable criteria.

Every gate must define:

- gate id and applicability class (`terminal`, `blocked-strongest-earned`, or `intermediate`) recorded in the `gate-producer-route matrix` as design evidence; do not add classification fields to workflow JSON unless the exact runtime schema declares them
- explicit pass predicates over context keys and/or artifact existence
- required evidence references (artifact path and/or payload field path)
- missing-data ownership route (`AskUser` only for user-owned fields; runtime facts/artifact paths must use runtime-owned seams)
- runtime-supported route mapping: map terminal gates to `requiredTerminalGateIds` and blocked gates to `requiredBlockedGateIds` on applicable `validation.routes`; map intermediate gates to their stage and producer transitions in the `gate-producer-route matrix` without adding them to the terminal/blocked route lists
- `failureGuidance` satisfying the Gate Failure Guidance Hard Gate above

Reject gate definitions that only state generic outcomes like "approved", "validated", or "complete" without predicates and evidence.

