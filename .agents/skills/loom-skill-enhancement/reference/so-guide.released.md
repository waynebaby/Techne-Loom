# Local Offline Loom Skill Orchestrator Guide (Released)

This file is the self-contained released-channel runtime guide for `/loom-skill-enhancement`.

Use this file only when the SO package is not installed yet or the restored SO runtime is not runnable yet.

Once the SO runtime is runnable, execute `dotnet so.dll --guide` from that runtime and treat the emitted guide as the only runtime truth for that installed version.

Once that fresh guide result exists, governed execution for `/loom-skill-enhancement` itself and for any Loom-governanced target skill must stay on the corresponding published SO package runtime surface described by that guide. It does not matter whether the guide was reached from a skill entry point, direct CLI use, or a restored runtime bundle: once the guide exists, official governed execution must route back to the published SO package runtime it describes. Do not read the guide and then drift back to repository builds, hand-assembled runtimes, or non-governed execution paths for official skill or target-skill runs.

Do not keep using this offline file as the authority after `so.dll` is runnable.

## Channel Snapshot

- Channel: `released`
- Current latest released SO bundle version for this offline snapshot: `0.3.245`
- Runtime bundle packages: `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, `Techne.Loom.Abstractions`

## Product Role

Loom Skill Orchestrator executes deterministic workflow steps directly and blocks only when a seam requires outside participation.

It is the official execution authority for Loom-governanced skills that use exclusive Loom Skill Orchestrator governance.

## Official Execution Authority

In exclusive Loom Skill Orchestrator governance mode, official skill runs are only:

- `dotnet so.dll run`
- `dotnet so.dll resume`

These commands support but do not replace official skill execution:

- `dotnet so.dll --guide`
- `dotnet so.dll compile`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`
- `dotnet so.dll ls`

## Environment Setup

1. Confirm the released channel.
2. Restore the full SO runtime bundle at `0.3.245`.
3. Assemble one unified runtime directory outside any skill folder.
4. Verify `so.dll`, `so.runtimeconfig.json`, and dependency closure. If `so.deps.json` exists, keep it beside the runtime bundle; if it does not, do not fail preflight on that fact alone before testing the co-located runtime bundle.
5. As soon as the runtime is runnable, run `dotnet so.dll --guide` from that runtime and switch guide authority to that emitted guide.
6. Keep compile outputs, runtime workflow copies, and event sidecars outside skill-owned paths.
7. Before any target-skill planning, authoring, validation, compile, run, resume, or downstream input collection, prove that the selected published SO runtime is runnable and can emit a fresh `dotnet so.dll --guide` result from that runtime.

## Preferred Launch Mode

The default package-channel launch is the exact-RID published self-contained executable package: run `.\so.exe` on Windows or `./so` on Unix. The framework-dependent launch shown below is only for explicit legacy framework/library mode.

Keep one launch descriptor after preflight and use it for the fresh guide and all later commands.

Framework-dependent IL mode:

