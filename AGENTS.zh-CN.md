# Workspace Agent Rules（中文镜像）

[English](AGENTS.md)

> `AGENTS.md` 是仓库自动化执行规则的规范源文件。
> `AGENTS.zh-CN.md` 是中文镜像，必须与英文文件保持同步；若两者冲突，以 `AGENTS.md` 为准。

## Copilot 工具限制

- GitHub Copilot 在本仓库中禁止使用 `apply_patch` 工具。请改用 VS Code 编辑器或其他经仓库批准的文件编辑方式。

## 共享 Python 环境

- 这个工作区通过 `.venv.path` 使用共享虚拟环境指针。
- 该环境由 `cto-skills-manager` 管理。
- 在 Windows 上，调用 Python 类工具前，先用 PowerShell 解析 `.venv.path`。
- 如果 Python 运行时可用，但 `.venv.path` 指向的环境还不存在，先初始化该虚拟环境，再继续后续工作。
- 在 Linux 上，调用 Python 类工具前，先用 bash 解析 `.venv.path`。

## 运行输出命名

- 当某个 skill 创建按次运行的输出根目录时，保留 skill 自己的父目录，并将本次输出根命名为 `exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/`。
- 时间戳必须紧跟在 `exec-` 后面，保证即使相邻步骤切换 skill，输出目录依然按时间可排序。

## 仓库方向

- Techne Loom 是一个 `.NET` 优先、面向多生态发布的 mono-repo，会在 `.NET`、Node.js、Python 三个生态维护平行包系。
- `AgentOrchestrator` 和 `SkillOrchestrator` 是两个生态位不同的独立产品。它们不会互相调用，也不能被描述成父子运行时关系。
- AO 在用户侧叙事、landing page 文案和 guide 定位中的产品名称统一使用 `Loom Agent Execution Orchestrator`。
- 这个用户侧名称不改变实现身份。`Techne.Loom.AgentOrchestrator`、`dotnet ao.dll`、`/loom-plan-execution`、源码路径和类型名都保持不变，除非任务明确要求做代码或包级重命名。
- 低层抽象可以对齐，但包身份、发布身份、产品对外契约必须保持独立。

## 包与目录布局

- 从一开始就按语言根目录组织源码：`/src/dotnet`、`/src/nodejs`、`/src/python`。
- 每个项目单元都必须是一个可发布包。
- 在 `.NET` 中，每个 `.csproj` 都对应一个 NuGet 包或 `dotnet tool` 包。
- 在 Node.js 中，每个带独立 `package.json` 的 package 目录都对应一个 npm 包。
- 在 Python 中，每个带独立 `pyproject.toml` 的包/构建单元都对应一个 PyPI distribution 或 wheel。
- 各生态里的包系按角色保持平行：`abstractions`、`common`、`agent-orchestrator`、`skill-orchestrator`。

## 文档与语言规则

