# Local Offline Loom Skill Orchestrator Guide (Beta)

This file is the self-contained beta-channel runtime guide for `/loom-skill-enhancement`.

Use this file only when the SO package is not installed yet or the restored SO runtime is not runnable yet.

Once the SO runtime is runnable, execute `dotnet so.dll --guide` from that runtime and treat the emitted guide as the only runtime truth for that installed version.

Once that fresh guide result exists, governed execution for `/loom-skill-enhancement` itself and for any Loom-governanced target skill must stay on the corresponding published SO package runtime surface described by that guide. It does not matter whether the guide was reached from a skill entry point, direct CLI use, or a restored runtime bundle: once the guide exists, official governed execution must route back to the published SO package runtime it describes. Do not read the guide and then drift back to repository builds, hand-assembled runtimes, or non-governed execution paths for official skill or target-skill runs.

Do not keep using this offline file as the authority after `so.dll` is runnable.

## Channel Snapshot

- Channel: `beta`
- Current latest beta SO bundle version for this offline snapshot: `0.2.156-beta`
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

1. Confirm the beta channel.
2. Restore the full SO runtime bundle at `0.2.156-beta`.
3. Assemble one unified runtime directory outside any skill folder.
4. Verify `so.dll`, `so.runtimeconfig.json`, and dependency closure. If `so.deps.json` exists, keep it beside the runtime bundle; if it does not, do not fail preflight on that fact alone before testing the co-located runtime bundle.
5. As soon as the runtime is runnable, run `dotnet so.dll --guide` from that runtime and switch guide authority to that emitted guide.
6. Keep compile outputs, runtime workflow copies, and event sidecars outside skill-owned paths.
7. Before any target-skill planning, authoring, validation, compile, run, resume, or downstream input collection, prove that the selected published SO runtime is runnable and can emit a fresh `dotnet so.dll --guide` result from that runtime.

## Preferred Launch Mode

Use explicit launch mode when deterministic host binding matters:

```powershell
dotnet exec --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

If `so.deps.json` is present and the host requires it for deterministic binding, this explicit form also remains valid:

```powershell
dotnet exec --depsfile .\so.deps.json --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

## CLI Surface

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--guide` | none | `--lang`, `--section`, `--export` | Emit the SO guide surface |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing workflow JSON and emit audit artifacts |
| `run` | `--workflow-file` | `--context-file`, `--audit-output` | Run until blocked or completed |
| `resume` | `--workflow-file`, `--result-file` | `--audit-output` | Resume from structured external results |
| `status` | `--workflow-file` | none | Emit current status payload |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-events` | `--workflow-file` | none | Print the event sidecar |
| `ls` | path optional | none | Run the built-in sample deterministic workflow |

## Workflow File And Audit Rules

- `compile` validates an existing workflow file and emits Mermaid Markdown, HTML, workflow JSON backup, and workflow analysis JSON.
- For `/loom-skill-enhancement`, workflow template generation and revision should use the local workflow-designer subagent at [../assets/agents/loom-skill-enhancement-workflow-designer.agent.md](../assets/agents/loom-skill-enhancement-workflow-designer.agent.md).
- For `/loom-skill-enhancement` itself and any Loom-governanced target skill, official workflow operations must use the published beta-channel SO package artifacts. Do not treat repository source builds or ad hoc local binaries as the normal workflow-operation surface.
- `run` and `resume` should target a mutable runtime copy outside the skill folder.
- Do not run against the checked-in source template.
- Keep event sidecars and audit outputs outside skill-owned paths.
- The workflow JSON template is the authority; Mermaid and HTML are presentation artifacts.

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

## Workflow Analysis Expectations

`workflow.analysis.json` is the machine-readable summary of:

- requested inputs
- published output families
- branches and loops
- user seams and runtime seams
- gates and route coverage
- Turing-complete control risk

Mermaid node colors follow stable semantics:

- AI/model/subagent work: green
- code/tool work: blue
- optional user-owned branch choice: yellow
- required user input: red
- generic condition branch: amber/yellow
- governance and gate states: white or very light gray

## Completion Gate

Completion requires:

- requested target-skill deliverables or runtime outputs to exist as required by the task
- governed validation contracts to be present when the workflow uses root `templateKind: so-governed-target-skill`
- compile-clean governed routes and seam ownership
- official run evidence from SO workflow state, event logs, and audit artifacts
- when checked-in source deliverables remain authoritative for a slice, any runtime-owned completion manifest should reference those checked-in source assets explicitly instead of implying that temporary runtime artifacts replaced them

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
