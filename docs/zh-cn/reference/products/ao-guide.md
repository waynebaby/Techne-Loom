# Loom Agent Execution Orchestrator Guide

[English](../../../en/reference/products/ao-guide.md) | [根目录](../../README.md)

Version: draft

Build: repository source

Compatibility: pre-release public runtime contract

## Overview

Loom Agent Execution Orchestrator 是面向顶层 agent 的探索式编排产品，专门处理不确定环境中的推进问题。

它不会掩盖不确定性，而是持久化不断演化的 workflow 状态，输出 machine-first 的控制数据，并在主要控制 seam 处 weave out；当协议层需要显式表达时，则输出带显式 boundary 字段的 blocked payload，让调用方有意识地决定下一步。

本 guide 使用 repo 级的 [Workflow 术语](../../../zh-cn/architecture/workflow-terminology.md)。按照这套词汇，Loom Agent Execution Orchestrator 会在控制 seam 上 weave out，并通过 blocked 控制载荷里的 `boundary_reason`、`weave_out_request` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet ao.dll resume` result envelope weave back。

当前实现状态：

- `.NET` runtime 已实现 `dotnet ao.dll --guide`、`dotnet ao.dll --help`、`dotnet ao.dll compile`、`dotnet ao.dll prompt-plan`、`dotnet ao.dll prompt-replan`、`dotnet ao.dll run`、`dotnet ao.dll resume`
- Loom Agent Execution Orchestrator 在本项目里是 CLI-only；不再公开 MCP 宿主或 MCP tools
- 当前 AO 控制载荷实际发出 `blocked` 与 `completed`；CLI/runtime 失败会以 `type: error` 的 `<ao_property>` 形式输出
- AO compile 会针对调用 agent 预先编写的 workflow 文件产出 Mermaid Markdown、HTML 与 workflow JSON 备份，作为校验输出
- AO prompt-plan 与 prompt-replan 会通过 `<ao_property type="prompt">` 输出 AO 自有、由代码生成的 planner / replanner prompt 文本
- 每次 AO run/resume 还会返回 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact links
- `run` 现在还可通过 `--instance-file` 接受一份外部编写的 `WorkflowInstance`，让第一次 runtime blocked step 的审计沿用 compile/prompt-plan 已验证的同一份图

## 环境准备

通过 skill 或直接 CLI 使用 Loom Agent Execution Orchestrator 前：

1. 先从 [`packages.released.zh-CN.md`](../../../../packages.released.zh-CN.md) 或 [`packages.beta.zh-CN.md`](../../../../packages.beta.zh-CN.md) 选择 package 通道。
2. 把 NuGet.org 作为一等“最新包来源”来安装或确认版本；如果本地 Loom Agent Execution Orchestrator 执行需要从 NuGet 下载，请把 Loom Agent Execution Orchestrator runtime bundle 一起恢复：`Techne.Loom.AgentOrchestrator`、`Techne.Loom.Common`、`Techne.Loom.Abstractions`，并保持三者使用同一通道/版本。只有在 NuGet.org 不可用，或你明确需要包资产链接时，才退回 GitHub release asset。
3. 通过 `dotnet ao.dll --guide` 阅读 guide。
4. 如需用于规划审阅或产物交换，由调用 agent 在 AO CLI 之外预先编写 Loom Agent Execution Orchestrator workflow JSON snapshot。
5. 准备可写的 session 目录；如有需要，再准备显式 audit 输出根目录，用于 compile 校验产物和 run/resume 审计产物。
6. 保持 checked-in 计划和预编写 snapshot 不可变：不要把 Loom Agent Execution Orchestrator 的 `--session-dir` 输出或 `--audit-output` 放到 skill 文件夹下面；应改用运行时 temp 目录或显式 execution-output 目录。

## Contracts

```guide-contract
inputs:
  objective: 用户目标或任务请求
  context: 当前已知事实、产物和既有决策
  session_dir: 必填，作为 CLI 字段表示 AO 会话目录，对应 `--session-dir`；必须位于 skill 文件夹之外