- 对外文档默认全部双语。
- 文档树保持 `/docs/zh-cn` 与 `/docs/en` 镜像结构。
- `/demos` 下的 demo 索引与阶段性 `README.md` / `Readme.md` 也属于公开文档，必须让英文默认文件与同目录的中文 `.zh-CN.md` 镜像成对存在。
- 每一对双语页面都必须在页首提供互相链接。
- `.agents/skills/*/reference/` 下的 skill 本地 reference 文档必须只使用英文，避免多语言漂移并保证 skill 离线执行时的确定性。
- 当公开文档需要给“外部 target skill 根目录”举 agent 中立示例路径时，优先使用 `{agentskillfolder}/...`，不要写死仓库特定根目录。
- 只有在文档明确描述“本仓库内置 skill 根目录”或“本仓库内置 manifest catalog”时，才显式使用 `.agents/skills/...`。
- skill 的本地化叙述应放在 `/docs/en` 与 `/docs/zh-cn` 双语文档中，不要在 skill 本地 `reference/` 目录下维护多语言变体。
- 根目录必须维护双语文件：`README.md`、`CONTRIBUTING.md`、`CHANGELOG.md`、`SECURITY.md`、`AGENTS.md`。
- 根目录英文文件保留默认文件名，中文镜像统一使用 `.zh-CN.md` 后缀。
- 根目录双语文件也应该在页首互链。
- `AGENTS.md` 只保留在仓库根，不在 `/docs` 下复制。
- 产品 guide 的源文档固定放在 `/docs/<lang>/reference/products/ao-guide.md` 与 `/docs/<lang>/reference/products/so-guide.md`。
- SO product guide 是 `/loom-skill-enhancement` 以及每一个 Loom-governanced target skill 的仓库强制契约。其 transition、gate、seam ownership、output evidence 与 unattended mode 规则必须应用于 target-skill authoring、review、compile readiness 和 governed execution handoff；这条规则不扩展到 AO 行为或无关 workflow。
- 在 AO 面向用户的文档里，标题、开场定位、README 文案和 guide 导航优先使用 `Loom Agent Execution Orchestrator` 这个用户侧名称；`ao-guide.md`、`dotnet ao.dll` 和 package 标识继续保留为实现侧名称。
- 在文档 prose、标题和 callout label 中，禁止使用 `SO Governance`、`SO-enhanced`、`SO-governed` 这类旧叙事话术。
- 请改用 `Loom-governanced target skill`、`Loom Skill Orchestrator governance`、`Loom Skill Orchestrator-governanced skill`，或按当前切片要求使用更精确的执行状态话术。
- 对于文件名、命令名、package ID、schema field、template kind 等有意保留 `so` 命名的实现身份字面值，必须保持不变。
- `dotnet ao.dll --guide` 与 `dotnet so.dll --guide` 必须输出与当前版本匹配、可离线使用、由精选文档源生成的 guide 内容。
- 根目录的 package 获取索引固定为 `packages.released.md`、`packages.released.zh-CN.md`、`packages.beta.md`、`packages.beta.zh-CN.md`，skills 应通过绝对 GitHub URL 引用它们。
- released / beta 包获取指引都要把 NuGet.org 视为一等“最新包来源”；GitHub 托管包资产只保留为 NuGet.org 不可用时，或用户明确要求资产 URL 时的 fallback 下载路径。
- 对 AO 与 SO skills 来说，package 下载所使用的 channel 和精确 runtime version 必须跟随 skill 本地由 CI/CD 管理的 package version block，或跟随 checked-in runtime lock；不能在下载时再临时按用户口头选择一个 channel。只有在运行层面确实需要区分时，才从这个绑定版本推导 `released` 或 `beta`。
- 这些 package 获取索引除了包管理器安装命令外，还必须提供托管在 GitHub 上的 stable / beta 最新 release fallback 下载链接。
- MCP、CLI、skill 输入/输出契约文档属于一等交付物，不能只散落在 README prose 里。

## Package Version 治理规则

- 所有带 package version 的内容只能归入四类之一：live docs / indexes、skill-local offline references、checked-in runtime locks，或 historical demos / audit examples。
- live docs / indexes，例如根 README 的 release notes、`packages.released*.md`、`packages.beta*.md`、精确版本 NuGet 直达 URL 示例，以及 package 安装命令，必须反映各自 channel 的当前最新已发布版本，并应由 CI/CD publish workflows 统一刷新。
- `.agents/skills/*/reference/` 下的 skill-local offline references 是按 channel 固定的确定性快照，不是浮动 latest prose。在同一份 snapshot 内，version block、安装命令、精确版本 package URL、guide 示例，以及 `resolved_runtime_version` 示例都必须使用同一个 channel 对应的 snapshot version。
- `so-package-lock.json` 这类 checked-in runtime lock 是其所属 workflow / runtime surface 的权威版本来源。其顶层 resolved version、bundle 成员版本，以及相邻的 runtime binding prose，都必须保持为同一个精确版本，并与所属 skill contract 和 channel 一致。
- historical demos、audit artifacts，以及用于回溯叙述的材料，可以为了可复现性而保留旧 package version。但这些旧版本必须明确限定在 demo / audit surface 内，不能被表述成当前 latest-version guidance。
- 当某个当前 channel version 变化时，同一类别里的所有 version-bearing surface 都要一起更新：version blocks、安装命令、精确版本 URL、workflow refresh regex replacements，以及 lock file 的 resolved versions。
- 如果某个值已经由 CI/CD 管理的 version block、skill version block，或 checked-in runtime lock 持有，就不要再在其他 prose 里新增临时硬编码的“current” package version。

## Mermaid 图规则

