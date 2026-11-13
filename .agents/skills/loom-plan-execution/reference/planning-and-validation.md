# Planning And Validation Contract

Before planning, replanning, workflow design, or validation, read this complete contract. Paths in backticks are relative to the skill root unless stated otherwise. Return to the [skill entrypoint](../SKILL.md) for the exact version and execution route.

## Scope-First Planning And Validation

Before planning a broad review, repair, validation, or run, identify the requested outcome, actual changed files, affected behavior and dependency boundaries, risk, and mandatory approval/terminal gates. Produce a bounded selection matrix before execution: each minimum check names its target, rationale, evidence producer, and expected result; list excluded checks with their scope reason. Match checks to the change rather than defaulting to every node, subsystem, or evidence family. Reuse evidence only when supporting source snapshots, inputs, exact runtime/version where relevant, and dependencies remain unchanged. Invalidate only affected evidence and direct dependents; every repeated check records its concrete invalidation cause. Optional development-support hashes are not freshness gates.

Documentation-only changes need relevant document and link checks; field/schema/contract changes need affected types, producers, consumers, and tests; workflow topology/routing/gate/wait-recovery changes need corresponding graph, dataflow, compile, and affected scenarios. Compile and static checks do not replace contract-required public `run`/`resume`, genuine approval, or terminal-state checks. Run a full end-to-end chain only when the requested deliverable, changed behavior, or active contract requires it, and record any mandatory gates that remain.

## Workflow Authoring Contract

Detailed assumptions, startup contracts, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/ao-skill-reference.md`
- Producer output evidence: [reference/producer-output-evidence-contract.md](./producer-output-evidence-contract.md); inspect target skill assets and available run evidence before asking the user for a schema or sample.

Workflow generation or revision for this skill must use the local workflow-designer subagent with context-rich relative links, not a freeform generic agent call:

- `assets/agents/loom-plan-execution-workflow-designer.agent.md`

When using that workflow-designer route, enforce deterministic workflow authoring contracts instead of descriptive-only prose:

- each transition must declare structured executable `guardExpression` and canonical `passExpression` predicates, explicit evidence outputs, and explicit seam ownership; `succeedExpression` is a legacy input to migrate, not a field to author
- every context path must be declared in the owning contract's `context_fields` catalog; each `context.Get<T>(path)` type and each producer/output projection must match the declared JSON type; use `context.Has(path)` for presence-only checks
- Map the skill being enhanced's `SKILL.md` first as Priority 1 in `assets/so-workflow/node-to-file-map.md`; include its exact target-relative path, owning nodes, requested changes, and validation evidence before supporting files.
- For every added or changed `context_fields` entry, follow [Producer Output Evidence Discovery](./producer-output-evidence-contract.md) first: the driver inspects target skill contracts, assets, and available run/probe results, then compares the declared type with independent producer evidence. Ask the user only for a necessary gap that remains after discovery and cannot be safely probed. Record `contextFieldTypeComparisons` in `static-contract-review.json`; unknown type evidence blocks readiness before broad tests.
- Every target skill's `SKILL.md` must carry the conditional `Runtime Documentation References` rule: when the target or its local agents need AO/SO runtime docs, resolve English pages from the selected exact apphost's successful `--guide` JSON `docs_root`, verify version/absolute paths/containment/readability, pass exact paths in `referencePackManifest`, and never construct paths relative to the installed skill.
- each gate must declare machine-checkable pass predicates, required evidence references, and route coverage mapping
- weave-out hints must preserve resume continuity contract fields and expected payload/evidence shapes
- final workflow proposals must include transition, gate, and ownership preflight checklists before JSON output is accepted
- fail closed: reject vague transition/gate wording that lacks concrete predicates or evidence paths

That exact `.agent.md` file is the authoritative behavior source for the workflow-designer subagent. Do not require it to be mirrored into `.github/agents/`, a user-profile agent folder, or any other discoverable agent root before use. Dispatch it only through [Named Agent Resolution](../SKILL.md#named-agent-resolution): preflight the host agent list, call the exact frontmatter name when it is registered, and otherwise use the driver route with inline or path-only delivery; driver fallback does not require user approval. Do not replace this route with a freeform approximate agent role.

## Workflow Designer Reference Pack

Before dispatching `assets/agents/loom-plan-execution-workflow-designer.agent.md`, provide a bounded `referencePackManifest` and fresh `schemaDemoInput` from the unified `loom` apphost through `loom agent`. Include the successful guide JSON and returned guide file, same-apphost schema/demo/successful demo compile audit, current target contract and requirements, current workflow source, applicable `AGENTS.md`, and latest compile feedback when revising.

Every development-support reference entry records its normalized path, exact runtime version, authority role, read status, and validation result. A SHA-256 may be recorded when the reference is generated, but it is optional metadata. While consuming references, do not calculate, compare, re-attest, freshness-check, or gate on support hashes. A `generationSetId` groups the same-apphost schema/demo/compile evidence; it is not a freshness proof. Exact runtime version, readable files, schema validity, and successful same-apphost demo compilation remain required. An older workflow is `previous_runnable_reference` only and needs a source version/path/copy-time/differences/rejected-item disposition; its source hash is optional metadata.

- Resolve runtime documentation only from the selected apphost's successful English `--guide` result: verify its exact version, absolute `docs_root` and `guide_path`, containment, and readability; resolve English pages only under `docs_root`. For workflow/task contracts, also validate same-version `contract_docs_root`, `contract_index_path`, and `builder_template_path`, then follow `index.md` links only beneath that root and pass exact selected paths via `referencePackManifest`. Translated pages are localization aids only when supplied. Never construct documentation paths relative to the installed skill.
- For contract authority and the C# builder route, follow [Version-Matched Abstractions And C# Builder](./ao-skill-reference.md#version-matched-abstractions-and-c-builder); it defines the exact `--guide` document fields, `.cs.md`/`.csx.md` rules, `Build`/`Edit` and `export → edit → compile` paths, external JSON boundary, and canonical JSON authority. Apply the same method when this skill's own workflow or a future revision is changed.

The designer must return runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` with schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Descriptors require path, schemaVersion, verdict, and exact runtime version; optional support hashes may be recorded when generated but are not consumed as freshness gates. Failed or unknown required semantic probes prevent readiness; compile success alone is not semantic evidence. Exact package SHA-512 and every artifact hash explicitly required by the target business contract remain mandatory.

