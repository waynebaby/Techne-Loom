# Skills 输入输出参考

[English](../../en/reference/skills.md) | [根目录](../README.md)

如果你想先看操作者视角的 usage、demo 和入口选择，请先阅读 [使用 Techne Loom Skills](../guides/skill-usage.md)。

## 语言策略

- `.agents/skills/*/reference/` 下的 skill 本地 reference 文档必须只使用英文，以保证离线执行与维护一致性
- 仓库文档 `docs/en` 与 `docs/zh-cn` 必须保持中英双语镜像，用于公开文档表面
- 如果 skill 需要本地化说明，应放在 `docs/` 双语文档中，而不是在 skill 本地 `reference/` 目录新增非英文版本

## Loom-bin 共享规则

- Loom Agent Execution Orchestrator skill、SO skill，以及任何采用 Loom bin skill 体系的目标产品，都必须在自己的 skill 文档或产品文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- Loom Agent Execution Orchestrator skill、SO skill，以及任何采用 Loom bin skill 体系的目标产品，都必须在包获取指引里把 NuGet.org 视为一等“最新包来源”，同时保留 released / beta package index 的绝对 URL 与 GitHub asset fallback links
- Released package index URL（English canonical）：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL（English canonical）：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL（zh-CN mirror）：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL（zh-CN mirror）：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## Workflow 文件语言



在 AO、SO 以及受 Loom 治理的 target skill 中，workflow 定义文件是规范英文信息载体。workflow 自己拥有的 schema key、node 和 transition 名称/描述、workflow phase、expression、hint、failure guidance、evidence reference 以及 control metadata 必须使用英文。用户/业务 payload 可以保留来源语言，面向用户的输出可以使用请求语言；本地化属于展示层，不能改变 workflow key 或控制语义。
## Runtime 选择

获取任何 package 前先遵循[平台检测步骤](runtime/platform-detection.md)。运行时选择采用双官方通道且只接受精确版本：

- self-contained 是默认通道：为检测出的 RID 选择一个 exact-RID package，AO 使用 `Techne.Loom.AgentOrchestrator.Runtime.<rid>`，SO 使用 `Techne.Loom.SkillOrchestrator.Runtime.<rid>`，其中的 direct `ao`/`so` executable 作为 launch file。
- `.NET CLI 模式`必须显式选择，使用精确版本的 .NET runtime bundle（含 Roslyn 的 NuGet restore set）。所有 bundle 成员都使用 owning skill 的精确版本。CLI 启动后不在两种模式间隐式 fallback。
- 两种模式都必须先运行 fresh `--guide`，再进入 compile 或正式 run/resume；后续命令复用同一个 launch descriptor、精确版本和 RID。CLI 启动后的错误不是 fallback 触发条件。
- package resolver 先使用精确 NuGet V3 `.nupkg` 与 `.sha512` URL，只有 NuGet 获取失败后才使用同版本官方 GitHub asset；启动前校验 identity、manifest、ZIP 安全和缓存状态。


## `/loom-plan-execution`

### /loom-plan-execution 使命

这是一个以 guide 和环境配置为先的计划执行入口，围绕 plan-execution package flow 工作。

它同时采用 Loom Agent Execution Orchestrator 强治理：Loom Agent Execution Orchestrator 是这个 skill 唯一正式 execution authority，只有显式 `dotnet ao.dll run` / `resume` 才算正式 skill run。

### /loom-plan-execution 输入

- 丰富的计划文本，建议至少 10 行非空内容
- 或详细的计划文件路径
- package 通道选择：released 或 beta
- guide 表面只提供英文：调用方运行不带参数的 `dotnet ao.dll --guide`，解析其中的 `version`、`docs_root`、`guide_path` JSON 字段，并读取返回的 guide 路径
- 可选 runtime source mode：默认是 `package-channel`；当你正在当前仓库里调试这个 skill 并且明确要求使用当前源码输出时，可显式传 `repo-src-debug`
- 可选审计输出路径

### /loom-plan-execution 默认假设

