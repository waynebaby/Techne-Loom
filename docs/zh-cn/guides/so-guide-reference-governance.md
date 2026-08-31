# SkillOrchestrator Guide：Governance

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-governance.md) | [根目录](../README.md)

版本：draft
构建：repository source

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

## 受治理的 SO 入口

对于每个由 Loom Skill Orchestrator 治理的 target skill 校验，包括 `/loom-skill-enhancement` 自举，精确的发布 runtime 预检通过后，本机 MCP server 是第一个外部接口。

1. 使用 `dotnet so.dll mcp stdio` 或已经核验的 self-contained 等价入口启动选定的发布 runtime。
2. 完成 `initialize` 和不带 `id` 的 `notifications/initialized` 通知。
3. 针对同一份外部 workflow copy 调用 `so_inspect_workflow_fragment`，并保留有界结果。
4. 只有 `mcp_startup_evidence` 完整后，workflow 才能继续捕获 `--guide`，再进入规划、编写、校验、compile、run 或 resume。

这是受治理 workflow 的步骤，不是要求配置当前编辑器的 `mcp.json`。如果 MCP 无法启动或片段调用失败，就把保存的 workflow 停在失败预检状态；direct CLI 和本地编排不能绕过它。MCP 调用用于支持校验，但不能替代正式的 `dotnet so.dll run` / `dotnet so.dll resume` 链路。

### Shared Context And Parallel Review Batches（共享上下文与并行审查批次）

MCP-first runtime proof 和 fresh guide capture 完成后，只构建一次有界的 shared review context。它必须保留真实 checked-in 快照、source manifest、guide/schema/runtime 引用、确定性的 `context_hash`，以及同一份 external workflow copy 的身份。每个独立审查都必须引用这份 context。

对于共享同一目标 state 且彼此独立的外部 `SubagentCall` 审查或验证，使用 `ConcurrencyStrategy.All`。SO 会在一份已持久化的批次中登记所有等待，直到全部结果返回才推进。缺少或重复结果必须 fail closed。所有 finding 必须先统一汇总，再做一次协调修复；修复后再运行第二个并行验证批次并汇总。最后一个校验阶段必须串行执行：解析 JSON、检查图和 dataflow、使用当前 runtime compile、compile 对应的 schema/demo，并运行有序 runtime 校验。

这是 enhancement 的规划与治理行为，不是通用 AO/SO runtime Review engine。

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

这个命令会一次性写出完整文件集：`workflow.schema.json`、`workflow.demo.json`、`workflow.model.cs`、`workflow.demo.cs` 与 `workflow.demo.verify.cs`。其中两个可执行示例是普通 `.cs` 文件；把它们的路径传给 `--script-file` 和 `--verify-script`，不需要 project 文件，也不需要额外安装 C# script runtime。请使用同一份 runtime 通过 `compile --workflow-file <path>` 校验生成的 demo。除非明确要求作为交付物，否则生成文件必须放在 skill 目录之外。

```guide-template
dotnet so.dll compile \
  --workflow-file so-template.json \
  --audit-output outputs/audit
```

`so-template.json` 仍然是 checked-in source template。`outputs/audit` 也必须放在 skill 文件夹之外。

对 `/loom-skill-enhancement` 和任何 Loom-governanced target skill，不要把直接修改 checked-in workflow JSON 当作常规维护路径。只有当当前 `dotnet so.dll` 路径已经完全 blocked，且用户明确同意一个狭义变通方案时，才允许做最小的直接 JSON 修改去打通下一次 `dotnet so.dll compile`、`dotnet so.dll run` 或 `dotnet so.dll resume`；随后必须立刻回到 Loom 治理路径。

对正在运行中的外部 workflow `.json` 副本做手动修改，也只能视为 blocked 状态下的最后手段应急变通，不能当作常规 workflow 操作路径。

对于 Loom-governanced target-skill template，还必须设置根 `templateKind: so-governed-target-skill` 和根 `validation` 契约。`compile` 会在 workflow 获得 execution authority 之前，同时校验结构正确性、route-aware business-output gates、seam ownership、blocked strongest-earned outputs 与 done reachability。
每个受治理 instance 还必须声明 `taskType`、`workflowKind`、`caseId` 和 `runId`。enhancement workflow kind（`so_self_bootstrap` 与 `target_skill_enhancement`）必须使用 `taskType: skill_enhancement`；`target_skill_business` 必须使用 target-specific business task。validator 会拒绝 target business workflow 发布 SO enhancement output family，或调用 `assets/agents/loom-skill-enhancement-*` subagent。新的 runtime copy 物化或第一次 run 时，会把 `template:` run 标记替换成生成的 `run-<guid>`；同一条外部 run/resume 链会保留这个 runId。

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
