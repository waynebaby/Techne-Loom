# AO Runtime Behavior Reference Copy

<!-- loom-document-copy:start -->
- source_document: `tools/linux-x64/docs/en/guides/ao-guide-reference-behavior.md`
- source_reference_path: `docs/en/guides/ao-guide-reference-behavior.md`
- source_package_id: `Techne.Loom.AgentOrchestrator.Runtime.linux-x64`
- source_package_rid: `linux-x64`
- source_product: `ao`
- source_channel: `beta`
- source_version: `0.3.258-beta`
- source_sha256: `8962e68f0b0d9288053f9969d589cd72e5a93f232d1d485b3a1a45710ffacb34`
- target_bound_version: `0.3.262`
- content_mode: `full-document`
- artifact_origin: `verified-copy`
- authority_scope: `target-local context/reference only; the fresh published-runtime guide_path remains authoritative`
- refresh_policy: `refresh this copy, its manifest, the node map, and the package lock together when the bound AO version changes`
<!-- loom-document-copy:end -->

This target-local file is the complete AO behavior page extracted from the exact published runtime package.

# Loom Agent Execution Orchestrator Guide: Behavior And Responsibilities

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [Root](../README.md)

Version: 0.3.262
Build: published package 0.3.262

## Behavior

AO should:

- inspect current context
- expand or refine the workflow frontier
- choose among clarification, probing, delegation, replanning, or completion
- persist decisions, artifacts, and blocked-payload metadata
- keep a mutable workflow file plus an append-only event or snapshot log
- generate AO-owned planner/replanner prompt text from code when callers ask for prompt-plan or prompt-replan support surfaces
- express weave-out requests for external comparison, planning, or similar analysis through explicit blocked-payload fields rather than hiding them in opaque prose
- reject resume envelopes whose `transition_id` does not match the currently blocked workflow seam as recorded by the pending payload fields
- treat session metadata as explicit CLI input when needed instead of depending on hidden host state

AO should not:

- impersonate a deterministic skill executor
- hide control state inside narrative-only text
- collapse every decision into one opaque prompt roundtrip
- hide or bypass the documented CLI control surface with private wrappers
- treat prompt-plan or prompt-replan as official AO run surfaces equal to run/resume

## Responsibilities

### Caller

- Provide the objective and current known context.
- When local runtime restoration is needed, follow [Platform Detection Steps](../reference/runtime/platform-detection.md): after a successful .NET 9 host preflight, validate and use the exact-version AO IL bundle of `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`; if the host is missing or cannot start the CLI, validate and use one exact `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package for the detected RID.
- Execute external actions requested by AO.
- Resume AO with structured results.
- Preserve `session_id` between turns.
- Keep a stable session directory and pass it through `--session-dir`.
- Keep `--session-dir` outputs and any `--audit-output` outside skill-owned directories.
- On every AO progress update, surface the current workflow Mermaid Markdown and HTML in think-out-loud output. If the chat agent provides a Mermaid card-display tool, pass the existing Mermaid file path directly to it without reading or returning the file contents again solely for display. Otherwise use direct clickable Markdown file links; a bare path is not sufficient. When no fresh render exists, repeat the latest card or links and state that the render is unchanged.

### Author

- Define how control-state files are stored and surfaced.
- Keep AO outputs machine-first and stable.
- Keep weave-out requests, their current wire fields, and their event-log traces visible rather than hidden in private heuristics.

### Outer-agent

- Decide whether to accept AO's proposed frontier.
- Preserve artifact references and blocked-payload context across resumes.
- Treat AO as the exploratory coordinator, not as the place to execute SO-owned deterministic work.
- When a pre-authored AO workflow file is needed, generate that JSON so it matches the AO snapshot schema before calling `dotnet ao.dll compile`.
- Keep audit artifacts, intermediate workflow materializations, and conversation-referenceable outputs under a runtime temp root, repo-root temp root, or an explicit user-chosen execution output root, never under a skill folder by default.

### Schema And Demo Export

Use the exact runtime to write the current workflow schema contract and a compile-ready demo as a pair:

```powershell
dotnet ao.dll --schema-demo-output outputs\schema-demo
# or on Windows self-contained runtime
.\ao.exe --schema-demo-output outputs\schema-demo
```

The command writes the complete set `workflow.schema.json`, `workflow.demo.json`, `workflow.model.cs`, `workflow.demo.cs`, and `workflow.demo.verify.cs`. The two executable examples are ordinary `.cs` files: pass their paths to `--script-file` and `--verify-script`; no project file or external C# script runtime is required. Use the same runtime to validate the generated demo with `compile --workflow-file <path>`. Keep these generated files outside skill folders unless they are explicitly requested deliverables.

```guide-template
dotnet ao.dll compile \
  --workflow-file ao-plan.json \
  --audit-output outputs/audit
```

`ao-plan.json` can stay as a checked-in or exchanged source artifact, but `outputs/audit` should resolve outside any skill folder.

```guide-template
dotnet ao.dll run \
  --objective-file objective.md \
  --context-file context.json \
  --session-dir outputs/sessions \
  --audit-output outputs/audit
```

`outputs/sessions` and `outputs/audit` must live outside any skill-owned directory so AO runtime state does not dirty checked-in skill assets.

```guide-template
dotnet ao.dll resume \
  --session-dir outputs/sessions \
  --session-id 20260609010101_abc12345 \
  --result-file latest-boundary-result.json
```

Resume must point back to the same external session directory, not to a path under a skill folder.

```guide-checklist
- objective is explicit
- when the caller wants a reusable AO workflow snapshot artifact, the calling agent authors that AO workflow JSON file before validation handoff
- compile writes Mermaid Markdown and HTML validation outputs before execution handoff
- session_id is preserved by caller
- session directory is stable and writable
- session directory and audit output stay outside skill folders
- artifact references are durable
- caller can resume with structured data
- control outputs are persisted for audit
- documented CLI control path is preserved
- weave-out requests are expressed explicitly, not hidden in prose
- audit and intermediate outputs stay in temp-root or explicit execution-output locations outside skill folders by default
- compile must fail instead of overwriting pre-existing artifact files
```