outputs:
  status: blocked | completed（当前 control payload 的实际取值）
  session_id: AO 生成的稳定会话标识
  boundary_reason: 可选，返回原因
  workflow_file: 基于该会话目录与 session_id 派生的当前可变 workflow 路径
  workflow_instance_file: 当前用于审计连续性与 replan 编辑的 caller-managed 或 runtime-owned WorkflowInstance 路径
  event_log_file: 基于该会话目录与 session_id 派生的追加式日志路径
  current_node_id: 当前焦点节点
  result_file: 为未来 AO 自有输出 artifact 预留的可选字段；当前不会填充
  pending_requirements: 可选，结构化缺失输入
  next_frontier: 可选，候选下一步动作
  human_or_agent_hint: 可选，给调用方的短动作提示
  weave_out_request: 当 AO 需要外界做比较、规划或类似分析时，承载结构化 weave-out request 数据
  audit_artifacts:
    output_root: 审计输出根目录
    step_directory: 按 step 划分的审计目录
    mermaid_file: 该时刻的 Mermaid Markdown 路径
    html_file: 该时刻的 HTML 路径
    workflow_backup_file: 该时刻的 workflow JSON 备份
    summary_file: 该 step 的结构化摘要文件，汇总 boundary、frontier 与关键路径引用，便于直接复盘
progress_output:
  type: progress
  workflow_file: 当前可变 workflow 路径
  workflow_instance_file: 当前 caller-managed 或 runtime-owned WorkflowInstance 路径
  event_log_file: AO 的追加式事件日志路径
  current_node_id: 当前焦点节点
  audit_artifacts:
    mermaid_file: 当前 workflow 的 Mermaid Markdown 路径
    html_file: 当前 workflow 的 HTML 路径
event_log:
  file_shape: append-only jsonl
  common_fields:
    - event_type
    - ts
    - session_id
    - workflow_file
    - event_log_file
    - workflow_instance_file
    - step_sequence
    - step_action
    - step_directory
    - summary_file
  boundary_event_fields:
    - boundary_reason
    - transition_id
    - correlation_key
    - pending_requirements
    - next_frontier
prompt_output:
  type: prompt
  command: prompt-plan | prompt-replan
  prompt_kind: plan | replan
  prompt_template_version: AO 自有 prompt 模板版本
  prompt: 由代码生成的 prompt 文本
  blocks:
    - block_id: 稳定的 machine-ingestible 查找键，例如 workflow.output-schema 或 prompt.replan.current-workflow-projection
      block_kind: guide-contract | guide-example | guide-template
      semantic_role: schema | task-contract | runtime-context | workflow-projection | workflow-instance | selected-seam | user-objective
      title: 面向人的 block 标题
      content_type: 通常为 application/json
      order: 在生成 prompt 内部的稳定渲染顺序
      consumption_requirement: required | optional，供下游 prompt 消费方判断必须消费还是参考即可
      content: 由代码生成的 JSON block 内容
      tags: 供下游工具使用的可选分类标签
  allowed_node_kinds: 允许使用的 workflow node kind discriminator 值
  allowed_command_kinds: 允许使用的 command invocation kind 值
  workflow_file: 使用 prompt-replan 时对应的 AO 当前可变 workflow 路径
  workflow_instance_file: 使用 prompt-replan 时显式传入的 WorkflowInstance 文件路径
  selected_tbr_id: 使用 prompt-replan 时显式选中的 TBR 节点 id
resume_input:
  transition_id: 必填，且必须与当前 blocked seam 的 `workflow_file.last_transition_id` 一致
  correlation_key: 可选，调用方针对单轮 boundary 的关联键
  payload: 必填，调用方结构化结果对象，AO 会并入运行时 context
