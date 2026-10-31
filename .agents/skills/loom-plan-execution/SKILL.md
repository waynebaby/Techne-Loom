---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and Loom Agent Plan-Execution Orchestrator runtime surfaces.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the package and guide surface that matches the current CI/CD-managed skill package version block, then routes execution through the applicable Loom Agent Plan-Execution Orchestrator runtime surface.

Once the skill-bound version is chosen, detect one supported host RID, acquire and verify that exact published AO self-contained package, extract it safely, then directly run `ao.exe --guide` on Windows or `ao --guide` on Unix. Verify the JSON version and readable contained `guide_path` before planning or downstream work. The package carries the English docs; no guide page is embedded in the apphost.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local Loom Agent Plan-Execution Orchestrator repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill uses Loom Agent Plan-Execution Orchestrator as the official execution authority. Only direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix count as official skill runs. Other paths remain outside official execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## Read This First

- Shared terminology authority: use the English workflow-terminology page supplied through the validated runtime `referencePackManifest` when available; Chinese mirrors are localization aids only. Do not locate documentation by traversing from an installed skill directory.

- Runtime binding authority: this skill records only the exact bound AO package version. The host derives OS, architecture, Linux libc, RID, and apphost from the current machine; do not persist those transient details in checked-in skill state.

## Published Runtime Transport Rule

Published AO execution uses its direct self-contained apphost and never requires MCP. Optional SO/MCP support is a separate later workflow concern; it cannot gate AO package acquisition or guide capture. A dispatched MCP application failure remains a failure.

## Agent-Facing User Questions

Use `/loom-ask-user` by default for structured clarification or choices that an agent needs from the user outside an authored workflow. Batch known independent questions into one ordered form and return the validated typed answers and receipt to the caller. Do not make a workflow prerequisite. Use an optional AO `AskUser` node only when an authored workflow owns a user-owned value and must project the answer back into that same `WorkflowInstance`; runtime-owned facts, provenance, and artifact paths stay on runtime-owned boundaries. If the selected published apphost lacks standalone `ask`, report the exact capability gap instead of silently substituting an agent-native form or workflow node.

## Dependency Acquisition Fallback Policy

Apply the shared L0-L6 ladder to exact AO package acquisition and any dependency this skill needs; never install a runtime or downloader solely to acquire an artifact.

| Tier | Preferred capability | Rule |
| --- | --- | --- |
| L0 | Existing local artifact or cache | Reuse only after exact identity/version/RID and required integrity are verified. |
| L1 | Agent/Host-controlled fetch | Use a controlled fetch for the exact artifact from its configured primary source; never start a downloader subprocess. |
| L2 | Existing `nuget.exe` | Reuse only if already installed and executable for the exact NuGet package; do not install `nuget.exe`, .NET Framework, or an SDK. |
| L3 | Existing runtime native HTTP API | Use an installed runtime in-process API (for example Node `fetch`, Python `urllib`, or .NET `HttpClient`); do not spawn `curl`/`wget` or install a runtime. A direct in-process .NET `HttpClient` call from PowerShell is L3; PowerShell `Invoke-WebRequest`/`Invoke-RestMethod` remain L6. |
| L4 | Known GitHub MCP release asset | Use only a preconfigured/approved repository, exact tag/asset and matching sidecar, and only when the capability is already exposed without registering or probing MCP before fresh `--guide`. |
| L5 | Other approved Host/MCP source | Use only an already-available, configured source and exact artifact with integrity evidence; do not make MCP registration or inspection a runtime-startup gate. |
| L6 | Generic network CLI | Last resort only after L0-L5 are unavailable, with explicit per-operation approval naming the tool, source/host, exact artifact/version, destination and purpose. |

