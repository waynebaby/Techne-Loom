# SkillOrchestrator Guide

[English](../../../en/reference/products/so-guide.md) | [根目录](../../README.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

把 `dotnet so.dll --guide` 当成 governance 锚点，而不是一条绕行路径。对于 `/loom-skill-enhancement` 自身，以及任何已经被 SO 增强过的 target skill，只要某个可运行的 SO runtime 已经成功产出一份新的 guide 结果，后续所有受治理执行都必须留在这份 guide 所对应的已发布 SO 包 runtime 表面上。无论这份 guide 是从 skill 入口、直接 CLI，还是某个已恢复的 runtime bundle 拿到的，只要 guide 已经存在，官方治理执行就必须回到它所描述的已发布 SO 包 runtime。不要先读到 guide，然后官方 SO skill 或 target skill 执行又漂回仓库构建产物、手工拼装 runtime，或其他非治理路径。

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

1. 如果是 direct CLI 或手动获取 package，先从 [`packages.released.zh-CN.md`](../../../../packages.released.zh-CN.md) 或 [`packages.beta.zh-CN.md`](../../../../packages.beta.zh-CN.md) 选择 package 通道。对于 `/loom-skill-enhancement` 和 SO-enhanced target skill，常规执行则应复用 checked-in lock 与当前由 CI/CD 管理的 skill package version block 已绑定的 runtime 版本。如果这两个权威一度不一致，应先以当前由 CI/CD 管理的 skill package version block 作为即时下载依据，并在继续受治理执行前把 checked-in lock 更新到一致状态。
2. 如果要从 NuGet 下载本地运行时，请把 SO runtime bundle 一起恢复：`Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common`、`Techne.Loom.Abstractions`，并保持三者使用同一通道/版本。不要只恢复 `Techne.Loom.SkillOrchestrator`。当精确 package id/version 已知时，应直接探测或下载对应的 `.nupkg` URL，而不是等待页面、搜索结果或 registration 索引刷新。
3. 对 `/loom-skill-enhancement` 和任何 SO-enhanced target skill，正式 workflow 操作都应使用绑定 runtime 版本及其派生通道对应的已发布 SO 包产物，不要把仓库源码构建产物或手工拼装的本地 runtime 当作常规 workflow 操作表面，除非用户明确批准 blocked 状态下的最后手段例外。
4. 通过 `dotnet so.dll --guide` 阅读 guide。
5. 在任何 target-skill 的 planning、authoring、validation、compile、run、resume 或下游输入收集开始之前，先证明所选已发布 SO runtime 真实可运行，并且能从该 runtime 产出一份新的 `dotnet so.dll --guide` 结果。
6. 一旦这份新的 guide 结果已经存在，`/loom-skill-enhancement` 自身以及任何 SO-enhanced target skill 的后续受治理执行都必须回到该 guide 所描述的已发布 SO 包 runtime 上。`--guide` 不是官方 skill 或 target skill 执行继续停留在仓库构建产物、手工拼装 runtime，或其他非治理路径上的许可。
7. 准备 workflow JSON 路径；如有需要，再准备显式 audit 输出根目录，用于 compile 校验产物和 run/resume 审计产物。
8. 保持 checked-in source template 不可变：在启动一轮新的正式 `run` 之前，把 checked-in source workflow 复制到运行时 temp 目录或显式 execution-output 目录，并且不要把 runtime workflow copy、`.events.jsonl` sidecar 或 audit 输出放进 skill 文件夹。同一执行链后续的 `resume` 必须继续使用这份已持久化的 runtime copy。
9. 对 `/loom-skill-enhancement` 和任何 SO-enhanced target skill，常规 workflow 治理都必须留在 `dotnet so.dll --guide`、`dotnet so.dll compile`、`dotnet so.dll run` 与 `dotnet so.dll resume` 路径上。不要把直接修改 workflow JSON 当作常规维护路径。

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

当 `MemoryRead` 被用于 re-enhancement 或 governance review 阶段去检查 checked-in target-skill 资产时，它必须读取真实文件快照，而不是占位式 context copy，并且每一个被检查的资产路径都必须留在声明的 target-skill asset root 之下。

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
- 每次启动新的正式 `run` 前，都要先把 checked-in source template 复制到运行时 temp 或 execution-output 目录；当 workflow 之后进入 blocked，`resume` 必须继续作用于同一份已持久化的 runtime copy。
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

对正在运行中的外部 workflow `.json` 副本做手动修改，也只能视为 blocked 状态下的最后手段应急变通，不能当作常规 workflow 操作路径。

对于 SO-governed target-skill template，还必须设置根 `templateKind: so-governed-target-skill` 和根 `validation` 契约。`compile` 会在 workflow 获得 execution authority 之前，同时校验结构正确性、route-aware business-output gates、seam ownership、blocked strongest-earned outputs 与 done reachability。

`compile` 还要求每个 state 节点都声明一个非空的 `workflowPhase`。这个字段表示该节点属于整个 workflow 的哪个阶段，compile 会把它当成泳道分组的必填编写信息，而不是可有可无的渲染元数据。

如果某次 target-skill 修改打算让该 governed workflow 成为可运行的 execution authority，那么物化后的 runtime workflow 还必须能在当前公开的 `dotnet so.dll run` 和 `dotnet so.dll resume` 路径上实际执行。不要让可运行 workflow 保持在 `Drafting`，也不要依赖当前公开 runtime 并未暴露的私有或不可用 built-in tool 名称。若某份 checked-in workflow JSON 只是 draft 或 compile-review source template，必须明确这样标注，而不能把它描述成可直接运行。

Compile 还会在 `workflow.mermaid.md`、`workflow.html` 和 `workflow.json` 旁边写出 `workflow.analysis.json`。用这份 analysis artifact 在执行前审阅控制流结构：branch、switch-like group、loop、所需输入、发布的输出族、用户 seam、运行时 seam 和 gate 覆盖。

```guide-template
dotnet so.dll run \
  --workflow-file workflow.current.json \
  --context-file context.json \
  --audit-output outputs/audit
```

`workflow.current.json` 是在 skill 文件夹之外创建的可变 runtime copy。不要把 `--workflow-file` 指回 `<target-skill-root>/assets/so-workflow/`，`outputs/audit` 也不要放在那里。启动一轮新的正式 run 时才需要创建新的 runtime copy；后续 resume 必须继续使用同一份已持久化的 runtime copy，而不是重新从 checked-in source asset 重建。

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
- 每次启动新的正式 run 链时，都要先从 checked-in source asset 复制出新的外部 workflow 执行文件
- resume 必须沿用该 run 链中同一份已持久化的 runtime workflow copy
- 直接 workflow JSON 修改不是常规治理路径；blocked 状态下的应急变通必须先得到用户明确许可，并在修改后立刻回到 `dotnet so.dll`
- audit 输出也必须位于 skill 文件夹之外
- compile 在执行前会先产出 Mermaid Markdown、HTML、workflow backup 与 workflow analysis 校验输出
- 对于 SO-governed target-skill template，compile 还要求根 validation 契约、route-aware business-output gates、strongest-earned blocked-output 声明与 ownership-safe seams 全部通过
- 对于 target-skill 修改，runtime-ready 证据和 fresh-guide 证据应在任何后续 planning、authoring、validation、compile、run 或 resume 步骤之前显式建模出来
- 如果 re-enhancement review 要检查 checked-in 资产，这些 inspection 节点必须先读取真实文件快照，再交给 gap-review subagent 消费
- 基于文件的 checked-in asset inspection 必须声明显式的 target-skill asset root，并且必须拒绝绝对路径或逃逸该 root 的路径遍历
- 如果某个 governed workflow 被作为可运行 execution authority 对外呈现，那么它的 materialized runtime copy 必须能在当前公开 `dotnet so.dll run` 路径上实际执行，而不能只是 compile-clean
- 当某条 workflow route 用 runtime-owned completion manifest 去引用 checked-in source deliverables 时，这条 route 的 contract 还应显式声明 checked-in source deliverable output families 和 runtime-owned completion-manifest output family，避免 done reachability 退化成只有治理型证据
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
- 当 runtime 版本已经由 CI/CD 管理的 skill package version block 或 checked-in runtime lock 绑定时，受治理 skill 仍然要求用户再选 package / 通道。