- 默认把与所选语言界面和当前 CI/CD 管理的 skill version block 相匹配的 released / beta package index 绝对 URL 作为获取 Loom Agent Execution Orchestrator package 的事实来源；其中 NuGet.org 是一等“最新包来源”，GitHub asset links 仅作 fallback
- 获取 AO package 前先遵循[平台检测步骤](runtime/platform-detection.md)并执行 host 启动预检。可用 .NET 9 host 使用精确版本三包 IL bundle 并统一放入外部目录；host 缺失或启动失败时获取一个精确的 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package 并直接运行其 executable。两条路径都保持绑定版本，不使用 repository build 作为 fallback。
- 当走 package-channel runtime 获取时，默认复用标准外部目录布局，例如 `<execution-root>/runtime-bundle/ao-<resolved_runtime_version>/{downloads,extracted,unified}/`：原始包资产放到 `downloads/`，每个包解压到 `extracted/<package-id>/`，可运行的 `lib/<tfm>/` 内容汇总到 `unified/`，之后所有 Loom Agent Execution Orchestrator 命令都只能从这个 unified runtime 目录执行
- 当调用方正在当前仓库里调试这个 skill，并且显式请求 `repo-src-debug` 时，默认改为构建并使用 `src/dotnet/Techne.Loom.AgentOrchestrator` 的当前仓库 Loom Agent Execution Orchestrator 项目输出，而不是下载 package assets；但 package index links 与 guide surface 仍然保持 authority reference 身份
- 默认要求任何采用 Loom bin skill 体系的目标产品，在自己的文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- 默认把不带参数的 `dotnet ao.dll --guide` 视为权威运行入口；解析其 JSON 结果并优先读取 `guide_path`，不要在 skill 中复制一套私有执行模板
- 默认把 Loom Agent Execution Orchestrator 视为本项目里的 CLI-only 表面；不要依赖 MCP 宿主或 MCP tools
- 除非用户明确指定输出位置，否则 workflow 编写中间文件、compile、audit、think-out-loud 支撑输出以及其他运行时临时文件默认都放在运行时临时根目录或 repo 根临时目录，绝不默认放到 skill 路径下
- 默认把 checked-in 的计划文档和任何外部编写的 Loom Agent Execution Orchestrator workflow snapshot 都视为不可变 source artifact；Loom Agent Execution Orchestrator 的可变运行时状态只能落在 `session_dir` 输出或显式 execution output 根目录下，不能落在 skill 文件夹里
- 默认把 Loom Agent Execution Orchestrator 视为这个 skill 唯一正式 execution authority
- 默认只把显式 `dotnet ao.dll run` 和 `dotnet ao.dll resume` 视为正式 skill run
- 默认把 `dotnet ao.dll compile`、`dotnet ao.dll --guide`、`dotnet ao.dll prompt-plan` 和 `dotnet ao.dll prompt-replan` 视为准备、校验或 authority-supporting 表面，而不是正式 skill run
- 默认把 skill-level history、checklist、run map、evidence 全部锚定到 Loom Agent Execution Orchestrator workflow state、frontiers、workflow JSON、event logs 和 audit artifacts
- 默认拒绝把非 Loom Agent Execution Orchestrator 输出或非 Loom Agent Execution Orchestrator 测试记作正式 skill execution evidence

### /loom-plan-execution 输出预期

- 绑定 runtime 版本确认，以及由该版本推导出的 released / beta 证据
- 绝对 package index links
- released / beta package index link 集合；如果存在本地化页面，也要包含对应镜像
- 实际 runtime source 选择；如果启用了该覆盖，还要明确给出 `current-repo-src` / `repo-src-debug`
- guide surface 引用
- 当走 package-channel runtime 获取时，实际使用的 AO bundle 精确版本号、runtime bundle package 列表与 unified runtime 目录
- 当走 package-channel runtime 获取时，复用的 unified runtime 标准目录模板与规定恢复顺序
- 可选的、由外部编写并经 AO compile 校验的 workflow JSON snapshot 路径
- 可选的、外部编写的 `WorkflowInstance` 路径；它可以继续传给 `dotnet ao.dll run --instance-file <path>`，让第一次 blocked runtime audit 保持同一份图
- runtime 返回 payload links，包括 audit artifacts
- 当用户没有显式指定位置时，还应给出位于 skill 路径之外的 workflow 编写 / compile / audit 临时输出根目录
- 明确说明 checked-in 的计划或 snapshot artifact 保持不可变，AO runtime state 只能落在 `session_dir` 或显式 execution output 根目录下
- package runtime 准备完成后，以及之后每次 AO progress update，think-out-loud 输出都必须显式回报 `resolved_runtime_version`、`runtime_bundle_packages` 与 `unified_runtime_directory`
- think-out-loud 输出必须在每次 AO progress update 时把当前 workflow 的 Mermaid Markdown 与 HTML 路径作为显式 `audit_markdown_file` 与 `audit_html_file` 字段带出来
- AO-only governance 的 execution authority 与 official run 明确定义
- 锚定到 AO workflow 与 audit artifacts 的 history / checklist / run-map / evidence / reporting honesty 输出