PowerShell `Invoke-WebRequest`/`Invoke-RestMethod`, `curl`, `gh`, `wget`, BITS tools/cmdlets and equivalent first- or third-party download CLIs are all L6; being preinstalled or first-party does not exempt them. The only earlier CLI exception is an already-installed `nuget.exe` at L2. After any failed request, do not automatically switch transports to repeat the same artifact/source. A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted and the user approves that exact CLI action. If the source or request is explicitly blocked or denied by network/security policy, stop all attempts to that source through every transport; continue only with an independently configured and approved fallback source, otherwise stop and report the blocker. If the user denies CLI approval, stop and do not switch tools or sources to evade that decision. User approval does not override host security policy and cannot guarantee that an alert will not fire.

For every new NuGet package, verify its exact package ID/version/RID and bytes against the exact registration `catalogEntry.packageHash` (SHA-512), then validate nuspec, `runtime.json`, size/path safety and apphost before extraction. A same-version GitHub package needs the configured matching `.sha512` sidecar. Required business artifact hashes remain mandatory; optional development-support hashes are not consumer freshness gates.

## Named Agent Resolution

The [workflow designer](./assets/agents/loom-plan-execution-workflow-designer.agent.md) is a compact entrypoint. Its required contract documents are linked in reading order and ship beside it under `assets/agents/`. For direct and driver dispatch, require the complete entrypoint and every linked contract document before work begins. For inline delivery, also pass the resolved entrypoint path so relative links resolve in the active skill copy. If a reference is missing or unreadable, follow the designer's Missing Reference Fallback: try its relative path first, then the same skill-relative path in the current user's matching installed skill under `~/.agents/skills/` or other host-supported skill roots. Record the selected copy and block only after no readable matching document can be resolved; never substitute an entrypoint-only summary. Preserve this full asset set when distributing or installing the skill.

Every `.agent.md` named by this skill is the exact behavior contract for that role; host registration is only a dispatch mechanism, not a capability gate.

- Identify the role by the exact `name` in the file's frontmatter, not by the file stem. For the workflow designer that name is `loom-plan-execution Workflow Designer`. Resolve the file from this skill's `assets/agents/` folder; in standalone installs, resolve the same relative path under the active skill root, such as `~/.agents/skills/<skill-folder>/` or `~/.claude/skills/<skill-folder>/`. Use the current repository/workspace copy first and the matching global installed-skill copy second.
- Preflight before dispatch: compare that exact name with the agent list the host exposes for subagent calls. Skill-local `assets/agents/` files are usually not registered (VS Code does not register them), so a missing name means go straight to the driver route below; do not make a direct call that is expected to fail.
- If the name is registered, call it directly. Treat any of these results as not registered and switch to the driver route in the same step: `agent not found`, an unknown agent or invalid agent name, a subagent tool with no `agentName` (or equivalent) parameter, or a call that was silently served by a default agent that did not acknowledge the declared role.
- Driver route: call an available registered general-purpose agent that can edit, run commands, and validate (in VS Code, the default agent with `agentName` omitted). Never use a read-only agent such as `Explore` when the contract requires design, editing, or validation, and never substitute a similarly named role. In the dispatch, state the exact frontmatter name and a clickable Markdown link to the exact `.agent.md`, then deliver the contract in one of two ways:
	- Inline: include the full file content.
	- Path-only: give the resolved file path, without a hash, and instruct the driver to read the entire file itself before acting. The driver must first echo the frontmatter `name` it read and must stop if it cannot read the file.
- Both delivery modes must also pass all required inputs and reference context; pass large inputs by path. Never pass only a summary in place of the contract.
- Prefer path-only delivery when the agent file plus inputs is large. A request-size error, HTTP 408, or timeout is a transport failure, not a missing agent: retry once in path-only mode with inputs passed by path, then report the transport blocker if it fails again.
- Driver fallback does not require user approval; it is normal operation. Do not pause to ask the user merely because the dedicated name is not registered.
- Stop only when the exact file is missing or ambiguous, or no capable non-read-only driver exists. Then preserve failed evidence, report the concrete blocker, and do not claim completion or advance dependent checks. Doing the role's work directly in the main conversation without any subagent driver requires explicit user approval.
- Record every dispatch: exact frontmatter name, `.agent.md` link, route (`direct` or `driver-fallback`), delivery mode (`inline` or `path-only`), fallback trigger evidence, and the driver's name echo.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.
## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.

