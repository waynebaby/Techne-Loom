# SkillOrchestrator Guide：Contracts

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-contracts.md) | [根目录](../README.md)

<!-- guide-version:start -->
版本：0.3.320-beta
构建：已发布的 0.3.320-beta 包
<!-- guide-version:end -->





## Guide 输出

Windows 直接运行 `so.exe --guide`，Unix 运行 `so --guide`。它会读取 apphost 旁边的英文文档，并返回包含实际 `version`、`docs_root` 和 `guide_path` 的 JSON。Apphost 不嵌入 guide 页面；package 缺少文档时命令会报错。

将 `guide_path` 作为当前 package version 的权威入口。只有本 guide 无法消除疑问时，才查看 `docs_root`。命令只支持英文，并拒绝 `--lang`、`--section` 与 `--export`；非致命安装警告写入 stderr。

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

把 direct SO apphost 的 `--guide` 结果视为版本权威，而不是绕行路径。精确 package 中的 guide 可读后，治理执行继续使用同一个发布 apphost；不要切换到 repository build 或手工拼装的 runtime。

SO 是一个确定性的 skill 执行与跟踪产品。

它会先编译或加载 workflow，直接执行由 SO 自己拥有的步骤，并且只有在 workflow 完成，或遇到必须由外部参与的 seam 时才返回。

本 guide 使用 repo 级的 [Workflow 术语](../../en/architecture/workflow-terminology.md)。按照这套词汇，调用方通过 direct `so.exe resume` 或 `so resume` 及携带 `transition_id`、`correlation_key`、`payload` 的 result envelope weave back。

当前实现状态：

- 自包含 SO apphost 支持 `--guide`、`--help`、`--patch`、`--schema-demo-output`、`compile`、`run`、`resume`、`status`、`inspect-workflow`、`inspect-workflow-fragment`、`inspect-events`、`ls` 和 `copy-audit-step`。
- SO 的公开参数面使用 `compile` 来校验已有 `--workflow-file`
- SO 的每次 compile 都会产出 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis，作为 compile 校验输出
- 每次 SO apphost 调用后，遵循[Mermaid artifact delivery](../../../.agents/skills/loom-skill-enhancement/reference/mermaid-artifact-delivery.md)：校验返回路径可读，再按顺序输出 Mermaid、HTML、Analysis 和 Dataflow 链接及对应路径围栏。之后使用当前交互语言输出 `## 执行信心: x%` 和 `## 预计整体进度: x%` 标题，各附一句简短原因或进度说明。只使用已验证路径，并使用 plain language。
- `--patch` 可从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围
- `--workspace-root <directory>` 可选地把已验证的 Mermaid 和 HTML 镜像到 workspace 下新的、被忽略的 `temp/exec-<timestamp>-mermaid-delivery-result/` 目录。`audit_artifacts.mermaid_delivery` 记录 `status`、`generation_status`、`artifact_generated`、`link_resolvable`、workspace 相对路径、SHA-256、`visual_preview_rendered`、`card_display_available` 和失败详情。`must_show_to_user_files` 仍然只是审计清单，不保证链接可打开。

优先直接使用 SO apphost 的 `--patch` 命令进行按行范围替换；否则使用仓库批准的文件编辑方式。

## 环境准备

通过 skill 或直接 CLI 使用 SO 前：

1. 从 owning skill 的 lock 和版本区块读取精确 package version。不要使用 `latest`。
2. 根据操作系统、CPU 架构和 Linux libc 检测一个受支持的 RID。
3. 只复用标准 NuGet global-packages cache 中通过校验的精确包；否则校验精确 NuGet registration hash 或同版本 GitHub `.sha512` sidecar。
4. 解压前校验 package ID、版本、RID、nuspec、manifest、压缩包安全、apphost 和英文 guide 文件。通过校验后解压到每次运行专用的外部目录。
5. Windows 直接运行 `so.exe --guide`，Unix 运行 `so --guide`，作为第一个 runtime 操作。校验返回版本及可读取且位于文档根目录内的路径。
6. 后续 schema/demo、compile、run 和 resume 使用同一个 apphost。Workflow copy、event sidecar 和 audit output 都放在 skill 目录之外。
7. 对 `/loom-skill-enhancement` 和受治理 skill，正式 workflow 执行仅使用 Windows 的 `so.exe run`/`so.exe resume` 或 Unix 的 `so run`/`so resume`。
## B+ Contract Context