- Markdown 里的 Mermaid 图属于一等文档表面，不是装饰性的补图。
- Mermaid 图必须对色盲读者保持可读。颜色只能增强语义，不能成为唯一语义通道。
- 当图中存在类别、阶段或语义节点类型时，节点标签必须增加第二语义通道：使用与节点意义贴近的 emoji，而不是通用彩色方块 emoji。
- emoji 必须尽量贴合节点意义。例如：`🧭` 表示 intake / 导航，`🔎` 表示 research / 检查，`💬` 表示用户 review / 讨论，`📝` 表示 drafting，`✅` 表示完成，`⚙️` 表示 runtime 执行，`📜` 表示 contract，`🧾` 表示审计证据，`❓` 表示 decision gate，`🚧` 表示 blocked / boundary，`🔁` 表示 continuation / loop。
- 当一个节点类别映射到一个 emoji 时，该图内这一类别的所有节点都应一致使用同一个 emoji。
- 如果嵌入式 legend subgraph 会扭曲版式、制造大块空白、或干扰主阅读路径，优先把图例放在 Mermaid 代码块外侧的 Markdown 中。
- 只有当图本身的版式明显受益时，才把 legend 放在图内；否则保持图例紧凑并放在图外。
- 相关文档中的 Mermaid 样式要保持语义稳定：同一概念家族在可行时应复用相同 emoji 和大致一致的颜色族。
- 在中文 Markdown 文档里，只要 Mermaid 节点出现英文术语或英文优先标签，就应在同一节点中另起一行追加中文对照，使用 `<br/>`，并保持英文在前、中文在后；但刻意保持原样的代码化术语除外。
- 当 Mermaid 标签包含双语文本、HTML 换行，或容易让解析器误判的标点时，应把标签文本放进引号，并保持一行一种语言，而不是继续使用单行 `English / 中文` 写法。
- 文件名、CLI token、字段名、协议值以及其他必须精确保真的实现身份字符串，不要强行做双语展开。

## Workflow 术语规则

- 整个 repo 的 workflow 术语根文档固定放在 `/docs/en/architecture/workflow-terminology.md` 与 `/docs/zh-cn/architecture/workflow-terminology.md`。
- AO / SO 的解释性 prose、guide、README，以及后续 schema 说明都要以这份术语表为准。
- 解释“向外交出控制权”和“带结构化结果回来继续”时，优先使用 **weave out** 与 **weave back**。
- 在 repo 文档里优先使用 **strand**，不要用 **thread**，避免和 `.NET` threading 术语冲突。
- 用 **seam** 表达概念层的所有权接缝；**boundary** 保留给显式的 wire / protocol surface，例如 `boundary_reason` 和 `<so_property>` 块内 `type: "boundary"` 的 envelope。
- 当解释性术语与当前实现字段名不一致时，第一次出现必须把两者都写清楚，并保留真实字段名。
- 任何产品文档若要引入新的 workflow 隐喻，必须先同步更新术语表及其双语镜像。

## Subagent 权威来源规则

- 当某个 skill 或 target skill 明确指定了某个 subagent markdown 文件，例如 `./assets/agents/<agent-name>.agent.md` 时，这个被指定的文件就是该 subagent 的权威行为来源。
- 不要求 skill-owned 或 target-skill-owned 的 `.agent.md` 文件先镜像到 `.github/agents/`、用户 profile agent 目录，或其他 discoverable agent root 之后才能使用。
- 如果运行时支持按精确 subagent 名直接解析，就直接调用该 subagent 名，但仍然要把被指定的 `.agent.md` 文件视为行为合同。
- 如果运行时不能按精确名称直接解析，就先解析被指定的 `.agent.md` 文件路径，并把解析后的文件路径与完整文件内容一起传入子代理驱动调用，确保执行仍受同一份合同约束。
- 解析 skill-owned 或 target-skill-owned 的 `.agent.md` 路径时，失败前必须先测试当前 repository/workspace 副本，再测试对应的全局已安装 skill 副本。
- 一旦某个路由已经指定了 `.agent.md` 文件，就不允许临时拼一个“近似角色”，也不允许脱离该文件去即兴改写 subagent 合同，或用 repository-global prose 替代该文件。

## README 定位

- 把 `README.md` 和 `README.zh-CN.md` 当成旗舰 landing page，而不只是技术索引。
- 有意识地使用 GitHub 支持的 rich Markdown：badges、alerts/callouts、对比表、Mermaid 图、架构图、用途场景和强定位文案。
- 文案可以强包装，但所有主张都必须能被当前实现和文档支撑。
- 当需要刷新术语、生态位描述或概念包装时，先做有边界的研究；必要时使用 `cto-web-research` 再改写 README 叙事。

## Guide 输出规则