### /loom-plan-execution 运行时衔接

- 默认把不带参数的 `dotnet ao.dll --guide` 视为权威运行入口；解析其 JSON 结果并优先读取 `guide_path`，不要在 skill 中复制一套私有执行模板
- 当 `repo-src-debug` 在当前仓库里被显式启用时，先构建 `src/dotnet/Techne.Loom.AgentOrchestrator`，再用产出的 `ao.dll` 执行同一套 AO CLI surface，而不是下载 package assets
- 当走 package-channel runtime 时，复用双模式 resolver。self-contained 是默认通道，从精确版本的 RID 缓存目录直接运行 executable；`.NET CLI 模式`显式使用精确版本的统一 IL 目录。两条分支都要先运行 fresh `--guide`，再执行下游命令。
- 先写好 objective/context 输入，再通过 `dotnet ao.dll prompt-plan` 获取 AO 自有的 planner prompt 文本，以及 typed prompt blocks，用于 WorkflowInstance 文件生成
- 对 `consumption_requirement = required` 的 prompt block 视为必须消费的输入契约，对 `consumption_requirement = optional` 的 block 视为仅供参考的形状示例
- 使用这些 `prompt-plan` 输出在 skill 文件夹之外编写 WorkflowInstance JSON 文件，再通过 `dotnet ao.dll compile` 校验该 workflow JSON
- 然后还可以把同一份 authored WorkflowInstance 文件继续传给 `dotnet ao.dll run --instance-file <path>`，让 runtime 从同一份图起步，而不是先退回到最小 sidecar-only 图
- AO blocked 之后，再通过 `dotnet ao.dll prompt-replan` 获取 AO 自有的 replanner prompt 文本，以及 typed blocked-context 与 current-workflow blocks，用于在 blocked frontier action 收敛失败后替换 WorkflowInstance 的 TBR seam
- 使用这些 `prompt-replan` 输出修改当前 `workflow_instance_file`，然后再进入下一轮 resume
- 只通过 `dotnet ao.dll run` / `resume` 作为正式 skill run 表面执行
- blocked 之后根据返回的 workflow JSON frontier 继续
- audit artifacts 与中间输出可以在对话或 think-out-loud 里引用，但默认仍放在 runtime temp、repo-root temp，或用户显式指定的 execution output 根目录，而不是 skill 文件夹
- compile 与 audit 流程必须在目标 artifact 已存在时失败，而不是覆盖
- checked-in 的计划文件和 snapshot artifact 必须保持干净；AO 的可变控制态继续通过 `workflow_file` 追踪，而 runtime 图连续性则通过 `workflow_instance_file`、runtime sidecar 和可选 pointer 文件在 skill 文件夹之外追踪
- 每次 AO progress update 都应在 runtime temp 或显式 execution-output 根目录下渲染当前 workflow 的 Mermaid Markdown 与 HTML，并把这些路径写入 think-out-loud 输出

## `/loom-skill-enhancement`

### /loom-skill-enhancement 使命

这是一个以 guide 为先的 deterministic skill 创建 / 升级入口，围绕 Loom Skill Orchestrator package flow 工作。

当目标 skill 已经暴露出 Loom Skill Orchestrator governance 信号时，这个 skill 必须在一次增强过程中把它升级成一个排他采用 Loom Skill Orchestrator 治理的 skill，而不是停留在一般性的 Loom Skill Orchestrator 支持补充或文档补全。

### /loom-skill-enhancement 输入

- 目标 skill 路径或目标 skill 仓库路径
- 确定型 skill 目标 / 改造请求
- 本次增强中必须创建或修改的目标 skill 变更项
- runtime 版本依据：复用 checked-in 的 `assets/so-workflow/so-package-lock.json` 与当前 skill package version block，需要区分 released 或 beta 时再从这个绑定版本推导
- guide 表面只提供英文：调用方运行不带参数的 `dotnet so.dll --guide`，解析其中的 `version`、`docs_root`、`guide_path` JSON 字段，并读取返回的 guide 路径
- 可选 JSON context 文件
- 可选审计输出路径

### /loom-skill-enhancement 默认假设