SO runtime 可以通过共享的 bounded provider 消费 target contract。skill being enhanced 把自己的业务 contract 放在 `assets/so-workflow/contract.json`；workflow root 的 `contractBinding` 指向它，transition 再通过 `contractRefs` 声明需要的 JSON Pointer fragment。

`compile` 只校验 workflow binding 和引用语法。`run` 与 `resume` 在引用 transition 执行前读取当前 contract，并使用同一份字节快照完成 parse 与 fragment projection，再把 bounded fragment 注入 transition 的 `contract_context`。成功读取的 metadata 包含路径、可选 SHA-256、缓存状态、返回字节数和 refs。fragment 超过任一配置限制时会 fail closed，并在替换旧 contract context 前失败。允许手动修改 contract；hash 变化会刷新 path-plus-hash cache，但本身不会阻断执行。

SO 使用相同的 B+ provider 和语义。runtime 不解释领域含义，业务步骤负责解释。参见 [Contract Context 参考](../../architecture/contract-context-reference.zh-CN.md) 与 [Contract 一致性规则](../../architecture/contract-consistency-rules.zh-CN.md)。

## Contracts
### Workflow 身份与业务范围

受治理 workflow 必须在根部声明 `taskType`、`workflowKind`、`caseId` 和 `runId`。SO 自举使用 `skill_enhancement` 配合 `so_self_bootstrap`；外层 enhancement of the skill being enhanced 使用 `skill_enhancement` 配合 `target_skill_enhancement`；business workflow for the skill being enhanced 使用 `requirement_generation`、`model_generation` 等 target-specific business task 配合 `target_skill_business`。compile 会拒绝不兼容组合，也会拒绝 business workflow for the skill being enhanced 携带已知 skill enhancement output family 或 `assets/agents/loom-skill-enhancement-*` subagent。

