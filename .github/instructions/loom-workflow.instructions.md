---
name: Loom Workflow And Expression Rules
description: Workflow identity, execution state, dataflow, validation, and C# expression rules for Loom workflow changes.
applyTo: ".agents/skills/**,src/dotnet/**,tests/dotnet/**,docs/**,demos/**"
---

# Loom Workflow And Expression Rules

Use these rules when changing workflow templates, runtime state, workflow validation, external result projection, C# expressions, or workflow-facing tests and docs.

## Workflow Identity And Business Scope

- A workflow template's business steps must match its declared business intent. SO self-bootstrap and skill enhancement workflows may contain guide, asset review, aggregate, repair, and validation steps because those are their business purpose; business workflows for the skill being enhanced must not inherit those enhancement steps.
- Governed workflow instances must declare `taskType` and `workflowKind`. Use `skill_enhancement` with `so_self_bootstrap` for `/loom-skill-enhancement` self-bootstrap, `skill_enhancement` with `target_skill_enhancement` for an outer skill enhancement run, and a skill-specific task type with `target_skill_business` for the skill's domain workflow.
- Compile and load validation must reject incompatible `taskType`/`workflowKind` declarations before execution. A skill business task such as `requirement_generation` or `model_generation` must never execute an enhancement workflow merely because both use the SO runtime.
- `caseId` and `runId` identify one business execution and remain on the same external workflow copy through compile, run, resume, audit, and completion evidence. They are execution identity, not a replacement for business outputs.

## Current Implementation Contract

- Plan, replan, and execution are disk-backed and sessionless. MCP transport connections, host processes, and in-memory objects must not be required to recover business state.
- AO is an independent workflow executor with a first-class `Plan` step. SO remains an independent executor. They may share a framework-neutral execution core, but product identity, CLI, package, and release boundaries remain separate.
- Each product owns one canonical `WorkflowInstance` as mutable business state. Events, audit records, logs, result envelopes, and large artifacts are companion records and must not become a second mutable execution truth.
- Planning Review is an implementation-planning edit loop. It may revise workflow drafts, templates, and default bundles before implementation, but it is not a runtime node, gate, MCP tool, or persisted execution state.
- Agent-facing workflow access is fragment-first: expose summaries, bounded JSON Pointer fragments, bounded events, and artifact manifests by default. Full workflow reads require an explicit purpose and configured size limits.
- MCP is local stdio only for this scope. Existing local workflow command kinds, including explicitly authored Python and HTTP commands, are not MCP Web transport and must not be removed solely because MCP transport is stdio.
- For `/loom-skill-enhancement` self-bootstrap and every target-skill route under Loom Skill Orchestrator governance, the first governed capability after exact published SO runtime preflight is a bounded workflow-fragment inspection against the same external workflow copy. Select transport in order: reuse an already registered MCP server only when runtime version and descriptor identity match; otherwise try ad hoc MCP only when this agent/host can start it directly; otherwise use the descriptor-driven CLI. MCP is optional and must never be required.
- The exact published runtime descriptor is the only source for MCP or CLI launch details. For CLI-selected inspection, use `inspect-workflow-fragment` directly with that descriptor; registration, configuration, and handshake evidence are required only when MCP is selected. Record the selected transport, runtime/descriptor identity, workflow identity, bounds, operation identity, and result hash in `mcp_startup_evidence`.
- An MCP application or command failure after dispatch remains a failure and must not be hidden by CLI retry. Every downstream external route must be dominated by the shared governance-entry check. AO remains CLI-only.

## SO Workflow Validation Rules

- For target-skill templates under Loom Skill Orchestrator governance, `dotnet so.dll compile` and workflow-load paths must reject missing business-output checks, ownership violations, and completion paths that can finish with governance-only evidence.
- `AskUser` seams may request only user-owned inputs or decisions. Runtime-owned facts, provenance, and system-generated artifact paths belong to runtime-owned seams such as `WaitResume` or blocked-resume payloads.
- Route-aware workflow templates must declare business-output checks and strongest-earned blocked outputs for each governed route so compile/load validation can prove meaningful business artifacts exist before completion or a runtime-owned wait boundary.

## External Result And Evidence Dataflow