- `dotnet so.dll --guide` 与 `dotnet ao.dll --guide` 必须从已发布 runtime 的内嵌资源安装与版本匹配的英文 `docs/en` 文档包，然后输出一个 JSON 对象，包含实际的 `version`、`docs_root` 与 `guide_path` 绝对路径。guide 路径是权威入口；只有 guide 无法消除疑问时，调用方才可以查看返回的 docs 根目录。命令只支持英文，并拒绝 `--lang`、`--section` 与 `--export`；每次契约变化都必须同步更新 CLI 文档、skill contract 与测试。
- 对于走 AO 或 SO 路线的 skill，一旦选定 package channel 或 runtime source，下一道硬门就是先证明所选 Loom runtime 真实可运行，并且能从该 runtime 成功产出一份新的 `--guide` 结果。在这份 `--guide` 结果存在之前，禁止进入规划、编写、校验、compile、run、resume，或任何后续输入收集步骤。
- 一旦新的 `--guide` 结果已经存在，就必须把这份 guide 当成一条硬性的 governance handoff，后续执行权威必须回到该 guide 对应的已发布 AO 或 SO 包 runtime 上。不要让 `--guide` 变成一条旁路：先读到 guide，随后又漂回仓库构建产物、手工拼装 runtime，或非治理执行路径。
- Guide 页首应包含版本、构建号与兼容性元数据。
- Guide 必须覆盖行为、职责、契约、模板、示例和反模式。
- Guide 既要适合人阅读，也要适合模型直接 ingest；当需要稳定抽取时，使用 `guide-contract`、`guide-template`、`guide-checklist`、`guide-example` 这类 fenced block 标签。
- Guide 与 reference 内容要显式列出 MCP 方法、CLI 参数、planner 流程、审计 artifact 路径，以及 skill 输入/输出 payload 形状。

## SO Workflow 校验规则

- 对于受 Loom 治理的 target-skill 模板，`dotnet so.dll compile` 与 workflow load 路径不能只做结构校验；它们还必须拒绝缺少 business-output gate、违反 seam ownership、或能只凭治理型证据到达 `done` 的 workflow。
- `AskUser` seam 只能请求 user-owned inputs 或 user-owned decisions。runtime-owned facts、runtime provenance，以及 system 生成的 artifact paths 都属于 `WaitResume` 或 blocked-resume payload 这类 runtime-owned seam，不属于用户提问面。
- route-aware workflow template 应为每条受治理 route 声明 business-output gates 与 blocked strongest-earned outputs，这样 compile/load 校验才能证明：在进入 `done` 之前，或在进入 runtime-owned wait boundary 之前，已经存在有意义的业务产物。

## External Result 与 Evidence Dataflow 规则

- External transition 必须使用一条明确的 projection contract：先校验 payload path，再相对于 payload 提取 `resumeOutputKey`，把提取后的值写入 `outputPath`，最后应用显式 `outputBindings`；受治理模板不得依赖隐式 wrapper 嵌套。
- `satisfiesGateIds` 与 `publishesOutputFamilies` 只是声明，不是 evidence。每个 required output family 都必须有可达 producer，并通过具体的 `outputPath` 或 `outputBindings` 投影到当前 workflow instance context，gate 才能通过。
- 当空字符串、空数组、空对象或 boolean 值具有业务含义时，受治理 gate 必须声明 required family 的 value semantics。校验和运行时诊断必须区分 evidence 缺失与 evidence 为空。
- `Failed` 或 `Succeeded` 状态的持久化 workflow instance 对 resume 来说是 terminal。恢复必须创建新的 external workflow copy，并保留失败实例、event log 与 audit evidence。
- 发布包 runtime 恢复必须在联网前先校验本地 cache 中锁定精确版本的完整三包 bundle；缺失或无效时只能下载该精确版本，禁止浮动到 latest。
- verified audit step 复制必须携带 `audit-reuse.json` provenance 与 `artifact_origin: verified-copy`；它只能保持审计展示连续性，不能替代 workflow 执行、event log、gate、guide 或 completion evidence。
- Enhancement plan 与可变运行 checklist 属于 execution output root 下的 per-run evidence，不是稳定 target-skill asset；completion manifest 可以引用它们，但不能把它们复制进 skill bundle。
- Published package-channel runtime preflight 必须在任何 guide 或 workflow command 之前校验 `so.dll`、`so.deps.json`、`so.runtimeconfig.json` 以及 dependency closure；缺少启动契约文件时必须判定 preflight 失败，绝不能写成成功 runtime evidence。

## 表达式合同规则

