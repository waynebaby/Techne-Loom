# SkillOrchestrator Guide

[English](../../../en/reference/products/so-guide.md) | [根目录](../../README.md)

Version: draft

Build: repository source

## Guide 输出

运行不带额外参数的 `dotnet so.dll --guide`。它会把内嵌的英文 `docs/en` 文档包安装到 `<binary>/docs/<package-version>/`，并输出包含实际 `version`、`docs_root` 与 `guide_path` 绝对路径的 JSON 对象。如果二进制目录不可写，则 runtime 使用 `%TEMP%/docs/<package-version>/` 并返回实际路径。

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

本 guide 使用 repo 级的 [Workflow 术语](../../../zh-cn/architecture/workflow-terminology.md)。按这套词汇，SO 会在遇到外部拥有的步骤时 weave out，并通过 blocked `<so_property>` payload 里的 `current_step_kind` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet so.dll resume` result envelope weave back。

当前实现状态：

- 当前 `.NET` runtime 已实现 `dotnet so.dll --guide`、`dotnet so.dll --help`、`dotnet so.dll --patch`、`dotnet so.dll compile`、`dotnet so.dll run`、`dotnet so.dll resume`、`dotnet so.dll status`、`dotnet so.dll inspect-workflow`、`dotnet so.dll inspect-events` 与 `dotnet so.dll ls` 以及 `dotnet so.dll copy-audit-step`
- SO 的公开参数面使用 `compile` 来校验已有 `--workflow-file`
- SO 的每次 compile 都会产出 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis，作为 compile 校验输出
- SO 在 run/resume 表面会返回 Mermaid Markdown、HTML、workflow JSON 备份与 workflow analysis report 的审计 artifact links
- `--patch` 可从外部 patch 内容文件替换现有文本文件中的一段闭区间行范围
- Mermaid render 会根据 workflow step kind 语义和 owned-input 元数据同时使用浅色节点背景与稳定 emoji 标签：`🔎` AI/model/subagent 工作用绿色，`⚙️` 代码/工具工作用蓝色，`💬` user-owned 的可选分支决策用黄色，`🚧` 必须用户输入用红色，`❓` 一般条件分支用琥珀黄/浅黄，`📜` gate/governance 状态用白色或极浅灰色

对于文件编辑，`dotnet so.dll --patch` 在 GitHub Copilot 场景下，只要满足适用条件就直接使用；在其他平台或工具场景下，把它视为常规补丁应用失败后的命令行兜底方案。

## 环境准备

通过 skill 或直接 CLI 使用 SO 前：

1. direct CLI 或手动调用者从 package index 选择 released 或 beta。`/loom-skill-enhancement` 和 Loom-governanced target skill 以当前 CI/CD version block 加 checked-in lock 作为精确版本权威；如果不一致，必须先解决再继续。
2. 遵循[平台检测步骤](../runtime/platform-detection.md)，检测 OS/架构/libc，并在任何 target-skill planning、authoring、validation、compile、run、resume 或下游输入收集前执行候选 .NET 9 CLI 启动预检。
3. 访问网络前，若 host 分支可用，先校验本地完整的精确版本 SO IL bundle。有效 framework bundle 包含同一版本的 `Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`。
4. .NET 9 host 与 CLI 预检通过时，从统一 IL bundle 使用显式 `dotnet exec`。bundle 必须放在 skill 目录之外。
5. host 缺失或无法启动 CLI 时，解析一个支持的 RID，获取一个精确的 `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package。启动其 direct `so` 或 `so.exe` executable 前，先校验 hash、nuspec、manifest、ZIP 安全与入口。
6. 使用选定的 launch descriptor 运行 fresh `--guide`，校验 JSON 中的 `version` 并读取返回的 `guide_path`。不能从过期或失败的 guide output 开始 target-skill 工作。
7. `compile`、`run`、`resume`、`status` 和 inspection commands 必须持续使用同一个 launch descriptor、精确 runtime version 与 RID。CLI 启动后的错误不是 fallback 触发条件。
8. 把 checked-in workflow template 复制到外部 runtime copy，并把 compile/audit outputs 与 event sidecar 放在 skill 路径之外。
9. 对 `/loom-skill-enhancement` 和受治理 target skill，只有针对该 runtime copy 的公开 `dotnet so.dll run` 与 `dotnet so.dll resume` 才是正式 workflow 执行表面；`--guide` 与 `compile` 只是准备或校验。
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
- 如需下载本地运行时，遵循[平台检测步骤](../runtime/platform-detection.md)：host 预检成功后，校验并使用精确版本的 SO IL bundle（`Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`）；如果 host 缺失或无法启动 CLI，则为检测出的 RID 校验并使用一个精确版本的 `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package。
- 每次启动新的正式 `run` 前，都要先把 checked-in source template 复制到运行时 temp 或 execution-output 目录；当 workflow 之后进入 blocked，`resume` 必须继续作用于同一份已持久化的 runtime copy。
- 当 SO weave out 时执行外部动作。
- 用结构化 weave-back envelope 恢复 SO。
- 把 `<so_property>` 视为权威 SO 控制载荷。
- 把 `<wrapped_exec>` 视为面向 shell 的流式 wrapper 输出表面。
- 在 resume sidecar JSON 中使用 `transition_id`、`correlation_key` 和 `payload`。
- 让 runtime workflow copy、event sidecar 和 audit 输出都位于 skill-owned 目录之外。
- 每次 progress update 都要在 think-out-loud 输出中带上当前 workflow 的 Mermaid Markdown 与 HTML 路径。
- 把 `workflow.analysis.json` 视为 machine-readable 摘要，用来审阅输入、输出族、分支、循环、用户 seam、运行时 seam、gate 与图灵完备控制风险。
- 只有在明确确认 audit 输入未变化时，才能使用 `dotnet so.dll copy-audit-step`。它的 `audit-reuse.json` 会把复制产物标记为 `artifact_origin: verified-copy` 与 `official_execution_evidence: false`；复制产物不能替代 `run`、`resume`、事件日志、gate 或 guide evidence。

### Author

- 显式编码 step kind。
- 当下一步需要上下文提炼时，定义 memory extraction 提示。
- 保证本地确定性步骤没有隐藏侧通道。

### Outer-agent

- 字面消费 `skill_hint`。
- 在阻塞 seam 与对应的 resume handoff 之间保留 `memory_for_next_step`。
- 不要超出当前阻塞步骤契约进行即兴发挥。

## 面向增强 Skill 的 SO 强制治理规则

当某个 skill 正在由 `/loom-skill-enhancement` 增强，或已经运行在 Loom Skill Orchestrator governance 下时，必须应用本节规则。该 skill 不需要先认定自己是 target skill 才能应用这些规则。本节不重新定义 AO 行为，也不适用于无关 workflow。

### 确定性 Transition 契约

为该 skill author 或 review 的每个 workflow transition 都必须声明：

- `guardExpression`：在 transition 执行前求值的可执行布尔条件，只能证明当前输入具备执行资格，不能假设执行输出已经存在
- `succeedExpression`：在 transition 执行后求值的可执行布尔条件，必须根据声明的输出证据证明结果被接受，不能只是重复 guard
- user-owned 与 runtime-owned 输入的明确归属
- 明确的输出证据路径或 output-family 声明
- 当 transition 能离开当前 state 时，明确的 blocked route 或 terminal route

只使用描述性 prose、隐含谓词、无边界自然语言条件，或让 guard 与 success 采用同一语义检验的 transition，都必须被拒绝。缺少谓词、归属或证据形状时，authoring check 必须失败。

### Boundary Check And Approval Gate（强制）

该 skill 被迫进入 Loom Skill Orchestrator-governanced route。任何下一步都不得推进，直到它在精确的外部 runtime workflow copy 上通过 boundary check；跨越所有者的步骤还必须先获得针对该具体下一步的显式批准或结构化续行：

- **boundary check** 在推进前校验每个 transition：`guardExpression` 依据已声明证据证明可执行资格（绝不允许声称执行输出已经存在）；离开当前 state 时还必须满足 gate predicates（`passExpression` / `succeedExpression`）对 runtime evidence、route coverage、seam ownership、strongest-earned blocked outputs 或 terminal business-output gates 的判定。
- 内部确定性 transition——guard/succeed predicates 可机器求值的 `stateUpdate`、`conditionBranch`、`memoryRead` 以及 native-code/tool step——由 boundary check 本身校验，不需要单独的 user approval。跨越所有者的 seam 则必须有显式续行：(a) 针对已声明 user-owned fields 或 decisions 的 `AskUser` seam 上的显式用户批准/指示；或 (b) 结构化非人类 continuation payload——其字面 `skill_hint` 加 blocked step kind 明确指向可机器延续的 seam（如 `WaitResume`）。
- 任何下一步都不得基于推断意图、纯 prose、过期 guide result、compile success、未经批准的 draft copy、local orchestration，或直接 workflow JSON edits 推进；任何 transition 也都不得在其谓词求值之前就声称执行输出已经存在。
- 如果 boundary check fail closed——缺少谓词、ownership violations、仅治理型证据、未经批准的 route，或无显式续行的 seam——立即停止并保留失败状态。不得伪造成功证明、中途切换 workflow copy、把 blocked payload 当作治理完成，或用本地执行顶替。
- compile-clean 只是 boundary-check precondition，绝不是跳过后续 gate 的批准。同一外部 runtime copy 上的每个 transition 都必须通过该 gate，直到最终 `Done`。

### 表达式契约

当前 .NET 表达式语言是 **C#**，由 Roslyn 在进程内编译。workflow 根部声明 `runtimeBinding` 与 `expressionBinding`；不得添加 per-node language override。binding 包含 `language`、`languageVersion`、`contractId`、`contractVersion`、`requiredExpressionCapabilities` 与 `compileFeedbackContract: "detailedCompileFeedbackV1"`。

`guardExpression`、`succeedExpression` 与 `passExpression` 使用带 `kind`、`source`、`entryPoint`、`resultType` 的结构化 `ExpressionDefinition`。只有显式 C# binding 才允许字符串 shorthand，序列化时始终写为对象。当前表达式是同步 predicate；异步构造与 legacy 非 C# 表达式语法非法并 fail closed。应使用 `context.Get<T>("path")` 等只读 context contract API，不得使用隐式裸字段。

每次 compile 都必须按 `detailedCompileFeedbackV1` 输出 `ExpressionCompileFeedback`，包含位置、source span、稳定 code/category、severity、可行动 message、suggested fix、referenced symbols、compiler identity、解析后的 form、result type、capabilities 与 warnings。仅透传 compiler 原文不合格。Rust+CEL 是未来第四条 runtime 路线，复用同一 schema 与 feedback contract，但不是执行 Rust 代码；Node.js 与 Python 在实现同一合同前仍是 adapter 路线。
### 确定性 Gate 契约

为该 skill author 或 review 的每个 gate 都必须声明：

- `passExpression`：基于 runtime evidence context 求值的 machine-checkable 布尔通过条件
- 必需的证据引用和 output families
- 能够满足该 gate 的 route 覆盖
- 无法继续时已获得的最强 blocked gate
- 进入 `Done` 前所需的 terminal route 和 business-output gate

只有治理型 artifact 不能满足 business-output gate。如果缺少必需证据、output family、blocked route 或 terminal route 声明，route 必须 fail closed。

### 明确的 Unattended 模式契约

只有在 SO 路径处于 blocked 状态，且当前 session 明确声明 unattended mode 时，才允许使用 unattended workaround。不得从 earlier turn 推断 unattended 状态；每个关键决策边界都必须重新确认当前是 attended 还是 unattended。

在执行 autonomous workaround 之前，必须记录结构化 decision-evidence：预期收益明确高于风险、已考虑的替代方案、选定的是最小可逆改动，以及可以一步执行的 rollback plan。workaround 后必须立即回到公开的 `dotnet so.dll compile`、`dotnet so.dll run` 或 `dotnet so.dll resume` 路径。post-run acknowledgement 默认是 non-blocking，除非用户明确要求 blocking behavior。

### Weave-Out 引用契约

每一次 weave-out 或 external handoff 都必须返回一个最小引用清单，引用导致下一步动作或支撑该动作的文档。不得倾倒完整 context pack，也不得把读过的每个文件全部列出。只保留入口文档、继续当前边界所必需的 workflow 或 contract 文件，以及控制当前决策的具体 guide 证据。

每个引用必须包含：

- `path`：workspace-relative 或 runtime-output-relative 路径，不得使用机器绝对路径
- `start_line` 与 `end_line`：从本次 weave-out 实际使用的精确文件内容中核验出的 1-based inclusive 行号
- `role`：说明为什么下一步动作需要这段引用

如果涉及 guide，必须引用最新一次 `dotnet so.dll --guide` 成功 JSON 结果返回的实际 `guide_path`，并引用其输出行号。只引用 guide source 地址是不充分的。该命令不会导出 guide 文件；如果无法读取 `guide_path`，必须标明失败的 runtime evidence。没有经过核验的 `evidence_references` 的 weave-out 不完整，不得作为成功证据 weave back。

每次 weave-out 输出都必须保持紧凑：只返回下一步动作或决策、最小化的 `evidence_references` 清单，以及 resume payload 契约。不得重复完整 context-pack 清单。

这些规则是该 skill 在 SO 下进行 authoring、review、compile readiness 与 governed execution handoff 时的强制 guide 要求。

### Schema 与 Demo 导出

请使用同一份 runtime，把当前 workflow schema 合同和可以编译的 demo 成对写出：

```powershell
dotnet so.dll --schema-demo-output outputs\schema-demo
# Windows self-contained runtime 使用：
.\so.exe --schema-demo-output outputs\schema-demo
```

这个命令会同时写出 `workflow.schema.json` 和 `workflow.demo.json`。请使用同一份 runtime 通过 `compile --workflow-file <path>` 校验生成的 demo。除非明确要求作为交付物，否则生成文件必须放在 skill 目录之外。

```guide-template
dotnet so.dll compile \
  --workflow-file so-template.json \
  --audit-output outputs/audit
