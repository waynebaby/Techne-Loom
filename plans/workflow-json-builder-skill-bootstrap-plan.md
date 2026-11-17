# Loom Flow Weaver（loom-flow-weaver）：自举与治理计划

> **状态：Proposal / Review only（提案，仅供审阅）**
> 供用户 review，明确未来范围、治理边界、父子 SO 交接协议和验收证据；不授权 skill/workflow 实施。计划文件本身可按用户指示提交并推送。
> **本次边界：** 完成并更新本计划，预告根 README 中的 Loom Flow Weaver Coming Next 项，并同步发布工作流中的 README 生成文本和双语 workflow glossary。此处只做计划/文档说明，不实施 skill、agent、workflow template 或运行时代码，不运行 SO、build 或 test。

## 1. 摘要

建议新增一个独立、可复用的 workflow 与代码资产构建 skill，采用用户确认的名称 Loom Flow Weaver，slug 为 loom-flow-weaver。范围包含三类任务：从空白需求新建 workflow；把外部已生成的 AO/SO workflow 拆解为多个小型 C# script 片段，逐步通过自包含 EXE 中的内部 Roslyn 验证/执行，冻结并导出全部通过验证的资产，最后产出目标业务 workflow；以及对现有 workflow 或相关 C# 资产做增量修改。其他 workflow 格式需要显式 schema/转换 adapter。每次变更都保留约束、基线、差异、未决项、验证与冻结证据。该 skill 不属于 AO Planner 或 /loom-skill-enhancement；两者均是首发调用方。

推荐“组合治理 + self-hosting”：/loom-skill-enhancement 负责受控交付边界、正式 SO package/runtime 选择与 fresh-guide gate；最小、暂时无自身 SO governance 的 seed 负责 workflow 与 C# 代码资产的逐步构建；seed 再用此能力逐步设计自己的 SO workflow template。候选 template 经独立 review，并由选定 published SO runtime 完成 fresh guide、compile、run/resume 验证后，才成为正式治理 authority。用户明确要求首版就达到正式 run/resume authority，不能以 compile-only 或候选 JSON 作为首发完成线。

parent/child 仅指单次调用的控制流：caller 与 builder 是两个独立 execution，不代表 AO 和 SO 是父子产品，也不预设 SO 内建嵌套生命周期管理。首次 seed bootstrap 由 /loom-skill-enhancement 的受治理外层 strand、明确的输入输出合同和独立 audit 记录约束；正式态之后，builder 必须由自身 template 和 published SO package runtime 治理。两位首发调用方均纳入范围，先完成 /loom-skill-enhancement 集成，再完成 AO Planner 集成。

## 2. 目标与非目标

### 目标
1. 新 skill 独立、可复用，覆盖 workflow 新建、外部 workflow 到 C# 代码资产的转换、既有 workflow/代码资产的修改。
2. 支持从空白需求构造 workflow；把已有 workflow 拆成细粒度 C# script 片段，通过自包含 EXE 内部 Roslyn 逐段验证和执行，冻结/导出全部通过验证的资产后生成最终可用 workflow。
3. 明确 workflow 与 C# 资产之间的输入/输出、增量片段、基线保留、差异审阅、测试、冻结、导出和验证结果合同。
4. 用 seed self-hosting 减少首个治理 template 的重复劳动，同时保持过程可审计、可回退、可独立审查。
5. 定义 builder 与 caller 同时存在时的交接、阻塞、恢复、路径、event 和 audit 关联协议。
6. 正式态遵从 published SO runtime、fresh guide handoff、package-lock 语义；正式首版必须证明 template 在 published runtime 的 run/resume 路径可运行。

### 非目标
- 不把新 skill 纳入 AO Planner 或 /loom-skill-enhancement 的所有权，不将 AO 和 SO 描述为父子产品/runtime。
- 不重做整个 workflow designer，不替代 SO compiler/runtime 或 AO planner，不让 builder 执行任意业务 workflow。
- C# script 由 self-contained EXE 内的 Roslyn 验证和执行；用户不需要提供 .NET 环境。需在实施前从目标 skill 的权威上下文/实际 runtime 确认 EXE 的调用合同与证据字段。已完整读取仓库现有 AO/SO workflow-designer agent Markdown 与 AO/SO 本地 references：它们明确规定 workflow JSON 的细粒度设计、seam/gate 与治理规则，但没有 Roslyn runner 调用细节；因此不得把该 runner 误归于现有 designer，也不得在没找到权威 runner 合同前声称执行验证已完成。
- 不声称 SO 已自动支持嵌套、parent/child 恢复或审计合并。
- 不把 self-hosting 当成绕过 published runtime、fresh-guide、compile/run/resume 或 package-lock 的通用例外。
- 当前不实施，也不修改计划以外的文件。

未来完成证据至少包括：独立 skill 输入/输出与治理说明、SO template 和 package lock、由 template 生成的 Mermaid/HTML、node-to-file/artifact mapping、C# script 由自包含 Roslyn EXE 逐段验证/冻结/导出的记录、约定测试全通过、SO runtime 的完成路线及 blocked→resume 路线证据、实际实现和 audit evidence、先 LSE 后 AO Planner 的调用方集成合同/示例、独立 review。只有 compile 通过或 runtime validation 不足以完成。

