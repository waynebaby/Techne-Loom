# Loom Agent Plan-Execution Orchestrator Guide：Contracts

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [English](../../en/guides/ao-guide-reference-contracts.md) | [根目录](../README.md)

<!-- guide-version:start -->
版本：0.3.321
构建：已发布的 0.3.321 包
<!-- guide-version:end -->






## Guide 输出

Windows 直接运行 `ao.exe --guide`，Unix 运行 `ao --guide`。它会读取 runtime package 中 apphost 旁边的英文 `docs/en` 文档树，并输出包含实际 `version`、`docs_root` 和 `guide_path` 的 JSON。缺少 package docs 时命令会报错。

将 `guide_path` 作为当前 package version 的权威入口。只有本 guide 无法消除疑问时，才查看 `docs_root`。命令只支持英文，并拒绝 `--lang`、`--section` 与 `--export`；非致命安装警告写入 stderr。

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

把 direct AO apphost 的 `--guide` 结果视为治理锚点，而不是绕行路径。命令成功后，受治理执行继续使用同一个发布 package runtime；不要切换到 repository build 或手工拼装的 runtime。

Loom Agent Plan-Execution Orchestrator 是面向顶层 agent 的探索式编排产品，专门处理不确定环境中的推进问题。

它不会掩盖不确定性，而是持久化不断演化的 workflow 状态，输出 machine-first 的控制数据，并在主要控制 seam 处 weave out；当协议层需要显式表达时，则输出带显式 boundary 字段的 blocked payload，让调用方有意识地决定下一步。

本 guide 使用 repo 级的 [Workflow 术语](../../en/architecture/workflow-terminology.md)。按照这套词汇，Loom Agent Plan-Execution Orchestrator 会在控制 seam 上 weave out；调用方通过 direct `ao.exe resume` 或 `ao resume` 及携带 `transition_id`、`correlation_key`、`payload` 的 result envelope weave back。

当前实现状态：

- 自包含 AO apphost 支持 `--guide`、`--help`、`--patch`、`compile`、`prompt-plan`、`prompt-replan`、`run` 和 `resume`。
- Loom Agent Plan-Execution Orchestrator 同时公开 CLI 和本机 stdio-only MCP，通过 `ao.exe mcp stdio` 或 `ao mcp stdio` 启动；不支持 Web 或远程 MCP 传输。
- canonical 的 `--workflow-file` plan、replan、run、resume 和 status 路径是 sessionless 的；`--session-dir` 与 `--session-id` 只作为旧兼容输入保留
- 当前 AO 控制载荷实际发出 `blocked` 与 `completed`；CLI/runtime 失败会以 `type: error` 的 `<ao_property>` 形式输出
- AO compile 会针对调用 agent 预先编写的 workflow 文件产出 Mermaid Markdown、HTML 与 workflow JSON 备份，作为校验输出
- AO prompt-plan 与 prompt-replan 会通过 `<ao_property type="prompt">` 输出 AO 自有、由代码生成的 planner / replanner prompt 文本
- 每次 AO run/resume 都会返回 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis report 的审计 artifact links。每次 AO apphost 调用后，遵循[Mermaid artifact delivery](../../../.agents/skills/loom-plan-execution/reference/mermaid-artifact-delivery.md)：校验返回路径，按顺序输出 Mermaid、HTML、Analysis 和 Dataflow 链接及对应路径围栏，再使用当前交互语言输出 `## 执行信心: x%` 和 `## 预计整体进度: x%` 标题，各附一句简短原因或进度说明。
- `--workspace-root <directory>` 可选地把已验证的 Mermaid 和 HTML 镜像到 workspace 下新的、被忽略的 `temp/exec-<timestamp>-mermaid-delivery-result/` 目录。`audit_artifacts.mermaid_delivery` 记录 `status`、`generation_status`、`artifact_generated`、`link_resolvable`、workspace 相对路径、SHA-256、`visual_preview_rendered`、`card_display_available` 和失败详情。`must_show_to_user_files` 仍然只是审计清单，不保证链接可打开。
- `run` 现在还可通过 `--instance-file` 接受一份外部编写的 `WorkflowInstance`，让第一次 runtime blocked step 的审计沿用 compile/prompt-plan 已验证的同一份图
- `--patch` 可从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围

对于文件编辑，优先直接使用 AO apphost 的 `--patch` 命令；否则使用仓库批准的文件编辑方式。

## 环境准备

通过 skill 或直接 CLI 使用 Loom Agent Plan-Execution Orchestrator 前：

1. 从 package index 或 owning skill 的版本区块和 lock 读取精确版本；受治理执行前先解决不一致。
2. 根据操作系统、CPU 架构和 Linux libc 检测一个受支持的 RID。
3. 只复用标准 NuGet global-packages cache 中校验有效的精确包；否则校验精确 NuGet registration SHA-512 或同版本 GitHub `.sha512` sidecar。
4. 解压前校验 package ID、版本、RID、nuspec、`runtime.json`、压缩包安全、apphost 和英文 guide 文件，并解压到每次运行专用的外部目录。
5. Windows 直接运行 `ao.exe --guide`，Unix 运行 `ao --guide`，作为第一个 runtime 操作。核对返回版本和可读 guide path。
6. 后续 `compile`、`prompt-plan`、`prompt-replan`、`run` 和 `resume` 使用同一个 apphost。CLI 启动后的错误不会触发其他 runtime 路径。
7. workflow copy、session 目录、compile artifact 和 audit output 放在 skill 目录之外。只有显式 `run` 和 `resume` 是 AO skill 的正式执行面。
## B+ Contract Context

AO runtime 可以通过共享的 bounded provider 消费 target contract。skill being enhanced 把自己的业务 contract 放在 `assets/so-workflow/contract.json`；workflow root 的 `contractBinding` 指向它，transition 再通过 `contractRefs` 声明需要的 JSON Pointer fragment。

`compile` 只校验 workflow binding 和引用语法。`run` 与 `resume` 在引用 transition 执行前读取当前 contract，并使用同一份字节快照完成 parse 与 fragment projection，再把 bounded fragment 注入 transition 的 `contract_context`。成功读取的 metadata 包含路径、可选 SHA-256、缓存状态、返回字节数和 refs。fragment 超过任一配置限制时会 fail closed，并在替换旧 contract context 前失败。允许手动修改 contract；hash 变化会刷新 path-plus-hash cache，但本身不会阻断执行。

SO 使用相同的 B+ provider 和语义。runtime 不解释领域含义，业务步骤负责解释。参见 [Contract Context 参考](../../architecture/contract-context-reference.zh-CN.md) 与 [Contract 一致性规则](../../architecture/contract-consistency-rules.zh-CN.md)。

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

按 repo 术语，AO 返回 blocked 控制载荷时就是一次 weave out；direct `ao.exe resume` 或 `ao resume` 是 weave-back 路径。

当前 runtime 持久化故意同时保留两种形状：

- `workflow_file` 是 AO 的 snapshot 控制文件，runtime resume 会用它来校验 `transition_id`。
- `workflow_instance_file` 是当前图形态的 `WorkflowInstance` 表面，用于 compile 连续性、runtime audit 连续性，以及 caller-managed replan 编辑。
- 在 `session_dir` 下，AO 还会维护 `session_<id>_runtime.workflow.json` 作为 runtime `WorkflowInstance` sidecar，并维护 `session_<id>_runtime.workflow.pointer.json` 作为指向外部 caller-managed `workflow_instance_file` 的可选指针文件。
