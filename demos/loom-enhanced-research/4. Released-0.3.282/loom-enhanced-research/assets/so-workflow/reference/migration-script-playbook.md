# Migration Script Playbook

This reference defines the checked-in Node.js migration tools under `assets/so-workflow/scripts/`. The tools are designed for the released Skill Orchestrator `0.3.282` semantic migration. They inspect or create separate candidate artifacts; they never rewrite the workflow source in place.

## Scope

The migration set has four public entry points:

| Entry point | Input | Destination | Responsibility |
| --- | --- | --- | --- |
| `convert-noop-to-stateupdate.js` | One workflow JSON file | Candidate workflow plus migration manifest | Convert only unambiguous `toolCall`/`noop` literal-update transitions to `stateUpdate` with native `state.update`. |
| `strip-result-bindings.js` | One workflow JSON file | Candidate workflow plus migration manifest | Remove `$result` only where the emitter is known-null or the literal writer is not proven to produce that family; preserve proven real-tool, external, and covered literal-writer bindings. |
| `audit-output-family-producers.js` | One workflow JSON file | Producer audit report | Report reachable concrete, pseudo, missing, multiple, and ambiguous producers without changing the workflow. |
| `verify-migration-idempotence.js` | Source workflow, two existing candidates, and two existing manifests | Optional idempotence report | Compare source hashes, candidate bytes, canonical candidates, and path-free manifest projections from two independent runs. |

`migration-common.js` is an internal shared module. It owns path checks, JSON and hash helpers, emitter classification, graph reachability, guaranteed context, and manifest formatting. It is not a standalone migration command.

## File Contract

All file-valued arguments are paths. Do not pass JSON, JavaScript, or replacement text inline. The caller must create and close every input file before starting a command.

- `--workflow-file` and `--input-file` are equivalent input aliases.
- Candidate, manifest, report, and idempotence-report destinations must not already exist.
- The source input must be distinct from every destination, using case-insensitive normalized paths.
- Parent directories for new destinations are created by the script.
- Valid JSON outputs are pretty-printed with two-space indentation and a trailing newline.
- The source hash is recorded before analysis and `source.untouched` must remain true.
- A failed migration never overwrites the source or an existing destination.
- Exit code `0` means the requested check or candidate operation passed. Exit code `2` means invalid input, an ambiguous target, an unproven producer, a failed audit, or a destination/path violation. `--help` exits with `0`.

### Dry-run Semantics

`--dry-run` is a review mode, not a no-output mode. Conversion and binding-stripping commands still write their separate candidate and manifest destinations so the proposed result can be inspected, hashed, compiled, and rejected without touching the source. The manifest `mode` is `dry-run`. The producer audit and idempotence commands likewise write their requested report destinations with `mode: dry-run`.

This means a dry run needs fresh destination paths. Reusing a previous candidate, manifest, or report is rejected by the no-overwrite rule.

## Migration Rules

The tools encode the following `0.3.282` boundaries:

- A declaration in `publishesOutputFamilies`, `requiredOutputFamilies`, or a gate is not evidence of a value.
- Plain `ToolCall`/`noop` literal `parameters.updates` are inert and are not treated as context writes.
- `stateUpdate` and `memoryWrite` produce only keys covered by their declared `parameters.updates` map. `MemoryRead` has a separate result contract.
- Known-null `noop` results never prove an output family through `outputPath` or `$result`.
- Known real built-ins such as `echo`, `ls`, and valid `write-file` calls may use `$result` when their required parameters prove a result is possible.
- External resume projections must use the declared payload and canonical `$context:<path>` projection. Legacy `$context.<path>` syntax is rejected.
- A same-transition `outputPath` or `$context:<family>` self-binding cannot prove the family it publishes.
- Unknown emitters, unknown tools, duplicate binding locations, missing required parameters, and unproven branch or cycle context are reported as ambiguity or failure. The scripts never guess a new `$context` binding.
- Producer reachability is route-aware: a producer must be available before the consumer on every required incoming path. A DFS back edge is ignored for first arrival, and a producer on only one branch cannot satisfy a post-join consumer.

## Recommended Sequence

Use a new execution output directory for each migration pass. The following commands show the path-only shape; replace each placeholder with a real closed file path.

1. Scan and create a candidate for unambiguous noop literal writes:

   ```text
   node assets/so-workflow/scripts/convert-noop-to-stateupdate.js --workflow-file <source-workflow.json> --candidate-file <noop-stateupdate.candidate.json> --manifest-file <noop-stateupdate.manifest.json> --dry-run
   ```

2. Inspect the manifest and validate the candidate with the exact bound runtime. Keep the original source as the rollback point until validation passes.

3. Scan the validated candidate for unsupported or unproven result bindings:

   ```text
   node assets/so-workflow/scripts/strip-result-bindings.js --workflow-file <candidate-workflow.json> --candidate-file <stripped.candidate.json> --manifest-file <stripped.manifest.json> --dry-run
   ```

4. Audit every reachable output family on the resulting candidate:

   ```text
   node assets/so-workflow/scripts/audit-output-family-producers.js --workflow-file <candidate-workflow.json> --report-file <producer-audit.json> --dry-run
   ```

5. Repeat the candidate-producing command from the same source with new destinations. Verify both runs with the idempotence entry point:

   ```text
   node assets/so-workflow/scripts/verify-migration-idempotence.js --workflow-file <source-workflow.json> --first-candidate-file <candidate-one.json> --second-candidate-file <candidate-two.json> --first-manifest-file <manifest-one.json> --second-manifest-file <manifest-two.json> --report-file <idempotence.json> --dry-run
   ```

6. Only after the migration manifest, exact-runtime compile/run validation, and idempotence report pass should a caller choose the candidate as the next source for a separate migration step. The scripts do not replace the official SO workflow run/resume chain.

## Failure and Rollback

A conversion or strip failure writes a failed migration manifest when the workflow was readable, with `candidate.status: not_written`, failed target entries, and ambiguity details. The candidate is not created. A producer audit writes a failed report with per-family evidence. A malformed input fails before analysis and does not create a successful artifact. A destination collision or duplicate path fails before a source mutation.

The source workflow is the rollback point. Do not copy a candidate over it as part of these scripts. Preserve the source and all manifests until exact-runtime validation and the official external workflow-copy run/resume chain have completed.

## Repeatable Fixture Check

Run the built-in Node fixture entry point from the repository root:

```text
node .agents/skills/loom-skill-enhancement/assets/so-workflow/scripts/migration-fixture-tests.js
```

The fixture test uses only Node.js built-ins and a temporary directory. It invokes all four entry points as child processes and covers:

- noop-to-stateUpdate conversion and preservation of an unchanged noop;
- two dry-run candidates plus canonical idempotence verification;
- known-null cleanup and preservation of stateUpdate, `echo`, `write-file`, and external resume projections;
- concrete producer audit results;
- unknown emitter and same-transition self-binding failure closure;
- duplicate destination paths, malformed JSON, and no-overwrite behavior;
- source hash preservation and readable pretty-printed JSON artifacts.

The fixture test is a local behavior check for the migration tools. It does not claim exact-runtime execution evidence; that evidence still comes from the published `0.3.282` SO runtime and its workflow audit chain.