## Phase-Sliced Workflow Design

Count authored state nodes only; exclude transitions and synthetic harness nodes. Workflows below 40 authored state nodes stay on the whole-workflow route. When a workflow has 40 or more authored state nodes, first create a standalone `loom.workflow-skeleton.v1` design view, freeze typed interfaces and cross-phase routes, then author isolated phase fragments. Keep each phase at or below 25 authored state nodes as a soft cap and record exceptions. New workflows use `phaseSliced`; migrations may explicitly mix `phaseSliced` and `legacy`, but every cross-phase read and write remains declared and typed.

Use the same selected unified `loom` apphost via `loom agent` for `skeleton-check`, `phase-compile`, `impact`, `merge`, and final `compile`. Phase compilation is static only. Aggregate interface-change requests centrally, revise the skeleton, run `impact`, and redo only affected phases and proven dependents; unknown impact requires full revalidation. Merge exactly one passing result per phase into a new candidate, then require complete loom agent compile and its audit against that exact file. Never modify an active runtime workflow copy.

Allow at most three repair attempts per phase and two post-merge full-compile repair rounds. At either cap, preserve evidence and ask for an explicit decision instead of retrying silently. Return `phase_design_evidence` linked to `phaseDesignEvidence` in the existing `static-contract-review.json`; do not create a fourth report. Optional support hashes are not freshness gates. Package integrity and required business-output hash checks are unchanged.

