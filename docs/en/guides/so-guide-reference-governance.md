# SkillOrchestrator Guide: Governance

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [Root](../README.md)

Version: 0.3.270
Build: published package 0.3.270

## Mandatory Loom Skill Orchestrator Governance Rules for Enhanced Skills

Apply this section when a skill is being enhanced by `/loom-skill-enhancement` or is already operating under Loom Skill Orchestrator governance. The skill does not need to identify itself as a target skill before applying these rules. This section does not redefine AO behavior or apply to unrelated workflows.

### Deterministic Transition Contract

Every workflow transition authored or reviewed for that skill must declare all of the following:

- `guardExpression`: an executable boolean predicate evaluated before the transition runs; it proves eligibility from available inputs and must not claim that execution output already exists
- `succeedExpression`: an executable boolean predicate evaluated after the transition runs; it proves acceptance from declared output evidence and must not merely repeat the guard
- explicit user-owned versus runtime-owned input ownership
- explicit output evidence paths or output-family declarations
- a blocked route or terminal route when the transition can leave the current state

Reject transition definitions that use descriptive-only prose, an implicit predicate, an unbounded natural-language condition, or the same semantic test for both guard and success. A missing predicate, ownership declaration, or evidence shape is a failed authoring check.

### Boundary Check And Approval Gate (Compulsory)

The skill is forced onto the Loom Skill Orchestrator-governanced route. No next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step:

- A **boundary check** validates every transition before advancing: `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state, gate predicates (`passExpression` / `succeedExpression`) over runtime evidence plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) explicit user approval/instruction at `AskUser` seams for declared user-owned fields or decisions, or (b) structured non-human continuation payloads whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, compile success, an unapproved draft copy, local orchestration, or direct workflow JSON edits — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Every transition on the same external runtime copy must pass this gate until final `Done`.

### Expression Contract

The current .NET expression language is **C#**, compiled in-process by Roslyn. The root workflow declares `runtimeBinding` and `expressionBinding`; do not add per-node language overrides. The binding includes `language`, `languageVersion`, `contractId`, `contractVersion`, `requiredExpressionCapabilities`, and `compileFeedbackContract: "detailedCompileFeedbackV1"`.

`guardExpression`, `succeedExpression`, and `passExpression` use structured `ExpressionDefinition` values with `kind`, `source`, `entryPoint`, and `resultType`. A string is accepted only as a compatibility shorthand with an explicit C# binding and is always serialized as an object. Current expressions are synchronous predicates; asynchronous constructs and legacy non-C# expression syntax are invalid and fail closed. Use the read-only context contract, such as `context.Get<T>("path")`, rather than implicit bare identifiers.

Every compile emits `ExpressionCompileFeedback` under `detailedCompileFeedbackV1`, including location, source span, stable code/category, severity, actionable message, suggested fix, referenced symbols, compiler identity, resolved form, result type, capabilities, and warnings. Raw compiler text alone is insufficient. Rust+CEL is a future fourth runtime route using the same schema and feedback contract, not Rust code execution; Node.js and Python remain adapter routes until they implement the same contract.
### Deterministic Gate Contract

Every gate authored or reviewed for that skill must declare all of the following:

- `passExpression`: a machine-checkable boolean predicate evaluated against the runtime evidence context
- a machine-checkable pass predicate
- required evidence references and output families
- the route coverage that can satisfy the gate
- the strongest-earned blocked gate when the route cannot continue
- the terminal route and business-output gate required before `Done`

Governance-only artifacts cannot satisfy a business-output gate. A route must fail closed when its required evidence, output family, blocked route, or terminal route declaration is missing.

### Explicit Unattended-Mode Contract

An unattended workaround is permitted only for a blocked SO path and only when unattended mode is explicitly declared in the current session. Do not infer unattended mode from an earlier turn. Re-confirm attended versus unattended status at every critical decision boundary.

Before an autonomous workaround, require a structured decision-evidence record showing that expected benefit clearly exceeds risk, the alternatives considered, the smallest reversible change selected, and a rollback plan executable in one step. After the workaround, immediately return to the public `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume` path. The post-run acknowledgement request is non-blocking unless the user explicitly requires blocking behavior.

### Weave-Out Citation Contract

Every weave-out or external handoff must return a minimal citation manifest for the documents that caused or support the next action. Do not dump the full context pack or cite every file that was read. Include only the entry document, the necessary workflow or contract files, and the specific guide evidence that controls the decision.

Each citation must contain:

- `path`: a workspace-relative or runtime-output-relative path, never an absolute machine path
- `start_line` and `end_line`: verified 1-based inclusive line numbers from the exact file content used for this weave-out
- `role`: why the cited excerpt is required for the next action

When a guide is involved, cite the actual successful `guide_path` returned by the latest `dotnet so.dll --guide` JSON result and cite its output line numbers. Citing only the guide source location is insufficient. The command does not export a guide file; if no `guide_path` can be read, identify the failed runtime evidence instead. A weave-out without verified `evidence_references` is incomplete and must not be woven back as successful evidence.

Keep every weave-out response compact: return the next action or decision, the minimal `evidence_references` manifest, and the resume payload contract. Do not repeat the full context-pack inventory.

These rules are mandatory guide requirements during the skill's authoring, review, compile readiness, and governed execution handoff under SO.

### Schema And Demo Export

Use the exact runtime to write the current workflow schema contract and a compile-ready demo as a pair:

```powershell
dotnet so.dll --schema-demo-output outputs\schema-demo
# or on Windows self-contained runtime
.\so.exe --schema-demo-output outputs\schema-demo
```

The command writes the complete set `workflow.schema.json`, `workflow.demo.json`, `workflow.model.cs`, `workflow.demo.cs`, and `workflow.demo.verify.cs`. The two executable examples are ordinary `.cs` files: pass their paths to `--script-file` and `--verify-script`; no project file or external C# script runtime is required. Use the same runtime to validate the generated demo with `compile --workflow-file <path>`. Keep these generated files outside skill folders unless they are explicitly requested deliverables.

```guide-template
dotnet so.dll compile \
  --workflow-file so-template.json \
  --audit-output outputs/audit
