# SkillOrchestrator Guide: Behavior And Responsibilities

[中文](../../zh-cn/guides/so-guide-reference-behavior.md) | [Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.324
Build: published package 0.3.324
<!-- guide-version:end -->







## Behavior

SO executes these step kinds directly when they are local and deterministic:

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

When `MemoryRead` is used to inspect checked-in assets of the skill being enhanced during re-enhancement or governance review, it must load real file snapshots instead of placeholder context copies, and every inspected asset path must remain under the declared asset root of the skill being enhanced.

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
- When local runtime acquisition is needed, follow [Platform Detection Steps](../reference/runtime/platform-detection.md), acquire one exact self-contained SO product/RID package, and verify its registration hash or same-version release sidecar, package identity, archive safety, apphost, and guide.
- Before a new official `run`, copy checked-in source templates to a runtime temp or execution-output folder. When the workflow later blocks, `resume` must continue against that same persisted runtime copy.
- Execute the external action when SO weaves out.
- Resume SO with the structured weave-back envelope.
- Parse `<so_property>` as the authoritative SO control payload.
- Treat `<wrapped_exec>` as the streamed shell-facing wrapper surface.
- Use `transition_id`, `correlation_key`, and `payload` in the resume sidecar JSON.
- Keep runtime workflow copies, event sidecars, and audit outputs outside any skill-owned directory.
- After every SO apphost call, the think-out-loud update must start with the current verified Mermaid artifact link and matching normalized path fence, then do the same for HTML, Analysis, and Dataflow in that order. After the four pairs, print a localized execution-confidence heading and one short reason. Use only verified current or continuity paths; report delivery failure and next action without a link. All user-facing progress, blocked, error, and completion text must use plain words in the active interaction language.
- Treat `workflow.analysis.json` as the machine-readable summary of inputs, output families, branches, loops, user seams, runtime seams, gates, and Turing-complete control risk.
- Use `so.exe copy-audit-step` or `so copy-audit-step` only for explicitly verified unchanged audit inputs. Its `audit-reuse.json` provenance marks copied artifacts as `artifact_origin: verified-copy` and `official_execution_evidence: false`; copied artifacts cannot replace `run`, `resume`, event-log, gate, or guide evidence.

### Author

- Encode step kinds explicitly.
- Define memory extraction hints when the next step requires context curation.
- Keep local deterministic steps free of hidden side channels.

### Outer-agent

- Consume `skill_hint` literally.
- Preserve `memory_for_next_step` across the blocked seam and its resume handoff.
- Avoid improvising beyond the contract of the blocking step.