## Selective JSON Reading

For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective inspection: use search or regex only to locate the target key or path, then read a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract the needed subtree. Read the full JSON only when the user explicitly requests it or whole-document validation requires it. This rule applies to JSON data; `.agent.md` role contracts remain a separate full-read requirement.

## Guide Hub Structure

The repository source guide pages live under `docs/en/guides/` and are packaged recursively into the exact runtime documentation bundle. This source-tree location is not relative to the installed skill; at runtime use the absolute `docs_root` and `guide_path` returned by a successful exact-apphost `--guide` result. This skill publishes no AO guide files; use `reference/ao-skill-reference.md` for runtime acquisition and the fresh extracted guide for version-specific authority.
<!-- skill-package-version-block:start -->
- Current published AO package runtime version: `0.4.10`.
- This block is refreshed by the publish workflows whenever AO package versions change, so the skill contract stays aligned with the latest published stable package set.
<!-- skill-package-version-block:end -->





































































Follow the current skill package version block first, then derive the matching package surface:

- Package indexes remain skill-local references; runtime AO guide paths are the absolute `docs_root` and `guide_path` returned by the selected apphost's successful `--guide`, not paths relative to this skill.
- Do not add or publish any AO guide file under this skill; use the fresh guide returned by direct `ao.exe --guide` or `ao --guide` from the exact locked package.

- Workflow designer subagent: `assets/agents/loom-plan-execution-workflow-designer.agent.md`
- Loom Skill Orchestrator governance baseline assets for AO enhancement:
	- Per-run plan output: `<execution-output-root>/plan/skill-plan.md` (runtime-owned; not a stable skill asset)
	- `assets/so-workflow/so-template.json`
	- `assets/so-workflow/so-package-lock.json`
	- `assets/so-workflow/node-to-file-map.md`
- `assets/so-workflow/reference/ao/runtime-contracts.md`
- `assets/so-workflow/reference/ao/runtime-behavior.md`
- `assets/so-workflow/reference/document-copy-manifest.json`

## Plain-Language Feedback For Every Language

All user-facing progress, blocked, error, and completion updates from this skill must be understandable to a high-school reader with no workflow background, in the language requested by the user. English is not automatically plain language, and the rule applies equally to every supported language.

Use short sentences, familiar words, and direct verbs. Say four things in order: what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and exactly what will happen next.

Do not make the reader translate status values, step kinds, node IDs, gate names, handoff terms, runtime details, or audit jargon. Explain a necessary technical word in ordinary language before showing its exact name. Keep commands, paths, IDs, and payload fields in a separate `Technical details` line only when they help the user act or verify the result. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the user-facing explanation; keep exact identifiers in technical details or evidence only. The same rule and the term examples below apply to any skill being enhanced feedback reported through AO; for the skill being enhanced instructions should carry a compact version of them.

## Artifact Address Usability

Use the complete local file URL for every artifact address in the standard three-slash form (`file://` plus an empty authority plus the absolute path): Windows `file:///C:/...`; Linux/macOS `file:///...`. Drive-letter case is insignificant; never use the two-slash `file://C:/...` form, which places the drive letter in the URL authority and does not open. Verify that the URL resolves to an existing readable file in the active host. If the file URL cannot be opened, create and verify a mirror in the active workspace and use the mirror URL instead. Encode every space in the URL as `%20`; use that identical encoded URL in the clickable Markdown link and following text fence. Never return a literal-space URL, bare path, truncated or stale URL, or code fence without a link. If no URL opens, report the delivery blocker.

## Per-Execution Progress Log

For each AO post-execution progress report, write the complete Markdown entry to a separate external file. Include ISO 8601 UTC and local timestamps with numeric UTC offset and OS timezone ID. Resolve this skill's root to an absolute path and invoke its bundled helper under `assets/scripts/`: Windows uses `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <active-skill-root>/assets/scripts/AppendProgressReport.ps1 -TargetFile <execution-output-root>/progress-report.md -EntryFile <entry-file>`; Linux/macOS uses `bash <active-skill-root>/assets/scripts/AppendProgressReport.sh --target-file <execution-output-root>/progress-report.md --entry-file <entry-file>`. The helper reads only the entry file and appends to the target. It returns the target's full file URL; reuse that exact URL if reporting the log location. Never read, open, or inspect the existing `progress-report.md`, and never use manual append or read-modify-write. If the helper fails, report the failure and pause later business-step progression without changing workflow state.