```

`so-template.json` remains the checked-in source template. Place `outputs/audit` outside the skill folder.

For `/loom-skill-enhancement` and any Loom-governanced target skill, do not directly edit checked-in workflow JSON as a normal maintenance path. Only when the active `dotnet so.dll` path is fully blocked and the user explicitly approves a narrow workaround may you make the smallest direct JSON change needed to unblock the next `dotnet so.dll compile`, `dotnet so.dll run`, or `dotnet so.dll resume`, then immediately return to the Loom-governanced path.

Manual edits to the running external workflow `.json` copy are also last-resort blocked-state emergency workarounds only, not part of the normal workflow-operation path.

For Loom-governanced target-skill templates, set root `templateKind: so-governed-target-skill` and a root `validation` contract. `compile` validates structural integrity plus route-aware business-output gates, seam ownership, blocked strongest-earned outputs, and done reachability before the workflow may become execution authority.

`compile` also requires every state node to declare a non-empty `workflowPhase`. That field means which stage of the overall workflow the node belongs to, and compile uses it to enforce swimlane-ready authoring instead of treating phase grouping as optional rendering metadata.

If a target-skill modification intends that governed workflow to become runnable execution authority, the materialized runtime workflow must also be executable on the current public `dotnet so.dll run` and `dotnet so.dll resume` path. Do not leave the runnable workflow in `Drafting`, and do not depend on private or unavailable built-in tool names that the current public runtime does not expose. If a checked-in workflow JSON is only a draft or compile-review source template, label it that way explicitly and do not present it as directly runnable.

For wording discipline, treat guide refresh, checked-in asset authoring, and compile validation as intermediate milestones rather than normal completion states. For full-delivery governed slices, the stable completion wording should describe a Loom-governanced target skill whose official execution surface is the public `dotnet so.dll run` and `dotnet so.dll resume` path against a materialized runtime workflow copy, and whose governed run has actually reached final `Done`.

Compile also writes `workflow.analysis.json` beside `workflow.mermaid.md`, `workflow.html`, and `workflow.json`. Use that analysis artifact to review control-flow structures before execution: branches, switch-like groups, loops, requested inputs, published output families, user seams, runtime seams, and gate coverage.

```guide-template
dotnet so.dll run \
  --workflow-file workflow.current.json \
  --context-file context.json \
  --audit-output outputs/audit
```

`workflow.current.json` is a mutable runtime copy created outside the skill folder. Do not point `--workflow-file` back at `<target-skill-root>/assets/so-workflow/`, and do not place `outputs/audit` there either. Create a fresh runtime copy when starting a new official run chain, then keep resume on that same persisted runtime copy instead of rebuilding it from checked-in source assets.

```guide-template
{
  "transition_id": "transition.ask",
  "correlation_key": null,
  "payload": {
    "answer": "approved"
  }
}
```

```guide-template
dotnet so.dll resume \
  --workflow-file workflow.current.json \
  --result-file external-step-result.json