## 3. 术语与治理层级

- **weave out / weave back：** 控制权交给外部执行方/子 workflow，以及携带结构化结果恢复 caller。
- **strand：** 可独立恢复的一次执行实例；避免用 thread 表示执行实例。
- **seam：** 用户、caller、child 与 runtime 的责任交接位置。
- **boundary：** 保留协议边界含义。
- **source template / runtime copy：** checked-in 不可变模板 / 一次 run-resume 使用的外置可变 workflow copy。
- **audit root：** 单一 SO execution strand 独占写入的审计根目录。

~~~mermaid
flowchart TD
  P[调用方目标：AO Planner 或 loom-skill-enhancement] --> G[caller 自己的治理与 runtime authority]
  G -->|显式调用 / weave out| B[独立 Loom Flow Weaver]
  B --> BG[Builder 自己的 SO authority（正式态）]
  BG --> W[增量构建、审阅、workflow/C# scripts 交付]
  W -->|结构化结果 / weave back| G
  W --> O[可用 workflow / C# scripts + diff/验证/冻结证据]
~~~

首次 bootstrap 时 builder 尚无自身 template，只能由外层治理 strand 的显式调用边界和 audit records 约束，不能说 builder 已自治理。AO Planner 与 LSE 是可能 callers，各自保持自身权威。

## 4. 现状证据与规则

只读检查所得；实施时仍须以所选 published package guide/runtime 复核公开能力：

1. .github/skills/loom-plan-execution/SKILL.md 与 AO workflow-designer agent/reference：AO path 是选 channel/source、证明 runtime 可运行并产生 fresh guide 后，才进入 plan、workflow authoring、compile、run/resume；designer 要求单节点单责任、显式 weave-out seam、blocked hint 和结构化 resume continuity。
2. .github/skills/loom-skill-enhancement/SKILL.md 与 SO workflow-designer agent/reference：先证明 selected published SO runtime 与 fresh guide；改目标前 plan；template JSON 是 authority。SO target template 根需 templateKind=so-governed-target-skill 和 validation contract，含 gates、routes、declaredUserOwnedFields、reservedRuntimeOwnedFields；designer 要求显式 ModelThink、SubagentCall、AskUser、WaitResume 与 review-confirmation seams。
3. LSE 的 repo-src 自举例外只限其自身 Step 1 已 commit 后的 Step 2，local runtime manifest 只记 audit root。不能推广给 builder。
4. docs/en/reference/products/so-guide.md：run/resume 使用外置 runtime workflow copy，event sidecar 与 copy 关联，audit root 外置；SubagentCall 是外部交接 seam；resume envelope 有 transition_id、correlation_key、payload。
5. 当前 guide 未证明 SO 自动管理嵌套 parent/child lifecycle。路径隔离可防覆盖，但不能单独解决控制流与恢复。
6. 每个 SO workflow step 都必须有 per-step audit：{output}/wf-{wfid}/step-{seq}-{action}/ 中保存 point-in-time Mermaid Markdown、HTML、workflow JSON backup；不能覆盖既有文件。
7. AskUser 只能询问 user-owned 输入/决策。runtime provenance、session ID、自动生成路径交由 runtime seam（如 WaitResume/blocked resume payload）。
8. public docs 默认中英互链，skill-local reference 仅英文。本计划可先中文。未来每个 major implementation slice 后立即执行 cto-review-and-commit；协议/schema/runtime control 变更即时 review。

## 5. 方案比较与推荐

| 方案 | 做法 | 优点 | 风险/代价 | 结论 |
|---|---|---|---|---|
| A：LSE 全程生产 template | 用 LSE 的 plan、designer、治理流程构造完整 builder workflow | 与现有 enhancement 贴合，fresh-guide/SO authority 清晰 | JSON 能力可能变成 LSE 内部工具，独立 ownership/self-hosting 不清楚 | 可行但不满足独立复用 |
| B：seed 自己生产 template | 最小无治理 seed 直接 self-host 并自我验收 | 自举最直接 | 初期无 authority；可能把草稿当治理或绕过 fresh-guide/package-lock；自评盲点 | 不单独采用 |
| C：组合治理（推荐） | LSE 管正式交付边界/外层审计/package gates；seed 逐步构建 workflow/C# 资产并生成自身候选 template；独立 review + published runtime 验证后切入正式态 | 独立复用与受控自举并存，authority 转换可审计 | 需定义 bootstrap contract、父子交接、C# 测试冻结与证据链接 | 推荐，受阶段门约束 |

组合不表示 LSE 永久拥有 builder，也不合并 runtime。正式状态属于 builder 自己的 SO template 和 published SO runtime；callers 保留自身治理和调用合同。

## 6. 推荐生命周期

### 阶段 0：冻结边界、合同、能力探针计划
明确 skill 边界、字段 owner、输入/输出 envelope、失败恢复、artifact layout、验收标准。未来正式工作由选定 package/runtime 进入治理；不先写 template JSON。

### 阶段 1：最小 seed（无自身 SO governance）
创建最小 seed，明确标记为临时 bootstrap authoring tool，**无自治理 authority**。每次调用由 /loom-skill-enhancement 的受治理执行承担外层治理：LSE 限制输入范围，记录 seed 来源、请求、结果、diff、未决项与 audit reference。seed 不宣称完成，候选产物须独立 review。该职责仍受 LSE 自身 published SO runtime 与 fresh-guide hard gate 约束。

### 阶段 2：seed self-hosting 构造候选 template
seed 按冻结合同和仓库规则逐段设计自己的 SO template。每轮保留约束与 diff。候选 JSON 不是 execution authority。不得把 runtime-owned facts 交给 AskUser，也不得以泛化节点隐藏多步流程。此阶段也要验证 seed 能按新建、转换、修改三种任务模式产出并保留 C# 资产。

### 阶段 3：独立审查 + published runtime 验证
独立 reviewer 检查 template、schema/contract、node mapping、颗粒度、route evidence。选择 released/beta published SO runtime，证明可运行且由同一 runtime 产生 fresh dotnet so.dll --guide。guide 产生后即治理 handoff：compile/run/resume 归该 package runtime；不返回 repo build 或 hand-built runtime。fresh guide 前不进入下游 compile/run/resume。
用该 runtime compile，验证 route gates、seam ownership、strongest-earned blocked outputs、done reachability、resume，在外置 runtime copy 上验证至少一条成功完成路线和一条 blocked→resume 路线。用户要求首版达到正式 run/resume authority；候选、compile-only 或仅状态声明均不够。若能力探针没有证明 parent/child 控制语义，列为阻断项。C# 转换路线还需按合同验证并冻结代码资产、导出清单和测试通过结果。

### 阶段 4：正式 SO-governed skill
template、package lock、node map、audit evidence 及 run/resume 验证达标后才进入正式 SO-exclusive 状态。后续增强遵从 published SO runtime fresh-guide → compile → run/resume；seed 例外不再适用。

### 阶段 5：caller 复用
首版接入顺序为 /loom-skill-enhancement 在先、AO Planner 在后；分别定义调用合同、分别验证控制权与恢复语义，并在每个 major slice 后 review。caller 传递自身拥有的目标/决策；child result 成为 caller 后续输入。AO 与 SO 各自保留独立产品身份和 runtime authority。Loom Flow Weaver 的 business workflow 输出与其 self-hosting 用的 SO governance template 是两个不同产物；前者由代码转换 route 交付，后者由自举 route 独立交付。

## 7. Nested/call 控制模型与协议草案

这是未来须设计、实现、验证的协议，不是 SO 已内建能力声明。若公开 runtime 不支持所需 lifecycle，须由公开能力组成显式流程或列为阻断。

### 7.1 身份、路径与 audit 隔离
parent 与 child 各自拥有独立 SO execution/session ID、外置 mutable workflow copy、event log/sidecar、audit root、workflow ID/step sequence 和 resume state。双方只通过只读 correlation_id、parent_execution_id、child_execution_id 关联；字段名待能力探针确认。

已确认的设计方向是用显式外部 adapter 启动独立 child strand。adapter 是待实现/验证的控制边界，不代表当前已有可复用实现。必须用选定 published SO guide/公开 runtime surface 证明启动、status、child resume 和结果回传可行；缺任何关键公开能力时标记 blocked，先解决 adapter/runtime 能力，不采用无审计人工绕行。

建议目录：

~~~text
<execution-output>/exec-<YYYYMMDD_HHMMSS>-loom-flow-weaver-result/
  parent/<parent-execution-id>/workflow.current.json
  parent/<parent-execution-id>/events.jsonl
  parent/<parent-execution-id>/audit/wf-<parent-wfid>/step-.../
  child/<child-execution-id>/workflow.current.json
  child/<child-execution-id>/events.jsonl
  child/<child-execution-id>/audit/wf-<child-wfid>/step-.../
  handoffs/<correlation-id>/request.json
  handoffs/<correlation-id>/result.json
~~~

布局须符合 CLI 实际参数。runtime copy/events/audit/materialization 不放 .github/skills/... 或 assets/so-workflow。默认临时 output root；每步骤在 {output}/wf-{wfid}/step-{seq}-{action}/ 写 Mermaid Markdown、HTML、workflow JSON backup；先检查冲突，冲突就报告路径并失败，不能原地覆盖。

### 7.2 weave out / weave back
1. caller workflow 到显式 BuilderCall/SubagentCall（候选名，须 guide 证实），准备结构化 request。
2. caller 把控制交给 child，保留自身 workflow/session/events/audit root；传递 parent ID 与 correlation ID。runtime paths 不得伪装 user input。
3. child 在独立 runtime copy/session/event/audit root 启动或恢复，返回结构化结果或带业务进度的 blocked result。
4. caller 记录结果摘要/引用、child identity、audit link。child 原始 artifact 仍归 child；caller 写自己的 handoff record，不搬移/覆盖。
5. child blocked 时 caller 进入 runtime-owned wait/blocked seam，保存 child identity、correlation ID、parent resume location。child resume 使用同一 execution chain/runtime copy，不重 run/覆盖。
6. child 返回后，由 caller 自身 SO/AO 正式 resume 恢复；命令发起方需依 guide 确认。parent 与 child resume 是独立状态转换。

### 7.3 Request envelope 候选
~~~yaml
builder_request:
  schema_version: "1"
  correlation_id: "caller-generated-stable-id"
  parent_execution_id: "owner to be confirmed"
  task_mode: "from-blank | workflow-to-csharp | modify-existing"
  goal: "user-owned desired outcome"
  source_workflow_json: "optional content or approved reference"
  schema_and_constraints: []
  preserve_rules: []
  user_decisions: []
  requested_increment_size: "small-reviewable-fragments"
~~~

已确认的传递方向是空白需求与明确粘贴内容 inline，既有 workflow/C# 文件从 caller 声明的 source root 受限读取；拒绝 traversal/越界访问，不设统一字节阈值。身份字段 owner、designer context 是否传递、文件快照/授权细节仍待设计。runtime 生成临时路径属 runtime-owned。

### 7.4 Result envelope 候选
~~~yaml
builder_result:
  schema_version: "1"
  correlation_id: "same-as-request"
  child_execution_id: "runtime-owned"
  status: "completed | blocked | failed"
  strongest_earned_output: "latest valid business artifact or partial"
  workflow_json: "final JSON or approved reference"
  fragments_completed: []
  unresolved_user_decisions: []
  validation_summary: []
  change_record: []
  audit_links: []
  failure_or_block_reason: null
  resume_descriptor: null
~~~

Envelope 区分 business artifact 与 governance evidence。blocked 时返回 strongest-earned JSON/片段、缺失决策和恢复信息；不能只返回治理状态。成功须有最终可用 JSON 与 constraints 验证证据。

### 7.5 失败、重试、恢复
- child completed：caller 检查 correlation、状态、artifact 存在性和验证摘要后再消费。
- child blocked：保留 child execution/runtime copy；AskUser 只处理 user-owned 决策；runtime-owned 信息由 wait/resume payload 提供；先恢复 child，再 weave back。
- child failed：留存失败审计和最强有效部分；部分输出不算 done。明确同 execution resume 或新 execution 重试，以新 ID 关联旧记录。
- parent 中断：parent resume 通过 handoff descriptor 找回现有 child strand；child 状态不明时先用公开 status/inspect，不盲目重复调用。
- 重复/迟到结果：用 correlation/transition ID 幂等核验；旧结果不得覆盖新请求。
- 路径冲突/丢失：不复用旧文件；冲突换 root。runtime copy 丢失则 blocked 并走 runtime-owned 恢复，不问用户系统路径。

## 8. Builder 输入/输出合同

### 输入
必需：任务模式（from-blank / workflow-to-csharp / modify-existing）、目标 workflow 或相关 C# 资产内容/受限文件来源；首版直接支持 AO/SO 公开 workflow JSON，其他格式必须提供显式 schema/转换 adapter；适用 workflow schema、代码约束与成功标准。空白需求和明确粘贴的内容走 inline；既有 workflow/C# 文件默认从 caller 声明的 source root 受限读取并拒绝 traversal/越界访问，不设统一字节阈值。未知业务项列作 user decision，不猜造。C# script 段由自包含 EXE 内部 Roslyn 运行验证，不依赖用户提供 .NET 环境；具体 EXE 参数、Roslyn 输入/输出证据字段需从权威 runner 文档/runtime 获取。不得让用户提供 runtime-owned session/event/audit path、provenance 或 runtime-generated ID。

### 每轮片段
说明单一片段目的/节点或 C# script 责任、增改/保留的 workflow JSON path 或代码资产范围、片段内容、Roslyn 执行/验证结果、相对上轮 diff、影响的 route/gate/seam、测试结果、冻结状态、待定用户决策和下轮建议。每段通过 Roslyn 验证后可成为候选冻结资产；全部集成测试通过后，才冻结并导出完整资产集，再产出目标业务 workflow。节点/代码段不可承担“执行整个计划”等泛化多步职责，必须可独立审阅并映射到文件、测试与证据。

### 成功输出
依任务模式产出最终 workflow JSON、可导出的 C# script 资产及其冻结清单，或 workflow 与代码的成对更新；标明候选/validated/frozen/runtime-ready 状态。附基线 diff、保留说明、逐段 Roslyn 执行验证、全部约定测试通过结果、冻结清单与 schema/compile/route/gate/ownership/done reachability 验证。用户确认首版必须达到正式 run/resume authority，且验证至少覆盖一条完成路线和一条 blocked→resume 路线；代码资产需全部通过约定测试，才可标记冻结并进入最终 workflow 产出。

### blocked 输出
至少含 strongest-earned business artifact、已完成 fragments、user-owned 决策、runtime-owned resume descriptor、阻塞原因和 audit links。不可只返回治理状态，也不能要求用户重贴 runtime 自行生成的信息。

## 9. Builder SO template 候选节点拆分（计划，不是实际 JSON）

至少三个业务 routes：from-blank（从空白需求新建 workflow）、workflow-to-csharp（将 AO/SO 公开 JSON workflow 转化为 C# script 资产）、modify-existing（修改既有 workflow 或相关 C# 资产）。三条业务 route 共用基线保护、增量变更、验证证据和交付 gates。另有单独的 self-hosting route 生成 Loom Flow Weaver 自己的 SO governance template；其产物不混入目标业务 workflow。

`workflow-to-csharp` 必须体现闭环：读取并盘点输入 JSON → 建立输入快照和节点/责任映射 → 按小而可审阅的增量生成 C# script 片段 → 使用 self-contained EXE 内部 Roslyn 做语法/编译检查与片段执行 → 运行该片段的输入/断言测试 → 形成候选冻结资产并记录哈希/版本/验证证据 → 与其他片段组合后运行集成测试 → 全部集成测试通过后冻结、导出完整资产集 → 按冻结资产和映射产出目标业务 workflow → 验证最终 workflow 与完整资产集的一致性。任一门失败时保留 strongest-earned 输出并阻止后续冻结/交付。

`self-hosting` 是另一条治理闭环：LSE 的 published SO runtime 与其 fresh-guide handoff 负责 seed 阶段外层治理 → seed 逐轮构建自身 SO template 候选 → 独立 reviewer 审查并修订 → 选定 published SO runtime 产生 fresh guide → compile/load 与治理合同检查 → 在独立 runtime copy 上完成一条正式完成路线和一条 blocked→resume 路线 → 形成 Loom Flow Weaver 自身 SO template、package lock、节点映射和审计证据。此 route 不生成或替代代码转换 route 的目标业务 workflow。

| 候选职责 | 单一责任 | seam/owner | 业务输出与 gate |
|---|---|---|---|
| 接收/分流 | 判断空白或已有 JSON | user-owned goal/source；runtime facts | 缺 user input 则 blocked |
| 约束清单 | 列 schema、constraints、保留规则、成功标准 | 用户确认含义；runtime供来源事实 | constraints-ready |
| 既有基线 | 安全读取 JSON、记录快照/修改边界 | 用户授权/source；runtime resolved path | 基线解析结果；非法则保留错误摘要 |
| 结构盘点 | 提取 nodes/routes/seams/gates 与缺口 | AI/tool 分析 | 可审阅 gap map |
| 增量片段 | 每次输出有限职责片段 | AskUser 只问业务选择；工具产候选 | fragment-ready + diff/约束核对 |
| C# 逐段验证 | 生成一个 C# script 片段并通过自包含 EXE 内部 Roslyn 验证、执行及片段测试 | runner/runtime 事实由工具提供；业务断言由用户标准约束 | 片段验证证据；失败不得候选冻结 |
| 组合测试与冻结导出 | 运行最终组合测试；通过后冻结并导出全量 C# 资产集 | 测试标准缺失时 AskUser；冻结清单由 runtime/tool 生成 | 全部集成测试通过才允许整体冻结和导出 |
| 业务 workflow 产出 | 根据通过验证的冻结 C# 资产集产出目标业务 workflow | tool/runtime 负责一致性事实 | workflow 与冻结资产映射一致才可交付 |
| Self-hosting template | 增量构造 Loom Flow Weaver 自身 SO template 并经独立 review 与 published runtime run/resume 验证 | LSE 外层 seed 治理；之后由 builder 自身 SO authority | 单独的治理 template 与验证证据，不替代目标业务 workflow |
| 用户决策 | 仅询问 declared user-owned fields | AskUser | 未答保留片段并 blocked |
| 合并/差异 | 合并片段并核对保留规则/JSON path | 工具负责合并/路径事实 | merge-integrity |
| 结构/合同验证 | 调用已证实可用的验证/compile | runtime command/version/provenance | contract-valid；不替代业务输出 |
| 业务审查 | 查 routes、business gates、blocked outputs、done reachability | 独立 review | business-output gate |
| 交付 | 输出 JSON、change record、验证和 artifact references | runtime 生成路径/ID | final artifact + gates 才 done |
| blocked/resume | 保留 strongest output 并用同一 copy 恢复 | WaitResume/runtime payload | audit 不能单独达 done |

根 template 必须声明 templateKind=so-governed-target-skill 与 root validation contract；具体 gates/routes/user-owned/runtime-owned 字段待确认。每条 route 声明 business-output gate 和 strongest-earned blocked output。done 不得由 compile、audit link 或 governance manifest 单独满足。

## 10. Package/fresh-guide 与 bootstrap 边界

1. 正式治理先选 released/beta，遵循 package acquisition index 和 package-lock。
2. 选定 runtime 后硬门：证明该 runtime 可运行且由同一 runtime 产生 fresh dotnet so.dll --guide。失败即停；不进入下游 compile/run/resume 或收集依赖后续的输入。
3. fresh guide 是治理 handoff；之后 selected published package runtime 为 authority，不转回 repo build 或手工 runtime。
4. seed 是有限 bootstrap authoring，不是正式 SO execution。受治理 caller 通过输入范围、交接记录和外层 audit 约束。若无合适 caller，不得称自治理；先建立获准外层治理路径。
5. LSE 自身 Step 2 repo-src 例外不适用于 builder。
6. 能力探针检查公开 SubagentCall/外部交接、child run 启动、child resume 发起方、parent WaitResume/envelope 如何携 child state、status/inspect、audit link、幂等语义、路径参数/并发写入。证据来自 selected published guide/runtime；缺能力则规划 adapter 或列 blocker，不能写作自动管理。

## 11. 阶段计划与 review checkpoints

未来每阶段需记录 input、动作、output、authority、gate、失败回退和 audit evidence。review cadence 属未来实施要求，不是本轮授权。

| Slice | 工作/输出 | authority 与 gate | 证据、回退与 review |
|---|---|---|---|
| 0. 计划/范围批准 | 确认名称、首发 caller、协议 owner；必要时先同步双语 AGENTS | 协议不清不 scaffold | 决策记录；review/fix/validate/commit 后再推进 |
| 1. Package probe/contract | 选 channel、published runtime、fresh guide、探交接能力、冻结 envelope/schema | SO package authority；fresh guide/关键能力失败则停 | manifest、guide、探针、audit；protocol review/commit |
| 2. 最小 seed | author 最小 I/O 能力并标记 no-governance | 外层治理负责；seed 不自称治理 | seed I/O snapshots；slice review/commit |
| 3. Self-host template candidate | 现有 workflow designer 可协助 seed 产候选 JSON；独立 reviewer 把关；保留 diff/决策/route-gate-seam map | 候选非执行 authority；约束丢失、泛节点、ownership 错则回退 | fragments、mapping、独立 review；template/schema 即时 review/commit |
| 4. Independent review/fix | 查 template、routes/gates、blocked、done、AskUser ownership | protocol unresolved 不得通过 | findings/resolutions、Mermaid/HTML/JSON；review/fix/validate/commit |
| 5. Published SO 验证 | fresh guide 后 compile；外置 copy 验成功/blocked/resume/path collision | 未证明任一路径或治理-only done 可达则返工 | per-step audit/events/backups；runtime/protocol slice 即时 review/commit |
| 6. Formal governance | 固化 skill/template/package lock/references/mapping | lock/channel/version 不一致不得正式化 | completion packet；review/fix/validate/commit |
| 7. LSE integration | 加 caller I/O、恢复、失败合同 | 不破 fresh-guide、不混 authority | 双方 audit links；review/fix/validate/commit |
| 8. AO Planner integration | 定义 AO 调用与交接 | AO authority 独立 | AO/child evidence refs；review/fix/validate/commit |
| 9. Final documentation completion, consistency review, and release handoff | **这是计划的最后执行阶段。只有所有实现、两位 caller 集成、验证与各实现 slice review 完成后，才开始统一补充/修订文档。** 盘点本 skill 造成的全部文档新增/修改；补齐公开合同、用户指南、示例、caller 文档、skill-local 说明、导航与 release-facing 内容；对照模板、实际 I/O、runtime authority 与实现证据逐条核对，统一来源、术语与中英文；解决和现有 AO/SO、skill、runtime 文档的重复、冲突或过时内容；最后复核 README 生成源与生成结果 | 文档 inventory 覆盖所有变更文件；公开文档双语互链；skill-local references 仅英文；不得存在与实现/guide 或既有产品边界相冲突的说明；release-facing claims 与已完成实现一致。该 review 是全计划最终完成门 | 文档变更清单、双语配对核验、cross-reference/claim/terminology conflict review、README generator consistency、修复记录；只有所有不一致解决后才可完成 |

除已确认的 caller 顺序外，其他阶段顺序可根据依赖和仓库状态细化；必须先完成 /loom-skill-enhancement integration，再开始 AO Planner integration，不能调换两者顺序。最终完整文档补充与 review 必须在所有实现和集成工作完成后作为计划的最后执行阶段，不能用本计划阶段的预告性 Coming Next/glossary 更新替代。不可跨多个 major slice 后统一 review。默认每 slice 不超约 50 个文件；协议/schema/package seam/runtime control 变更即时 review。

## 12. 验收矩阵

| 维度 | 通过标准 | 证据 |
|---|---|---|
| 独立性 | ownership 独立；Planner/LSE 是 callers；不声称 AO/SO 父子产品 | contract、调用图、文案 review |
| 双路线 | 空白与既有 JSON 正确分流，信息保留/改动可见 | 两路样例与 diff |
| 增量质量 | fragments 可审阅；有 path diff/constraints/待决项；node 责任小 | snapshots、node map |
| SO contract | root kind/validation 完整；compile/load 查业务 gates、ownership、blocked、done | template、compile report、独立 review |
| AskUser ownership | 只问 user-owned；runtime facts/path 由 runtime 给 | ownership matrix、payload review |
| Runtime authority | published runtime runnable，由同 runtime fresh guide，后续 compile/run/resume 在其 authority 下 | manifest、guide、lock、execution evidence |
| Parent/child | copies/sessions/events/audits 隔离；交接关联；blocked/resume 恢复正确；重复结果不覆盖 | 双侧 audit/event/handoff/resume records |
| Audit completeness | 每步 Mermaid/HTML/JSON backup；冲突不覆盖；business 与 governance evidence 分开 | artifact inventory、冲突记录 |
| done/blocked | done 有 final JSON + gates；blocked 有 strongest output + resume 信息 | route-level evidence |
| Self-hosting | seed 增量产候选 template，独立 review 证明未替代正式 authority | seed I/O、diff、review |
| Completion evidence | template、Mermaid、node map、implementation/audit、target deliverables 齐 | completion packet |
| 文档 | public docs 中英互链，skill reference 英文，I/O/CLI/MCP/audit contract 明确 | paired docs、review |

## 13. 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| 误信 SO 自动管理 child lifecycle | 重复执行、丢状态、parent 恢复错误 | 先探公开能力；显式协议及端到端证据；未知即阻断 |
| seed 自我背书 | bootstrap 被误作正式 authority | source-only 标记、caller audit、独立 review、published runtime gate |
| 混淆设计与执行 authority | 未验证 template 被正式使用 | 明确状态转换；fresh guide 后由 package runtime 接管 |
| audit 交叉写入 | 覆盖或责任不清 | 独立 roots、只读 cross-links、冲突失败、owner metadata |
| 首发范围过宽 | 两 caller 控制语义同时变化 | 澄清范围，分别集成验收 |
| 增量合并丢字段 | JSON 失效 | baseline、约束清单、path diff、局部验证与最终 compile |
| 公开 surface 缺 child/resume | 协议不可执行 | fresh-guide/探针前置；单独规划 runtime 能力或 adapter |
| audit 泄露/膨胀 | 敏感内容持久化/资源增长 | 临时 root；保留到 review/交付完成；不把完整输入或代码副本长期写入仓库；保留必需 per-step records |

## 14. 文件落位与语言策略

- 当前计划交付：`plans/workflow-json-builder-skill-bootstrap-plan.md`，以及本计划范围内的 README Coming Next、其发布生成源和双语 workflow glossary 更新。
- 未来 skill 预期 .github/skills/<独立-slug>/；template/package lock 按现行 skill-owned assets 约定；runtime copy/events/audit 不进入 skill 或 assets/so-workflow。
- 执行材料位于外置 exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/ 下，按 parent/child ID 和 {output}/wf-{wfid}/step-{seq}-{action}/ 展开；以 CLI 实际能力为准。
- 本计划中文且非公开产品文档。正式 public docs 在 /docs/zh-cn 与 /docs/en 配对互链；skill-local reference 英文。
- 当前不编辑 AGENTS；实施时重新判断是否同步双语治理规则。

### 14.1 文档变更清单与最终文档阶段

本次计划阶段已更新首页 Coming Next 作为前瞻性项目说明，并同步其生成源，避免下次 publish 覆盖；同时在双语 workflow glossary 定义 Loom Flow Weaver。此处没有宣称 skill 已实现，也没有提前编写尚未验证的运行合同。

未来实现全部完成后，Slice 9 必须基于最终实现、published runtime guide、实际测试与 audit evidence 盘点并完成该 skill 造成的所有文档更新。至少检查以下文档面；某项若经证据确认无需修改，也必须在 inventory 中记录理由：

| 文档面 | 预期位置 / 语言 | 最终阶段动作 |
|---|---|---|
| 首页 Coming Next 与发布生成源 | `README.md`、`README.zh-CN.md`、`.github/workflows/publish-development.yml`、`.github/workflows/publish-main.yml` | 当前已加入预告条目；最终依据交付状态决定保留、改写成已发布能力入口或移除，并确认两条生成源与两个 README 完全一致 |
| Workflow glossary | `docs/en/architecture/workflow-terminology.md`、`docs/zh-cn/architecture/workflow-terminology.md` | 当前已定义 Loom Flow Weaver；最终核对术语与实现、产品身份及 workflow 用法一致，保持双语对齐 |
| Skill 输入/输出参考 | `docs/en/reference/skills.md`、`docs/zh-cn/reference/skills.md` | 增加该 skill 的使命、三种任务模式、输入输出、格式支持、C# Roslyn 验证/冻结、状态、限制与 runtime handoff；双语互链 |
| 用户操作和集成指南 | `docs/en/guides/skill-usage.md`、`skill-integration.md` 及中文镜像；必要时 `agent-integration.md` | 仅在需要操作者选择入口、传递 inline/source-root 输入、解释 child strand weave-out/weave-back、blocked/resume 或调用方责任时补充指南和示例；定义 LSE 先、AO Planner 后的集成顺序，不声称 AO/SO 是父子产品/runtime |
| 新 skill 自有文档 | `.github/skills/loom-flow-weaver/SKILL.md` 与 skill-local references；references 英文 | 文档写明正式 SO authority、package-lock/fresh-guide gate、输入输出 schema、workflow JSON 与 C# script 两类资产、Roslyn EXE 合同、逐段与集成测试、冻结/导出、错误/blocked/resume、audit 路径和 anti-patterns；与最终 template/node mapping 对齐 |
| Caller 侧说明 | `/loom-skill-enhancement` 与 AO Planner 相关 skill/agent 文件及对应公开 docs | 只更新真实变更的调用合同、adapter 启动、状态/结果回传、resume ownership、路径范围和 audit cross-link；各方独立 authority，不复制 builder 内部实现细节 |
| AO/SO guide 或 CLI/MCP contracts | 对应 `docs/<lang>/reference/products/ao-guide.md`、`so-guide.md`、MCP/CLI/input-output contracts | 先判断是否改变现有公开 runtime 行为；若只是 skill 层调用，不改 Loom runtime guide。若实现确实改变公开合同，才按 bilingual contract 更新，并同步离线 guide 来源 |
| 文档导航与例子 | `docs/en`/`docs/zh-cn` README/index、适用的 examples | 补充导航链接和覆盖 from-blank、workflow-to-csharp、modify-existing、自举治理与 blocked→resume 的可核对样例；例子须标明版本和证据性质 |
| 实施计划与审计交付 | 本计划及最终 execution output | 计划状态/阶段记录与实现一致；最终 completion packet 链接模板、Mermaid、node-to-file/artifact map、测试、冻结清单、run/resume 与 audit evidence |

最终 consistency review 必须检查：事实主张有实现或 runtime guide 依据；“planned/beta/released” 状态没有过期；AO 与 SO 产品/runtime 边界准确；同一术语和字段命名一致；README 生成工作流不会回滚编辑；EN/ZH 页面互相链接且语义对应；skill-local references 不出现中文变体；指南、CLI/MCP/input-output合同没有遗漏或互相矛盾。发现的冲突应在此阶段修复并重新 review，未解决项不得标记计划完成。

## 15. 澄清结论与剩余实施验证项

### 已由用户明确决定

1. **首发调用范围：** 同时接入 /loom-skill-enhancement 与 AO Planner；分别定义合同和验收，不合并 AO/SO 产品或 runtime authority。
2. **Bootstrap 外层治理：** 由 /loom-skill-enhancement 的 published SO runtime 负责 seed 阶段治理，并遵守其 fresh-guide hard gate。
3. **Child 启动：** 用显式外部 adapter 启动独立 child strand。此为计划方向；adapter 和 runtime 能力仍须实施/验证，缺任一关键公开能力即 blocked，不采用无审计人工绕行。
4. **JSON 传递：** 空白需求和明确粘贴内容走 inline；既有 workflow/C# 文件从 caller 声明的 source root 受限读取；拒绝 traversal/越界，不设统一字节阈值。
5. **首版 authority：** 正式首版必须达到 published SO runtime 下的 run/resume authority，并跑通一条完成路线与一条 blocked→resume 路线；不以候选 JSON 或 compile-only 作为完成线。
6. **名称与 slug：** 采用 Loom Flow Weaver / loom-flow-weaver；计划与 Coming Next 预告阶段已先定义 glossary 术语，最后文档阶段再按实际实现复核和完善中英文 workflow glossary。
7. **Workflow designer 分工：** 现有 AO/SO designer 可协助 seed 产候选 template，由独立 reviewer 把关；两者当前都以 workflow JSON 设计为职责，不替代 builder 的 C# script 资产流程。
8. **整体职责与代码语言：** 同一个新 skill 负责 workflow 新建、外部 workflow 转换为细粒度 C# script 资产并逐步验证/冻结/导出，以及既有 workflow/相关 C# 资产修改；目标语言为 C#。
9. **C# script 执行：** 所有脚本片段通过 self-contained EXE 内部 Roslyn 验证和执行；不要求用户安装或提供 .NET 环境。
10. **首版输入格式：** 直接支持 AO/SO 公开 workflow JSON；其他格式必须走显式 schema/转换 adapter。
11. **冻结与测试：** 每段通过 Roslyn 验证后成为候选冻结资产；全部集成测试通过后冻结并导出完整资产集，再产出目标业务 workflow。测试包括语法/编译检查、片段运行测试、最终组合测试；缺少业务测试标准时先 AskUser。
12. **业务 workflow 与治理 template：** 代码转换 route 交付目标业务 workflow；self-hosting route 单独交付新 skill 自己的 SO governance template。
13. **两位 caller 的顺序：** 先 /loom-skill-enhancement，再 AO Planner；分开 review/验收。
14. **计划交付：** Clarification Loop 完成且计划确认无误后，将本计划文件提交到 development branch 并同步到远程；此授权不包含 skill/workflow 实施。

### 尚需确认或留待实施阶段验证

- **Roslyn runner 合同：** 用户已确认执行模型；正式实施前需从对应权威 skill/runtime 找到 EXE 参数、测试输入、结果/审计字段和失败语义。已读 AO/SO designer agent 与本地 references 覆盖 workflow JSON 设计/governance，不含 runner 调用细节。
- **Adapter 能力证据：** 实施时须从所选 published guide/runtime 验证启动、status、child resume、结果回传；这是硬 gate，不是用户偏好问题。

Clarification 进度：Clarification Loop 已完成。最终确认轮接受当前计划和审计保留策略；剩余 Roslyn runner 命令、adapter 能力与实际 runtime evidence 均属于实施阶段的硬验证门，不是未决用户偏好。用户要求将计划与本次 README/发布生成源/glossary 更新一并完成、提交到 development branch 并同步远程；当前仍不实施 skill/workflow。计划最终执行阶段还必须补全全部文档并完成 cross-document consistency review。

## 附录：本轮边界

- 已创建并更新：本计划，Proposal / Review only；README Coming Next、两个发布工作流生成源、双语 workflow glossary 已添加 Loom Flow Weaver 预告/术语。
- 未改：任何 skill、agent、workflow template、runtime code、AGENTS 或现有无关未跟踪文件。
- 未执行：SO guide/compile/run/resume、build、test、安装、部署。
- 下一步按用户授权 review、提交并推送本计划及上述文档更新；不实施 skill/workflow。