## Plain-Language Term Examples

The left column is for machine records. The right column shows the meaning to express to a user. These English sentences are examples, not fixed output: translate the meaning into the user's requested language. English is not a substitute for simple language in another locale.

| Internal wording | Say this in the user's language |
| --- | --- |
| `Done` | "The requested work is complete." |
| `WaitResume` | "I need your information or confirmation before I can continue." |
| `SubagentCall` | "A specialist is checking this part." |
| `gate` | "A required check has not passed yet." |
| `transition` | "The next step is to ..." |
| `seam` or `boundary` | "The work is waiting at a handoff point." |
| `frontier` | "There are a few possible next actions." |
| `runtime` | "the program that is running the task" |
| `render unchanged` | "The earlier diagram is still valid; no new diagram was needed." |

### Example: output folder already has a record

Internal note: `step-0008-compiled` already exists; the render is unchanged.

User-facing update: "The task itself is fine. The output folder already has the earlier record, so this run did not overwrite it. The earlier diagram and report are still valid. I will use a new output folder and continue the same saved run."

### Example: the review found unresolved problems

Internal note: the review returned four findings, but the clean-review check was claimed before classification and repair.

User-facing update: "The review found four problems. The process stopped too early because it treated the review as finished instead of checking whether the problems were fixed. I will sort and fix the four problems, then check again."

### Example: waiting for a decision

Internal note: the task is waiting at a user-input step.

User-facing update: "I need one decision from you before I can continue: [state the decision in one short sentence]."

Do not copy the internal note into the user-facing update. Keep exact commands, paths, IDs, and evidence fields in a separate `Technical details` section when they are needed for action or verification.


## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Runtime version authority: the current CI/CD-managed skill package version block; derive `released` versus `beta` from that bound version when needed
- Guide input: directly run `ao.exe --guide` on Windows or `ao --guide` on Unix; it is English-only and returns JSON with `version`, `docs_root`, and `guide_path`.
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Apply these defaults during Loom Agent Plan-Execution Orchestrator-based plan execution:

- Loom Agent Plan-Execution Orchestrator is the only official execution authority for this skill; official runs use direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix.
- Business-outcome-first is mandatory when plan content clearly targets business deliverables; runtime/meta-only mode requires explicit user intent.
- Official AO runtime uses the exact version supplied by this skill and the single product+RID package selected by the host. No host probe, DLL closure, second runtime mode, or cross-mode fallback is supported.
- In Windows PowerShell 5.1 package-channel mode, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`; use ZIP APIs or an equivalent ZIP-based extraction path.
- Apply the L0-L6 policy above to generic network CLIs. PowerShell web commands, `curl`, `gh`, `wget`, BITS and equivalent downloaders are L6 and require explicit per-operation approval after L0-L5. A failed request must not automatically trigger a same-source transport switch; a nonblocked source may be retried only through explicit L6 approval after managed options are exhausted. A blocked or denied source is a stop across every transport.
- If package validation, extraction, apphost startup, or guide execution fails, stop with failed evidence. Never record success from stderr; keep only the successful JSON result and readable `guide_path` returned by the direct apphost.
- In repo-src-debug mode, build and use the current repository Loom Agent Plan-Execution Orchestrator output only as an explicit debug override.
- Keep checked-in source plans/snapshots immutable and keep mutable runtime state in the same external workflow file or explicit execution-output roots.
- Write valid workflow, template, schema, demo, runtime-copy, audit-backup, and compile-feedback JSON outputs as indented multi-line JSON. Keep compact JSON only for JSONL, MCP/CLI wire payloads, and explicit canonical hash projections.
- Output targets may be outside the Git worktree or ignored by Git. Return normalized real paths, verify each output exists and is readable, and use a verified workspace-relative mirror for direct editor opening when `--workspace-root` is available; Git tracking is never a delivery condition.
- After every AO apphost call or audit-producing step, including `ao.exe` and `ao` calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order. Each title must link to the complete absolute verified runtime file address (Windows drive path or Unix absolute path), normalized with `/`; immediately follow it with a `text` fence whose body contains only the identical full address. Never use a workspace-relative address, shorten, truncate, ellipsize, or add labels/commentary inside the fence. Preserve this exact pair shape and artifact order. If a file is not emitted or verified, apply the delivery-failure/continuity rule and do not invent its address. Follow the block with localized `##` headings for execution confidence and estimated overall progress in that order, each with one short reason. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress measures approximate completion of the whole request. Base both on verified evidence; no fixed formula or threshold is prescribed, and neither percentage replaces required completion checks. Use only current or latest verified paths; on `not_emitted`, say the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. Keep user-facing text plain and in the active interaction language.
- For `runtime_path_only`, include the complete verified absolute address in the fixed artifact report. Use a verified workspace mirror only for a separate host/editor open action when `link_resolvable=true`; do not replace or suppress the report address.
AO is CLI-only. Execute the extracted AO apphost directly; dynamic MCP registration, a runtime descriptor, and an MCP preflight are not required for guide, compile, run, or resume.