```

AO 的恢复输入应是结构化结果，而不是自由叙述的回顾文本。

按 repo 术语，AO 返回 blocked 控制载荷时就是一次 weave out，而 `dotnet ao.dll resume` 就是 weave-back 路径。

当前 runtime 持久化故意同时保留两种形状：

- `workflow_file` 是 AO 的 snapshot 控制文件，runtime resume 会用它来校验 `transition_id`。
- `workflow_instance_file` 是当前图形态的 `WorkflowInstance` 表面，用于 compile 连续性、runtime audit 连续性，以及 caller-managed replan 编辑。
- 在 `session_dir` 下，AO 还会维护 `session_<id>_runtime.workflow.json` 作为 runtime `WorkflowInstance` sidecar，并维护 `session_<id>_runtime.workflow.pointer.json` 作为指向外部 caller-managed `workflow_instance_file` 的可选指针文件。

## Plan/Replan 操作手册

本节给出调用方与 outer-agent 的操作层手册，并与当前 `AoBoundaryPlanner`、`AoRuntimeService` 的实际行为逐字段对齐。

### Schema 边界（按现有代码）

plan/replan 的机器级下发只能使用当前 AO runtime 字段。

boundary/progress 读取字段：

- `status`
- `session_id`
- `workflow_file`
- `event_log_file`
- `current_node_id`
- `boundary_reason`
- `pending_requirements`
- `next_frontier`
- `human_or_agent_hint`
- `weave_out_request`

resume 写入字段：

- `transition_id`
- `correlation_key`
- `payload`

不要在文档、提示词、示例中引入新的 AO 顶层字段。

### 下发约定层（非 schema）

当调用方需要更强执行提示时，把扩展信息放到 resume `payload` 里的调用方约定数据。

建议稳定约定键：

- `payload.plan_meta.plan_phase`: `initial-plan` | `replan`
- `payload.plan_meta.unsolved_target_id`: 调用方选定的未决目标节点 id
- `payload.plan_meta.selected_frontier_action`: 从 `next_frontier` 选中的动作
- `payload.plan_meta.method`: `u2d-expand-bridge`
- `payload.plan_meta.determined_path_ids`: 本轮产出的确定通路节点 id（有序）
- `payload.plan_meta.unresolved_bridge_ids`: 暂保留到后续轮次的未决桥接节点 id
- `payload.plan_meta.next_step_prompt`: 下一轮执行用祈使式操作提示词

以上键是约定，不是 AO 官方 wire-schema 字段。

AO 现在还拥有两个 prompt 生成支持表面：

- `dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>]`：生成用于编写 WorkflowInstance JSON 文件的 planner prompt 文本
- `dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id>`：生成用于修改当前 WorkflowInstance、替换某个选中 `tbr` 节点的 replanner prompt 文本

AO run 现在还暴露一个 authored-graph 连续性表面：

- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--instance-file <path>] [--audit-output <path>]`：当传入 `--instance-file` 时，AO 会从这份外部编写的 `WorkflowInstance` 起步，并在后续返回里把它作为 `workflow_instance_file` 返回，直到 runtime sidecar / pointer 接管或更新当前图。

这两个 prompt 命令是 AO 自有的 inspection / authoring 表面，不是新的 AO 正式执行模式，也不会改变 AO 现有 run/resume 顶层 wire schema。

### 触发矩阵

当满足以下任一条件时，必须进入 AO plan/replan 流程：

- AO 返回 `status: blocked`
- AO progress 或 boundary payload 出现非空 `next_frontier`
- AO boundary payload 出现 `boundary_reason`
- AO boundary payload 出现 `weave_out_request`

当前 boundary reason 与默认 planner 产物映射：

- `clarification_required`: `current_node_id=boundary.clarification`，`transition_id=transition.clarify`，`pending_requirements=[confirmed_scope]`
- `tool_probe_required`: `current_node_id=boundary.tool_probe`，`transition_id=transition.tool_probe`，`pending_requirements=[probe_report]`
- `delegation_required`: `current_node_id=boundary.delegation`，`transition_id=transition.delegation`，`pending_requirements=[delegation_result]`
- `weave_out_required`: `current_node_id=boundary.weave_out`，`transition_id=transition.weave_out`，`pending_requirements=[weave_back_result]`，并携带结构化 `weave_out_request`

当上下文包含 `context.force_boundary_reason` 时，AO 会先归一化后强制采用该 reason。
当 `confirmed_scope` 被恢复为真且没有强制 boundary reason 时，当前默认 planner 会离开 clarification seam，并继续进入默认的 `tool_probe_required` seam。`payload.plan_meta.selected_frontier_action` 目前作为调用方的结构化决策记录保留在 context 中，但不会单独创建新的 boundary reason。

