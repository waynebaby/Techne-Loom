# AgentOrchestrator Guide

[English](../../../en/reference/products/ao-guide.md)

Version: draft

Build: repository source

Compatibility: pre-release public runtime contract

## Overview

AO 是面向顶层 agent 的探索式编排产品，专门处理不确定环境中的推进问题。

它不会掩盖不确定性，而是持久化不断演化的 workflow 状态，输出 machine-first 的控制数据，并在主要控制 seam 处 weave out；当协议层需要显式表达时，则输出带显式 boundary 字段的 blocked payload，让调用方有意识地决定下一步。

本 guide 使用 repo 级的 [Workflow 术语](../../../zh-cn/architecture/workflow-terminology.md)。按照这套词汇，AO 会在控制 seam 上 weave out，并通过 blocked 控制载荷里的 `boundary_reason`、`weave_out_request` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet ao.dll resume` result envelope weave back。

当前实现状态：

- `.NET` runtime 已实现 `dotnet ao.dll --guide`、`dotnet ao.dll --help`、`dotnet ao.dll compile`、`dotnet ao.dll run`、`dotnet ao.dll resume`
- AO 在本项目里是 CLI-only；不再公开 MCP 宿主或 MCP tools
- 当前 AO 控制载荷实际发出 `blocked` 与 `completed`；CLI/runtime 失败会以 `type: error` 的 `<ao_property>` 形式输出
- AO compile 会针对调用 agent 预先编写的 workflow 文件产出 Mermaid Markdown、HTML 与 workflow JSON 备份，作为校验输出
- 每次 AO run/resume 还会返回 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact links

## 环境准备

通过 skill 或直接 CLI 使用 AO 前：

1. 先从 [`packages.released.zh-CN.md`](../../../../packages.released.zh-CN.md) 或 [`packages.beta.zh-CN.md`](../../../../packages.beta.zh-CN.md) 选择 package 通道。
2. 把 NuGet.org 作为一等“最新包来源”来安装或确认版本；只有在 NuGet.org 不可用，或你明确需要包资产链接时，才退回 GitHub release asset。
3. 通过 `dotnet ao.dll --guide` 阅读 guide。
4. 如需用于规划审阅或产物交换，由调用 agent 在 AO CLI 之外预先编写 AO workflow JSON snapshot。
5. 准备可写的 session 目录；如有需要，再准备显式 audit 输出根目录，用于 compile 校验产物和 run/resume 审计产物。

## Contracts

```guide-contract
inputs:
  objective: 用户目标或任务请求
  context: 当前已知事实、产物和既有决策
  session_dir: 必填，作为 CLI 字段表示 AO 会话目录，对应 `--session-dir`
outputs:
  status: blocked | completed（当前 control payload 的实际取值）
  session_id: AO 生成的稳定会话标识
  boundary_reason: 可选，返回原因
  workflow_file: 基于该会话目录与 session_id 派生的当前可变 workflow 路径
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
```

AO 的恢复输入应是结构化结果，而不是自由叙述的回顾文本。

按 repo 术语，AO 返回 blocked 控制载荷时就是一次 weave out，而 `dotnet ao.dll resume` 就是 weave-back 路径。

## Behavior

AO 应当：

- 检查当前上下文
- 扩展或细化 workflow frontier
- 在澄清、探测、委派、重规划和完成之间做选择
- 持久化决策、产物和 blocked payload 元数据
- 维护可变 workflow 文件和 append-only event/snapshot log
- 当需要外部比较、规划或类似分析时，通过显式的 blocked payload 字段表达 weave-out request，而不是把它藏进不透明 prose
- 当 resume envelope 的 `transition_id` 与当前待处理 payload 字段所记录的 blocked workflow seam 不匹配时，明确拒绝恢复
- 当会话元数据确实需要参与执行时，把它视为显式 CLI 输入，而不是依赖隐藏的宿主状态

AO 不应当：

- 冒充确定性 skill 执行器
- 把控制态藏进纯叙述文本
- 把所有决策都折叠进一次不透明的 prompt 往返
- 不要绕开文档化的 CLI 控制面去写私有胶水

## Responsibilities

### Caller

- 提供目标和当前已知上下文。
- 执行 AO 请求的外部动作。
- 用结构化结果恢复 AO。
- 在多轮之间保留 `session_id`。
- 保持稳定且可写的会话目录，并通过 `--session-dir` 传入。

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

```guide-template
dotnet ao.dll run \
  --objective-file objective.md \
  --context-file context.json \
  --session-dir outputs/sessions \
  --audit-output outputs/audit
```

```guide-template
dotnet ao.dll resume \
  --session-dir outputs/sessions \
  --session-id 20260609010101_abc12345 \
  --result-file latest-boundary-result.json
```

```guide-checklist
- 目标清晰明确
- 当调用方希望保留可复用的 AO workflow snapshot artifact 时，调用 agent 会先编写 AO workflow JSON 文件，再进入校验交接
- compile 在执行前会先产出 Mermaid Markdown 与 HTML 校验输出
- 调用方已保存 session_id
- 会话目录稳定且可写
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
```

## Anti-Patterns

- 把 AO 当成通用聊天外壳。
- 返回只包含 prose、却没有 workflow、node 或 artifact 状态的数据。
- 用 AO 执行本应属于 SO 的确定性逐步 skill 逻辑。
- 没有明确理由就绕开文档化的 CLI / package 控制路径，改写成私有 wrapper。
- AO 需要 weave-out request 时，不发结构化 boundary，而是用自由叙述去暗示。
- skill 隐藏 package / 通道选择，不先引导用户阅读 package index。
