# AgentOrchestrator Guide

[English](../../../en/reference/products/ao-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public design

## Overview

AO 是面向顶层 agent 的探索式编排产品，专门处理不确定环境中的推进问题。

它不会掩盖不确定性，而是持久化不断演化的 workflow 状态，输出 machine-first 的控制数据，并在主要控制边界返回，让调用方有意识地决定下一步。

当前实现状态：

- 这份 guide 已经先于当前 AO 代码落地
- 仓库已经把这页当作下一批 major implementation slice 的公开 handoff 契约
- 目标 runtime 路线是官方 `ModelContextProtocol` C# SDK + `MCP/stdio`，并且从设计上保留 sampling planner 路线

## Contracts

```guide-contract
inputs:
  objective: 用户目标或任务请求
  context: 当前已知事实、产物和既有决策
  workflow_file: 可选，现有可变 workflow 快照
  event_log_file: 可选，追加式事件日志
outputs:
  status: active | blocked | completed | failed
  boundary_reason: 可选，返回原因
  workflow_file: 当前可变 workflow 路径
  event_log_file: 追加式日志路径
  current_node_id: 当前焦点节点
  result_file: 可选，最终或中间结果路径
  pending_requirements: 可选，结构化缺失输入
  next_frontier: 可选，候选下一步动作
  human_or_agent_hint: 可选，给调用方的短动作提示
  sampling_request: 可选，当 AO 希望外层宿主触发 model-side sampling 时给出的结构化请求
```

AO 的恢复输入应是结构化结果，而不是自由叙述的回顾文本。

## Behavior

AO 应当：

- 检查当前上下文
- 扩展或细化 workflow frontier
- 在澄清、探测、委派、重规划和完成之间做选择
- 持久化决策、产物和边界元数据
- 维护可变 workflow 文件和 append-only event/snapshot log
- 当需要 model-side sampling 或 planner 时，通过官方 MCP 路线显式表达，而不是把它藏进不透明 prose

AO 不应当：

- 冒充确定性 skill 执行器
- 把控制态藏进纯叙述文本
- 把所有决策都折叠进一次不透明的 prompt 往返
- 没有明确阻塞理由就绕开官方 MCP transport surface 去写 repo 私有胶水

## Responsibilities

### Caller

- 提供目标和当前已知上下文。
- 执行 AO 请求的外部动作。
- 用结构化结果恢复 AO。
- 托管 AO 的 MCP server/session，并在多轮之间保留当前 workflow 与 event log 路径。

### Author

- 定义控制态文件如何存储和暴露。
- 保持 AO 输出稳定且 machine-first。
- 让 sampling/planner 集成可见地反映在 event log 和 control payload 中，而不是埋进私有启发式里。

### Outer-agent

- 决定是否采纳 AO 给出的 frontier。
- 在恢复之间保留产物引用与边界上下文。
- 把 AO 当作探索式协调者，而不是执行 SO 拥有的确定性工作的地方。

## Templates

```guide-template
ao run \
  --objective-file objective.md \
  --context-file context.json \
  --workflow-file current-workflow.json \
  --event-log-file current-events.jsonl
```

```guide-template
ao resume \
  --workflow-file current-workflow.json \
  --event-log-file current-events.jsonl \
  --result-file latest-boundary-result.json
```

```guide-checklist
- 目标清晰明确
- 现有 workflow 路径稳定
- 产物引用可持久化
- 调用方可以用结构化数据恢复
- 控制输出已持久化并可审计
- 保持官方 MCP/stdio 宿主路径
- sampling/planner 请求必须显式表达，不能藏在 prose 里
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
```

```guide-example
name: probe-local-repository
input: 顶层 agent 需要定位一个失败 CLI 路径的控制代码
ao-return:
  status: active
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
name: request-sampling-planner
input: AO 需要 model-side 比较两个竞争的 execution frontier
ao-return:
  status: blocked
  boundary_reason: sampling_required
  sampling_request:
    objective: compare two frontier candidates
    artifacts:
      - frontier-a.json
      - frontier-b.json
```

```guide-example
name: complete-with-artifact
input: 顶层任务已经收敛，最终产物已写出
ao-return:
  status: completed
  result_file: outputs/final-report.md
  workflow_file: outputs/current-workflow.json
```

## Anti-Patterns

- 把 AO 当成通用聊天外壳。
- 返回只包含 prose、却没有 workflow、node 或 artifact 状态的数据。
- 用 AO 执行本应属于 SO 的确定性逐步 skill 逻辑。
- 没有明确理由就绕开官方 MCP/stdio 路线，改写成私有 transport 层。
- AO 需要 sampling/planning 时，不发结构化 boundary，而是用自由叙述去暗示。