### 首次 blocked 后的 Plan 步骤

1. 读取 `<ao_property type="boundary">`，提取 `status`、`boundary_reason`、`current_node_id`、`pending_requirements`、`next_frontier`、`human_or_agent_hint`、`workflow_file`、`event_log_file`。
1. 打开 `workflow_file`，读取 AO workflow snapshot 里的 `last_transition_id`。这是必须步骤，因为 boundary payload 顶层不会直接给 `transition_id`，而 runtime resume 会严格校验它。
1. 如果 AO 返回了 `workflow_instance_file`，把它视为当前审计连续性与 caller-managed replan 编辑的图源。它可能是传给 `run --instance-file` 的外部 authored 文件，也可能是 `session_dir` 下的 runtime sidecar 图。
1. 如果需要 AO 自有 prompt 文本，调用 `dotnet ao.dll prompt-plan --objective-file <path> [--context-file <path>]`。该 prompt 应明确要求结果是一个 WorkflowInstance 文件生成任务，且必须同时包含至少一条可到达终点的可行路径和至少一条仍可通向终点的 `tbr` 路径。
1. 基于当前 `pending_requirements` 与 `next_frontier` 只生成一份聚焦行动计划，并明确选择一个 frontier 分支。
1. 只执行满足当前分支所需的最小外部动作。
1. 生成结构化 resume envelope JSON：

- `transition_id`: 必须等于 snapshot 的 `last_transition_id`
- `correlation_key`: 可选，用于本轮 boundary 的稳定关联键
- `payload`: 结构化外部结果字段，可附带调用方约定元数据（例如 `payload.plan_meta.unsolved_target_id` 与 `payload.plan_meta.next_step_prompt`）

1. 通过 `dotnet ao.dll resume --session-dir <path> --session-id <id> --result-file <path>` weave back。

### 后续 blocked 的 Replan 循环

1. 每次 resume 后都重新解析 AO 输出；若仍是 `status: blocked`，立即开始下一轮 replan。
2. 重新读取最新 `workflow_file` snapshot，刷新 `last_transition_id`、`last_boundary_reason`、`pending_requirements`、`next_frontier`。如果 AO 也返回了 `workflow_instance_file`，同步刷新它。
3. 除非与最新 blocked payload 一致，否则旧 frontier 选择全部视为过期。
4. 如果需要 AO 自有 prompt 文本，调用 `dotnet ao.dll prompt-replan --session-dir <path> --session-id <id> --instance-file <path> --tbr-id <id>`，其中 `--instance-file` 通常应直接使用 AO 最新返回的 `workflow_instance_file`。该 prompt 应明确说明最近一次选中的 frontier action 没有收敛、现在要展开指定 `tbr` 节点、替换路径必须重新接回原来上下游图点，并且总图里仍要保留一个或多个 `tbr`。
5. 依据最新 boundary 重新计算外部动作切片，写新的 `result-file` envelope。`payload.plan_meta` 只保留与最新 boundary 仍然一致的约定元数据。
6. 使用新的 envelope 再次 resume。

### 默认 runtime audit 图说明

当 `run` 没有传入 `--instance-file` 时，AO 仍会生成合法的 runtime audit artifact，但当前图模式是 `minimal-sidecar-only`：它只保证 blocked seam、wait-resume transition 与 boundary metadata 可审计，并不等价于一份 caller-authored 的完整执行图。

当 `run --instance-file <path>` 被显式使用时，AO 会尽量保持 compile、prompt-plan、首次 blocked runtime audit 与后续 replan 之间的图连续性；这种情况下 `workflow_instance_file` 应被视为当前审计连续性的主图源。

禁止在 AO 已移动到新 blocked seam 后复用旧 `transition_id`。当 `transition_id` 与当前 blocked seam 不匹配时，runtime 会拒绝 resume。

### 完成门槛

当合并后的 context 中任一布尔键为 true 时，AO 会进入 completed 分支：

- `mark_completed`
- `completed`
- `is_completed`

操作要求：

