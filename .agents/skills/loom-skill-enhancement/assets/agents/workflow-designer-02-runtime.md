## Mandatory Post-Design Self-Compile Gate (Required)

A workflow design is not a deliverable until the designer compiles the exact candidate it produced with the same SO runtime and the same schema contract. The designer must never return a successful candidate that has not passed this gate.

After the candidate is written to a fresh external candidate path, run these steps in order:

1. Parse the complete candidate with a structured JSON parser.
2. Re-run the duplicate-id, state/transition reference, projection, producer, gate, and ownership checks against that exact candidate.
3. Compile that exact candidate with the current runtime:

```powershell
so.exe compile --workflow-file <external-candidate-workflow.json> --audit-output <external-candidate-audit-root>
```

4. Read the process exit code separately from stdout and stderr. Parse the structured <so_property> payload when the runtime emits one, preserve `ExpressionCompileFeedback` and dataflow diagnostics, and require successful compile audit evidence for exit code 0.
5. If compile fails, classify the failure as runtime/preflight, JSON parse, graph reference, expression, projection, or dataflow. Repair only the current candidate layer, rerun every preceding check, and compile again. Do not add a new workflow behavior while the current candidate failure remains unresolved.

The successful dispatch/result must preserve a machine-readable `selfCompileEvidence` object containing `runtime`, `runtimeVersion`, `schemaSha256`, `candidateFile`, `candidateSha256`, `compileCommand`, `compileExitCode: 0`, and the external compile-audit path. It must also include a concise `schemaCoverage` map showing which schema sections supplied the authored root fields, node discriminators, expressions, enum values, and compile constraints.

If the designer cannot run the exact runtime, cannot read the supplied schema/demo, or cannot make the candidate compile after local repairs, fail closed: return the structured blocker and evidence needed for the next repair, but do not return the candidate as ready, do not claim compile success, and do not weave it back as an authoritative workflow.

## Validation Runtime Efficiency Contract (Required)

When producing or revising validation workflow scripts and validation node topologies, enforce these runtime constraints:

- Keep each single validation run bounded to 5 minutes or less. Split oversized checks into smaller independent validations.
- Keep internal delay between validation steps at 5 seconds or less. Avoid artificial waits or long pacing gaps.
- Prefer parallel execution for independent validations and reserve serial ordering for strict dependency paths only.
- Use one aggregation step per parallel validation batch, then run only minimal required serial checks.
- If runtime or host limits prevent compliance, fail closed with explicit blocker evidence and return a concrete split/parallel redesign plan.

### Automated Polling Timer Rule (Required)

For automated readiness polling, check immediately when supported and begin with the smallest valid interval. Increase the next wait only after a recorded `pending` or `not-ready` result shows the current timing did not permit verification; increase by exactly 1 second per retry. Do not use exponential backoff or add delay without observed evidence.

Keep each idle wait between validation steps at or below 5 seconds and preserve existing operation and session deadlines. If a check remains unready at the 5-second ceiling, continue only with the existing runtime deadline or report the blocker; do not extend a runtime deadline. This polling rule does not shorten a human response wait.

## AO/SO AskUser Test Isolation (Required)

When a validation workflow tests AskUser behavior through AO or SO:

- For apphost integration or end-to-end tests, invoke the runtime selected for the run directly by its normalized absolute apphost path. Record its exact version, executable path, and working directory. Use the exact package apphost in `package-channel` mode; use current-source output only for explicit repository debugging and label that evidence development-only. Do not resolve the executable through `PATH` alone or silently substitute DLL/project output.
- Give every test lane a unique external root for its contract file, AskUser store/session data, workflow copy, event/log records, and audit outputs. Use only documented or test-supported storage-root controls and verify the resolved paths. If the store cannot be isolated, serialize the affected tests rather than sharing mutable state.
- Give every simultaneously active worker/test lane its own OS-assigned ephemeral loopback port. Consume only the endpoint returned by AO/SO and assert that active parallel lanes have different ports. Never hard-code or reuse one fixed port, and do not invent port-selection CLI flags.
- Keep pairing tokens and full route URLs private; pass only the runtime-returned approved endpoint to the browser capability.

## SO-Specific Design Target

SO is for deterministic workflow governance and skill being enhanced delivery.

Design around these SO-specific facts:

- SO official execution surfaces are `so.exe run`/`so.exe resume` on Windows and `so run`/`so resume` on Unix.
- `compile`, `--guide`, `status`, `inspect-workflow`, and `inspect-events` are supporting surfaces, not official run modes.
- After exact package verification and safe extraction, run the direct apphost `--guide` before planning, authoring, validation, compile, run, resume, or downstream input collection. MCP is optional only for later steps; do not add a pre-guide transport or fragment-inspection gate.
- Templates for the skill being enhanced that use root `templateKind: so-governed-target-skill` must carry `validation.gates`, `validation.routes`, `validation.declaredUserOwnedFields`, and `validation.reservedRuntimeOwnedFields`.
- `AskUser` seams may request only user-owned inputs or decisions.
- `WaitResume` and other runtime-owned seams must hold runtime facts, provenance, and artifact paths.
- For every fan-out result, copy `wait_id` from the matching uncompleted entry in the same persisted workflow's `activeWaitGroups`; include it on success and error, and never reuse an ID across retries or re-entry. Missing or stale IDs are invalid.
- For an external execution failure, also send a non-empty `execution_error` and no non-empty `payload`. The runtime retries that branch, waits for remaining branches, and then applies the documented error/no-pass/join precedence.
- Use the linked execution-model, JSON-contract, SO behavior, and CLI usage pages as the wire and outcome authority. Never author runtime wait IDs or cancellation signals in the workflow template.
- For targets already under Loom Skill Orchestrator governance, re-enhancement logic must be explicit rather than collapsed into one branch.
## Shared Context And Batch Topology



When designing `/loom-skill-enhancement` or another workflow for the skill being enhanced under Loom Skill Orchestrator governance, add one bounded shared-context producer after exact package validation and fresh guide proof. The context must carry a source manifest, bounded snapshots, guide/schema/runtime references, a `context_hash`, and the same external workflow-copy identity.



Use a `TransitionGroup` with `strategy: whenAllPass` only for independent external `SubagentCall` transitions that share one explicit join target. Add an explicit aggregation transition after every parallel batch. Put one coordinated repair transition after the pre-repair aggregate, then a second `whenAllPass` validation batch, a post-fix aggregate, and one serial validation transition before official run/resume. Require all expected results, preserve accepted/rebutted/needs-validation dispositions, fail closed on missing or duplicate results, and never hide repair or validation in a generic planner node. Legacy wire value `all` is migration-only and must not be authored.

## Host-Selected Governed Entry

## Direct Self-Contained Runtime Entry

Every workflow generated for a skill being enhanced under Loom Skill Orchestrator governance, including the self-bootstrap workflow for `/loom-skill-enhancement`, must model package validation before guide capture and planning.

- Detect one supported RID from the host OS, architecture, and Linux libc. Use only the exact product+RID package version bound by the skill lock.
- Reuse only a fully validated immutable generation in the shared current-user OS-temp AO/SO cache, keyed by exact package ID/version/RID/full verified `.nupkg` SHA-512. On a miss, check the standard NuGet cache, then acquire the exact NuGet package; a same-version GitHub Release fallback requires a valid `.sha512` sidecar. Stage and validate before atomic publication; never overwrite a different valid or in-use generation.
- Safely extract the verified package to an external per-run directory. Do not require an installed `dotnet` host, a fixed bootstrap script, a resolver, or a launch descriptor.
- Directly run `so.exe --guide` on Windows or `so --guide` on Unix immediately after extraction. Validate the returned version and contained readable paths before any other runtime operation.
- Use the same extracted apphost for schema/demo, compile, run, and resume on the same external workflow copy. A package, extraction, startup, or guide failure stops the route with failed evidence.
- MCP may be used later when a step benefits from it; it is never a package, guide, compile, run, or resume prerequisite.

## Governance Wrapper Scope Boundary (SO)

Classify the target intent and workflow type before adding nodes. For `templateKind: so-governed-target-skill` with an enhancement or re-enhancement intent, model only runtime and guide authority, source/copy integrity, route and ownership checks, case/run binding, blocked recovery, the structured handoff to the existing domain orchestrator, and governance completion evidence. Do not copy domain business steps into the SO graph. Generation Step 01-15, Layout Step 01-06, MATLAB/MCP work, model or geometry generation, simulation, behavior, and release remain owned by the existing domain orchestrator.

`full_regeneration` is a strategy for rebuilding the governance wrapper template. It is not a model-generation mode and must never be used as a reason to import the domain workflow into the SO graph. A target-specific business workflow must retain its own declared `taskType` and `workflowKind` and must not inherit enhancement-only guide, asset-review, aggregate, repair, or validation steps.

## Caller File Preparation Contract

The caller must create the complete set of input files in one preparation step before dispatching a CLI command. Pass file paths only. This includes the schema/demo inputs, workflow JSON, builder/editor script, verifier script, input JSON, base workflow, reference workflow, patch content, and every objective/context/instance/result file required by the selected route.

Never send script source, JSON, or replacement text as an inline option. Never let a later node fill in a missing file or patch a partial file. Confirm every required input path exists and is readable before the command starts; output paths are destinations written by the CLI.
