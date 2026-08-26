# Loom Agent Execution Orchestrator Guide：Plan And Replan

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [English](../../en/guides/ao-guide-reference-plan-replan.md) | [根目录](../README.md)

版本：draft
构建：repository source

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

每一次 weave-out 都必须在 `weave_out_request` 中携带最小化的 `evidence_references` 引用清单，引用导致下一步动作或支撑该动作的文档。不要为此新增 AO 顶层字段。

每个引用必须包含：

- `path`：workspace-relative 或 runtime-output-relative 路径，不得使用机器绝对路径
- `start_line` 与 `end_line`：从本次 weave-out 实际使用的精确文件内容中核验出的 1-based inclusive 行号
- `role`：说明为什么下一步动作需要这段引用

如果 guide 控制当前决策，必须引用最新一次 `dotnet ao.dll --guide` 成功 JSON 结果返回的实际 `guide_path` 及其输出行号。只引用 guide source 不充分。该命令不会导出 guide 文件；没有经过核验的 `evidence_references` 的 weave-out 不完整，不得作为成功证据 weave back。输出必须保持紧凑：只返回下一步动作、最小引用清单和 resume payload 契约，不得重复完整 context-pack 清单。

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

### Blocked Route 历史交接

当确认当前 route 已经无法继续时，不得只把最新 boundary payload 丢给 planner。必须持久化并传入结构化的 `replan_history`，其中包括：

- 当前 `workflow_file`、`workflow_instance_file`、blocked `current_node_id` 与 `last_transition_id`
- blocker reason 与精确的未满足要求
- 按顺序记录的尝试动作、结果，以及经过核验的 `evidence_references`
- 失败 route 对应的 event log 与 audit artifact 引用
- terminal business objective 与此前的 route 决策
- 选定的 replan anchor 与 strategy

planner 必须明确选择以下一种 strategy：

- `continue_from_current`：保留当前状态，重新设计一条可行 bridge
- `rollback_to_unconfirmed`：退回最近一个未确认或尚未设计完成的节点，再从那里向前设计
- `redesign_from_current`：保留已完成历史，只替换失败的后续路径
- `full_redesign`：重新设计 route，但保留 blocker 历史与 terminal objective
- `reversible_workaround`：执行最小可逆 workaround，并提供一步 rollback plan

每种 strategy 都必须返回从选定 anchor 到 terminal business outcome 的候选路径。没有 rollback 证据的 workaround 无效。planner 不得静默丢弃失败尝试、blocker 历史、此前 route 决策或对应 artifact 引用。

### 默认 runtime audit 图说明

当 `run` 没有传入 `--instance-file` 时，AO 仍会生成合法的 runtime audit artifact，但当前图模式是 `minimal-sidecar-only`：它只保证 blocked seam、wait-resume transition 与 boundary metadata 可审计，并不等价于一份 caller-authored 的完整执行图。

当 `run --instance-file <path>` 被显式使用时，AO 会尽量保持 compile、prompt-plan、首次 blocked runtime audit 与后续 replan 之间的图连续性；这种情况下 `workflow_instance_file` 应被视为当前审计连续性的主图源。

禁止在 AO 已移动到新 blocked seam 后复用旧 `transition_id`。当 `transition_id` 与当前 blocked seam 不匹配时，runtime 会拒绝 resume。

### 完成门槛

只有当以下任一布尔键为 true 且 `terminal_evidence` 非空时，AO 才接受 completed 请求：

- `mark_completed`
- `completed`
- `is_completed`
- `terminal_evidence`（必需的证据对象或引用）

操作要求：

1. 仅在顶层任务确已收敛时，才在 resume payload 中设置完成键并提供非空 `terminal_evidence`。
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