1. 仅在顶层任务确已收敛时，才在 resume payload 中设置上述完成键。
2. 携带该 payload 执行一次 resume。
3. 只有 AO 返回 `status: completed` 且 `current_node_id: state.completed` 才可判定流程完成。

### 返回说明模板

对外说明 AO plan/replan 决策时，统一使用以下结构。第一段是 AO runtime 字段，第二段是通过 `payload` 携带的调用方约定元数据：

- `status`: blocked | completed
- `boundary_reason`: 当前 AO boundary reason（blocked 时必填）
- `current_node_id`: 当前 AO 节点
- `transition_id_source`: `workflow_file.last_transition_id`
- `pending_requirements`: 当前 AO payload 给出的要求列表
- `external_actions_executed`: AO 外部已执行动作
- `resume_envelope_written`: 结果文件路径与关键 payload 字段
- `resume_result`: 本次 resume 后返回的 blocked/completed 结果与关键字段
- `payload.plan_meta.plan_phase`: initial-plan | replan
- `payload.plan_meta.unsolved_target_id`: 本轮未决目标节点 id
- `payload.plan_meta.selected_frontier_action`: 从 `next_frontier` 中选中的动作
- `payload.plan_meta.method`: 建议 `u2d-expand-bridge`
- `payload.plan_meta.determined_path_ids`: 本轮确定通路节点 id
- `payload.plan_meta.unresolved_bridge_ids`: 延后收敛的未决桥接节点 id
- `payload.plan_meta.next_step_prompt`: 下一轮祈使式执行提示词

## Behavior

AO 应当：

- 检查当前上下文
- 扩展或细化 workflow frontier
- 在澄清、探测、委派、重规划和完成之间做选择
- 持久化决策、产物和 blocked payload 元数据
- 维护可变 workflow 文件和 append-only event/snapshot log
- 当调用方请求 prompt-plan 或 prompt-replan 支持表面时，由代码生成 AO 自有 planner / replanner prompt 文本
- 当需要外部比较、规划或类似分析时，通过显式的 blocked payload 字段表达 weave-out request，而不是把它藏进不透明 prose
- 当 resume envelope 的 `transition_id` 与当前待处理 payload 字段所记录的 blocked workflow seam 不匹配时，明确拒绝恢复
- 当会话元数据确实需要参与执行时，把它视为显式 CLI 输入，而不是依赖隐藏的宿主状态

AO 不应当：

- 冒充确定性 skill 执行器
- 把控制态藏进纯叙述文本
- 把所有决策都折叠进一次不透明的 prompt 往返
- 不要绕开文档化的 CLI 控制面去写私有胶水
- 不要把 prompt-plan 或 prompt-replan 当成与 run/resume 同级的正式 AO run surface

## Responsibilities

### Caller

- 提供目标和当前已知上下文。
- 如需下载本地运行时，必须恢复完整的 AO runtime bundle，而不是只下载 `Techne.Loom.AgentOrchestrator`。
- 执行 AO 请求的外部动作。
- 用结构化结果恢复 AO。
- 在多轮之间保留 `session_id`。
- 保持稳定且可写的会话目录，并通过 `--session-dir` 传入。
- 让 `--session-dir` 输出和任何 `--audit-output` 都位于 skill-owned 目录之外。
- 每次 AO progress update 都要在 think-out-loud 输出中带上当前 workflow 的 Mermaid Markdown 与 HTML 路径。

### Author

- 定义控制态文件如何存储和暴露。
- 保持 AO 输出稳定且 machine-first。
- 让 weave-out request、它们当前的 wire 字段，以及对应 event log 轨迹保持可见，而不是埋进私有启发式里。

### Outer-agent

- 决定是否采纳 AO 给出的 frontier。
- 在恢复之间保留产物引用与 blocked payload 上下文。
- 把 AO 当作探索式协调者，而不是执行 SO 拥有的确定性工作的地方。
- 如果需要预编写 AO workflow file，由 outer-agent 生成满足 AO snapshot schema 的 JSON，再调用 `dotnet ao.dll compile`。
- 审计产物、中间 workflow 物化文件，以及可在对话中引用的运行输出，默认都放在运行时 temp 根、repo 根 temp 根，或用户明确指定的 execution output 根，不能默认落到 skill 文件夹里。

