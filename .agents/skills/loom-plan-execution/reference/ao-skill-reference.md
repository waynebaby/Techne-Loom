# AO Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-plan-execution/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-plan-execution` needs to create or revise workflow JSON:

- [../assets/agents/loom-plan-execution-workflow-designer.agent.md](../assets/agents/loom-plan-execution-workflow-designer.agent.md)

Pass relative links to the plan file, guide file, workflow file, audit artifacts, and any blocked payload evidence so the subagent runs with explicit local context instead of relying on repository-global discovery.

That declared `.agent.md` file is the authoritative behavior contract for the workflow-designer subagent. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders. If the runtime supports direct exact-name resolution, invoke that exact subagent name while keeping the declared `.agent.md` file as the contract. If exact-name resolution is unavailable, resolve the same declared path from the current repository/workspace copy first and the corresponding global installed-skill copy second before failing, then pass the resolved file path plus the full file content into the subagent-driving call. Do not replace this route with a freeform approximate role or repository-global substitute prompt.

The subagent must generate node-level granularity where each node owns one visible responsibility and where every AO weave-out path has a detailed blocked-action hint.

The subagent must also enforce deterministic transition/gate contracts and fail-closed anti-hallucination behavior:

- transition contracts must use executable boolean predicates for `guardExpression` (pre-execution eligibility) and `succeedExpression` (post-execution output acceptance)
- transition contracts must include explicit output evidence and explicit ownership of required inputs
- gate contracts must include machine-checkable pass predicates, required evidence references, and route coverage mapping
- workflow output must include preflight checklists for transitions, gates, and `AskUser` ownership before final JSON is emitted
- reject vague prose-only transition/gate wording when predicates or evidence paths are missing

## Workflow Designer Reference Pack And Evidence

Before dispatching the local AO workflow designer, create one bounded reference manifest. It must point to the fresh guide JSON and the actual returned guide file, the same exact-runtime `workflow.schema.json`, `workflow.demo.json`, and successful demo compile audit, the current `SKILL.md`, applicable `AGENTS.md`, current requirements, current workflow source, and the latest compile feedback when this is a revision.

Each entry records a normalized path, SHA-256, exact runtime version, `authorityRole`, read status, and validation result. Use these roles: `authority` for exact-runtime guide/schema/demo/demo-audit and version-matched runtime contract docs; `current_contract` for the current target contract, requirements, applicable rules, and workflow source; `diagnostic_evidence` for compile feedback and prior probe reports; `previous_runnable_reference` for an older runnable workflow only; and `supplemental` for generated C# shape files, small fixtures, and source excerpts. The schema, demo, and demo audit must share one generation-set identity.

The exact-runtime guide/schema/demo/demo-audit set is authoritative. Current requirements and target contracts define business scope. An old runnable workflow is a comparison reference, never a schema authority or copy template. If it is supplied, record `previousRunnableReferenceDisposition` with source version, source hash, copy time, reusable shapes, differences from the current schema and requirements, and rejected or deprecated items.

The designer writes three runtime-owned records under `<execution-output-root>/workflow-design/`: `reference-manifest.json` (`workflow-designer.reference-manifest.v1`), `static-contract-review.json` (`workflow-designer.static-contract-review.v1`), and `semantic-probe-report.json` (`workflow-designer.semantic-probe-report.v1`). Return descriptors containing normalized path, SHA-256, schemaVersion, verdict, and exact runtime version. Do not write these records into the skill bundle or treat them as a second workflow state.

## Layered Design Evidence Protocol

Use the same ordered validation layers for every workflow-design dispatch: `runtime`, `JSON`, `graph`, `enum`, `expression`, `projection`, `dataflow`, `gate`, `ownership`, and `semantic`. Stop at the first failed or required-unknown layer; after repair, rerun all earlier layers before continuing. Compile is a validation checkpoint after the static layers, not proof of semantic readiness.

Return three runtime-owned JSON records under `<execution-output-root>/workflow-design/`:

- `reference-manifest.json` with schemaVersion `workflow-designer.reference-manifest.v1` and one entry per bounded input. Every entry records normalized path, SHA-256, exact runtime version, `authorityRole`, read status, and validation result. The exact-runtime guide/schema/demo/demo-audit set shares one generation-set identity.
- `static-contract-review.json` with schemaVersion `workflow-designer.static-contract-review.v1`. It records each layer result, schema coverage, expression audit, projection matrix, gate-producer-route matrix, ownership review, plain-language review, and gate failure-guidance review.
- `semantic-probe-report.json` with schemaVersion `workflow-designer.semantic-probe-report.v1`. It records stable `probeId` values, same-runtime fixture and command evidence, expected/observed paths and types, artifact and source/copy/case/run identity evidence, and the verdict.

A required probe is any semantic behavior used by the candidate that involves an external or canonical projection, an emitter, a gate-consumed family, or source/copy/case/run identity. A required `failed` or `unknown` probe blocks readiness. Optional behavior not used by the candidate may remain `unknown`, but it cannot support a readiness claim. A previous workflow is `previous_runnable_reference` only and must have a source/version/hash/copy-time disposition with reusable shapes, current differences, and rejected items.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.


## Caller File Preparation Contract

The calling agent must create the full input set on disk before one AO CLI call and pass only paths. Prepare every required script, JSON, workflow, objective, reference, patch, context, instance, and result input in one step. The CLI preflights all required files before reading or writing. Inline script, JSON, and replacement content is not a supported input form.
## Blocked-Route History And Replanning

When AO confirms that the current route cannot progress, do not send only the latest blocked payload to the planner. Persist and pass a structured `replan_history` containing:

- the current workflow and blocked node identifiers
- the blocker reason and the exact unmet requirement
- ordered attempted actions, outcomes, and evidence references
- the latest event log and audit artifact references
- the selected replan anchor and strategy

The planner must choose one explicit strategy:

- `continue_from_current`: continue from the current state with a new viable bridge
- `rollback_to_unconfirmed`: return to the latest unconfirmed or not-yet-designed node and design forward from there
- `redesign_from_current`: preserve completed history but replace the failing continuation
- `full_redesign`: discard the current route design while retaining historical evidence and the terminal business objective
- `reversible_workaround`: apply the smallest reversible workaround, with one-step rollback evidence

Every strategy must produce a candidate path that can reach the terminal business outcome. A workaround must additionally provide a rollback plan. Do not silently erase failed attempts, blocker history, or previous route decisions when generating `prompt-replan` input.

## Self-Contained Runtime Contract

There is one published package-channel runtime: the exact-version `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package for the detected host. The host selects one supported RID from OS, architecture, and Linux libc. Do not probe for an installed .NET host, assemble a DLL/Roslyn bundle, or switch runtime modes.

An explicit `repo-src-debug` override is allowed only when the user is debugging this repository's current source. It is not the official package-channel execution path.

## Runtime Acquisition

- Read the exact AO version from the current skill version block or package lock; derive `released` or `beta` from that version when needed. Never request `latest` or a version range.
- Before network access, reuse only a valid exact package in the standard NuGet global-packages cache. Otherwise download the exact package and verify it against NuGet registration `catalogEntry.packageHash`.
- A same-version GitHub Release fallback is allowed only when its `.sha512` sidecar validates. Do not add a fixed restore script, resolver, Loom-specific cache, or cache lock.
- On Windows PowerShell 5.1, treat `.nupkg` as ZIP content and use a ZIP-aware API; add `-UseBasicParsing` when using `Invoke-WebRequest` or `Invoke-RestMethod`.
- Before extraction, verify package ID/version/RID, SHA-512, root nuspec, `tools/<rid>/runtime.json`, archive path and size limits, apphost, and the complete English guide tree. Reject traversal, duplicate entries, oversized data, and unexpected files. Extract to an external per-run directory only after validation.
- If package validation, safe extraction, apphost startup, or guide retrieval fails, stop and preserve failed evidence. Do not substitute a repository build or another RID.

## Extracted Package Guide Entry

This skill publishes no `ao-guide*.md` file. The exact runtime package carries the English docs beside its apphost.

1. Detect one supported RID and acquire the exact locked AO package.
2. Verify package integrity and safely extract it to an external per-run directory.
3. Run `ao.exe --guide` on Windows or `ao --guide` on Unix as the first runtime operation.
4. Parse the JSON result and verify the exact version, absolute `docs_root` and `guide_path`, containment, and readability. Do not accept failed stderr as guide evidence.
5. Use the fresh guide as the version-specific authority. Continue with the same extracted apphost for schema/demo, compile, run, and resume.

## Runtime Flow Details

- Do not start planning, authoring, validation, compile, `prompt-plan`, `prompt-replan`, run, resume, or downstream input collection before the fresh guide result is readable.
- After guide validation, keep official execution on the same published AO package apphost. A guide result is not permission to switch to repository builds or hand-assembled runtimes.
- Direct apphost compile is validation only. Official skill runs use `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix against the same external workflow instance and persisted state.
- Use `ao.exe --guide`, `ao.exe prompt-plan`, `ao.exe prompt-replan`, and `ao.exe compile` on Windows; use the same command names with `ao` on Unix.

## Think-Out-Loud Required Fields

Report exact package and apphost fields after preparation, after every AO apphost execution, and on each progress update:

- `resolved_runtime_version`
- `runtime_package_id`
- `runtime_rid`
- `runtime_package_sha512`
- `runtime_extraction_directory`
- `runtime_apphost_path`
- `runtime_preflight_result`

Report audit fields after every AO apphost execution and on each progress update:

- `audit_markdown_file`
- `audit_html_file`
- `mermaid_file`
- `html_file`
- `analysis_file`
- `dataflow_file`
- `must_show_to_user_files`
- `workflow_location_summary`
- `execution_confidence`
- `estimated_overall_progress`

Every direct apphost execution (`ao.exe` or `ao`) must begin its think-out-loud update with the current verified Mermaid, HTML, Analysis, and Dataflow artifacts in that order. Each artifact is a Markdown link immediately followed by a `text` fence containing the same normalized path. Then print localized headings for execution confidence and estimated overall progress. Use only paths verified by the current call or latest verified continuity set. For `not_emitted`, say the render is unchanged; for `runtime_path_only`, keep paths as technical evidence; for `delivery_failed`, report the failure and next action without a guessed link.

`must_show_to_user_files` lists the same ordered audit paths for the current binary execution. It is an audit list, not a link guarantee. A card or notification may supplement, but cannot replace, the verified link-and-fence block.
## Plain-Language Feedback For Every Language

Write every user-facing progress, blocked, error, and completion update in the user's requested language for a high-school reader with no workflow background. English is not automatically plain language. Use short sentences and everyday words; state what happened, whether the user's work or data is still safe, why it happened, and the next action, in that order. Translate internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon before exposing exact technical details. Keep commands, paths, IDs, and evidence fields in a separate technical-details section only when needed. This rule also applies to skill being enhanced feedback reported through AO.

## Business-Outcome-First Gate

- If objective/plan clearly requests business outputs, completion requires business deliverables plus AO completed state.
- Runtime-only or meta-only reporting cannot replace business delivery completion.