```powershell
dotnet exec --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

The complete legacy bundle must include `.\so.deps.json` and `.\so.runtimeconfig.json`; pass `--depsfile .\so.deps.json` before `--runtimeconfig` for the explicit legacy launch.

Self-contained single-file mode:

```powershell
.\so.exe --guide
```

Use `./so` without `.exe` on Unix systems. The two modes have identical CLI, workflow state, guide, audit, and governance semantics. Parse the fresh guide JSON `version`, read its `guide_path`, and only then continue to compile or run. Reuse the same launch descriptor and do not switch hosts midway through a workflow.

## Guide Output

The bare `dotnet so.dll --guide` command returns one JSON object with `version`, `docs_root`, and `guide_path`. It installs the English docs bundle under `<binary>/docs/<package-version>/`, or `%TEMP%/docs/<package-version>/` when the binary directory is not writable. Read `guide_path` first; inspect `docs_root` only when needed. `--lang`, `--section`, and `--export` are rejected.

## Troubleshooting Notes

- Restore the full three-package SO bundle at one exact version. Do not probe only `Techne.Loom.SkillOrchestrator` in isolation.
- On Windows PowerShell 5.1, treat `.nupkg` files as ZIP content instead of using `Expand-Archive` directly on the package.
- When PowerShell 5.1 uses `Invoke-WebRequest` or `Invoke-RestMethod` for exact package probes, add `-UseBasicParsing`.
- Missing `so.deps.json` is a failed preflight because the legacy bundle must expose the complete dependency closure through its mandatory dependency manifest.
- Do not save stderr from a failed guide command as a guide artifact. Guide authority begins only after a successful bare `dotnet so.dll --guide` JSON result has been parsed and its returned `guide_path` has been read.

## CLI Surface

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--guide` | none | none | Install the version-matched English `docs/en` bundle and emit JSON paths |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing workflow JSON and emit audit artifacts |
| `copy-audit-step` | `--source-step`, `--workflow-id`, `--sequence`, `--action`, `--audit-output`, `--reason`, `--verified-by` | Copy a verified unchanged audit step and write reuse provenance; does not execute or advance a workflow |
| `run` | `--workflow-file` | `--context-file`, `--audit-output`, `--reuse-audit-step`, `--reuse-audit-reason`, `--reuse-audit-verified-by` | Run until blocked or completed; optionally reuse one verified audit step |
| `resume` | `--workflow-file`, `--result-file` | `--audit-output`, `--reuse-audit-step`, `--reuse-audit-reason`, `--reuse-audit-verified-by` | Resume from structured external results; optionally reuse one verified audit step |
| `status` | `--workflow-file` | none | Emit current status payload |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-events` | `--workflow-file` | none | Print the event sidecar |
| `ls` | path optional | none | Run the built-in sample deterministic workflow |

## Workflow File And Audit Rules

- `compile` validates an existing workflow file and emits Mermaid Markdown, HTML, workflow JSON backup, and workflow analysis JSON.
- For `/loom-skill-enhancement`, workflow template generation and revision should use the local workflow-designer subagent at [../assets/agents/loom-skill-enhancement-workflow-designer.agent.md](../assets/agents/loom-skill-enhancement-workflow-designer.agent.md).
- For `/loom-skill-enhancement` itself and any Loom-governanced target skill, official workflow operations must use the published released-channel SO package artifacts. Do not treat repository source builds or ad hoc local binaries as the normal workflow-operation surface.
- `run` and `resume` should target a mutable runtime copy outside the skill folder.
- Do not run against the checked-in source template.
- Keep event sidecars and audit outputs outside skill-owned paths.
- The workflow JSON template is the authority; Mermaid and HTML are presentation artifacts.
- Copied audit artifacts are marked `artifact_origin: verified-copy` with `official_execution_evidence: false`; they cannot replace official `run`/`resume`, event-log, gate, or guide evidence.

## Governed Template Rule

When authoring a governed template for this skill, the workflow-designer subagent should receive relative-link context for the target `SKILL.md`, local guide file, package-index file, package lock, current workflow JSON, and any audit artifacts or blocked seam evidence.

The exact linked `.agent.md` file is the authority source for that subagent. Use direct exact-name resolution when the runtime supports it; otherwise resolve the same declared file from the repository/workspace copy first and the corresponding global installed-skill copy second before failing, then pass the resolved path plus full file content into the subagent-driving call. Do not substitute a freeform approximate role or repository-global prompt for this route.

If the workflow design introduces a target-skill local `.agent.md` file for a reusable weave-out, the target `SKILL.md` and the workflow template JSON weave-out hints should both reference that `.agent.md` file by relative path.

That target-skill `.agent.md` file then becomes the authority source for the target-skill subagent route during both handoff and execution. Resolve the target-skill repository/workspace copy first and the corresponding global installed-skill copy second before failing, and do not replace the route with an approximate role or ad hoc summary.

The generated workflow and accompanying governance wording should explicitly say that manual edits to the running external workflow `.json` copy are last-resort blocked-state emergency workarounds only.

For target-skill templates that use root `templateKind: so-governed-target-skill`:

- set root `templateKind: so-governed-target-skill`
- declare a root `validation` contract
- include `validation.gates`
- include `validation.routes`
- include `validation.declaredUserOwnedFields`
- include `validation.reservedRuntimeOwnedFields`

Compile and workflow load reject governed templates that:

- omit the root validation contract
- let `AskUser` request runtime-owned fields
- reach `done` without required route business-output gates
- pause blocked routes without strongest-earned blocked business outputs

Target-skill modifications should also require all of these when the governed workflow is intended to become execution authority:

- runtime-ready evidence and fresh-guide evidence exist before downstream planning or authoring
- any re-enhancement inspection nodes that read checked-in assets load real file snapshots instead of placeholder context copies
- file-backed checked-in-asset inspection stays rooted under the declared target-skill asset root and rejects absolute or escaping paths
- the materialized runtime workflow is actually runnable on the current public `dotnet so.dll run` / `resume` path instead of being only compile-clean or left in `Drafting`

## Core Control Contract

SO control payloads are emitted in `<so_property>` blocks.

Common runtime fields:

- `workflow_file`
- `instance_id`
- `status`
- `current_node_id`
- `current_step_kind` when blocked
- `event_log_file`
- `audit_artifacts`

Blocked seam guidance fields may include:

- `skill_hint`
- `memory_for_next_step`
- `required_inputs`

Resume envelope fields:

- `transition_id`
- `correlation_key`
- `payload`

## Step-Kind Behavior

SO executes these kinds directly when local and deterministic:

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

SO blocks and weaves out for these externally owned kinds:

- `ModelThink`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `WaitResume`

`ConditionBranch` remains explicit in the workflow and is evaluated deterministically inside SO.

## Mandatory Loom Skill Orchestrator Governance Rules for Enhanced Skills

Apply this section when a skill is being enhanced by `/loom-skill-enhancement` or is already operating under Loom Skill Orchestrator governance. The skill does not need to identify itself as a target skill before applying these rules. This section does not redefine AO behavior or apply to unrelated workflows.

### Deterministic Transition Contract

Every workflow transition authored or reviewed for that skill must declare:

- `guardExpression`: executable boolean eligibility before execution; it must not claim that execution output already exists
- `succeedExpression`: executable boolean acceptance after execution from declared output evidence; it must not merely repeat the guard
- explicit user-owned versus runtime-owned input ownership
- explicit output evidence paths or output-family declarations
- a blocked route or terminal route when the transition can leave the current state

Reject descriptive-only prose, implicit predicates, unbounded natural-language conditions, or the same semantic test for guard and success. Missing predicates, ownership, or evidence shapes fail the authoring check.

### Boundary Check And Approval Gate (Compulsory)

The skill is forced onto the Loom Skill Orchestrator-governanced route. No next step may proceed until it has passed a boundary check on the exact external runtime workflow copy; steps that cross owners additionally require explicit approval or structured continuation for that specific next step:

- A **boundary check** validates every transition before advancing: `guardExpression` eligibility from declared evidence (never claiming execution output already exists), and when leaving the current state, gate predicates (`passExpression` / `succeedExpression`) over runtime evidence plus route coverage, seam ownership, strongest-earned blocked outputs, or terminal business-output gates.
- Internal deterministic transitions — `stateUpdate`, `conditionBranch`, `memoryRead`, and native-code/tool steps whose guard/succeed predicates are machine-evaluable — are validated by the boundary check itself; they do not require a separate user approval. Owner-crossing seams DO require explicit continuation: (a) explicit user approval/instruction at `AskUser` seams for declared user-owned fields or decisions, or (b) structured non-human continuation payloads whose literal `skill_hint` plus blocked step kind point to a machine-continuable seam such as `WaitResume`.
- No next step may advance on inferred intent, prose alone, a stale guide result, compile success, an unapproved draft copy, local orchestration, or direct workflow JSON edits — and no transition may claim execution output already exists before its predicates have evaluated.
- If the boundary check fails closed — missing predicates, ownership violations, governance-only evidence, an unapproved route, or a seam without explicit continuation — stop and keep that failed state. Do not fabricate success proof, switch workflow copies mid-chain, claim governed completion from a blocked payload, or substitute local execution.
- Compile-clean is only a boundary-check precondition, never approval to skip further gates. Every transition on the same external runtime copy must pass this gate until final `Done`.

### Deterministic Gate Contract

Every gate authored or reviewed for that skill must declare `passExpression` as a machine-checkable boolean predicate over runtime evidence, plus required evidence references and output families, route coverage, the strongest-earned blocked gate, and the terminal business-output gate required before `Done`. Governance-only artifacts cannot satisfy a business-output gate. Missing evidence, output-family, blocked-route, or terminal-route declarations fail closed.

### Explicit Unattended-Mode Contract

An unattended workaround is permitted only for a blocked SO path when unattended mode is explicitly declared in the current session. Re-confirm attended versus unattended status at every critical decision boundary; never infer it from an earlier turn. Before autonomous execution, record a structured decision-evidence evaluation showing benefit exceeds risk, alternatives considered, the smallest reversible change, and a one-step rollback plan. Immediately return to public `dotnet so.dll compile`, `run`, or `resume`; post-run acknowledgement is non-blocking unless explicitly required otherwise.

### Weave-Out Citation Contract

Every weave-out or external handoff must return a minimal citation manifest for the documents that caused or support the next action. Do not dump the full context pack or cite every file that was read. Include only the entry document, the necessary workflow or contract files, and the specific guide evidence that controls the decision.

Each citation must contain:

- `path`: a workspace-relative or runtime-output-relative path, never an absolute machine path
- `start_line` and `end_line`: verified 1-based inclusive line numbers from the exact file content used for this weave-out
- `role`: why the cited excerpt is required for the next action

When a guide is involved, cite the actual successful `guide_path` file returned by the bare `dotnet so.dll --guide`, including its output line numbers. Citing only the guide source location is insufficient. The command does not export a guide file; if no path can be read, identify the failed runtime evidence instead. A weave-out without verified `evidence_references` is incomplete and must not be woven back as successful evidence.

Keep every weave-out response compact: return the next action or decision, the minimal `evidence_references` manifest, and the resume payload contract. Do not repeat the full context-pack inventory.

These are mandatory guide requirements during the skill's authoring, review, compile readiness, and governed execution handoff under SO.

## Workflow Analysis Expectations

`workflow.analysis.json` is the machine-readable summary of:

- requested inputs
- published output families
- branches and loops
- user seams and runtime seams
- gates and route coverage
- Turing-complete control risk

Mermaid node colors and emoji labels follow stable semantics:

- `🔎` AI/model/subagent work: green
- `⚙️` code/tool work: blue
- `💬` optional user-owned branch choice: yellow
- `🚧` required user input: red
- `❓` generic condition branch: amber/yellow
- `📜` governance and gate states: white or very light gray

## Completion Gate

Completion requires:

- requested target-skill deliverables or runtime outputs to exist as required by the task
- governed validation contracts to be present when the workflow uses root `templateKind: so-governed-target-skill`
- compile-clean governed routes and seam ownership
- official run evidence from SO workflow state, event logs, and audit artifacts
- a boundary-check/approval-gate trail covering every governed transition on the same external runtime copy, including gate predicates checked, seam ownership verified, route coverage confirmed, and the explicit approval or structured non-human continuation that allowed each next step

Runtime validation alone is not enough when the user asked for concrete target-skill changes.

## Think-Out-Loud Fields

When runtime preparation completes, after every `dotnet so.dll` CLI call, and on each progress update, report:

- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`
- `mermaid_file`
- `html_file`
- `analysis_file` when present

If a specific `dotnet so.dll` call did not emit a fresh Mermaid render, repeat the latest known `mermaid_file`, `html_file`, and `analysis_file` and state that the render is unchanged, then add a concise workflow-location summary.

## Anti-Patterns

- Treating `compile` or `--guide` as official run modes
- Letting callers infer the next action from prose alone
- Running against a checked-in workflow source file instead of an external runtime copy
- Storing runtime workflow copies, event sidecars, or audit outputs inside a skill folder
