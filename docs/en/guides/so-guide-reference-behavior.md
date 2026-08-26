# SkillOrchestrator Guide: Behavior And Responsibilities

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [中文](../../zh-cn/guides/so-guide-reference-behavior.md) | [Root](../README.md)

Version: 0.3.253-beta
Build: published package 0.3.253-beta

## Behavior

SO executes these step kinds directly when they are local and deterministic:

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

When `MemoryRead` is used to inspect checked-in target-skill assets during re-enhancement or governance review, it must load real file snapshots instead of placeholder context copies, and every inspected asset path must remain under the declared target-skill asset root.

SO weaves out and returns guidance for these externally owned kinds:

- `ModelThink`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `WaitResume`

`ConditionBranch` stays explicit in the workflow and is resolved by deterministic evaluation inside SO.

Current public runtime support note:

- `FirstSuccess` is the fully supported transition-group strategy in v1.
- `FirstResponse` and `All` remain model-level values, but the current public runtime will fail explicitly when multiple ready transitions require those strategies.

## Responsibilities

### Caller

- Provide the workflow JSON to compile.
- When local runtime restoration is needed, follow [Platform Detection Steps](../reference/runtime/platform-detection.md): after host preflight, validate and use the exact-version SO IL bundle of `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`; if the host is missing or cannot start the CLI, validate and use one exact `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package for the detected RID.
- Before a new official `run`, copy checked-in source templates to a runtime temp or execution-output folder. When the workflow later blocks, `resume` must continue against that same persisted runtime copy.
- Execute the external action when SO weaves out.
- Resume SO with the structured weave-back envelope.
- Parse `<so_property>` as the authoritative SO control payload.
- Treat `<wrapped_exec>` as the streamed shell-facing wrapper surface.
- Use `transition_id`, `correlation_key`, and `payload` in the resume sidecar JSON.
- Keep runtime workflow copies, event sidecars, and audit outputs outside any skill-owned directory.
- On every progress update, surface the current workflow Mermaid Markdown and HTML paths in think-out-loud output.
- Treat `workflow.analysis.json` as the machine-readable summary of inputs, output families, branches, loops, user seams, runtime seams, gates, and Turing-complete control risk.
- Use `dotnet so.dll copy-audit-step` only for explicitly verified unchanged audit inputs. Its `audit-reuse.json` provenance marks copied artifacts as `artifact_origin: verified-copy` and `official_execution_evidence: false`; copied artifacts cannot replace `run`, `resume`, event-log, gate, or guide evidence.

### Author

- Encode step kinds explicitly.
- Define memory extraction hints when the next step requires context curation.
- Keep local deterministic steps free of hidden side channels.

### Outer-agent

- Consume `skill_hint` literally.
- Preserve `memory_for_next_step` across the blocked seam and its resume handoff.
- Avoid improvising beyond the contract of the blocking step.
