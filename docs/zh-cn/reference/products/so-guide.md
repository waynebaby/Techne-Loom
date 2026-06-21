# SkillOrchestrator Guide

[English](../../../en/reference/products/so-guide.md) | [根目录](../../README.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

SO 是一个确定性的 skill 执行与跟踪产品。

它会先编译或加载 workflow，直接执行由 SO 自己拥有的步骤，并且只有在 workflow 完成，或遇到必须由外部参与的 seam 时才返回。

本 guide 使用 repo 级的 [Workflow 术语](../../../zh-cn/architecture/workflow-terminology.md)。按这套词汇，SO 会在遇到外部拥有的步骤时 weave out，并通过 blocked `<so_property>` payload 里的 `current_step_kind` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet so.dll resume` result envelope weave back。

当前实现状态：

- 当前 `.NET` runtime 已实现 `dotnet so.dll --guide`、`dotnet so.dll --help`、`dotnet so.dll compile`、`dotnet so.dll run`、`dotnet so.dll resume`、`dotnet so.dll status`、`dotnet so.dll inspect-workflow`、`dotnet so.dll inspect-events` 与 `dotnet so.dll ls`
- SO 的公开参数面使用 `compile` 来校验已有 `--workflow-file`
- SO 的每次 compile 都会产出 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis，作为 compile 校验输出
- SO 在 run/resume 表面会返回 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis report 的审计 artifact links
- Mermaid render 会根据 workflow step kind 语义和 owned-input 元数据使用浅色节点背景：AI/model/subagent 工作用绿色，代码/工具工作用蓝色，user-owned 的可选分支决策用黄色，必须用户输入用红色，一般条件分支用琥珀黄/浅黄，gate/governance 状态用白色或极浅灰色

## 环境准备

通过 skill 或直接 CLI 使用 SO 前：

1. 先从 [`packages.released.zh-CN.md`](../../../../packages.released.zh-CN.md) 或 [`packages.beta.zh-CN.md`](../../../../packages.beta.zh-CN.md) 选择 package 通道。
2. 如果要从 NuGet 下载本地运行时，请把 SO runtime bundle 一起恢复：`Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common`、`Techne.Loom.Abstractions`，并保持三者使用同一通道/版本。不要只恢复 `Techne.Loom.SkillOrchestrator`。
3. 通过 `dotnet so.dll --guide` 阅读 guide。
4. 准备 workflow JSON 路径；如有需要，再准备显式 audit 输出根目录，用于 compile 校验产物和 run/resume 审计产物。
5. 保持 checked-in source template 不可变：每次正式 `run` 或 `resume` 尝试，都要重新把 checked-in source workflow 复制到运行时 temp 目录或显式 execution-output 目录，并且不要把 runtime workflow copy、`.events.jsonl` sidecar 或 audit 输出放进 skill 文件夹。
6. 对 `/loom-skill-enhancement` 和任何 SO-enhanced target skill，常规 workflow 治理都必须留在 `dotnet so.dll --guide`、`dotnet so.dll compile`、`dotnet so.dll run` 与 `dotnet so.dll resume` 路径上。不要把直接修改 workflow JSON 当作常规维护路径。

## Contracts

```guide-contract
inputs:
  workflow_file: 源 workflow 或已校验 workflow 路径；`run` 和 `resume` 必须指向 skill 文件夹之外的 runtime copy
  context_file: 可选，初始上下文
  external_result: 可选，上一次阻塞步骤的结构化 weave-back 结果
so_property_types:
  progress:
    status: active | blocked | completed | failed
    instance_id: 持久化 workflow instance 标识
    workflow_file: 持久化后的当前 workflow 路径
    current_node_id: 当前 workflow 焦点节点
    next_node_id: 可选，已知时的下一节点
    event_log_file: 追加式执行事件路径
    audit_artifacts:
      output_root: 审计输出根目录
      step_directory: 按 step 划分的审计目录
      mermaid_file: 当前 workflow 的 Mermaid Markdown 路径
      html_file: 当前 workflow 的 HTML 路径
      workflow_backup_file: 当前 workflow 的 JSON 备份路径
      analysis_file: 如可用，当前 workflow analysis JSON 路径
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
      analysis_file: 如可用，该时刻的 workflow analysis JSON 路径
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

- 提供待校验的 workflow JSON。
- 如需下载本地运行时，必须恢复完整的 SO runtime bundle，而不是只下载 `Techne.Loom.SkillOrchestrator`。
- 每次正式 `run` 或 `resume` 尝试，都要在继续执行前重新把 checked-in source template 复制到运行时 temp 或 execution-output 目录。
- 当 SO weave out 时执行外部动作。
- 用结构化 weave-back envelope 恢复 SO。
- 把 `<so_property>` 视为权威 SO 控制载荷。
- 把 `<wrapped_exec>` 视为面向 shell 的流式 wrapper 输出表面。
- 在 resume sidecar JSON 中使用 `transition_id`、`correlation_key` 和 `payload`。
- 让 runtime workflow copy、event sidecar 和 audit 输出都位于 skill-owned 目录之外。
- 每次 progress update 都要在 think-out-loud 输出中带上当前 workflow 的 Mermaid Markdown 与 HTML 路径。
- 把 `workflow.analysis.json` 视为 machine-readable 摘要，用来审阅输入、输出族、分支、循环、用户 seam、运行时 seam、gate 与图灵完备控制风险。

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
dotnet so.dll compile \
  --workflow-file so-template.json \
  --audit-output outputs/audit
```

`so-template.json` 仍然是 checked-in source template。`outputs/audit` 也必须放在 skill 文件夹之外。

对 `/loom-skill-enhancement` 和任何 SO-enhanced target skill，不要把直接修改 checked-in workflow JSON 当作常规维护路径。只有当当前 `dotnet so.dll` 路径已经完全 blocked，且用户明确同意一个狭义变通方案时，才允许做最小的直接 JSON 修改去打通下一次 `dotnet so.dll compile`、`dotnet so.dll run` 或 `dotnet so.dll resume`；随后必须立刻回到 SO 治理路径。

对于 SO-governed target-skill template，还必须设置根 `templateKind: so-governed-target-skill` 和根 `validation` 契约。`compile` 会在 workflow 获得 execution authority 之前，同时校验结构正确性、route-aware business-output gates、seam ownership、blocked strongest-earned outputs 与 done reachability。

Compile 还会在 `workflow.mermaid.md`、`workflow.html` 和 `workflow.json` 旁边写出 `workflow.analysis.json`。用这份 analysis artifact 在执行前审阅控制流结构：branch、switch-like group、loop、所需输入、发布的输出族、用户 seam、运行时 seam 和 gate 覆盖。

```guide-template
dotnet so.dll run \
  --workflow-file workflow.current.json \
  --context-file context.json \
  --audit-output outputs/audit
```

`workflow.current.json` 是在 skill 文件夹之外创建的可变 runtime copy。不要把 `--workflow-file` 指回 `<target-skill-root>/assets/so-workflow/`，`outputs/audit` 也不要放在那里。每次正式 run/resume 都要重新创建新的 runtime copy，不要把旧的 checked-in 文件当作 live execution file。

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

Resume 持续作用于同一个外部 runtime copy，而不是 checked-in source template。

```guide-checklist
- workflow JSON 在执行前已物化
- checked-in source template 保持干净；run/resume 只针对外部可变 workflow copy，例如 `workflow.current.json`
- 每次正式 run/resume 都要从 checked-in source asset 重新复制出新的外部 workflow 执行文件
- 直接 workflow JSON 修改不是常规治理路径；blocked 状态下的应急变通必须先得到用户明确许可，并在修改后立刻回到 `dotnet so.dll`
- audit 输出也必须位于 skill 文件夹之外
- compile 在执行前会先产出 Mermaid Markdown、HTML、workflow backup 与 workflow analysis 校验输出
- 对于 SO-governed target-skill template，compile 还要求根 validation 契约、route-aware business-output gates、strongest-earned blocked-output 声明与 ownership-safe seams 全部通过
- step kind 显式可见
- 本地工具具备确定性
- memory extraction 已定义或可推导
- 调用方可以把结构化外部结果送回 SO
```

## Examples

如果你想看一份更完整的 SO 治理 target skill 运行叙述示例，其中包含 stage gate、branch fan-out、validation、audit evidence 与 Mermaid 路线图，请阅读 [SO 增强 Skill 运行示例](../../../zh-cn/examples/so-enhanced-skill-run.md)。

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

```guide-example
name: enhanced-target-skill-runtime-lock-reference
target_skill_markdown: |
  ## SO-Enhanced Runtime Lock

  本 skill 已被 Loom SO 增强。
  权威 SO runtime 版本锁：`assets/so-workflow/so-package-lock.json`。
  日常 SO runtime bundle 恢复必须先从 NuGet 解析锁定的精确 bundle；如果本地 cache 已经持有该相同版本 bundle，则直接复用，否则重新从 NuGet 下载。
notes:
  - 保持这段引用随 target skill 一起 checked in
  - 把 lock 文件视为日常 SO runtime 恢复的权威来源
```

```guide-example
name: minimal-so-package-lock
so_package_lock_json: |
  {
    "package_id": "Techne.Loom.SkillOrchestrator",
    "channel": "released",
    "resolved_version": "1.2.3",
    "runtime_restore": {
      "source": "nuget",
      "fresh_download": true,
      "allow_local_cache_when_exact_version_matches": true,
      "fallback_source": "github-release-asset"
    },
    "enhancement": {
      "resolved_at_utc": "2026-06-12T00:00:00Z",
      "selected_language": "zh-cn"
    },
    "notes": [
      "先从 NuGet 解析精确版本。",
      "除非本地 cache 已经持有完全相同版本，否则重新下载。",
      "只有在 NuGet.org 不可用时才退回 GitHub release asset。"
    ]
  }
restore_rule:
  - 先从 NuGet 解析精确版本
  - 只有本地 cache 已经持有完全相同版本时才复用
  - 否则必须从 NuGet 重新下载该精确版本
```

## Anti-Patterns

- 让调用方只能从 prose 推测下一步动作。
- 把 memory 藏在 prompt 里，而不是 workflow context 里。
- 不经编译就直接运行简写命令，而不生成持久化 workflow。
- 把 wrapped command output 和 SO 边界载荷混成一条不可分辨的纯文本流。
- skill 隐藏 package / 通道选择，不先引导用户阅读 package index。
