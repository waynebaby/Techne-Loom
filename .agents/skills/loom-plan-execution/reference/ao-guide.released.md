# Local Offline Loom Agent Execution Orchestrator Guide (Released)

This file is the self-contained released-channel runtime guide for `/loom-plan-execution`.

Use this file only when the AO package is not installed yet or the restored AO runtime is not runnable yet.

Once the AO runtime is runnable, execute `dotnet ao.dll --guide` from that runtime and treat the emitted guide as the only runtime truth for that installed version.

Once that fresh guide result exists, governed execution must stay on the corresponding published AO package runtime surface described by that guide. Do not read the guide and then drift back to repository builds, hand-assembled runtimes, or non-governed execution paths for official skill runs.

Do not keep using this offline file as the authority after `ao.dll` is runnable.

## Channel Snapshot

- Channel: `released`
- Current latest released AO bundle version for this offline snapshot: `0.2.216`
- Runtime bundle packages: `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, `Techne.Loom.Abstractions`

## Product Role

Loom Agent Execution Orchestrator coordinates exploratory work under uncertainty.

It keeps workflow state, returns explicit blocked control payloads when outside action is required, and continues only after a structured resume envelope is provided.

## Official Execution Authority

Official skill runs are only:

- `dotnet ao.dll run`
- `dotnet ao.dll resume`

These commands support but do not replace official skill execution:

- `dotnet ao.dll --guide`
- `dotnet ao.dll compile`
- `dotnet ao.dll prompt-plan`
- `dotnet ao.dll prompt-replan`

## Environment Setup

1. Confirm the released channel.
2. Restore the full AO runtime bundle at `0.2.216`.
3. Assemble one unified runtime directory outside any skill folder.
4. Verify `ao.dll`, `ao.deps.json`, `ao.runtimeconfig.json`, and dependency closure.
5. As soon as the runtime is runnable, use `dotnet ao.dll --guide` from that runtime and switch guide authority to that emitted guide.
6. Keep session directories and audit outputs outside skill-owned paths.

## Preferred Launch Mode

Use explicit launch mode when deterministic host binding matters:

```powershell
dotnet exec --depsfile .\ao.deps.json --runtimeconfig .\ao.runtimeconfig.json .\ao.dll --guide
```

## Guide Output

The bare `dotnet ao.dll --guide` command returns one JSON object with `version`, `docs_root`, and `guide_path`. It installs the English docs bundle under `<binary>/docs/<package-version>/`, or `%TEMP%/docs/<package-version>/` when the binary directory is not writable. Read `guide_path` first; inspect `docs_root` only when needed. `--lang`, `--section`, and `--export` are rejected.

## CLI Surface

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--guide` | none | none | Install the version-matched English `docs/en` bundle and emit JSON paths |
| `compile` | `--workflow-file` | `--audit-output` | Validate an existing workflow JSON and emit audit artifacts |
| `prompt-plan` | `--objective-file` | `--context-file` | Emit AO-owned planner prompt text |
| `prompt-replan` | `--session-dir`, `--session-id`, `--instance-file`, `--tbr-id` | none | Emit AO-owned replanner prompt text |
| `run` | `--objective-file`, `--session-dir` | `--context-file`, `--instance-file`, `--audit-output` | Run until blocked or completed |
| `resume` | `--session-dir`, `--session-id`, `--result-file` | `--audit-output` | Resume from structured external results |

## Workflow And Audit Model

- AO workflow JSON is typically authored outside AO, then validated with `compile`.
- For `/loom-plan-execution`, workflow creation and revision should use the local workflow-designer subagent at [../assets/agents/loom-plan-execution-workflow-designer.agent.md](../assets/agents/loom-plan-execution-workflow-designer.agent.md).
- `compile` emits Mermaid Markdown, HTML, and workflow JSON backup validation artifacts.
- Run and resume also emit audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups.
- Audit artifacts live under a per-step output directory.
- Use a writable runtime session directory outside skill-owned paths.
- Keep checked-in plans and immutable source snapshots separate from mutable runtime outputs.

## Core Control Contract

AO control payloads are emitted in `<ao_property>` blocks.

Primary boundary and progress fields:

- `status`
- `session_id`
- `workflow_file`
- `workflow_instance_file`
- `event_log_file`
- `current_node_id`
- `boundary_reason`
- `pending_requirements`
- `next_frontier`
- `human_or_agent_hint`
- `weave_out_request`

When a guide controls the decision, cite the actual successful `guide_path` returned by the latest `dotnet ao.dll --guide` JSON result and its output lines. Citing only the guide source is insufficient. The command does not export a guide file; a weave-out without verified `evidence_references` is incomplete and must not be woven back as successful evidence. Keep the response compact: return the next action, the minimal citation manifest, and the resume payload contract; do not repeat the full context-pack inventory.

- `audit_artifacts`

Resume envelope fields:

- `transition_id`
- `correlation_key`
- `payload`

## Plan And Replan Playbook

When generating or revising AO workflow JSON, the preferred authoring surface is the local workflow-designer subagent linked above. Give it relative links to the active plan, current workflow JSON, audit artifacts, the actual `guide_path` returned by `--guide`, the returned `docs_root` when needed, and blocked payload evidence.

The exact linked `.agent.md` file is the authority source for that subagent. Use direct exact-name resolution when the runtime supports it; otherwise resolve the same declared file from the repository/workspace copy first and the corresponding global installed-skill copy second before failing, then pass the resolved path plus full file content into the subagent-driving call. Do not substitute a freeform approximate role or repository-global prompt for this route.

On a blocked return:

1. Read the `<ao_property>` payload.
2. Load the latest `workflow_file` snapshot.
3. Read `last_transition_id` from that snapshot.
4. Execute only the minimum external work required by the current boundary.
5. Write a structured resume result envelope.
6. Resume with `dotnet ao.dll resume`.

Use `prompt-plan` when creating or revising an authored workflow instance before runtime execution.

Use `prompt-replan` when a selected blocked seam needs graph-aware replanning on the latest authored or runtime workflow instance.

When the current route is confirmed unable to progress, pass a structured `replan_history` to the planner instead of only the latest boundary payload. It must retain the blocked workflow/node and unmet requirement, ordered attempted actions and outcomes, event and audit references, the terminal business objective, prior route decisions, and the selected replan anchor and strategy. Select exactly one strategy: `continue_from_current`, `rollback_to_unconfirmed`, `redesign_from_current`, `full_redesign`, or `reversible_workaround`. Every strategy must return a viable path from its anchor to the terminal business outcome; a workaround must include a one-step rollback plan. Failed attempts and their evidence must not be silently discarded.

## Completion Gate

AO should only be treated as completed when:

- AO returns `status: completed`
- the runtime has reached its completed state
- the completion resume payload includes non-empty `terminal_evidence`; a completion flag alone is insufficient
- any business deliverables requested by the caller are actually present and verified

Runtime-only status is not enough when the objective clearly requested business outputs.

## Think-Out-Loud Fields

When runtime preparation completes, after every `dotnet ao.dll` CLI call, and on each progress update, report:

- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`
- `audit_markdown_file`
- `audit_html_file`

If a specific `dotnet ao.dll` call did not emit a fresh Mermaid render, repeat the latest known `audit_markdown_file` and `audit_html_file` and state that the render is unchanged, then add a concise workflow-location summary.

## Anti-Patterns

- Treating `compile`, `--guide`, `prompt-plan`, or `prompt-replan` as official run modes
- Hiding blocked-state control inside prose only
- Treating AO runtime artifacts alone as final completion when the task asked for business outputs
- Writing runtime session state under the skill folder