- For every full-delivery execution of this skill itself, direct apphost compile is validation only; it is not an end state.
- After guide handoff and compile validation, use one fresh external runtime workflow copy. Run the same apphost against that copy, then use that apphost and persisted state for every resume until terminal completion.
- If the runtime returns a blocked state, preserve its evidence and resume the same workflow copy. Continue until terminal completion or a documented unrecoverable failure; a blocked payload alone is never completion.
- Never claim governed completion from local orchestration, direct scripts, repository-debug output, guide success, prompt planning, compile success, or an unresumed block.
- If the published apphost cannot start, preserve failed package/startup evidence and stop. Do not substitute a local or helper execution path.

Detailed assumptions, startup contracts, output matrices, and anti-drift rules live in the reference docs:

- Local skill reference: `reference/ao-skill-reference.md`
- Producer output evidence: [reference/producer-output-evidence-contract.md](reference/producer-output-evidence-contract.md); inspect target skill assets and available run evidence before asking the user for a schema or sample.

Workflow generation or revision for this skill must use the local workflow-designer subagent with context-rich relative links, not a freeform generic agent call:

- `assets/agents/loom-plan-execution-workflow-designer.agent.md`

When using that workflow-designer route, enforce deterministic workflow authoring contracts instead of descriptive-only prose:

- each transition must declare structured executable `guardExpression` and canonical `passExpression` predicates, explicit evidence outputs, and explicit seam ownership; `succeedExpression` is a legacy input to migrate, not a field to author
- every context path must be declared in the owning contract's `context_fields` catalog; each `context.Get<T>(path)` type and each producer/output projection must match the declared JSON type; use `context.Has(path)` for presence-only checks
- Map the skill being enhanced's `SKILL.md` first as Priority 1 in `assets/so-workflow/node-to-file-map.md`; include its exact target-relative path, owning nodes, requested changes, and validation evidence before supporting files.
- For every added or changed `context_fields` entry, follow [Producer Output Evidence Discovery](reference/producer-output-evidence-contract.md) first: the driver inspects target skill contracts, assets, and available run/probe results, then compares the declared type with independent producer evidence. Ask the user only for a necessary gap that remains after discovery and cannot be safely probed. Record `contextFieldTypeComparisons` in `static-contract-review.json`; unknown type evidence blocks readiness before broad tests.
- Every target skill's `SKILL.md` must carry the conditional `Runtime Documentation References` rule: when the target or its local agents need AO/SO runtime docs, resolve English pages from the selected exact apphost's successful `--guide` JSON `docs_root`, verify version/absolute paths/containment/readability, pass exact paths in `referencePackManifest`, and never construct paths relative to the installed skill.
- each gate must declare machine-checkable pass predicates, required evidence references, and route coverage mapping
- weave-out hints must preserve resume continuity contract fields and expected payload/evidence shapes
- final workflow proposals must include transition, gate, and ownership preflight checklists before JSON output is accepted
- fail closed: reject vague transition/gate wording that lacks concrete predicates or evidence paths