```text
skeleton-check --workflow-file <path> --skeleton-file <path>
phase-compile --workflow-file <path> --skeleton-file <path> --fragment-file <path> --phase-id <id> --output-file <path>
impact --previous-workflow-file <path> --previous-skeleton-file <path> --candidate-workflow-file <path> --candidate-skeleton-file <path> --output-file <path>
merge --workflow-file <path> --skeleton-file <path> --result-file <path>... --output-file <path>
```

## Validation Runtime Efficiency Contract (Required)

For planner-authored validation workflow scripts and validation steps, enforce these limits as hard defaults:

- Keep each single validation run small and convergent; target completion within 5 minutes. If one validation is likely to exceed 5 minutes, split it into smaller independent validations.
- Keep internal step-to-step idle delay at or below 5 seconds. Do not add artificial waits, long sleeps, or manual pacing gaps between validation steps.
- Run independent validation work in parallel whenever correctness allows, and keep only dependency-bound checks in serial order.
- Aggregate parallel validation results once per batch, then continue with the minimum required serial checks.
- If host limitations prevent these limits, fail closed with explicit blocker evidence and return a concrete split-or-parallelization repair plan.

### Automated Polling Timer Rule (Required)

For automated readiness polling, check immediately when supported and begin with the smallest valid interval. Increase the next wait only after a recorded `pending` or `not-ready` result shows the current timing did not permit verification; increase by exactly 1 second per retry. Do not use exponential backoff or add delay without observed evidence.

Keep each idle wait between validation steps at or below 5 seconds and preserve existing operation and session deadlines. If a check remains unready at the 5-second ceiling, continue only with the existing runtime deadline or report the blocker; do not extend a runtime deadline. This polling rule does not shorten a human response wait.

## AO/SO AskUser Test Isolation (Required)

When a validation workflow tests AskUser behavior through AO or SO:

- For apphost integration or end-to-end tests, invoke the runtime selected for the run directly by its normalized absolute apphost path. Record its exact version, executable path, and working directory. Use the exact package apphost in `package-channel` mode; use current-source output only for explicit repository debugging and label that evidence development-only. Do not resolve the executable through `PATH` alone or silently substitute DLL/project output.
- Give every test lane a unique external root for its contract file, AskUser store/session data, workflow copy, event/log records, and audit outputs. Use only documented or test-supported storage-root controls and verify the resolved paths. If the store cannot be isolated, serialize the affected tests rather than sharing mutable state.
- Give every simultaneously active worker/test lane its own OS-assigned ephemeral loopback port. Consume only the endpoint returned by AO/SO and assert that active parallel lanes have different ports. Never hard-code or reuse one fixed port, and do not invent port-selection CLI flags.
- Keep pairing tokens and full route URLs private; pass only the runtime-returned approved endpoint to the browser capability.

## Continuation Planning And Recovery

Before each public `run` or `resume`, inspect the same persisted external workflow copy and current status. For an active wait, read its exact current `transition_id`, applicable current fan-out `wait_id`, `resumeOutputKey`, required fields and JSON/container types, nullability, ownership, relevant context catalog types, `outputPath`, and `outputBindings`. Construct the structured envelope and payload to that live contract and validate it locally before submission; preserve declared sibling and nested shapes without flattening or wrong-typed summaries. Keep full review evidence in external files and place only contract-required values or references in context.

After a failed run or resume, preserve runtime diagnostics and inspect saved events, persisted state, current wait, submitted payload, `caseId`/`runId`, and relevant file changes before acting. Do not retry an unchanged action or payload. Correct a demonstrated cause first; if the runtime requires a fresh lineage, reuse static evidence only when source snapshots, inputs, exact runtime/version, and dependencies still match, and invalidate only affected evidence. Never fabricate approval, report blocked/failed state as success, or claim final completion from compile alone.

For large workflows and review results, return an existing readable file path plus a bounded status/summary, needed JSON Pointer fragments, and evidence locations. Do not dump the complete workflow JSON into the conversation. Whole-document parsing or validation stays file-backed; full conversational output requires an explicit request and applicable size limits.
