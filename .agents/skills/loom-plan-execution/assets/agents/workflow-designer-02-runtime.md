## Mandatory Post-Design Self-Compile Gate (Required)

A workflow design is not a deliverable until the designer compiles the exact candidate it produced with the same AO runtime and the same schema contract. The designer must never return a successful candidate that has not passed this gate.

After the candidate is written to a fresh external candidate path, run these steps in order:

1. Parse the complete candidate with a structured JSON parser.
2. Re-run the duplicate-id, state/transition reference, projection, producer, gate, and ownership checks against that exact candidate.
3. Compile that exact candidate with the current runtime:

```powershell
.\ao.exe compile --workflow-file <external-candidate-workflow.json> --audit-output <external-candidate-audit-root>
```

4. Read the process exit code separately from stdout and stderr. Parse the structured <ao_property> payload when the runtime emits one, preserve `ExpressionCompileFeedback` and dataflow diagnostics, and require successful compile audit evidence for exit code 0.
5. If compile fails, classify the failure as runtime/preflight, JSON parse, graph reference, expression, projection, or dataflow. Repair only the current candidate layer, rerun every preceding check, and compile again. Do not add a new workflow behavior while the current candidate failure remains unresolved.

The successful dispatch/result must preserve a machine-readable `selfCompileEvidence` object containing `runtime`, `runtimeVersion`, `schemaSha256`, `candidateFile`, `candidateSha256`, `compileCommand`, `compileExitCode: 0`, and the external compile-audit path. It must also include a concise `schemaCoverage` map showing which schema sections supplied the authored root fields, node discriminators, expressions, enum values, and compile constraints.

If the designer cannot run the exact runtime, cannot read the supplied schema/demo, or cannot make the candidate compile after local repairs, fail closed: return the structured blocker and evidence needed for the next repair, but do not return the candidate as ready, do not claim compile success, and do not weave it back as an authoritative workflow.

## Validation Runtime Efficiency Contract (Required)

When authoring or revising validation workflow scripts and validation-heavy node groups, enforce these runtime limits:

- Keep each single validation run bounded to 5 minutes or less by design. Split broad checks into smaller independent validations when needed.
- Keep internal idle time between validation steps at 5 seconds or less. Avoid artificial waits or manual delay padding.
- Use parallel fan-out for independent validation transitions by default, and keep serial order only for dependency-bound checks.
- Keep serial post-batch validation minimal and explicit after one aggregation step.
- If environment constraints make the limits unattainable, fail closed with blocker evidence and return a concrete split/parallel redesign path.

### Automated Polling Timer Rule (Required)

For automated readiness polling, check immediately when supported and begin with the smallest valid interval. Increase the next wait only after a recorded `pending` or `not-ready` result shows the current timing did not permit verification; increase by exactly 1 second per retry. Do not use exponential backoff or add delay without observed evidence.

Keep each idle wait between validation steps at or below 5 seconds and preserve existing operation and session deadlines. If a check remains unready at the 5-second ceiling, continue only with the existing runtime deadline or report the blocker; do not extend a runtime deadline. This polling rule does not shorten a human response wait.

## AO/SO AskUser Test Isolation (Required)

When a validation workflow tests AskUser behavior through AO or SO:

- For apphost integration or end-to-end tests, invoke the runtime selected for the run directly by its normalized absolute apphost path. Record its exact version, executable path, and working directory. Use the exact package apphost in `package-channel` mode; use current-source output only for explicit repository debugging and label that evidence development-only. Do not resolve the executable through `PATH` alone or silently substitute DLL/project output.
- Give every test lane a unique external root for its contract file, AskUser store/session data, workflow copy, event/log records, and audit outputs. Use only documented or test-supported storage-root controls and verify the resolved paths. If the store cannot be isolated, serialize the affected tests rather than sharing mutable state.
- Give every simultaneously active worker/test lane its own OS-assigned ephemeral loopback port. Consume only the endpoint returned by AO/SO and assert that active parallel lanes have different ports. Never hard-code or reuse one fixed port, and do not invent port-selection CLI flags.
- Keep pairing tokens and full route URLs private; pass only the runtime-returned approved endpoint to the browser capability.

## AO-Specific Design Target

AO is for exploratory orchestration under uncertainty.

Design around these AO-specific facts:

- AO official execution surfaces are `ao.exe run`/`ao.exe resume` on Windows and `ao run`/`ao resume` on Unix.
- `compile`, `--guide`, `prompt-plan`, and `prompt-replan` are preparation or authority-supporting surfaces, not official run modes.
- Before planning, authoring, validation, compile, `prompt-plan`, `prompt-replan`, run, resume, or downstream input collection, verify and extract the exact AO product+RID package, then directly run `ao.exe --guide` on Windows or `ao --guide` on Unix. Validate the returned version and readable contained guide paths first.
- AO weaves out at control seams and returns blocked payloads such as `boundary_reason`, `pending_requirements`, `next_frontier`, and `weave_out_request`.
- AO resume preserves seam continuity through `transition_id`, `correlation_key`, and `payload`.
- For every workflow-file fan-out result, copy `wait_id` from the matching uncompleted entry in the exact persisted workflow's `activeWaitGroups`. Include it for success and error; never invent or reuse it across retries or state re-entry. Missing and stale IDs must fail closed.
- For external execution failure, also send a non-empty `execution_error` and no non-empty `payload`. These fields apply to workflow-file fan-out; session-based AO resume rejects them. Do not author wait IDs or `fanoutCancellationSignals` in workflow JSON.
- Apply strategy precedence from the linked execution-model and AO behavior references: a passing `whenAnyPass` branch wins despite an exhausted sibling; `whenAllPass` waits for all branches; route exhausted errors only after the batch resolves.
- Use `firstPass` for ordered or dependent work. Use `whenAnyResponse`, `whenAnyPass`, `whenAllPass`, or `whenExpression` only for external-step fan-out; compile rejects synchronous/internal members even when one branch is ready. Do not put `contractRefs` on fan-out members because contract context is workflow-scoped.
- Keep concurrent branches independent and give each branch disjoint `outputPath` and `outputBindings`. Use `joinTargetStateId` for `whenAllPass` and `whenExpression`; define the applicable `noPassTargetStateId`, `errorHandlingTargetStateId`, and timeout fallback (`timeoutTargetStateId` or `timeoutTransition.targetNodeId`). Leave `fanoutCancellationSignals` runtime-owned.

## Governance Wrapper Scope Boundary (AO)

Classify the workflow intent before adding nodes. When the request explicitly classifies the workflow as a governance wrapper, model only runtime and guide authority, source/copy integrity, route and ownership checks, case/run binding, blocked recovery, the structured handoff to the existing domain orchestrator, and governance completion evidence. Do not copy domain business steps into the AO graph. `full_regeneration` may describe how the governance wrapper template is rebuilt; it is not a permission to model a domain generation workflow.

For an ordinary AO business workflow, keep the requested business steps and apply the shared reference-pack and layered-validation rules without imposing the governance-wrapper scope boundary.

## Caller File Preparation Contract

The caller must create the complete set of input files in one preparation step before dispatching a CLI command. Pass file paths only. This includes the schema/demo inputs, workflow JSON, builder/editor script, verifier script, input JSON, base workflow, reference workflow, patch content, and every objective/context/instance/result file required by the selected route.

Never send script source, JSON, or replacement text as an inline option. Never let a later node fill in a missing file or patch a partial file. Confirm every required input path exists and is readable before the command starts; output paths are destinations written by the CLI.