- 仓库不包含旧的表达式 evaluator 或语言值；当前只支持 `csharp`，任何其他语言值都必须 fail closed。
- 当前唯一已实现的表达式语言是 `csharp`，由 .NET runtime 中的 Roslyn 编译器求值。VB 与 F# 不受支持，不得作为语言值、evaluator 或未来候选加入。
- workflow template 声明根级 `runtimeBinding`（哪个 runtime/CLI 执行 workflow）与根级 `expressionBinding`（language、language version、contract id/version、`requiredExpressionCapabilities`、`compileFeedbackContract`）。`requiredExpressionCapabilities` 是唯一规范 capability 字段名；不得引入 `expressionCapabilities`、`expressionFeatureSet` 等平行命名。
- guard、succeed 与 gate pass 表达式使用结构化 `ExpressionDefinition` 形状（`kind`、`source`、`entryPoint`、`resultType`）。纯字符串只是兼容 shorthand，且必须伴随显式 C# binding 与 version；序列化器必须始终写出结构化形式。旧的非 C# 表达式源文本必须 fail closed，绝不允许被静默重解释为 C#。
- 不支持 per-node 或 per-gate 表达式语言 override。根级 binding 是唯一规范 binding；在混合语言 boundary 合同被显式批准前，不得添加局部 override 字段。
- 表达式仅同步：`async`、`await`、`Task` 一律拒绝。runtime 执行不可变编译后的布尔 delegate；compile 与 execute 生命周期内部分离，validator、compile、run、resume 必须全部经过同一 compiler/router。
- 表达式输入是已通过 review 与 compile 的受信任已检入模板。analyzer、引用白名单与只读 contract API 是对受信任代码的约束边界，不是恶意代码 sandbox。文档、guide 与诊断不得声称更强的隔离。
- `compile` 必须为每个表达式输出详细、结构化的 `ExpressionCompileFeedback`：status、language 与 version、contract identity、workflow/gate/transition/field 位置、source span、稳定 diagnostic code 与 category（syntax、semantic、contract、security、reference、resource）、severity、可行动 message、suggested fix、referenced symbols、compiler identity。成功结果也必须记录解析后的 kind、entry point、result type、capabilities 与 warnings。仅透传原始 compiler 文本不构成合格诊断。
- 任何未来受支持的表达式语言与 runtime 必须先实现同一 `detailedCompileFeedbackV1` 合同才能被标记为 supported。这是 Rust+CEL 架构文档与任何未来 Node.js/Python adapter 合同的强制章节；原样透传宿主解释器异常不满足该合同。
- Rust+CEL 是第四 runtime 路线：未来跨平台 Loom runtime core（Rust）以 CEL 作为规范表达式语言。它不是 Rust 代码执行，也不是 Lua 方案。其双语架构文档必须复用规范字段 `runtimeBinding`、`expressionBinding`、`ExpressionDefinition`、`requiredExpressionCapabilities`、`compileFeedbackContract`、`ExpressionCompileFeedback`；不得发明平行 schema。
- Node.js 与 Python 保持 adapter/生态路线。它们不因宿主语言自动成为表达式语言；除非批准带 `detailedCompileFeedbackV1` 的正式语言合同，否则不得实现独立 evaluator。
- 跨语言或跨 runtime 迁移是 skill 的责任：翻译表达式 source、更新 binding/version/contract/capabilities，并保留证据（source、translated source、翻译 agent/tool、review、compile feedback）。runtime 永不自动翻译表达式。

## Loom Skill Enhancement 治理规则

