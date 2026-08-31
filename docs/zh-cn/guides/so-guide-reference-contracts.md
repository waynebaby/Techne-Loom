# SkillOrchestrator Guide：Contracts

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-contracts.md) | [根目录](../README.md)

版本：draft
构建：repository source

## Guide 输出

运行不带额外参数的 `dotnet so.dll --guide`。它会读取与可执行文件放在同一个完整 runtime package 中的英文 `docs/en` 文档树，并输出包含实际 `version`、`docs_root` 与 `guide_path` 绝对路径的 JSON 对象。可执行文件本身不包含 guide 页面；如果 package docs 缺失，命令会报错。

将 `guide_path` 作为当前 package version 的权威入口。只有本 guide 无法消除疑问时，才查看 `docs_root`。命令只支持英文，并拒绝 `--lang`、`--section` 与 `--export`；非致命安装警告写入 stderr。

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## Overview

把 `dotnet so.dll --guide` 当成 governance 锚点，而不是一条绕行路径。对于 `/loom-skill-enhancement` 自身，以及任何 Loom-governanced target skill，只要某个可运行的 SO runtime 已经成功产出一份新的 guide 结果，后续所有受治理执行都必须留在这份 guide 所对应的已发布 SO 包 runtime 表面上。无论这份 guide 是从 skill 入口、直接 CLI，还是某个已恢复的 runtime bundle 拿到的，只要 guide 已经存在，官方治理执行就必须回到它所描述的已发布 SO 包 runtime。不要先读到 guide，然后官方 SO skill 或 target skill 执行又漂回仓库构建产物、手工拼装 runtime，或其他非治理路径。

SO 是一个确定性的 skill 执行与跟踪产品。

它会先编译或加载 workflow，直接执行由 SO 自己拥有的步骤，并且只有在 workflow 完成，或遇到必须由外部参与的 seam 时才返回。

本 guide 使用 repo 级的 [Workflow 术语](../../en/architecture/workflow-terminology.md)。按这套词汇，SO 会在遇到外部拥有的步骤时 weave out，并通过 blocked `<so_property>` payload 里的 `current_step_kind` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet so.dll resume` result envelope weave back。

当前实现状态：

- 当前 `.NET` runtime 已实现 `dotnet so.dll --guide`、`dotnet so.dll --help`、`dotnet so.dll --patch`、`dotnet so.dll compile`、`dotnet so.dll run`、`dotnet so.dll resume`、`dotnet so.dll status`、`dotnet so.dll inspect-workflow`、`dotnet so.dll inspect-workflow-fragment`、`dotnet so.dll inspect-events` 与 `dotnet so.dll ls` 以及 `dotnet so.dll copy-audit-step`
- SO 的公开参数面使用 `compile` 来校验已有 `--workflow-file`
- SO 的每次 compile 都会产出 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis，作为 compile 校验输出
- SO 在 run/resume 表面会返回 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis report 的审计 artifact links；如果 chat agent 提供 Mermaid card display 工具，面向用户的 think-out-loud 应直接传入已有 Mermaid 文件路径，不得为展示再次读取或回传文件内容；否则使用可直接点击的 Markdown 文件链接
- `--patch` 可从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围
- `--workspace-root <directory>` 可选地把已验证的 Mermaid 和 HTML 镜像到 workspace 下新的、被忽略的 `temp/exec-<timestamp>-mermaid-delivery-result/` 目录。`audit_artifacts.mermaid_delivery` 记录 `status`、`generation_status`、`artifact_generated`、`link_resolvable`、workspace 相对路径、SHA-256、`visual_preview_rendered`、`card_display_available` 和失败详情。`must_show_to_user_files` 仍然只是审计清单，不保证链接可打开。

对于文件编辑，`dotnet so.dll --patch` 在 GitHub Copilot 场景下，只要满足适用条件就直接使用；在其他平台或工具场景下，把它视为常规补丁应用失败后的命令行兜底方案。

## 环境准备

通过 skill 或直接 CLI 使用 SO 前：

1. direct CLI 或手动调用者从 package index 选择 released 或 beta。`/loom-skill-enhancement` 和 Loom-governanced target skill 以当前 CI/CD version block 加 checked-in lock 作为精确版本权威；如果不一致，必须先解决再继续。
2. 遵循[平台检测步骤](../reference/runtime/platform-detection.md)，检测 OS/架构/libc，并在任何 target-skill planning、authoring、validation、compile、run、resume 或下游输入收集前执行候选 .NET 9 CLI 启动预检。
3. 访问网络前，若 host 分支可用，先校验本地完整的精确版本 SO IL bundle。有效 framework bundle 包含同一版本的 `Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`。
4. .NET 9 host 与 CLI 预检通过时，从统一 IL bundle 使用显式 `dotnet exec`。bundle 必须放在 skill 目录之外。
5. host 缺失或无法启动 CLI 时，解析一个支持的 RID，获取一个精确的 `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package。启动其 direct `so` 或 `so.exe` executable 前，先校验 hash、nuspec、manifest、ZIP 安全与入口。
6. 使用选定的 launch descriptor 运行 fresh `--guide`，校验 JSON 中的 `version` 并读取返回的 `guide_path`。不能从过期或失败的 guide output 开始 target-skill 工作。
7. `compile`、`run`、`resume`、`status` 和 inspection commands 必须持续使用同一个 launch descriptor、精确 runtime version 与 RID。CLI 启动后的错误不是 fallback 触发条件。
8. 把 checked-in workflow template 复制到外部 runtime copy，并把 compile/audit outputs 与 event sidecar 放在 skill 路径之外。
9. 对 `/loom-skill-enhancement` 和受治理 target skill，只有针对该 runtime copy 的公开 `dotnet so.dll run` 与 `dotnet so.dll resume` 才是正式 workflow 执行表面；`--guide` 与 `compile` 只是准备或校验。
## Workflow 文件语言



Workflow 定义文件是 AO、SO 以及受 Loom 治理 target skill 的规范英文信息载体。workflow 自己拥有的 schema key、node 和 transition 名称/描述、workflow phase、expression、hint、failure guidance、evidence reference 以及 control metadata 必须使用英文。用户/业务 payload 可以保留来源语言，面向用户的输出可以使用请求语言；本地化属于展示层，不能改变 workflow key 或控制语义。
## Contracts
### Workflow 身份与业务范围

受治理 workflow 必须在根部声明 `taskType`、`workflowKind`、`caseId` 和 `runId`。SO 自举使用 `skill_enhancement` 配合 `so_self_bootstrap`；外层 target-skill enhancement 使用 `skill_enhancement` 配合 `target_skill_enhancement`；target business workflow 使用 `requirement_generation`、`model_generation` 等 target-specific business task 配合 `target_skill_business`。compile 会拒绝不兼容组合，也会拒绝 target business workflow 携带已知 SO enhancement output family 或 `assets/agents/loom-skill-enhancement-*` subagent。

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

按 repo 术语，SO 返回 blocked payload 时就是一次 weave out，而 `dotnet so.dll resume` 就是 weave-back 路径。
