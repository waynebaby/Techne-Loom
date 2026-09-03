# Runtime Semantic Migration Reference

This reference records the released SkillOrchestrator 0.3.282 behavior that controls enhancement migrations. It is an offline skill reference. The fresh guide from the selected published runtime remains the authority when a later runtime changes the contract.

## Version Binding

- Runtime: released `Techne.Loom.SkillOrchestrator.Runtime.<rid>` at exact version `0.3.282`.
- Default mode: resolver-selected exact-RID self-contained executable.
- Windows entry point used by the probe: `so.exe`.
- The probe must run `--guide` successfully before a workflow is compiled or executed.
- The probe output, workflow copies, events, and audit files belong under the external execution output root, not in this skill bundle.

## Observed Semantic Matrix

| ID | Emitter or projection | 0.3.282 observation | Migration rule |
| --- | --- | --- | --- |
| D1 | Plain `ToolCall` or `noop` with literal `parameters.updates` | The updates map is not written to workflow context. `noop` returns null. | Do not use this shape as a context producer. |
| D2 | `StateUpdate` or `MemoryWrite` with declared updates | Dotted update keys are written to context. | Use this shape for literal context writes. |
| D3 | `state.update` or `memory.write` `$result` | The result is the value at the transition's own output path after updates are applied, when that path is covered by the updates map. | `$result` is valid only after the exact-version probe proves the path and type. |
| D4 | External resume with canonical projection | `resumeOutputKey: result` extracts the top-level `result` value. Required-input siblings remain top-level context values. | Keep required inputs as sibling fields and project explicitly; never add a duplicate `result` wrapper. |
| D5 | Real `echo` or `write-file` result | The built-in result is non-null and can be projected through `$result`. | Classify real tools by actual supported command and required parameters, not by a non-empty command name. |

A compile result is structural evidence only. The inherited and replacement fixtures must both be compiled and run on the exact runtime. The replacement must reach final `Done` with the expected non-empty context values.

## Emitter-Aware Producer Rules

A published output family is a declaration, not evidence. A governed dataflow check may accept a family only when a concrete value can reach the family on the current route.

- Known-null `noop` emitters never produce a family through `outputPath`, literal `updates`, or `$result`.
- A `ToolCall` with literal `updates` is not a literal writer. Its updates are inert in 0.3.282.
- `StateUpdate` and `MemoryWrite` produce only keys covered by their declared `parameters.updates`. An output path outside that map is not proven.
- `MemoryRead` has its own runtime result contract and must be validated separately.
- Real built-in tools are legitimate only when the command is known and its required parameters prove that a non-null result is possible. Unknown tools and known-null invocations remain unresolved.
- External resume results are legitimate only through the declared payload and canonical projection contract.
- Do not globally reject `$result`. Decide it from the emitter and the exact-version probe.
- A transition must not use its own `outputPath` or a same-transition `$context:<family>` binding to prove a family that it publishes. Self-binding is not a prior producer.

## gov4 Reachability Probe

The required gov4 probe has this order:

1. A `StateUpdate` writes `probe.value` before entering a branch.
2. A branch consumer reads `$context:probe.value` and moves to a join.
3. A join transition returns to the branch for another guarded pass.
4. A later branch consumer reads the same prior value and completes.

The released 0.3.282 runtime passes this order, including the branch and cycle. The governed analyzer must model the same first-arrival order:

- Initial context is available at the start state.
- A producer is available to a consumer only when it is initial, in the consumer payload, or strictly before the consumer on every required incoming path.
- A back edge is not a first-pass producer. Do not include an active DFS back edge as a required incoming predecessor during the first fixed-point pass.
- A producer on only one branch cannot satisfy a consumer after the branch join.
- A producer before a branch back edge remains available on later passes.
- The analyzer must preserve the legacy ungoverned behavior separately from governed fail-closed behavior.

Keep the gov4 fixture and its expected context in runtime-owned probe evidence. If a new runtime disagrees, record the result as `unknown` until the template, analyzer, and runtime contract are reviewed together.

## Migration Procedure

Before changing a repeated target pattern:

1. Run an exact-version dry scan and record source paths, source hashes, target paths, and candidate changes.
2. Convert only unambiguous `ToolCall`/`noop` literal-update shapes to `StateUpdate` plus the native `state.update` command. Preserve output families, routes, guards, and gate declarations.
3. Remove `$result` bindings from emitters whose result shape is not proven. Keep real-tool and external-result bindings only when the probe records the result type and projection.
4. Report missing producers, self-bindings, ambiguous bindings, and unknown emitters without guessing a `$context` binding.
5. Write candidate files separately from source files. A failed scan or validation must not overwrite the source.
6. Validate the candidate with the exact runtime compile and run/resume chain.
7. Run the migration a second time. The canonical manifest and hashes must be unchanged; this is the idempotence check.

A migration manifest must include the script path and hash, scan input paths and hashes, changed/unchanged/failed target lists, ambiguity findings, candidate paths and hashes, validation commands, and rollback instructions. The source remains the rollback point until post-migration validation passes.

## Resume Contract

A blocked external step defines its required input names. The resume file must contain those fields at the top level of the payload. With `resumeOutputKey: result` and canonical projection, the payload shape is:

```json
{
  "transition_id": "transition.review",
  "correlation_key": null,
  "payload": {
    "review_round": {
      "accepted_findings": []
    },
    "target_scope": "...",
    "result": {
      "status": "complete"
    }
  }
}
```

Do not place `target_scope` or `review_round` only inside `result` when they are required inputs. Before every resume, inspect the blocked transition's required inputs and keep the same external workflow file, case id, and run id.

## Windows Tooling Notes

- Use Windows PowerShell 5.1 only for orchestration that is compatible with its language mode.
- Do not inline multi-line JavaScript or nested PowerShell quoting. Write a `.js` file and execute it with Node.
- Use `ConvertFrom-Json` for simple PowerShell JSON reads; use Node for complex JSON edits and UTF-16LE/binary probes.
- Treat `.nupkg` files as ZIP files. Do not use `Expand-Archive` directly on a package when the shell is constrained.
- Combine paths with `Join-Path` or an equivalent structured API. Do not calculate ZIP documentation paths with hand-written substring offsets.
- Keep binary string detection in Node using `Buffer` and the correct encoding.
- A failed package, guide, compile, or runtime operation produces failed evidence only. Never turn a missing or partial output into pseudo-success evidence.

## Required Evidence

The final runtime-owned evidence must reference:

- exact runtime version, mode, RID, package identity, and successful guide JSON;
- inherited and replacement fixture files, hashes, compile results, run results, and terminal context;
- gov4 branch/cycle fixture and its compile/run evidence;
- emitter classification and producer matrix for every published output family;
- migration manifest, script hashes, dry-scan results, idempotence result, and rollback point;
- every resume payload and the blocked transition input contract used to construct it;
- final public `run`/`resume` status on the same external workflow copy.
