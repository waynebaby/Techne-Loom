---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and Loom Agent Plan-Execution Orchestrator runtime surfaces.
---

# /loom-plan-execution

## Stage-Specific Required References

Keep this entrypoint at or below 200 lines. Detailed contracts live in this skill's own reference directory; read the applicable contract before the named stage, not just its summary.

- Before acquiring dependencies or dispatching any named agent: [Runtime Acquisition And Agent Dispatch](./reference/runtime-and-dispatch.md).
- Before planning, replanning, workflow design, or validation: [Planning And Validation Contract](./reference/planning-and-validation.md).
- Before execution, progress reporting, recovery, or completion: [Execution And Reporting Contract](./reference/execution-and-reporting.md).

## Runtime Version

<!-- skill-package-version-block:start -->
- Current published Loom runtime version for the Agent module: `0.5.4`.
- This block is refreshed whenever the published Loom runtime changes, so this skill remains aligned with the latest package set.
<!-- skill-package-version-block:end -->

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the package and guide surface that matches the current CI/CD-managed skill package version block, then routes execution through the applicable Loom Agent Plan-Execution Orchestrator runtime surface.

Once the skill-bound version is chosen, detect one supported host RID, acquire and verify that exact published `Techne.Loom.Runtime.<rid>` self-contained package, extract it safely, then directly run `loom.exe --guide` on Windows or `loom --guide` on Unix. Verify the JSON version and readable contained `guide_path`; for workflow design, also require absolute readable `contract_docs_root`, `contract_index_path`, and `builder_template_path` from the same package version before planning or downstream work. The package carries the English docs; no guide page is embedded in the apphost.

When the caller is explicitly debugging this skill inside the current repository and asks to use the current source tree, this skill may build and use the local Loom Agent Plan-Execution Orchestrator repo output instead of downloading package assets. That local-source override is for repository debugging only and does not create a second official execution authority.

This skill uses Loom Agent Plan-Execution Orchestrator as the official execution authority. Only direct `loom.exe agent run`/`loom.exe agent resume` on Windows or `loom agent run`/`loom agent resume` on Unix count as official skill runs. Other paths remain outside official execution.

Business-outcome-first rule: when the caller request or plan content (for example `testplan.md`) clearly targets business execution outputs, this skill must treat that business outcome as the primary completion target and must not drift into AO meta-execution-only activity.

## Read This First

- Shared terminology authority: use the English workflow-terminology page supplied through the validated runtime `referencePackManifest` when available; Chinese mirrors are localization aids only. Do not locate documentation by traversing from an installed skill directory.

- Runtime binding authority: this skill records only the exact bound unified Loom runtime package version. The host derives OS, architecture, Linux libc, RID, and apphost from the current machine; do not persist those transient details in checked-in skill state.

## Published Runtime Transport Rule

Published Agent execution uses the unified self-contained `loom` apphost through `loom agent` and never requires MCP. Optional Skill/MCP support is a separate module route; it cannot gate runtime package acquisition or guide capture. A dispatched MCP application failure remains a failure.

## Agent-Facing User Questions

Use `/loom-ask-user` by default for structured clarification or choices that an agent needs outside an authored workflow, and batch known independent questions. Submitted sessions return validated typed answers and a receipt; unattended direct answers have no receipt and must carry a clear agent-generated, user-unconfirmed warning. When the host explicitly identifies autopilot or unattended mode, apply the [unattended AskUser policy](../loom-ask-user/SKILL.md) before acquiring a runtime for the ask or invoking a blocking question tool, browser, or wait. For this skill's own questions and workflow `AskUser` boundaries, use the same active runtime-returned endpoint's supported HTTP session/submission flow for context-supported, type-valid answers only when genuine human approval is not required; never start another worker, guess routes, or expose credentials. Otherwise provide best-supported warned suggestions without fabricating a receipt or approval, and leave the workflow or human approval waiting when required. Preserve the absence explanation in writable additional-context or existing question `context` before a session starts; never mutate an active contract or invent wire fields. Do not make a workflow prerequisite. Use an optional AO `AskUser` node only when an authored workflow owns a user-owned value and must project the answer back into that same `WorkflowInstance`; runtime-owned facts, provenance, and artifact paths stay on runtime-owned boundaries. Outside autopilot, use the normal approved interactive route; if the selected published apphost lacks standalone `ask`, report the exact capability gap instead of silently substituting an agent-native form or workflow node.

## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.

## Caller File Preparation Contract

Before one CLI call, the caller must prepare the complete input set on disk and close every input file. Pass paths only for `--script-file`, `--input-file`, `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--patch-content-file`, `--patch-target`, `--workflow-file`, `--objective-file`, `--context-file`, `--instance-file`, and `--result-file`.

Do not pass script source, JSON, patch replacement text, or reference content inline. Do not ask the CLI or a later step to create a missing input or repair an earlier partial file. The CLI preflights all required input files before reading or writing. Destination files such as candidate, verification, and audit outputs may be created by the CLI.

## Selective JSON Reading