`caseId` 标识业务案例，`runId` 标识一条外部 compile/run/resume 执行链，并且必须在这条链的 audit 与 completion evidence 中保持不变。checked-in template 可以使用 `template:` run 标记；物化或第一次对新的 `ReadyToStart` 副本执行 `run` 时会生成 `run-<guid>`，`resume` 会保留它。

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
    can_resume: 当 workflow instance 是带 active wait group 的 WaitingExternal，或是具备失败 history、失败前 state 且最近失败 transition 属于该 state 的 Failed 时为 true，否则为 false
    fresh_instance_required: Succeeded 或不可恢复 Failed 为 true；可恢复 Failed、WaitingExternal 与运行中状态为 false
    audit_artifacts:
      output_root: 审计输出根目录
      step_directory: 按 step 划分的审计目录
      mermaid_file: 当前 workflow 的 Mermaid Markdown 路径
      html_file: 当前 workflow 的 HTML 路径
      workflow_backup_file: 当前 workflow 的 JSON 备份路径
      analysis_file: 如可用，当前 workflow analysis JSON 路径
      dataflow_file: 如可用，当前 workflow dataflow JSON 路径
      reuse_manifest_file: 该 step 被复制时的 audit-reuse.json 路径
      artifact_origin: fresh-runtime | verified-copy
      official_execution_evidence: 当 artifact_origin 为 verified-copy 时必须为 false
      mermaid_delivery: Mermaid 与 HTML 是否生成、链接是否可解析、preview、card 能力、哈希和失败状态的结构化交付 evidence
      workspace_relative_mermaid_file: workspace 镜像成功时的已验证 workspace 相对 Mermaid 链接
      workspace_relative_html_file: workspace 镜像成功时的已验证 workspace 相对 HTML preview 链接
  status:
    status: active | blocked | completed | failed
    instance_id: 持久化 workflow instance 标识
    workflow_file: 持久化后的当前 workflow 路径
    current_node_id: 当前 workflow 焦点节点
    next_node_id: 可选，已知时的下一节点
    event_log_file: 追加式执行事件路径
    can_resume: 当 workflow instance 是带 active wait group 的 WaitingExternal，或是具备失败 history、失败前 state 且最近失败 transition 属于该 state 的 Failed 时为 true，否则为 false
    fresh_instance_required: Succeeded 或不可恢复 Failed 为 true；可恢复 Failed、WaitingExternal 与运行中状态为 false
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
    can_resume: 可恢复 boundary 时为 true；没有 active wait group 或可恢复失败 transition 时为 false
    fresh_instance_required: 只有持久化实例无法安全 resume 时为 true
  result:
    status: completed
    instance_id: 持久化 workflow instance 标识
    workflow_file: 持久化后的当前 workflow 路径
    current_node_id: 终态节点或当前已完成节点
    context: 在 completed 结果载荷中可选暴露当前 context 快照
    event_log_file: 追加式执行事件路径
    can_resume: completed result 始终为 false
    fresh_instance_required: completed result 始终为 true，因为 Succeeded 实例是 terminal
    audit_artifacts:
      output_root: 审计输出根目录
      step_directory: 按 step 划分的审计目录
      mermaid_file: 该时刻的 Mermaid Markdown 路径
      html_file: 该时刻的 HTML 路径
      workflow_backup_file: 该时刻的 workflow JSON 备份
      analysis_file: 如可用，该时刻的 workflow analysis JSON 路径
      dataflow_file: 如可用，该时刻的 workflow dataflow JSON 路径
      reuse_manifest_file: 该 step 被复制时的 audit-reuse.json 路径
      artifact_origin: fresh-runtime | verified-copy
      official_execution_evidence: 当 artifact_origin 为 verified-copy 时必须为 false
      mermaid_delivery: Mermaid 与 HTML 是否生成、链接是否可解析、preview、card 能力、哈希和失败状态的结构化交付 evidence
      workspace_relative_mermaid_file: workspace 镜像成功时的已验证 workspace 相对 Mermaid 链接
      workspace_relative_html_file: workspace 镜像成功时的已验证 workspace 相对 HTML preview 链接
  error:
    status: failed
    instance_id: 如可用则给出持久化 workflow instance 标识
    workflow_file: 如有可用则给出 workflow 路径
    message: 稳定、machine-readable 的错误摘要
    event_log_file: 如有可用则给出执行事件路径
    can_resume: 只有 Failed 实例具备失败 history、失败前 state，且最近失败 transition 属于该 state 时为 true
    fresh_instance_required: Succeeded 或不可恢复 Failed 为 true；可恢复 Failed 为 false
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

当 `transition_id` 标识属于失败前 state 的最近一次失败 transition 时，Failed 实例可以在同一个持久化 workflow 上 resume。runtime 会把实例恢复为 `Running`，从该 state 重试，并保留失败 history 与 event evidence。缺少失败 history、失败前 state 或 transition 归属 evidence 时，实例不可恢复，必须 fail closed。Succeeded 实例仍是 terminal，必须创建新的 external workflow copy。

CLI 会通过持久化 workflow 文件旁的跨进程 file lock 串行化同一个 workflow 的操作。并发的 `run`、`resume`、`status`、`compile` 与 inspection commands 会等待锁，然后重新读取当前 workflow 文件再继续。

按 repo 术语，blocked SO 返回是一次 weave out；direct `so.exe resume` 或 `so resume` 是 weave-back 路径。
