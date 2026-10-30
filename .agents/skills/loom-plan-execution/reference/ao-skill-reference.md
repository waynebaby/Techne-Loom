# AO Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-plan-execution/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-plan-execution` needs to create or revise workflow JSON:

- [../assets/agents/loom-plan-execution-workflow-designer.agent.md](../assets/agents/loom-plan-execution-workflow-designer.agent.md)

Pass relative links to the plan file, guide file, workflow file, audit artifacts, and any blocked payload evidence so the subagent runs with explicit local context instead of relying on repository-global discovery.

That declared `.agent.md` file is the authoritative behavior contract for the workflow-designer subagent. Do not require a mirror into `.github/agents/`, user-profile agent roots, or other discoverable agent folders. Dispatch follows [Named Agent Resolution](../SKILL.md#named-agent-resolution): preflight the host agent list against the exact frontmatter name, call it directly only when it is registered, and otherwise use a capable non-read-only driver with inline or path-only delivery. Treat `agent not found`, an unknown agent, no `agentName` parameter, or a silent default-agent response as not registered; driver fallback does not require user approval. Do not replace this route with a freeform approximate role or repository-global substitute prompt.

The subagent must generate node-level granularity where each node owns one visible responsibility and where every AO weave-out path has a detailed blocked-action hint.

The subagent must also enforce deterministic transition/gate contracts and fail-closed anti-hallucination behavior:

- transition contracts must use executable boolean predicates for `guardExpression` (pre-execution eligibility) and `passExpression` (post-execution output acceptance); treat `succeedExpression` as legacy input to migrate, not a field to author.
- transition contracts must include explicit output evidence and explicit ownership of required inputs
- gate contracts must include machine-checkable pass predicates, required evidence references, and route coverage mapping
- workflow output must include preflight checklists for transitions, gates, and `AskUser` ownership before final JSON is emitted
- reject vague prose-only transition/gate wording when predicates or evidence paths are missing

## Workflow Designer Reference Pack And Evidence

Before dispatching the local AO workflow designer, create one bounded reference manifest. It must point to the fresh guide JSON and the actual returned guide file, the same exact-runtime `workflow.schema.json`, `workflow.demo.json`, and successful demo compile audit, the current `SKILL.md`, applicable `AGENTS.md`, current requirements, current workflow source, and the latest compile feedback when this is a revision.

Each development-support entry records a normalized path, exact runtime version, `authorityRole`, read status, and validation result. A SHA-256 may be generated and recorded as optional metadata, but consuming agents must not calculate, compare, re-attest, freshness-check, or gate on support hashes. Use these roles: `authority` for exact-runtime guide/schema/demo/demo-audit and version-matched runtime contract docs; `current_contract` for the current target contract, requirements, applicable rules, and workflow source; `diagnostic_evidence` for compile feedback and prior probe reports; `previous_runnable_reference` for an older runnable workflow only; and `supplemental` for generated C# shape files, small fixtures, and source excerpts. The schema, demo, and demo audit must share one generation-set identity from the same apphost; that identifier groups evidence and is not a freshness proof.

The exact-runtime guide/schema/demo/demo-audit set is authoritative. Current requirements and target contracts define business scope. An old runnable workflow is a comparison reference, never a schema authority or copy template. If it is supplied, record `previousRunnableReferenceDisposition` with source version, source path, copy time, reusable shapes, differences from the current schema and requirements, and rejected or deprecated items; its source hash is optional metadata and not a freshness gate.

The designer writes three runtime-owned records under `<execution-output-root>/workflow-design/`: `reference-manifest.json` (`workflow-designer.reference-manifest.v1`), `static-contract-review.json` (`workflow-designer.static-contract-review.v1`), and `semantic-probe-report.json` (`workflow-designer.semantic-probe-report.v1`). Return descriptors containing normalized path, schemaVersion, verdict, and exact runtime version; optional support hashes may be recorded during generation but are not required or compared during consumption. Do not write these records into the skill bundle or treat them as a second workflow state.

## Layered Design Evidence Protocol

Use the same ordered validation layers for every workflow-design dispatch: `runtime`, `JSON`, `graph`, `enum`, `expression`, `projection`, `dataflow`, `gate`, `ownership`, and `semantic`. Stop at the first failed or required-unknown layer; after repair, rerun all earlier layers before continuing. Compile is a validation checkpoint after the static layers, not proof of semantic readiness.

Return three runtime-owned JSON records under `<execution-output-root>/workflow-design/`:

- `reference-manifest.json` with schemaVersion `workflow-designer.reference-manifest.v1` and one entry per bounded input. Entries require normalized path, exact runtime version, `authorityRole`, read status, and validation result. The exact-runtime guide/schema/demo/demo-audit set shares one generation-set identity; support hashes are optional generated metadata, not consumer gates.
- `static-contract-review.json` with schemaVersion `workflow-designer.static-contract-review.v1`. It records each layer result, schema coverage, expression audit, projection matrix, gate-producer-route matrix, ownership review, plain-language review, and gate failure-guidance review.
- `semantic-probe-report.json` with schemaVersion `workflow-designer.semantic-probe-report.v1`. It records stable `probeId` values, same-runtime fixture and command evidence, expected/observed paths and types, artifact and source/copy/case/run identity evidence, and the verdict.

A required probe is any semantic behavior used by the candidate that involves an external or canonical projection, an emitter, a gate-consumed family, or source/copy/case/run identity. A required `failed` or `unknown` probe blocks readiness. Optional behavior not used by the candidate may remain `unknown`, but it cannot support a readiness claim. A previous workflow is `previous_runnable_reference` only and must have a source/version/path/copy-time disposition with reusable shapes, current differences, and rejected items; source hashes are optional metadata and never freshness gates.

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
- Resolve the current user's OS temporary directory through a host-native API. Share `<OS-temp>/TechneLoom/runtime-cache/` across AO and SO skills; never put cache state in a skill bundle or workspace, and never let it override the exact version lock or detected RID.
- A generation contains the verified `.nupkg` and its validated apphost/docs extraction. Its identity is exact package ID, version, RID, and the full verified `.nupkg` SHA-512 in lowercase hex. A directory suffix may start with four hash characters, but extend it on any collision and always compare the full package hash.
- Reuse a matching generation only after recomputing the `.nupkg` SHA-512 and rechecking package ID/version/RID, nuspec, `runtime.json`, archive size and safe paths, apphost, and English guide files.
- On a complete cache miss, follow the L0-L6 acquisition policy in [the plan-execution skill](../SKILL.md#dependency-acquisition-fallback-policy) for the exact locked AO package. Do not install a runtime/downloader. After any failed request, do not automatically switch transports to repeat the same artifact/source; a nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted and approved. A blocked or denied source is a stop across every transport.
- Compare newly acquired NuGet bytes with `catalogEntry.packageHash` from the exact registration catalog entry. Use a same-version GitHub Release only when it is an independently configured and approved L4 source with a matching `.sha512` sidecar. Never float to `latest`.
- Stage and validate a new or repaired generation in a unique sibling directory, then publish it with an atomic same-volume rename. Never overwrite a different valid package or mutate files used by a running apphost. On a prefix collision, extend the prefix; do not replace the existing generation. Do not add a cache lock, fixed checked-in restore script, second resolver, or launch descriptor.
- On Windows PowerShell 5.1, treat `.nupkg` as ZIP content and use a ZIP-aware API. `Invoke-WebRequest` and `Invoke-RestMethod` are L6 and require explicit per-operation approval; a direct in-process .NET `HttpClient` call from PowerShell is L3. Add `-UseBasicParsing` only if that specific request was approved.
- If package validation, safe extraction, apphost startup, or guide retrieval fails, stop and preserve failed evidence. Do not substitute a repository build or another RID.

### NuGet SHA-512 Troubleshooting

- Confirm the exact package ID and version from the skill lock. Obtain `https://api.nuget.org/v3/registration5-semver1/<lowercase-package-id>/<version>.json` only through an available L1-L3 capability or a specifically approved L6 operation; follow the selected registration item's `catalogEntry` link and read `packageHash` from that exact catalog record. `packageHash` is Base64-encoded SHA-512; `packageSize` is a useful cross-check, not a substitute for the hash.
- Hash the exact downloaded `.nupkg` bytes, not the extracted directory or an individual package entry. This local calculation does not authorize or perform a download. In PowerShell, convert the registration hash and compare it with `Get-FileHash`:

```powershell
$expectedHex = [BitConverter]::ToString([Convert]::FromBase64String($packageHash)).Replace('-', '')
$actualHex = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash
if ($actualHex -ine $expectedHex) { throw 'The package does not match the NuGet registration SHA-512.' }
```

- On mismatch or an unavailable catalog record, do not extract or publish the package. Preserve the package ID/version/RID, source URL, expected and actual hashes, and failure reason. Use a GitHub asset only if it is independently configured and approved as an exact same-version source with its matching `.sha512` sidecar. If the NuGet source was blocked or denied, never retry it through another transport; stop if no independent approved source exists.

## Extracted Package Guide Entry

This skill publishes no `ao-guide*.md` file. The exact runtime package carries the English docs beside its apphost.

1. Detect one supported RID and acquire the exact locked AO package.
2. Verify package integrity and safely extract it to an external per-run directory.
3. Run `ao.exe --guide` on Windows or `ao --guide` on Unix as the first runtime operation.
4. Parse the JSON result and verify the exact version, absolute `docs_root` and `guide_path`, containment, and readability. Do not accept failed stderr as guide evidence.
5. Use the fresh guide as the version-specific authority. Continue with the same extracted apphost for schema/demo, compile, run, and resume.

### Runtime Documentation Path Resolution

After the fresh guide result is validated, use its absolute `docs_root` and `guide_path` for runtime documentation. Verify every additional page is readable, version-matched, and contained under `docs_root`; pass resolved paths through `referencePackManifest` to subagents. Never resolve runtime docs from the installed skill root. Translated pages are localization-only and cannot replace English authority.

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

Every direct apphost execution (`ao.exe` or `ao`) must begin its think-out-loud update with the current verified Mermaid, HTML, Analysis, and Dataflow artifacts in that order. Each artifact is a Markdown link immediately followed by a `text` fence containing the same complete absolute verified runtime address, normalized with `/`. Do not substitute a workspace-relative address or shorten the path. Then print localized headings for execution confidence and estimated overall progress. Use only paths verified by the current call or latest verified continuity set. For `not_emitted`, say the render is unchanged; for `runtime_path_only`, keep paths as technical evidence; for `delivery_failed`, report the failure and next action without a guessed link.

`must_show_to_user_files` lists the same ordered audit paths for the current binary execution. It is an audit list, not a link guarantee. A card or notification may supplement, but cannot replace, the verified link-and-fence block.
## Plain-Language Feedback For Every Language

Write every user-facing progress, blocked, error, and completion update in the user's requested language for a high-school reader with no workflow background. English is not automatically plain language. Use short sentences and everyday words; state what happened, whether the user's work or data is still safe, why it happened, and the next action, in that order. Translate internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon before exposing exact technical details. Keep commands, paths, IDs, and evidence fields in a separate technical-details section only when needed. This rule also applies to skill being enhanced feedback reported through AO.

## Business-Outcome-First Gate

- If objective/plan clearly requests business outputs, completion requires business deliverables plus AO completed state.
- Runtime-only or meta-only reporting cannot replace business delivery completion.