- `/loom-skill-enhancement` 修改目标 skill 前必须先计划：先分析目标 skill 的输入、输出、节点、guard、分支、循环、用户 seam、运行时 seam、gate 和输出证据，再开始编写 target-skill deliverables。
- `/loom-skill-enhancement` 自身以及每个 Loom-governanced target skill 都被强制走上 Loom Skill Orchestrator-governanced route：任何 step transition 都不得推进，直到它在精确的外部 runtime workflow copy 上通过 boundary check，然后收到针对该下一步的显式批准或结构化续行指示。compile-clean 只是前置条件；推断意图、纯 prose、过期 guide result、未经批准的 draft copy、local orchestration 以及直接 workflow JSON edits 都绝不是有效续行。
- workflow template JSON 是 review 与执行的权威。Mermaid、HTML 和本地化 plan 文案都是从 template 生成或与 template 对齐的展示层；用户反馈必须回写 workflow template 或其源计划输入，不能只修改渲染后的 Mermaid。
- 对 `/loom-skill-enhancement` 自举，以及以完整交付为目标的受 Loom 治理 target-skill 增强运行，默认成功路径必须复制运行时 workflow 副本，并沿公开 `dotnet so.dll run` / `dotnet so.dll resume` 链路继续到最终 `Done`；compile-review 完成、blocked seam，或 compile-ready 措辞都不能作为正常完成态。
- 对 `/loom-skill-enhancement` 自举和以完整交付为目标的受 Loom 治理 target-skill 增强切片，不要保留 `compile-only` 或 `compile-ready governance integration` 作为默认或例外完成路径，除非用户在实现开始前于当前会话中显式改写任务合同。
- 当受治理 route 包含 business-intake 或 `AskUser` seam 时，完成要求是在同一运行时 workflow-copy lineage 上 weave back through 这些 seam 直到最终 `Done`；到达 blocked seam 只是 strongest-earned blocked evidence，不是完成。
- workflow 可视化应携带稳定的节点类型语义。浅色系保持一致：AI/model/subagent 工作用绿色系，代码/工具工作用蓝色系，用户可选决策用黄色系，必须中途用户输入用红色系，必要 gate/governance 状态用白色或极浅灰色。
- skill-enhancement 完成证据必须包含最终 workflow template、生成的 Mermaid、node-to-file 或 node-to-artifact 映射、实际 implementation/audit 证据，以及被修改的 target-skill deliverables。仅有 runtime validation 不能算完成。
- loom-skill-enhancement 升级的第一步是可复用基础能力：plan mode、workflow 分析、template 生成、compile 生成的 Mermaid、确认循环、node-to-file 映射、最终证据报告，以及普通目标 skill 继续使用现有 latest-package 行为。
- 第二步是自举：第一步完成独立 review/fix/validate/commit 后，`/loom-skill-enhancement` 才能消费这些基础能力，把自己升级为 Loom-governanced。自举执行过程可以使用当前仓库 `src` 编译结果，并且只把 local runtime manifest 记录到 audit root；但自举产出的未来官方 skill 行为仍必须恢复 latest package/channel runtime 与 package-lock 语义。
- 自举备份发生在第一步提交之后、第二步修改之前。除非用户明确要求更大快照，否则只把 loom-skill-enhancement 的 skill-local 文件备份到 audit root。

## 审计 Artifact 规则

- Workflow 审计输出不是可选展示辅助，而是按步骤保存的审计记录。
- 除非用户明确指定审计输出目录，否则默认使用临时输出根目录。
- compile 产物、audit artifacts 以及其他运行时临时文件，默认都不得落在 skill 目录下，也不得默认落在 `assets/so-workflow/` 之下；除非用户明确指定位置，否则应放在运行时临时根目录或 repo 根临时目录。
- 审计产物、中间 workflow 物化文件，以及可在对话或 think-out-loud 中引用的运行输出，都可以在交流时被引用；但默认仍必须放在运行时临时根目录、repo 根临时目录，或用户明确指定的 execution output 根目录下，不能默认放进任何 skill 文件夹。
- 审计产物按 `{output}/wf-{wfid}/step-{seq}-{action}/` 落盘。
- 每个 step 目录都必须包含该时刻的 Mermaid Markdown、HTML 和 workflow JSON 备份。
- compile 与 audit 流程不得就地覆盖已有 artifact 文件；一旦目标文件已存在，必须以富错误形式失败，报告冲突路径集合，并提示调用方更换输出根或先清理目标目录。

## 执行顺序与评审提交节奏

- 在更大范围实现前，先更新 `AGENTS.md` 与 `AGENTS.zh-CN.md`，确保语言、文档和执行规则是最新的。
- 每完成一个大的实现切片，都必须先跑一次合理的 `cto-review-and-commit` review/fix/validate/commit 流程，再进入下一个切片。
- 这个节奏默认是硬 gate，不是软建议：除非用户明确覆盖，否则不要跨越多个大切片一路累计更改，最后再一次性 review。
- 每次 review-and-commit 的切片要保持在“有证据可审”的规模内。默认规划规则是：单次切片通常应控制在 50 个变更文件以内；如果待提交范围已经接近这个规模，就先停下来跑 `cto-review-and-commit`，不要继续堆更多文件。
- 即使文件数还不到 50，只要这一切片涉及协议契约、schema、包接缝或运行时控制行为变化，也要立刻进入 `cto-review-and-commit`。
- 大切片包括：根 AGENTS 规则、旗舰 README landing page、文档骨架、包骨架、协议/schema 变更以及代码实现。
- 除非用户明确覆盖这一节奏，否则不要带着未评审、未提交的更改直接进入下一个大切片。