```

Resume continues against the same external runtime copy, not the checked-in source template.

```guide-checklist
- workflow JSON is materialized before execution
- checked-in source template stays clean; run/resume target an external mutable workflow copy such as `workflow.current.json`
- every new official run chain starts from a fresh external workflow execution file copied from checked-in source assets
- resume stays on the same persisted runtime workflow copy from that run chain
- direct workflow JSON edits are not a normal governance path; blocked-state emergency workarounds require explicit user approval and immediate return to `dotnet so.dll`
- audit outputs also stay outside the skill folder
- compile writes Mermaid Markdown, HTML, workflow backup, and workflow analysis validation outputs before execution handoff
- for Loom-governanced target-skill templates, compile also requires a root validation contract, route-aware business-output gates, strongest-earned blocked-output declarations, and ownership-safe seams
- for target-skill modifications, runtime-ready evidence and fresh-guide evidence should be modeled explicitly before any downstream planning, authoring, validation, compile, run, or resume steps
- if re-enhancement review inspects checked-in assets, those inspection nodes must load real file snapshots before any gap-review subagent consumes them
- file-backed checked-in-asset inspection must declare an explicit target-skill asset root and must reject absolute paths or traversal that escapes that root
- if a governed workflow is presented as runnable execution authority, its materialized runtime copy must be executable on the current public `dotnet so.dll run` path rather than only compile-clean
- once a target skill has already switched into the Loom Skill Orchestrator governance type, the stable wording should say the target skill is a Loom-governanced target skill and that its official execution surface is the public `dotnet so.dll run` and `dotnet so.dll resume` path against a runtime workflow copy
- if a creation or re-enhancement slice has not yet produced a real public run/resume chain to final `Done`, describe it as an in-progress or blocked enhancement slice rather than a normal governed completion state
- when a workflow route uses runtime-owned completion manifests to reference checked-in source deliverables, the route contract should declare both the checked-in source deliverable output families and the runtime-owned completion-manifest output family explicitly so done reachability does not collapse into governance-only evidence
- governed completion must cite a boundary-check/approval-gate trail covering every transition on the same external runtime copy: gate predicates checked, seam ownership verified, route coverage confirmed, and the explicit approval or structured non-human continuation that allowed each next step
- step kinds are explicit
- local tools are deterministic
- memory extraction is defined or derivable
- caller can send structured external results back
```

### Exact-Version Cache And Verified Audit Reuse

When the `.NET CLI` host branch is eligible and a package lock already binds a runtime version, inspect the local NuGet cache before contacting NuGet.org. The complete .NET runtime bundle must be present and valid at that exact version, including package id, exact version, and nuspec identity. A partial or invalid cache is not reusable; download only the missing or invalid exact-version package members through their direct URLs.

When self-contained fallback is selected, inspect the product/version/RID cache entry instead and reuse it only when its single exact runtime package, manifest, entrypoint, and guide version are valid. Download only the missing or invalid exact-version package through its direct URL. Do not resolve a latest version or use a `*.latest.nupkg` alias during automated restore. Record `runtime_mode`, `rid`, `cache_hit`, `downloaded_packages`, `cache_validation`, `resolved_runtime_version`, and the applicable runtime package fields as runtime evidence.

For invocation-level reuse, SO compares a stable workflow graph/configuration projection and rejects structural drift. It compares the source Mermaid/HTML with the current render; exact matches are copied, while changed renders are regenerated from the current instance. The step always writes a fresh `workflow.json` for the current runtime instance and, when available, fresh `workflow.analysis.json` and `workflow.dataflow.json`; `audit-reuse.json` records copied and replaced file names. This prevents dynamic runtime state from being replaced by an older backup while preserving verified presentation continuity.

```guide-template
dotnet so.dll copy-audit-step \
  --source-step outputs/audit/wf-source/step-0001-compiled \
  --workflow-id current-run \
  --sequence 2 \
  --action reused-compiled \
  --audit-output outputs/audit \
  --reason "Workflow and render inputs were verified unchanged." \
  --verified-by reviewer-id
```

This copies the required Mermaid, HTML, and workflow JSON files plus optional analysis/dataflow/summary files, verifies SHA-256 values, rejects destination collisions, and writes `audit-reuse.json`. It is audit presentation continuity only. It does not advance a workflow, append runtime events, evaluate gates, or create official `run`/`resume` evidence; those operations remain mandatory on the same runtime workflow copy. When presenting the Mermaid artifact in chat, use a Mermaid card-display tool when one is available by passing the existing file path directly; do not read or return the file contents again solely for display. Otherwise use a direct clickable Markdown file link and repeat the latest card or link when the render is unchanged.
