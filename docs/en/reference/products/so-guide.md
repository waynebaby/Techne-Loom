# SkillOrchestrator Guide

[中文](../../../zh-cn/reference/products/so-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

SO is a deterministic skill execution and tracking product.

It compiles or loads a workflow, executes SO-owned steps directly, and returns only when the workflow finishes or reaches a seam that requires external participation.

This guide uses the repo-wide loom vocabulary from [Workflow Terminology](../../../en/architecture/workflow-terminology.md). In that vocabulary, SO weaves out when it reaches an externally owned step, surfacing that seam on blocked `<so_property>` payloads via fields such as `current_step_kind`, and callers weave back through `dotnet so.dll resume` result envelopes carrying `transition_id`, `correlation_key`, and `payload`.

## Contracts

```guide-contract
inputs:
  workflow_file: compiled or source workflow path
  context_file: optional initial context
  external_result: optional structured weave-back result for a previously blocked step
so_property_types:
  status:
    status: active | blocked | completed | failed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: current workflow focus node
    next_node_id: optional next node when known
    event_log_file: append-only execution event path
  boundary:
    status: blocked
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: current workflow focus node
    current_step_kind: current blocking step kind
    skill_hint: strict instruction for the next external action
    memory_for_next_step: curated memory summary plus referenced context slice
    required_inputs: optional structured inputs needed to continue
    event_log_file: append-only execution event path
  result:
    status: completed
    instance_id: durable workflow instance identifier
    workflow_file: persisted current workflow path
    current_node_id: terminal node or current completed node
    context: optional current context snapshot on completed result payloads
    event_log_file: append-only execution event path
  error:
    status: failed
    instance_id: durable workflow instance identifier when available
    workflow_file: optional workflow path when available
    message: stable machine-readable error summary
    event_log_file: optional execution event path
resume_envelope:
  transition_id: target blocked transition identifier
  correlation_key: optional blocked correlation key
  payload: structured result data for the blocked step
cli_stream:
  wrapped_exec_block:
    - <wrapped_exec>
    - <commandline>...</commandline>
    - <exectionstream>
    - ...streamed output lines...
    - </exectionstream>
    - </wrapped_exec>
  so_property_block:
    - <so_property>
    - {json}
    - </so_property>
```

The CLI keeps wrapped execution output streamable without forcing SO metadata into the same raw stream lines. Callers should treat the `type` field in `<so_property>` as the primary branch point for payload parsing.

In repo terminology, a blocked SO return is a weave out, and `dotnet so.dll resume` is the weave-back path.

## Behavior

SO executes these step kinds directly when they are local and deterministic:

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

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

- Provide the workflow or shorthand input to compile.
- Execute the external action when SO weaves out.
- Resume SO with the structured weave-back envelope.
- Parse `<so_property>` as the authoritative SO control payload.
- Treat `<wrapped_exec>` as the streamed shell-facing wrapper surface.
- Use `transition_id`, `correlation_key`, and `payload` in the resume sidecar JSON.

### Author

- Encode step kinds explicitly.
- Define memory extraction hints when the next step requires context curation.
- Keep local deterministic steps free of hidden side channels.

### Outer-agent

- Consume `skill_hint` literally.
- Preserve `memory_for_next_step` across the blocked seam and its resume handoff.
- Avoid improvising beyond the contract of the blocking step.

## Templates

```guide-template
dotnet so.dll run \
  --workflow-file workflow.json \
  --context-file context.json
```

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

```guide-checklist
- workflow is materialized before execution
- step kinds are explicit
- local tools are deterministic
- memory extraction is defined or derivable
- caller can send structured external results back
```

## Examples

```guide-example
name: local-tool-then-block-for-user
flow:
  - ToolCall: ls working directory
  - AskUser: choose target file
result:
  status: blocked
  current_step_kind: AskUser
```

```guide-example
name: model-think-with-memory
flow:
  - MemoryRead: summarize prior review findings
  - ModelThink: propose minimal code edit
result:
  status: blocked
  current_step_kind: ModelThink
  memory_for_next_step: curated summary of prior findings
```

```guide-example
name: wait-for-external-signal
flow:
  - WaitResume: wait for webhook completion
result:
  status: blocked
  current_step_kind: WaitResume
  required_inputs:
    - correlation_id
    - payload
```

```guide-example
name: finished-deterministic-run
flow:
  - ToolCall: generate output
  - ArtifactEmit: write report
result:
  status: completed
  current_node_id: state.done
  context:
    output_path: outputs/report.md
```

## Anti-Patterns

- Letting callers infer the next action from prose alone.
- Hiding memory in prompts instead of workflow context.
- Running shorthand commands without compiling them into a persisted workflow.
- Mixing wrapped command output and SO boundary payloads into one undifferentiated plain-text stream.