```

`so-template.json` 仍然是 checked-in source template。`outputs/audit` 也必须放在 skill 文件夹之外。

对 `/loom-skill-enhancement` 和任何 Loom-governanced target skill，不要把直接修改 checked-in workflow JSON 当作常规维护路径。只有当当前 `dotnet so.dll` 路径已经完全 blocked，且用户明确同意一个狭义变通方案时，才允许做最小的直接 JSON 修改去打通下一次 `dotnet so.dll compile`、`dotnet so.dll run` 或 `dotnet so.dll resume`；随后必须立刻回到 Loom 治理路径。

对正在运行中的外部 workflow `.json` 副本做手动修改，也只能视为 blocked 状态下的最后手段应急变通，不能当作常规 workflow 操作路径。

对于 Loom-governanced target-skill template，还必须设置根 `templateKind: so-governed-target-skill` 和根 `validation` 契约。`compile` 会在 workflow 获得 execution authority 之前，同时校验结构正确性、route-aware business-output gates、seam ownership、blocked strongest-earned outputs 与 done reachability。

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
- 对于 Loom-governanced target-skill template，compile 还要求根 validation 契约、route-aware business-output gates、strongest-earned blocked-output 声明与 ownership-safe seams 全部通过
- 对于 target-skill 修改，runtime-ready 证据和 fresh-guide 证据应在任何后续 planning、authoring、validation、compile、run 或 resume 步骤之前显式建模出来
- 如果 re-enhancement review 要检查 checked-in 资产，这些 inspection 节点必须先读取真实文件快照，再交给 gap-review subagent 消费
- 基于文件的 checked-in asset inspection 必须声明显式的 target-skill asset root，并且必须拒绝绝对路径或逃逸该 root 的路径遍历
- 如果某个 governed workflow 被作为可运行 execution authority 对外呈现，那么它的 materialized runtime copy 必须能在当前公开 `dotnet so.dll run` 路径上实际执行，而不能只是 compile-clean
- 如果某个 target skill 已经真正切换到 Loom Skill Orchestrator governance 类型，稳定话术应写成：该 target skill 已是 Loom-governanced target skill，且它的 official execution surface 是面向 runtime workflow copy 的公开 `dotnet so.dll run` 与 `dotnet so.dll resume` 路径
- 如果某次创建或 re-enhancement 切片还没有产出通往最终 `Done` 的真实公开 run/resume 链，就应把它表述为进行中或阻塞中的 enhancement 切片，而不是正常的治理完成状态，也不能暗示一个已 governanced 的 target skill 已经完成了 official run
- 当某条 workflow route 用 runtime-owned completion manifest 去引用 checked-in source deliverables 时，这条 route 的 contract 还应显式声明 checked-in source deliverable output families 和 runtime-owned completion-manifest output family，避免 done reachability 退化成只有治理型证据
- 受治理完成必须引用覆盖同一外部 runtime copy 上每个 transition 的 boundary-check/approval-gate trail：被校验的 gate predicates、已核验的 seam ownership、确认的 route coverage，以及允许每一步推进的显式批准或结构化非人类续行
- step kind 显式可见
- 本地工具具备确定性
- memory extraction 已定义或可推导
- 调用方可以把结构化外部结果送回 SO
```

