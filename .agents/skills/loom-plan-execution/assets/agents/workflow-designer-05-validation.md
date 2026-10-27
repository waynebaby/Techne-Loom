## Failure Triage And Incremental Authoring Gate (Required)

Before editing or compiling a workflow, classify the first failure and stop at the first failed layer. Do not repair a later layer while an earlier layer is unproven. The designer must classify failures in this exact ten-layer order:

1. **Runtime/preflight**: verify the exact AO runtime, package closure, startup contract, and fresh guide result. Do not edit workflow JSON when this layer fails.
2. **JSON**: parse the complete candidate as one JSON value with a UTF-8-aware structured parser and reject duplicate keys, malformed escapes, truncated output, and encoding artifacts.
3. **Graph**: verify unique node and transition ids, state groups, source and target references, start/end reachability, and referenced gates.
4. **Enum**: read node `$kind`, `stepKind`, status, command kind, and other allowed values from the supplied schema. Never promote a display name, old value, or report example into a permanent enum.
5. **Expression**: verify the root C# binding, complete `ExpressionDefinition`, boolean result type for guards/success/gates, synchronous source, and approved context reads.
6. **Projection**: verify external `resumeOutputKey`, `requiredInputs`, `outputPath`, explicit `outputBindings`, value types, and the absence of implicit or duplicate payload wrappers.
7. **Dataflow**: prove that every required output family has a reachable concrete producer through `outputPath` or explicit `outputBindings`; declarations alone are not producers.
8. **Gate**: prove that each gate predicate reads its required families, has value semantics, route coverage, and actionable evidence. A condition branch is routing, not a producer.
9. **Ownership**: prove that `AskUser` requests only user-owned input or decisions and that runtime facts, paths, provenance, and blocked recovery use runtime-owned seams.
10. **Semantic**: use same-runtime probes for every nontrivial emitter, projection, artifact, and identity check used by the design. Mark uncovered behavior `unknown`.

At the first failed or required-unknown layer, return its blocker and evidence paths. Repair only that layer, rerun every earlier layer, and do not add new workflow behavior until the current layer is clear. Compile is allowed only after layers 1-9 pass and is a validation checkpoint, not semantic completion.

### Incremental Authoring Preflight (Required)

For each concept slice, update all definitions and references atomically: state, state group, transition, source/target ids, referenced gates, route metadata, output families, and ownership. Never leave a placeholder transition id for a later pass.

Before the next AO compile, run these checks in order against the exact candidate:

- Parse the whole workflow and reject duplicate JSON keys, node ids, and transition ids.
- Verify every `state.groups[].transitionIds` entry resolves to a transition, every `sourceNodeId` resolves to its owning state, every `targetNodeId` resolves to a state, and every referenced gate exists.
- Read every enum from the supplied schema and record the schema path that supplied it.
- Audit every expression as a complete synchronous C# predicate and record its field location, source, result type, and compile feedback reference.
- Build a projection matrix for every external transition and a producer matrix for every required output family and gate. Each row must contain the concrete output path or binding, value semantics, route, and owning seam.
- Treat a transition that only selects the next state as a route, not a producer. A family with no reachable concrete producer fails closed.
- For canonical external projection, resolve `resumeOutputKey` relative to the returned payload, write the value to `outputPath`, and declare explicit bindings for every additional family. Reject implicit wrappers and a single result object being reused as unrelated status, path, hash, provenance, and evidence without explicit projection.
- If a `previous_runnable_reference` entry exists, verify before compile that `previousRunnableReferenceDisposition` is present and contains source version, source path, source hash, copy time, reusable shapes, differences from the current schema and requirements, and rejected or deprecated items.
- Only after these checks pass, run the exact current AO runtime compile and preserve its structured result.

### AO Compile Evidence Parsing

Read the process exit code separately from stdout and stderr. Preserve and parse the structured `<ao_property>` payload when emitted, including `ExpressionCompileFeedback` and dataflow diagnostics. Bind the result to the exact candidate path and SHA-256, the fresh audit root, runtime version, and schema hash. Do not infer success from an empty audit folder, a missing text match, truncated terminal output, or a failed command's stderr.

