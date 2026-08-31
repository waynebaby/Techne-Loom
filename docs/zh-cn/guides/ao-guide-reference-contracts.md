# Loom Agent Execution Orchestrator Guide：Contracts

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [English](../../en/guides/ao-guide-reference-contracts.md) | [根目录](../README.md)

版本：draft
构建：repository source

## Guide 输出

运行不带额外参数的 `dotnet ao.dll --guide`。它会读取与可执行文件放在同一个完整 runtime package 中的英文 `docs/en` 文档树，并输出包含实际 `version`、`docs_root` 与 `guide_path` 绝对路径的 JSON 对象。可执行文件本身不包含 guide 页面；如果 package docs 缺失，命令会报错。

将 `guide_path` 作为当前 package version 的权威入口。只有本 guide 无法消除疑问时，才查看 `docs_root`。命令只支持英文，并拒绝 `--lang`、`--section` 与 `--export`；非致命安装警告写入 stderr。

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

把 `dotnet ao.dll --guide` 当成 governance 锚点，而不是一条绕行路径。一旦某个可运行的 AO runtime 已经成功产出一份新的 guide 结果，后续所有受治理执行都必须留在这份 guide 所对应的已发布 AO 包 runtime 表面上。不要先读到 guide，然后官方 AO skill 执行又漂回仓库构建产物、手工拼装 runtime，或其他非治理路径。

Loom Agent Execution Orchestrator 是面向顶层 agent 的探索式编排产品，专门处理不确定环境中的推进问题。

它不会掩盖不确定性，而是持久化不断演化的 workflow 状态，输出 machine-first 的控制数据，并在主要控制 seam 处 weave out；当协议层需要显式表达时，则输出带显式 boundary 字段的 blocked payload，让调用方有意识地决定下一步。

本 guide 使用 repo 级的 [Workflow 术语](../../en/architecture/workflow-terminology.md)。按照这套词汇，Loom Agent Execution Orchestrator 会在控制 seam 上 weave out，并通过 blocked 控制载荷里的 `boundary_reason`、`weave_out_request` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet ao.dll resume` result envelope weave back。

当前实现状态：

- `.NET` runtime 已实现 `dotnet ao.dll --guide`、`dotnet ao.dll --help`、`dotnet ao.dll --patch`、`dotnet ao.dll compile`、`dotnet ao.dll prompt-plan`、`dotnet ao.dll prompt-replan`、`dotnet ao.dll run`、`dotnet ao.dll resume`
- Loom Agent Execution Orchestrator 在本项目里同时公开 CLI 和本机 stdio-only MCP 表面，通过 `dotnet ao.dll mcp stdio` 启动；不支持 Web 或远程 MCP 传输
- canonical 的 `--workflow-file` plan、replan、run、resume 和 status 路径是 sessionless 的；`--session-dir` 与 `--session-id` 只作为旧兼容输入保留
- 当前 AO 控制载荷实际发出 `blocked` 与 `completed`；CLI/runtime 失败会以 `type: error` 的 `<ao_property>` 形式输出
- AO compile 会针对调用 agent 预先编写的 workflow 文件产出 Mermaid Markdown、HTML 与 workflow JSON 备份，作为校验输出
- AO prompt-plan 与 prompt-replan 会通过 `<ao_property type="prompt">` 输出 AO 自有、由代码生成的 planner / replanner prompt 文本
- 每次 AO run/resume 还会返回 Mermaid Markdown、HTML 与 workflow JSON 备份的审计 artifact links；如果 chat agent 提供 Mermaid card display 工具，面向用户的 think-out-loud 应直接传入已有 Mermaid 文件路径，不得为展示再次读取或回传文件内容；否则使用可直接点击的 Markdown 文件链接
- `--workspace-root <directory>` 可选地把已验证的 Mermaid 和 HTML 镜像到 workspace 下新的、被忽略的 `temp/exec-<timestamp>-mermaid-delivery-result/` 目录。`audit_artifacts.mermaid_delivery` 记录 `status`、`generation_status`、`artifact_generated`、`link_resolvable`、workspace 相对路径、SHA-256、`visual_preview_rendered`、`card_display_available` 和失败详情。`must_show_to_user_files` 仍然只是审计清单，不保证链接可打开。
- `run` 现在还可通过 `--instance-file` 接受一份外部编写的 `WorkflowInstance`，让第一次 runtime blocked step 的审计沿用 compile/prompt-plan 已验证的同一份图
- `--patch` 可从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围

对于文件编辑，`dotnet ao.dll --patch` 在 GitHub Copilot 场景下，只要满足适用条件就直接使用；在其他平台或工具场景下，把它视为常规补丁应用失败后的命令行兜底方案。

## 环境准备

通过 skill 或直接 CLI 使用 Loom Agent Execution Orchestrator 前：

1. direct CLI 或手动获取先从 package index 选择 released 或 beta。对于 `/loom-plan-execution`，owning skill 的 CI/CD version block 是即时精确版本权威；如有 checked-in lock，继续受治理执行前必须与它一致。
2. 遵循[平台检测步骤](../reference/runtime/platform-detection.md)：确认 `dotnet`，接受 `Microsoft.NETCore.App 9.x`，并用精确 launch binding 执行无副作用的 CLI 启动预检。
3. .NET 9 host 预检通过时，以相同精确版本恢复 `Techne.Loom.AgentOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`，并用显式 `dotnet exec` 启动 IL bundle。
4. 如果 `dotnet` 或 .NET 9 缺失、host loading 失败、host 依赖缺失或 CLI 无法启动，就把平台映射到一个支持的 RID，并获取一个精确的 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package。直接运行缓存的 `ao` 或 `ao.exe`；不要使用 repository build 或其他 RID。
5. 通过选定的 launch descriptor 运行 fresh `--guide`，解析 JSON 中的 `version` 并读取返回的 `guide_path`。失败 stderr 不能当作 guide evidence。
6. `compile`、`prompt-plan`、`prompt-replan`、`run` 和 `resume` 必须持续使用同一个 launch descriptor、精确 runtime version 与 RID；CLI 启动后的错误不会触发 fallback。
7. workflow copy、session 目录、compile artifacts 和 audit outputs 必须放在 skill 路径之外。只有显式 `run` 与 `resume` 才是 AO skill 的正式执行表面。
## Workflow 文件语言



Workflow 定义文件是 AO、SO 以及受 Loom 治理 target skill 的规范英文信息载体。workflow 自己拥有的 schema key、node 和 transition 名称/描述、workflow phase、expression、hint、failure guidance、evidence reference 以及 control metadata 必须使用英文。用户/业务 payload 可以保留来源语言，面向用户的输出可以使用请求语言；本地化属于展示层，不能改变 workflow key 或控制语义。
## Contracts

```guide-contract
inputs:
  objective: 用户目标或任务请求
  context: 当前已知事实、产物和既有决策
  session_dir: 兼容旧 CLI 的可选字段，对应 `--session-dir`；必须位于 skill 文件夹之外
  workflow_file: canonical sessionless WorkflowInstance 文件，用于 `--workflow-file` plan/replan/run/resume/status 路径；必须位于 skill 文件夹之外
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
    summary_file: 用于直接复盘 boundary/frontier 的每 step 结构化 summary 文件
    mermaid_delivery: Mermaid 与 HTML 是否生成、链接是否可解析、preview、card 能力、哈希和失败状态的结构化交付 evidence
    workspace_relative_mermaid_file: workspace 镜像成功时的已验证 workspace 相对 Mermaid 链接
    workspace_relative_html_file: workspace 镜像成功时的已验证 workspace 相对 HTML preview 链接
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