## Templates

```guide-template
dotnet ao.dll compile \
  --workflow-file ao-plan.json \
  --audit-output outputs/audit
```

`ao-plan.json` 可以继续作为 checked-in 或交换用的 source artifact，但 `outputs/audit` 应位于 skill 文件夹之外。

```guide-template
dotnet ao.dll run \
  --objective-file objective.md \
  --context-file context.json \
  --session-dir outputs/sessions \
  --audit-output outputs/audit
```

`outputs/sessions` 和 `outputs/audit` 都必须位于 skill-owned 目录之外，避免 AO runtime state 写脏 checked-in skill assets。

```guide-template
dotnet ao.dll resume \
  --session-dir outputs/sessions \
  --session-id 20260609010101_abc12345 \
  --result-file latest-boundary-result.json
```

Resume 必须继续指向同一个外部 session 目录，而不能指向 skill 文件夹下的路径。

```guide-checklist
- 目标清晰明确
- 当调用方希望保留可复用的 AO workflow snapshot artifact 时，调用 agent 会先编写 AO workflow JSON 文件，再进入校验交接
- compile 在执行前会先产出 Mermaid Markdown 与 HTML 校验输出
- 调用方已保存 session_id
- 会话目录稳定且可写
- 会话目录和 audit 输出都位于 skill 文件夹之外
- 产物引用可持久化
- 调用方可以用结构化数据恢复
- 控制输出已持久化并可审计
- 保持文档化的 CLI 控制路径
- weave-out request 必须显式表达，不能藏在 prose 里
- 审计和中间输出默认放在 skill 文件夹之外的 temp / execution-output 根目录
- compile 不得覆盖已有 artifact 文件，必须失败
```

## Examples

```guide-example
name: clarify-missing-dimensions
input: 用户请求电池布局，但包络尺寸不完整
ao-return:
  status: blocked
  boundary_reason: clarification_required
  pending_requirements:
    - enclosure_length
    - enclosure_width
    - enclosure_height
  audit_artifacts:
    step_directory: outputs/audit/wf-20260609010101_abc12345/step-0001-blocked-clarification_required
    summary_file: outputs/audit/wf-20260609010101_abc12345/step-0001-blocked-clarification_required/summary.json
```

```guide-example
name: probe-local-repository
input: 顶层 agent 需要定位一个失败 CLI 路径的控制代码
ao-return:
  status: blocked
  boundary_reason: tool_probe_required
  next_frontier:
    - search_cli_entrypoints
    - inspect_recent_validation_logs
```

```guide-example
name: delegate-subtask
input: 编排过程需要将代码审查委派给更窄的 agent
ao-return:
  status: blocked
  boundary_reason: delegation_required
  current_node_id: review.slice.2
```

```guide-example
name: weave-out-for-frontier-comparison
input: AO 需要外部比较两个竞争的 execution frontier
ao-return:
  status: blocked
  boundary_reason: weave_out_required
  weave_out_request:
    objective: compare two frontier candidates
    artifacts:
      - frontier-a.json
      - frontier-b.json
```

```guide-example
name: complete-current-workflow
input: 顶层任务已经收敛，调用方带着完成数据恢复 AO
ao-return:
  status: completed
  session_id: 20260609010101_abc12345
  workflow_file: outputs/sessions/session_20260609010101_abc12345_workflow.json
  current_node_id: state.completed
  audit_artifacts:
    step_directory: outputs/audit/wf-20260609010101_abc12345/step-0002-completed
    summary_file: outputs/audit/wf-20260609010101_abc12345/step-0002-completed/summary.json
```

## Anti-Patterns

- 把 AO 当成通用聊天外壳。
- 返回只包含 prose、却没有 workflow、node 或 artifact 状态的数据。
- 用 AO 执行本应属于 SO 的确定性逐步 skill 逻辑。
- 没有明确理由就绕开文档化的 CLI / package 控制路径，改写成私有 wrapper。
- AO 需要 weave-out request 时，不发结构化 boundary，而是用自由叙述去暗示。
- skill 隐藏 package / 通道选择，不先引导用户阅读 package index。