## Runtime Semantic Evidence Gate (Required)

The schema/demo bundle proves the serialized structure, not every execution meaning. A small demo or a field name is not evidence that a complex emitter works. Before returning a design that relies on an uncovered parameter or emitter combination, obtain a minimal probe from the same AO runtime and preserve its compile/run/resume or inspection evidence.

Apply these runtime-semantic rules literally:

- `StateUpdate` and `MemoryWrite` apply `command.parameters.updates` to context before their success and gate checks. Do not expect `updates` on a `ToolCall` or `ArtifactEmit` to create independent context keys.
- A `ToolCall` writes its result to `outputPath`, then applies explicit `command.parameters.outputBindings`. Use `$result` or `$context:<path>` only where the current runtime contract and probe prove the projection.
- `ArtifactEmit` writes `command.parameters.content` to `command.parameters.path`; its `outputPath` records the artifact path value, not the artifact body. A gate requiring report content must point to a later concrete projection or a verified artifact path with the correct value semantics.
- `satisfiesGateIds` and `publishesOutputFamilies` never create evidence. Each family must be produced by this transition's own `outputPath` or `outputBindings`; a downstream route that only forwards inherited context must not publish the family again.
- External `canonical` projection must be checked with a real resume payload. Confirm that `resumeOutputKey` is read relative to the payload, that the value lands at `outputPath`, and that every additional family lands through an explicit binding without a duplicate wrapper.

A semantic probe is required whenever the candidate uses an external or canonical projection, `StateUpdate`, `MemoryWrite`, `ToolCall`, `McpCall`, `SubagentCall`, `ArtifactEmit`, a gate-consumed output family, a source/copy or case/run identity check, or any behavior not exercised by the same-runtime demo. A behavior is optional only when the candidate does not use it and no guard, gate, terminal route, or output evidence depends on it. Assign every required probe a stable `probeId`; the `semantic` layer in `static-contract-review.json` must link each result to that `probeId` in `semantic-probe-report.json`.

For a full-delivery workflow, compile is only a precondition. Run the exact AO workflow copy and continue with its public run/resume chain until final `Done`, or preserve a runtime-owned blocked/failure record that explains why continuation cannot proceed. A compile-clean template, a guide result, or a blocked payload alone is not execution evidence and is not completion.

The designer dispatch/result must include `runtimeSemanticEvidence` for every nontrivial emitter or projection used by the design: runtime and exact version, probe or workflow file, command chain, inspected context paths, artifact paths, output-family projections, and observed status. If the current demo does not exercise the requested combination, mark that semantic as unknown and stop at the evidence boundary; do not claim that schema presence or compile success proves it.


## Design Evidence Output Contract (Required)

Every design dispatch and result must create or reference three runtime-owned JSON records under `<execution-output-root>/workflow-design/`. Never write these records into a skill bundle or treat them as a second mutable workflow state.

1. `reference-manifest.json` uses schemaVersion `workflow-designer.reference-manifest.v1` and records the bounded inputs, hashes, exact runtime version, generation-set identity, authority roles, previous-runnable-reference disposition, read status, and validation result.
2. `static-contract-review.json` uses schemaVersion `workflow-designer.static-contract-review.v1` and records the ordered ten-layer result, candidate path/hash, schema coverage, expression audit, projection matrix, gate-producer-route matrix, ownership audit, plain-language review, and any gate failure-guidance review. A `passed` verdict requires every required static layer to pass.
3. `semantic-probe-report.json` uses schemaVersion `workflow-designer.semantic-probe-report.v1` and records each required same-runtime fixture, command chain, payload, expected and observed context paths and types, artifact and identity evidence, copy/source hashes, case/run binding, and `passed`/`failed`/`unknown` verdict.

Each result must return a `designEvidence` descriptor for all three records. Every descriptor contains `path`, `sha256`, `schemaVersion`, `verdict`, and exact `runtimeVersion`. A required probe that is `failed` or `unknown` prevents a `ready` result. Optional behavior that is not used by the candidate may remain `unknown`, but it cannot be cited as proof for the candidate.