That exact `.agent.md` file is the authoritative behavior source for the workflow-designer subagent. Do not require it to be mirrored into `.github/agents/`, a user-profile agent folder, or any other discoverable agent root before use. Dispatch it only through [Named Agent Resolution](#named-agent-resolution): preflight the host agent list, call the exact frontmatter name when it is registered, and otherwise use the driver route with inline or path-only delivery; driver fallback does not require user approval. Do not replace this route with a freeform approximate agent role.

## Workflow Designer Reference Pack

Before dispatching `assets/agents/loom-plan-execution-workflow-designer.agent.md`, provide a bounded `referencePackManifest` and fresh `schemaDemoInput` from one selected AO apphost. Include the successful guide JSON and returned guide file, same-apphost schema/demo/successful demo compile audit, current target contract and requirements, current workflow source, applicable `AGENTS.md`, and latest compile feedback when revising.

Every development-support reference entry records its normalized path, exact runtime version, authority role, read status, and validation result. A SHA-256 may be recorded when the reference is generated, but it is optional metadata. While consuming references, do not calculate, compare, re-attest, freshness-check, or gate on support hashes. A `generationSetId` groups the same-apphost schema/demo/compile evidence; it is not a freshness proof. Exact runtime version, readable files, schema validity, and successful same-apphost demo compilation remain required. An older workflow is `previous_runnable_reference` only and needs a source version/path/copy-time/differences/rejected-item disposition; its source hash is optional metadata.

- Resolve runtime documentation only from the selected apphost's successful English `--guide` result: verify its exact version, absolute `docs_root` and `guide_path`, containment, and readability; resolve only needed English pages under that root and pass their exact paths via `referencePackManifest`. Translated pages are localization aids only when supplied. Never construct documentation paths relative to the installed skill.

The designer must return runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` with schema versions `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Descriptors require path, schemaVersion, verdict, and exact runtime version; optional support hashes may be recorded when generated but are not consumed as freshness gates. Failed or unknown required semantic probes prevent readiness; compile success alone is not semantic evidence. Exact package SHA-512 and every artifact hash explicitly required by the target business contract remain mandatory.

## Phase-Sliced Workflow Design

Count authored state nodes only; exclude transitions and synthetic harness nodes. Workflows below 40 authored state nodes stay on the whole-workflow route. When a workflow has 40 or more authored state nodes, first create a standalone `loom.workflow-skeleton.v1` design view, freeze typed interfaces and cross-phase routes, then author isolated phase fragments. Keep each phase at or below 25 authored state nodes as a soft cap and record exceptions. New workflows use `phaseSliced`; migrations may explicitly mix `phaseSliced` and `legacy`, but every cross-phase read and write remains declared and typed.

Use the same selected direct AO apphost for `skeleton-check`, `phase-compile`, `impact`, `merge`, and final `compile`. Phase compilation is static only. Aggregate interface-change requests centrally, revise the skeleton, run `impact`, and redo only affected phases and proven dependents; unknown impact requires full revalidation. Merge exactly one passing result per phase into a new candidate, then require complete AO compile and its audit against that exact file. Never modify an active runtime workflow copy.

Allow at most three repair attempts per phase and two post-merge full-compile repair rounds. At either cap, preserve evidence and ask for an explicit decision instead of retrying silently. Return `phase_design_evidence` linked to `phaseDesignEvidence` in the existing `static-contract-review.json`; do not create a fourth report. Optional support hashes are not freshness gates. Package integrity and required business-output hash checks are unchanged.

```text
skeleton-check --workflow-file <path> --skeleton-file <path>
phase-compile --workflow-file <path> --skeleton-file <path> --fragment-file <path> --phase-id <id> --output-file <path>
impact --previous-workflow-file <path> --previous-skeleton-file <path> --candidate-workflow-file <path> --candidate-skeleton-file <path> --output-file <path>
merge --workflow-file <path> --skeleton-file <path> --result-file <path>... --output-file <path>
```

## Self-Contained Runtime Contract

There is one package-channel runtime path: the exact published AO product+RID package for the current host.

- Detect exactly one supported RID from OS, architecture, and Linux libc. Use the exact AO version bound by the skill.
- Reuse only an immutable generation from the shared current-user OS-temp AO/SO cache when exact package ID/version/RID and full verified `.nupkg` SHA-512 match. On a miss, follow the L0-L6 acquisition policy above for the exact locked AO package; L6 remains a last resort with explicit per-operation approval. Verify newly acquired NuGet bytes against exact `catalogEntry.packageHash`, or an independently configured same-version GitHub asset against its matching `.sha512` sidecar, before extraction.
- Before extraction, verify package ID, exact version, RID, SHA-512, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide files. Extract only after all checks pass.
- Run `ao.exe --guide` on Windows or `ao --guide` on Unix immediately after extraction. Verify its version and readable contained paths before downstream work.
- Use the same extracted apphost for schema/demo, compile, run, and resume. A failed package, extraction, startup, or guide check fails closed.
- Do not require an installed .NET host, DLL mode, runtime resolver, launch descriptor, or fixed bootstrap script. The shared AO/SO cache is permitted; do not add a cache lock.

## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm the current skill-bound package version, derive channel from its version shape when needed, and confirm runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build Loom Agent Plan-Execution Orchestrator from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: acquire and verify the exact AO product+RID package, safely extract it outside the skill folder, then run its direct apphost `--guide`. No DLL, Roslyn bundle, generated `.deps.json`, or framework startup contract is used.
3. Prove the selected runtime can run the bare `--guide` command, parse its JSON result, and read the returned `guide_path` and `docs_root` before proceeding.
4. Only after that guide result exists, run planning surfaces (`prompt-plan`) and capture required prompt blocks.
5. When creating or revising a workflow, invoke the local workflow-designer subagent and give it the relevant skill files, guide files, plan files, and audit artifacts through relative links.
6. Materialize one external WorkflowInstance copy outside skill paths, record its immutable instance identity and persisted runtime-state/session path, then run `compile` against that exact copy.
7. Run Loom Agent Plan-Execution Orchestrator with that same external WorkflowInstance copy; every later `resume` must reuse its persisted runtime state.
8. On blocked state, use payload signals plus `prompt-replan` to update seam nodes, then `resume` with structured envelope payload.
9. When the current route is confirmed blocked, persist the current workflow state, blocker report, all attempted remedies and their outcomes, and the relevant event/audit references before asking the AO planner to replan.
10. Replan from the retained history by selecting an explicit strategy: continue from the current state, roll back to an unconfirmed design node, redesign from the current state, replace the whole plan, or apply a smallest reversible workaround.
11. Require the planner to return a viable path to the terminal business outcome, including a rollback or workaround path when selected, before resuming execution.
12. Repeat replan/resume until Loom Agent Plan-Execution Orchestrator reaches completed state.
13. Report completion only when Loom Agent Plan-Execution Orchestrator is completed and requested business deliverables are verifiable.

For AO workflow design and AO weave-out planning, prefer existing capable subagents whenever they can already complete the weave-out goal instead of emitting generic agent placeholders.

Operational details for prompt blocks, payload conventions, and blocked-state handling are defined in reference docs.

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

## Required Outputs

- bound runtime version confirmation with derived released/beta evidence and matching canonical links
- runtime source selection and version-derived channel resolution metadata
- package-channel runtime evidence: exact package ID/version/RID, source, verified SHA-512, nuspec/manifest/archive checks, extraction result, apphost path, and fresh guide result
- package-channel runtime acquisition facts when Windows PowerShell 5.1 is involved: ZIP-based `.nupkg` extraction path, HTTP probe mode, and fail-fast evidence when extraction or guide generation fails
- workflow/session/event paths and audit artifact links
- named-agent dispatch record for every subagent call: exact frontmatter name, `.agent.md` link, route (`direct` or `driver-fallback`), delivery mode (`inline` or `path-only`), fallback trigger evidence, and the driver's name echo
- `workflow.compile-feedback.json` and the shared `workflow.compile-feedback.v1` result, including parse/validation status, counts, phase blockers, candidate path, optional generated candidate-hash metadata, and AO runtime identity/version. The candidate hash is not a consumer freshness gate.
- Verified real paths for every output artifact, including workflow, feedback, audit, Mermaid, and HTML files outside the Git worktree or under ignored workspace mirrors.
- guide hub, flow, and reference paths, with the fixed `guide_path` hub kept at or below 200 lines
- required think-out-loud fields for runtime and audit updates
- After every AO apphost call or audit-producing step, including `ao.exe` and `ao` calls, follow [Mermaid artifact delivery](reference/mermaid-artifact-delivery.md): verify the actual returned audit paths and readability, then begin the think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order. Each title must link to the complete absolute verified runtime file address (Windows drive path or Unix absolute path), normalized with `/`; immediately follow it with a `text` fence whose body contains only the identical full address. Never use a workspace-relative address, shorten, truncate, ellipsize, or add labels/commentary inside the fence. Preserve this exact pair shape and artifact order. If a file is not emitted or verified, apply the delivery-failure/continuity rule and do not invent its address. Follow the block with localized `##` headings for execution confidence and estimated overall progress in that order, each with one short reason. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress measures approximate completion of the whole request. Base both on verified evidence; no fixed formula or threshold is prescribed, and neither percentage replaces required completion checks. Use only current or latest verified paths; on `not_emitted`, say the render is unchanged, and on `delivery_failed`, report the failure and next action without a link or reuse. Keep user-facing text plain and in the active interaction language.
- business deliverable verification summary when business-first mode applies
- For `runtime_path_only`, include the complete verified absolute address in the fixed artifact report. Use a verified workspace mirror only for a separate host/editor open action when `link_resolvable=true`; do not replace or suppress the report address.

For the full output matrix and field-level contracts, use reference docs.

## Prohibited Results

Reject or mark invalid any execution result that says or implies any of these:

- AO is optional for official skill execution
- AO and another path are parallel official execution modes for this skill
- AO runtime artifacts alone are accepted as final completion when the caller explicitly requested business outputs
- business-output requests are silently downgraded to runtime/meta-only execution without explicit user approval
- `compile`, `--guide`, or helper shell steps are normal skill run modes
- `compile`, `--guide`, `prompt-plan`, `prompt-replan`, or helper shell steps are normal skill run modes
- non-AO output can count as official skill execution history
- non-AO tests can count as official skill execution evidence
- prose flow or examples are official execution authority by themselves

## Completion Criteria

Do not treat execution as properly governed until all of these conditions hold:

- only direct `ao.exe run`/`ao.exe resume` on Windows or `ao run`/`ao resume` on Unix counts as an official skill run
- direct apphost `compile`, `--guide`, `prompt-plan`, and `prompt-replan` are preparation or validation surfaces only
- skill-level history only comes from AO workflow state, session state, event logs, or audit artifacts
- blocker history must retain the blocked node, blocker reason, attempted actions, outcomes, evidence references, and the selected replan anchor/strategy
- the planner must receive that retained history as input and must not silently discard failed attempts or prior route decisions
- a replan is invalid unless it declares a path from its selected anchor to the terminal business outcome and preserves a one-step rollback plan for any workaround
- skill-level checklist only comes from AO workflow nodes, frontiers, transitions, blocked states, and resume points
- skill-level run map only comes from the AO runtime `workflow_file`, `next_frontier`, blocked state, and audit artifacts
- skill-level evidence only comes from AO-owned runtime state and audit artifacts
- non-AO tests do not count as official skill execution evidence
- prose flow and helper command examples are explanatory only, not execution authority
- when caller objectives explicitly request business outputs, AO completion state alone is insufficient without the corresponding business deliverables

Detailed prohibited/acceptance examples are maintained in reference docs.