### 精确版本缓存与已验证 audit 复用

当 framework host 分支满足条件且 package lock 已经绑定 runtime 版本时，应先检查本地 NuGet cache，再访问 NuGet.org。只有同一精确版本的三包 IL bundle（包括 package id、精确版本和 nuspec identity）完整且有效时，cache 才可复用；部分缺失或无效的 cache 不能复用，此时只通过 direct URL 下载缺失或无效的精确版本包。

选择 self-contained fallback 时，应改为检查 product/version/RID 对应的 cache entry；只有其中单个精确版本 runtime package、manifest、entrypoint 和 guide version 都有效时才可复用。只通过 direct URL 下载缺失或无效的精确版本包。自动恢复过程中不得解析 latest 版本或使用 `*.latest.nupkg` 别名。把 `runtime_mode`、`rid`、`cache_hit`、`downloaded_packages`、`cache_validation`、`resolved_runtime_version` 及适用的 runtime package 字段保存为 runtime evidence。

对于 invocation 级别的复用，SO 会比较稳定的 workflow 图与配置投影，并拒绝结构发生漂移的输入。它还会比较 source Mermaid/HTML 与当前 render：完全一致时才复制，发生变化时则根据当前 instance 重新生成。该 step 始终为当前 runtime instance 写入新的 `workflow.json`，并在可用时写入新的 `workflow.analysis.json` 与 `workflow.dataflow.json`；`audit-reuse.json` 会记录 copied 与 replaced 文件名。这样既不会用旧备份替换动态 runtime state，也能保持已验证的 audit 展示连续性。

