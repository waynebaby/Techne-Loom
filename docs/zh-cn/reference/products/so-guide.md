# SkillOrchestrator Guide

[English](../../../en/reference/products/so-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

SO 是一个确定性的 skill 执行与跟踪产品。

它会先编译或加载 workflow，直接执行由 SO 自己拥有的步骤，并且只有在 workflow 完成，或遇到必须由外部参与的 seam 时才返回。

本 guide 使用 repo 级的 [Workflow 术语](../../../zh-cn/architecture/workflow-terminology.md)。按这套词汇，SO 会在遇到外部拥有的步骤时 weave out，并通过 blocked `<so_property>` payload 里的 `current_step_kind` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet so.dll resume` result envelope weave back。

当前实现状态：

- 当前 `.NET` runtime 已实现 `dotnet so.dll --guide`、`dotnet so.dll --help`、`dotnet so.dll planner`、`dotnet so.dll compile`、`dotnet so.dll run`、`dotnet so.dll resume`、`dotnet so.dll status`、`dotnet so.dll inspect-workflow`、`dotnet so.dll inspect-events` 与 `dotnet so.dll ls`
- SO 的每次 planner/compile 也会产出 Mermaid Markdown、HTML 与 workflow JSON 备份，作为 compile 校验输出
- SO 在 run/resume 表面会返回 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact links

## 环境准备

通过 skill 或直接 CLI 使用 SO 前：

1. 先从 [`packages.released.zh-CN.md`](../../../../packages.released.zh-CN.md) 或 [`packages.beta.zh-CN.md`](../../../../packages.beta.zh-CN.md) 选择 package 通道。
2. 安装或构建目标 package。
3. 通过 `dotnet so.dll --guide` 阅读 guide。
4. 准备 workflow JSON 路径；如有需要，再准备显式 audit 输出根目录，用于 planner/compile 校验产物和 run/resume 审计产物。

## Contracts

```guide-contract
inputs:
  workflow_file: 已编译或源 workflow 路径
  context_file: 可选，初始上下文
  external_result: 可选，上一次阻塞步骤的结构化 weave-back 结果
so_property_types:
  status:
    status: active | blocked | completed | failed
    instance_id: 持久化 workflow instance 标识
    workflow_file: 持久化后的当前 workflow 路径
    current_node_id: 当前 workflow 焦点节点
    next_node_id: 可选，已知时的下一节点
    event_log_file: 追加式执行事件路径
  boundary:
    status: blocked
    instance_id: 持久化 workflow instance 标识
    workflow_file: 持久化后的当前 workflow 路径
    current_node_id: 当前 workflow 焦点节点
    current_step_kind: 当前阻塞 step kind
    skill_hint: 下一步外部动作的严格指令
    memory_for_next_step: 精选 memory 摘要与显式引用的 context 切片
    required_inputs: 可选，继续所需的结构化输入
    event_log_file: 追加式执行事件路径
  result:
    status: completed
    instance_id: 持久化 workflow instance 标识
    workflow_file: 持久化后的当前 workflow 路径
    current_node_id: 终态节点或当前已完成节点
    context: 在 completed 结果载荷中可选暴露当前 context 快照
    event_log_file: 追加式执行事件路径
    audit_artifacts:
      output_root: 审计输出根目录
      step_directory: 按 step 划分的审计目录
      mermaid_file: 该时刻的 Mermaid Markdown 路径
      html_file: 该时刻的 HTML 路径
      workflow_backup_file: 该时刻的 workflow JSON 备份
  error:
    status: failed
    instance_id: 如可用则给出持久化 workflow instance 标识
    workflow_file: 如有可用则给出 workflow 路径
    message: 稳定、machine-readable 的错误摘要
    event_log_file: 如有可用则给出执行事件路径
resume_envelope:
  transition_id: 目标阻塞 transition 的标识
  correlation_key: 可选的阻塞关联键
  payload: 该阻塞步骤的结构化结果数据
cli_stream:
  wrapped_exec_block:
    - <wrapped_exec>
    - <commandline>...</commandline>
    - <exectionstream>
    - ...持续流出的输出行...
    - </exectionstream>
    - </wrapped_exec>
  so_property_block:
    - <so_property>
    - {json}
    - </so_property>
```

CLI 会把套壳执行输出保持为可流式消费的形式，同时不把 SO 元数据硬塞进同一批原始输出行里。调用方解析 `<so_property>` 时，应首先按 `type` 进行分型。

按 repo 术语，SO 返回 blocked payload 时就是一次 weave out，而 `dotnet so.dll resume` 就是 weave-back 路径。

## Behavior

当步骤本地且确定时，SO 直接执行：

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

遇到这些外部拥有的步骤时，SO 会 weave out，并返回指导：

- `ModelThink`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `WaitResume`

`ConditionBranch` 在 workflow 中保持显式，并由 SO 内部做确定性求值。

当前公开 runtime 支持说明：

- v1 完整支持的 transition-group 策略是 `FirstSuccess`。
- `FirstResponse` 与 `All` 仍保留在模型层中，但当前公开 runtime 在多 ready transition 场景下会显式失败，而不是假装支持。

## Responsibilities

### Caller

- 提供 workflow 或待编译的简写输入。
- 当 SO weave out 时执行外部动作。
- 用结构化 weave-back envelope 恢复 SO。
- 把 `<so_property>` 视为权威 SO 控制载荷。
- 把 `<wrapped_exec>` 视为面向 shell 的流式 wrapper 输出表面。
- 在 resume sidecar JSON 中使用 `transition_id`、`correlation_key` 和 `payload`。

### Author

- 显式编码 step kind。
- 当下一步需要上下文提炼时，定义 memory extraction 提示。
- 保证本地确定性步骤没有隐藏侧通道。

### Outer-agent

- 字面消费 `skill_hint`。
- 在阻塞 seam 与对应的 resume handoff 之间保留 `memory_for_next_step`。
- 不要超出当前阻塞步骤契约进行即兴发挥。

## Templates

```guide-template
dotnet so.dll planner \
  --description-file skill-plan.md \
  --workflow-file so-template.json \
  --context-file context.json \
  --audit-output outputs/audit
```

```guide-template
dotnet so.dll run \
  --workflow-file workflow.json \
  --context-file context.json \
  --audit-output outputs/audit
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
- workflow 在执行前已物化
- planner 或 compile 在执行前会先产出 Mermaid Markdown 与 HTML 校验输出
- step kind 显式可见
- 本地工具具备确定性
- memory extraction 已定义或可推导
- 调用方可以把结构化外部结果送回 SO
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

- 让调用方只能从 prose 推测下一步动作。
- 把 memory 藏在 prompt 里，而不是 workflow context 里。
- 不经编译就直接运行简写命令，而不生成持久化 workflow。
- 把 wrapped command output 和 SO 边界载荷混成一条不可分辨的纯文本流。
- skill 隐藏 package / 通道选择，不先引导用户阅读 package index。