For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective inspection: use search or regex only to locate the target key or path, then read a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract the needed subtree. Read the full JSON only when the user explicitly requests it or whole-document validation requires it. This rule applies to JSON data; `.agent.md` role contracts remain a separate full-read requirement. For large workflows and review results, return an existing readable file path plus a bounded status/summary, needed JSON Pointer fragments, and evidence locations. Do not dump the complete workflow JSON into the conversation. Whole-document parsing or validation stays file-backed; full conversational output requires an explicit request and applicable size limits.

## Guide Hub Structure

The repository source guide pages live under `docs/en/guides/` and are packaged recursively into the exact runtime documentation bundle. This source-tree location is not relative to the installed skill; at runtime use the absolute `docs_root` and `guide_path` returned by a successful exact-apphost `--guide` result. This skill publishes no AO guide files; use `reference/ao-skill-reference.md` for runtime acquisition and the fresh extracted guide for version-specific authority.

Follow the current skill package version block first, then derive the matching package surface:

- Package indexes remain skill-local references; runtime AO guide paths are the absolute `docs_root` and `guide_path` returned by the selected apphost's successful `--guide`, not paths relative to this skill.
- Do not add or publish any AO guide file under this skill; use the fresh guide returned by direct `loom.exe --guide` or `loom --guide` from the exact locked package.

- Workflow designer subagent: `assets/agents/loom-plan-execution-workflow-designer.agent.md`
- SO governance designer-contract review: [Workflow Designer Contract Reviewer](assets/agents/loom-plan-execution-workflow-designer-contract-reviewer.agent.md). Its versioned summary and report are unconfirmed agent analysis, not human approval; independently validate the same instance, current wait, report, and exact citations before continuing. Until a deployable verifier exists, leave this review waiting.
- Loom Skill Orchestrator governance baseline assets for AO enhancement:
	- Per-run plan output: `<execution-output-root>/plan/skill-plan.md` (runtime-owned; not a stable skill asset)
	- `assets/so-workflow/so-template.json`
	- `assets/so-workflow/so-package-lock.json`
	- `assets/so-workflow/node-to-file-map.md`
- `assets/so-workflow/reference/ao/runtime-contracts.md`
- `assets/so-workflow/reference/ao/runtime-behavior.md`
- `assets/so-workflow/reference/document-copy-manifest.json`

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Runtime version authority: the current CI/CD-managed skill package version block; derive `released` versus `beta` from that bound version when needed
- Guide input: directly run `loom.exe --guide` on Windows or `loom --guide` on Unix; it is English-only and returns JSON with `version`, `docs_root`, `guide_path`, `contract_docs_root`, `contract_index_path`, and `builder_template_path`.
- Optional input: runtime source mode (`package-channel` by default, or explicit `repo-src-debug` when debugging this skill inside the current repository and intentionally using current source output)
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Fixed Progress And Recovery Contract

Use one request-level task ledger with stable task IDs and statuses `未开始` (not started), `进行中` (in progress), `受阻` (blocked), or `完成` (complete). Every item carries verifiable evidence, or an explicit no-evidence-yet statement, and its next action; completed items say no further action is required. Keep the task set and denominator fixed across phases. Report a progress percentage only against that fixed denominator and state the basis; otherwise show the checklist without a percentage. If scope changes, record and re-baseline the ledger explicitly rather than silently changing the denominator. Execution confidence is a separate estimate, not task completion.

Planners and designers must apply the [live-wait and recovery checklist](./reference/planning-and-validation.md#continuation-planning-and-recovery) before planning a continuation or resuming. Keep candidate generation, compile success, source writeback, and terminal execution as separate milestones.
## Runtime Flow

0. Classify intent first: business execution versus explicit runtime verification. Lock business-first mode when objectives clearly request business deliverables.
1. Confirm the current skill-bound package version, derive channel from its version shape when needed, and confirm runtime source (`package-channel` or explicit `repo-src-debug`).
2. Prepare runtime:
	- `repo-src-debug`: build Loom Agent Plan-Execution Orchestrator from `src/dotnet/Techne.Loom.AgentOrchestrator`.
	- `package-channel`: acquire and verify the exact `Techne.Loom.Runtime.<rid>` package, safely extract it outside the skill folder, then run its direct apphost `--guide`. No DLL, Roslyn bundle, generated `.deps.json`, or framework startup contract is used.
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

## Named Agent Resolution

Read [the complete dispatch contract](./reference/runtime-and-dispatch.md#named-agent-resolution) before any dispatch. Verify the exact non-empty frontmatter name, required inputs, linked documents, and output contract; never infer a role from its filename or silently skip it. Use the documented matching-copy and capable-driver fallback, or block dependent work when the contract remains incomplete.

## Dependency Acquisition Fallback Policy

Read the [complete ordered acquisition policy](./reference/runtime-and-dispatch.md#dependency-acquisition-fallback-policy) before fetching any dependency; it includes the exact-NuGet exception below.

## Exact NuGet Acquisition Completion Rule

Read [the complete acquisition policy](./reference/runtime-and-dispatch.md#dependency-acquisition-fallback-policy) before acquisition. A missing exact package requires download and verification; it is not completion. Use the installed runtime's in-process HTTP API, preserve source-specific integrity checks, and keep policy/publication blockers in a resumable acquisition-pending pause.