```guide-template
dotnet so.dll copy-audit-step \
  --source-step outputs/audit/wf-source/step-0001-compiled \
  --workflow-id current-run \
  --sequence 2 \
  --action reused-compiled \
  --audit-output outputs/audit \
  --reason "已验证 workflow 与 render 输入没有变化。" \
  --verified-by reviewer-id
```

该命令会复制必需的 Mermaid、HTML、workflow JSON，以及存在时的 analysis/dataflow/summary 文件，校验 SHA-256，拒绝目标碰撞，并写出 `audit-reuse.json`。它只保持 audit 展示连续性，不会推进 workflow、追加 runtime event、评估 gate 或生成官方 `run`/`resume` evidence；这些操作仍必须在同一 runtime workflow copy 上执行。
## Examples

如果你想看一份更完整的 Loom 治理 target skill 运行叙述示例，其中包含 stage gate、branch fan-out、validation、audit evidence 与 Mermaid 路线图，请阅读 [Loom 治理 Skill 运行示例](../../../zh-cn/examples/so-enhanced-skill-run.md)。

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
  ## Loom-Governanced Runtime Lock

  本 skill 已切换到 Loom-governanced execution。
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
      "cache_policy": "exact-version-first",
      "reuse_exact_local_bundle_when_valid": true,
      "download_exact_locked_version_when_missing_or_invalid": true,
      "never_float_to_latest": true,
      "required_bundle_validation": ["package_id_matches", "exact_version_matches", "nuspec_identity_matches", "complete_three_package_bundle"],
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