- External transitions use one explicit projection contract: validate payload paths, extract `resumeOutputKey` relative to the payload, write the extracted value to `outputPath`, and apply explicit `outputBindings`. Governed templates must not rely on implicit wrapper nesting.
- `satisfiesGateIds` and `publishesOutputFamilies` are declarations, not evidence. Every required output family needs a reachable producer and a concrete `outputPath` or `outputBindings` projection into the current workflow instance context before a check can pass.
- Governed checks must declare value semantics when empty strings, arrays, objects, or booleans have business meaning. Missing and empty evidence must remain distinguishable in validation and diagnostics.
- A persisted `Failed` instance can recover to the previous state and resume only when the request identifies the most recent failed transition belonging to that state. Missing failure history, previous state, or ownership evidence fails closed. Recovery preserves failed history and evidence, restores `Running`, and retries from that state. A `Succeeded` instance remains terminal and requires a fresh external workflow copy.
- SO CLI commands that read or mutate one persisted workflow file must hold its adjacent cross-process file lock for the complete load, execution, and persistence operation. A contending process must re-read the workflow file after acquiring the lock.
- Verified audit-step copies must carry `audit-reuse.json` provenance and `artifact_origin: verified-copy`; they are presentation continuity only and cannot replace workflow execution, event logs, checks, guide evidence, or completion evidence.

## Expression Contract Rules

- Only `csharp` is supported. Legacy expression evaluators and non-C# language values must fail closed. VB and F# must not be added as language values, evaluators, or future candidates.
- Workflow templates declare root `runtimeBinding` and `expressionBinding` with language, language version, contract id/version, `requiredExpressionCapabilities`, and `compileFeedbackContract`. Do not introduce parallel capability field names.
- Roslyn API exposure is owned by one typed `RoslynCapabilityCatalog` in `Techne.Loom.Common`. Each entry identifies a stable capability id, execution surface, exact semantic symbol/signature, required assembly, constraint policy, diagnostic guidance, and documentation id. Do not approve a platform namespace or type with a wildcard.
- The catalog is the policy authority for both semantic analyzers. Method-name or syntax-only allowlists are insufficient; aliases, fully qualified names, extension methods, overloads, static members, conversions, lambdas, and referenced symbols must resolve to an approved catalog entry.
- Use native C# syntax. PowerShell forms such as `[regex]::Match(...)` must fail with a correction to `Regex.Match(...)`; the C# form must use the approved overload and explicit constraints.
- Expressions are synchronous and deterministic. Reject `async`, `await`, and `Task`. Scripts are trusted, reviewed checked-in code under constrained references and analyzers; the wait timeout is not a hostile-code sandbox.
- Regex matching requires a positive finite explicit `TimeSpan` timeout no greater than 5 seconds and an allowlisted `RegexOptions` combination. Reject timeout-free or infinite-timeout overloads, instance matching, compilation/cache mutation, match-evaluator delegates, and disallowed options.
- Guard, succeed, and gate pass expressions use structured `ExpressionDefinition` with `kind`, `source`, `entryPoint`, and `resultType`. A plain string is only a compatibility shorthand requiring explicit C# binding and version; serializers always write the structured form. Legacy non-C# source fails closed.
- Per-node or per-gate expression language overrides are unsupported. The root binding is canonical.
- The runtime executes immutable compiled boolean delegates; compile and execute lifecycles are separate, and validator, compile, run, and resume use the same compiler/router.
- Expression inputs are trusted checked-in templates that passed review and compile. The analyzer, reference allowlist, and read-only contract API are constraint boundaries, not a malicious-code sandbox. Docs and diagnostics must not claim stronger isolation.
- `compile` must emit structured `ExpressionCompileFeedback` for every expression: status, language/version, contract identity, location, source span, stable diagnostic code/category, severity, actionable message, suggested fix, referenced symbols, and compiler identity. Success also records resolved kind, entry point, result type, capabilities, and warnings.
- Every future supported expression language and runtime must implement `detailedCompileFeedbackV1` before it can be marked supported. Host exceptions alone do not satisfy the contract.
- Rust+CEL is a future cross-platform runtime core with CEL as the canonical expression language, not Rust code execution or a Lua scheme. Its docs must reuse the canonical runtime and expression fields.
- Node.js and Python remain adapter/ecosystem routes. They do not automatically become expression languages and must not implement independent evaluators without a formal contract with `detailedCompileFeedbackV1`.
- Cross-language or cross-runtime migration belongs to the skill: translate source, update binding/version/capabilities, and preserve source, translated source, translator, review, and compile evidence. Runtimes never auto-translate expressions.