- 默认把与所选语言界面和绑定 runtime 版本相匹配的 package index 绝对 URL 作为获取 Loom Skill Orchestrator package 的事实来源；如果执行时需要本地二进制，则按派生出的通道把对应 runtime 安装或解包到目标仓库外部的临时目录
- 默认要求每次增强执行都先从当前选定 package runtime 运行不带参数的 `dotnet so.dll --guide`，解析 JSON 结果并读取其 `guide_path`，再开始编写、修改或校验目标 skill 交付物；不要复用旧会话或旧版本包留下的 guide 输出
- 如果目标项目本身还没有安装依赖，默认只安装完成本次请求的 target-skill 变更和当前 guide 对齐校验路径所需的最小依赖集；不要扩大成无关的整仓恢复或可选工具链安装
- 获取 SO package 前先遵循[平台检测步骤](runtime/platform-detection.md)并执行 host 启动预检。可用 .NET 9 host 使用精确版本三包 IL bundle；host 缺失或启动失败时获取一个精确的 `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package 并直接运行其 executable。两条路径都保持绑定版本，并位于目标仓库之外。
- 默认要求任何采用 Loom bin skill 体系的目标产品，在自己的文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- 默认把 Loom Skill Orchestrator 相关材料放在 `<target-skill-root>/assets/so-workflow/`
- 默认在目标 `SKILL.md` 已存在时根据它和补充 references 生成 `<execution-output-root>/plan/skill-plan.md`；如果是新建 skill，则改为根据 `goal` 和补充 references 生成
- 默认写入 `<target-skill-root>/assets/so-workflow/so-package-lock.json`，记录本次增强所使用的精确 Loom Skill Orchestrator NuGet 包版本、所选通道以及 runtime bundle members，并遵循标准示例 `.agents/skills/loom-skill-enhancement/examples/so-package-lock.example.json`
- 如果存在 `references/*.md`，默认用“简单拼接 + 清晰分隔头”的方式生成临时 `merged-context.md` 工作文件，再把需要的内容转换成临时 JSON context 文件，供 Loom Skill Orchestrator 的 `--context-file` 流程使用
- 默认把 workflow template 独立存放；除非用户显式指定输出位置，否则 compile 产物、audit artifacts、中间工作文件以及其他运行时临时文件默认放在运行时临时根目录或 repo 根临时目录，而不是任何 skill 路径，也不是 `<target-skill-root>/assets/so-workflow/`
- 默认把 `<target-skill-root>/assets/so-workflow/` 下的 checked-in workflow template 视为不可变 source template；在任何 `dotnet so.dll run` / `resume` 之前，都先把它复制到运行时 temp、repo-root temp，或用户显式指定的 execution output 根目录下的外部 runtime workflow copy，再让可变 copy 和 sidecar 在那里演进
- 增强完成后，默认烧录一个 machine-readable 的 Loom Skill Orchestrator package lock，记录 `package_id`、所选 `released` 或 `beta` 通道，以及本次增强实际解析出的精确 NuGet 版本
- 增强后的目标 `SKILL.md` 必须显式引用 `<target-skill-root>/assets/so-workflow/so-package-lock.json` 作为权威 Loom Skill Orchestrator runtime 版本锁，并明确日常 Loom Skill Orchestrator runtime bundle 恢复必须先校验并复用本地完整的精确版本 bundle；仅在校验失败时下载锁定的精确 bundle，禁止浮动到 latest
- 之后运行增强后的目标 skill 时，默认恢复这个锁定的 Loom Skill Orchestrator runtime bundle，而不是在同一通道内悄悄漂到更高版本，或遗漏 `Common` / `Abstractions`
- 如果目标 skill 需要再次增强，默认不再让用户选择通道；而是复用 checked-in lock 与当前 skill build metadata 里已经绑定的 runtime 版本，仅在运行层面需要时才推导 `released` 或 `beta`，并且只在绑定版本变化时重写 lock 文件
- gap review 之后必须明确判断模板采用 `local_patch`、`structural_refactor` 还是 `full_regeneration`；结构性变化要把旧模板与当前需求、概念文档、target-skill 资产和最新 guide 一起作为输入，重新生成候选模板
- 默认把 workflow template 的正确性放在绝对优先级：生成出来的 workflow JSON template 必须完整、详细、与当前绑定 runtime 版本捕获到的 guide 对齐，并且先通过 `dotnet so.dll compile --workflow-file <path>`，之后才可以成为增强后目标 skill 的执行依据
- 对于根 `templateKind: so-governed-target-skill` 的 target-skill template，还必须写入根 `validation` 契约，其中包含 `gates`、`routes`、`declaredUserOwnedFields`、`reservedRuntimeOwnedFields`
- 受治理 route 必须声明 terminal business-output gates 与 strongest-earned blocked-output gates，这样 compile 才能拒绝只靠治理字段到达 `done` 或空心 blocked pause 的 workflow
- `AskUser` seam 只能请求已声明的 user-owned fields 或 decisions；runtime-owned facts 和 artifact paths 属于 `WaitResume` 之类的 runtime-owned seam
- 强制 `/loom-skill-enhancement` 自身以及每个增强后的 target skill 都走上 Loom Skill Orchestrator-governanced route：任何 step transition 都必须先在精确的外部 runtime workflow copy 上通过 boundary check，再收到针对该下一步的显式批准或结构化续行指示后才能推进；compile-clean 只是前置条件，绝不是跳过后续 gate 的批准
- 当目标 skill 已暴露 Loom Skill Orchestrator governance 信号时，例如已存在 workflow assets、`skill-plan` / `so-template` contract、audit contract，或文档已把 Loom Skill Orchestrator 写成 execution authority 候选 / 正式运行面，默认自动进入排他的 Loom Skill Orchestrator governance mode
- 在排他的 Loom Skill Orchestrator governance mode 下，默认把 Loom Skill Orchestrator 视为目标 skill 唯一正式 execution authority
- 在排他的 Loom Skill Orchestrator governance mode 下，默认只把显式 `dotnet so.dll run` 和 `dotnet so.dll resume` 视为正式 skill run
- 在排他的 Loom Skill Orchestrator governance mode 下，默认把 direct CLI 和 direct MCP 降级为 runtime primitive 或 component execution；它们不是正式 skill run
- 在排他的 Loom Skill Orchestrator governance mode 下，默认把 skill-level history、checklist、run map、evidence 全部锚定到 Loom Skill Orchestrator workflow state、event log、workflow template、guards、seams 和 audit artifacts
- 在排他的 Loom Skill Orchestrator governance mode 下，默认要求目标 skill 明确表述自己已经切换到 Loom-governanced execution
- workflow template 必须使用显式的受治理步骤、guards、seams 与可复核输出；绝不能编写或保留任何目的上表示或暗示 `run a multistep plan` 的节点
- 还必须审查 workflow template 中任何把多步指令或宽泛 agent prompt 塞进单个节点的写法，并在可行时拆成更小的受治理节点
- 默认把增强后的 `SKILL.md` 压缩到约 80-100 行，同时保留高层步骤、guardrail 标题、Loom Skill Orchestrator 指引以及 `## Workflow Contract` 标题
- 如果 released 通道并没有真正提供同等 Loom Skill Orchestrator 增强产物，released 文案默认标注为 Beta Only
- weave-out 时，默认先用 `current_step_kind` 等结构化 blocked payload 字段判断等待类别，再把 `skill_hint` 按字面当作下一步外部动作指令消费：只有结构化字段明确要求人类输入 seam 时才询问用户；如果它表明正在等待邮件、文件、消息或下游脚本结果，则把它视为合法的外部等待状态，向用户返回下一步所需输入形状，或等待外部结果到达后再 `resume`；只有结构化字段加上字面 `skill_hint` 一起明确指向非人类续行时，才默认由 agent 自动继续
- 这些规则默认只是 skill 层的改造约定，不应自动当成通用 Loom Skill Orchestrator runtime 契约；如果当前绑定版本对应的 guide 没有公开等价表面，则应明确标成 Beta Only

### /loom-skill-enhancement 输出预期

- package / 通道选择确认
- 绝对 package index links
- released / beta package index link 集合；如果存在本地化页面，也要包含对应镜像
- guide surface 引用
- 经审查编写流程产出的确定型 workflow 模板路径；只有在 guide 对齐审查加上 `dotnet so.dll compile` 通过之后，这个模板才是增强后目标 skill 的执行依据
- 面向未来 target-skill workflow 的 governed-template validation 契约证据，包括 route-aware gate 声明与 seam ownership 声明
- 面向 terminal 与 blocked governed path 的 route-aware business-output gate 证据
- 锁定 Loom Skill Orchestrator 包元数据路径，以及本次增强实际使用的精确版本号、所选通道与 runtime bundle members
- 当 source deliverable 仍保持 checked-in 资产形态时，锁定的 Loom Skill Orchestrator 包元数据应拆成两层表达：checked-in 的 `so-package-lock.json` 源资产，以及本轮 slice 用来引用该 checked-in 源资产的 runtime-owned completion/reference artifact
- runtime 返回 payload links，包括 audit artifacts
- 当用户没有显式指定位置时，还必须给出位于目标 skill 路径之外、且位于 `<target-skill-root>/assets/so-workflow/` 之外的 compile / audit 临时输出根目录
- 可在对话中引用的中间输出与 think-out-loud 支撑文件，默认也必须位于目标 skill 路径之外，并且位于 `<target-skill-root>/assets/so-workflow/` 之外
- 运行时 workflow copy 路径与 event-log 路径必须独立于 checked-in source template 路径
- think-out-loud 输出必须在增强后目标 skill 的每次 Loom Skill Orchestrator progress update 时带上当前 workflow 的 Mermaid Markdown 与 HTML 路径
- 当排他的 Loom Skill Orchestrator governance mode 生效时，还必须输出明确治理声明：Loom Skill Orchestrator 是唯一正式 execution authority，只有 `dotnet so.dll run` / `resume` 算正式 skill run，direct CLI / direct MCP 仅是 primitive path
- 当排他的 Loom Skill Orchestrator governance mode 生效时，还必须输出锚定到 Loom Skill Orchestrator workflow 和 audit artifacts 的 history / checklist / run-map / evidence / reporting honesty / test classification 结果
- 当排他的 Loom Skill Orchestrator governance mode 生效时，还必须输出显式完成态文案，表明目标 skill 已切换到 Loom-governanced execution
- 当排他的 Loom Skill Orchestrator governance mode 生效且 checked-in source asset 仍是权威交付物时，显式完成态文案还必须区分 checked-in source deliverables 与 runtime-owned completion manifest，不能暗示后者替代了前者
- 还必须给出 workflow template 治理证据，证明不存在任何目的或意图上表示或暗示 `run a multistep plan` 的节点

### /loom-skill-enhancement 运行时衔接

- 以不带参数的 `dotnet so.dll --guide` 作为 Loom Skill Orchestrator 的事实来源；解析其 JSON 结果，并在下游工作前读取 `guide_path`
- 且这次不带参数的 `dotnet so.dll --guide` 必须来自本轮增强当前选定 package runtime，并且返回的 `guide_path` 可读取，不能复用旧的 guide 运行结果
- 由 AI agent 直接在终端执行 `dotnet so.dll compile` / `run` / `resume`
- 先通过受审查的编写流程在 `<target-skill-root>/assets/so-workflow/` 下产出 workflow JSON，再执行 `dotnet so.dll compile --workflow-file <path>`；除非用户明确指定其他位置，否则 compile 和 audit 临时输出必须路由到运行时 temp 或 repo 根 temp
- 在把模板当作执行依据之前，先按当前绑定 runtime 版本捕获到的 guide 审查它是否完整、详细，再要求 `dotnet so.dll compile` 成功
- 对于根 `templateKind: so-governed-target-skill` 的 target-skill template，`dotnet so.dll compile` 与 workflow load 还会拒绝缺失根 validation 契约、`AskUser` seam ownership 非法、只靠治理字段到达 `done`，以及未发布 strongest-earned business outputs 的 blocked route
- 每次增强都复用当前 skill build 与已 checked-in `so-package-lock.json` 已经绑定好的精确 Loom Skill Orchestrator 包版本，并在需要时从该绑定版本推导 channel；后续运行目标 skill 时则先校验并复用本地完整的精确版本 runtime bundle，仅在校验失败时下载锁定版本，禁止浮动到 latest
- 后续运行增强后的 target skill 时复用双模式 launch descriptor：self-contained 默认恢复锁定的精确 RID runtime package；`.NET CLI 模式`显式恢复锁定的 .NET runtime bundle。两条分支都在任何 SO 调用前建立一个外部 runtime 目录。
- 每次 `dotnet so.dll run` / `resume` 之前，都要先把已固化模板复制到外部 runtime workflow copy，确保 checked-in source template 保持干净
- 当排他的 Loom Skill Orchestrator governance mode 生效时，只能通过 `dotnet so.dll run` / `resume` 作为目标 skill 的正式运行面执行确定型步骤，而且这些调用只针对外部 runtime copy
- 目标 skill 只在出现变数时才重新规划 source template
- compile 与 audit 流程在目标 artifact 已存在时必须失败，并报告冲突路径集合，不能覆盖
- 每次 Loom Skill Orchestrator progress update 都应在 runtime temp 或显式 execution-output 根目录下渲染当前 workflow 的 Mermaid Markdown 与 HTML，并把这些路径写入 think-out-loud 输出
