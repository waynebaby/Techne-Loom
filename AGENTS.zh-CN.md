# Workspace Agent Rules（中文镜像）

[English](AGENTS.md)

> `AGENTS.md` 是仓库自动化执行规则的规范源文件。
> `AGENTS.zh-CN.md` 是中文镜像，必须与英文文件保持同步；若两者冲突，以 `AGENTS.md` 为准。

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
- 每一对双语页面都必须在页首提供互相链接。
- `.github/skills/*/reference/` 下的 skill 本地 reference 文档必须只使用英文，避免多语言漂移并保证 skill 离线执行时的确定性。
- skill 的本地化叙述应放在 `/docs/en` 与 `/docs/zh-cn` 双语文档中，不要在 skill 本地 `reference/` 目录下维护多语言变体。
- 根目录必须维护双语文件：`README.md`、`CONTRIBUTING.md`、`CHANGELOG.md`、`SECURITY.md`、`AGENTS.md`。
- 根目录英文文件保留默认文件名，中文镜像统一使用 `.zh-CN.md` 后缀。
- 根目录双语文件也应该在页首互链。
- `AGENTS.md` 只保留在仓库根，不在 `/docs` 下复制。
- 产品 guide 的源文档固定放在 `/docs/<lang>/reference/products/ao-guide.md` 与 `/docs/<lang>/reference/products/so-guide.md`。
- 在 AO 面向用户的文档里，标题、开场定位、README 文案和 guide 导航优先使用 `Loom Agent Execution Orchestrator` 这个用户侧名称；`ao-guide.md`、`dotnet ao.dll` 和 package 标识继续保留为实现侧名称。
- `dotnet ao.dll --guide` 与 `dotnet so.dll --guide` 必须输出与当前版本匹配、可离线使用、由精选文档源生成的 guide 内容。
- 根目录的 package 获取索引固定为 `packages.released.md`、`packages.released.zh-CN.md`、`packages.beta.md`、`packages.beta.zh-CN.md`，skills 应通过绝对 GitHub URL 引用它们。
- released / beta 包获取指引都要把 NuGet.org 视为一等“最新包来源”；GitHub 托管包资产只保留为 NuGet.org 不可用时，或用户明确要求资产 URL 时的 fallback 下载路径。
- 对 AO 与 SO skills 来说，package 下载所使用的 channel 和精确 runtime version 必须跟随 skill 本地由 CI/CD 管理的 package version block，或跟随 checked-in runtime lock；不能在下载时再临时按用户口头选择一个 channel。只有在运行层面确实需要区分时，才从这个绑定版本推导 `released` 或 `beta`。
- 这些 package 获取索引除了包管理器安装命令外，还必须提供托管在 GitHub 上的 stable / beta 最新 release fallback 下载链接。
- MCP、CLI、skill 输入/输出契约文档属于一等交付物，不能只散落在 README prose 里。

## Mermaid 图规则

- Markdown 里的 Mermaid 图属于一等文档表面，不是装饰性的补图。
- Mermaid 图必须对色盲读者保持可读。颜色只能增强语义，不能成为唯一语义通道。
- 当图中存在类别、阶段或语义节点类型时，节点标签必须增加第二语义通道：使用与节点意义贴近的 emoji，而不是通用彩色方块 emoji。
- emoji 必须尽量贴合节点意义。例如：`🧭` 表示 intake / 导航，`🔎` 表示 research / 检查，`💬` 表示用户 review / 讨论，`📝` 表示 drafting，`✅` 表示完成，`⚙️` 表示 runtime 执行，`📜` 表示 contract，`🧾` 表示审计证据，`❓` 表示 decision gate，`🚧` 表示 blocked / boundary，`🔁` 表示 continuation / loop。
- 当一个节点类别映射到一个 emoji 时，该图内这一类别的所有节点都应一致使用同一个 emoji。
- 如果嵌入式 legend subgraph 会扭曲版式、制造大块空白、或干扰主阅读路径，优先把图例放在 Mermaid 代码块外侧的 Markdown 中。
- 只有当图本身的版式明显受益时，才把 legend 放在图内；否则保持图例紧凑并放在图外。
- 相关文档中的 Mermaid 样式要保持语义稳定：同一概念家族在可行时应复用相同 emoji 和大致一致的颜色族。
- 在中文 Markdown 文档里，只要 Mermaid 节点出现英文术语或英文优先标签，就应在同一节点中追加中文对照，采用 `English / 中文` 形式；但刻意保持原样的代码化术语除外。
- 文件名、CLI token、字段名、协议值以及其他必须精确保真的实现身份字符串，不要强行做双语展开。

## Workflow 术语规则

- 整个 repo 的 workflow 术语根文档固定放在 `/docs/en/architecture/workflow-terminology.md` 与 `/docs/zh-cn/architecture/workflow-terminology.md`。
- AO / SO 的解释性 prose、guide、README，以及后续 schema 说明都要以这份术语表为准。
- 解释“向外交出控制权”和“带结构化结果回来继续”时，优先使用 **weave out** 与 **weave back**。
- 在 repo 文档里优先使用 **strand**，不要用 **thread**，避免和 `.NET` threading 术语冲突。
- 用 **seam** 表达概念层的所有权接缝；**boundary** 保留给显式的 wire / protocol surface，例如 `boundary_reason` 和 `<so_property>` 块内 `type: "boundary"` 的 envelope。
- 当解释性术语与当前实现字段名不一致时，第一次出现必须把两者都写清楚，并保留真实字段名。
- 任何产品文档若要引入新的 workflow 隐喻，必须先同步更新术语表及其双语镜像。

## README 定位

- 把 `README.md` 和 `README.zh-CN.md` 当成旗舰 landing page，而不只是技术索引。
- 有意识地使用 GitHub 支持的 rich Markdown：badges、alerts/callouts、对比表、Mermaid 图、架构图、用途场景和强定位文案。
- 文案可以强包装，但所有主张都必须能被当前实现和文档支撑。
- 当需要刷新术语、生态位描述或概念包装时，先做有边界的研究；必要时使用 `cto-web-research` 再改写 README 叙事。

## Guide 输出规则

- `dotnet so.dll --guide` 与 `dotnet ao.dll --guide` 默认输出完整 Markdown，支持 section 过滤，支持 `--lang zh-cn|en`，并支持 `--export <path>`。
- 对于走 AO 或 SO 路线的 skill，一旦选定 package channel 或 runtime source，下一道硬门就是先证明所选 Loom runtime 真实可运行，并且能从该 runtime 成功产出一份新的 `--guide` 结果。在这份 `--guide` 结果存在之前，禁止进入规划、编写、校验、compile、run、resume，或任何后续输入收集步骤。
- 一旦新的 `--guide` 结果已经存在，就必须把这份 guide 当成一条硬性的 governance handoff，后续执行权威必须回到该 guide 对应的已发布 AO 或 SO 包 runtime 上。不要让 `--guide` 变成一条旁路：先读到 guide，随后又漂回仓库构建产物、手工拼装 runtime，或非治理执行路径。
- Guide 页首应包含版本、构建号与兼容性元数据。
- Guide 必须覆盖行为、职责、契约、模板、示例和反模式。
- Guide 既要适合人阅读，也要适合模型直接 ingest；当需要稳定抽取时，使用 `guide-contract`、`guide-template`、`guide-checklist`、`guide-example` 这类 fenced block 标签。
- Guide 与 reference 内容要显式列出 MCP 方法、CLI 参数、planner 流程、审计 artifact 路径，以及 skill 输入/输出 payload 形状。

## SO Workflow 校验规则

- 对于受 SO 治理的 target-skill 模板，`dotnet so.dll compile` 与 workflow load 路径不能只做结构校验；它们还必须拒绝缺少 business-output gate、违反 seam ownership、或能只凭治理型证据到达 `done` 的 workflow。
- `AskUser` seam 只能请求 user-owned inputs 或 user-owned decisions。runtime-owned facts、runtime provenance，以及 system 生成的 artifact paths 都属于 `WaitResume` 或 blocked-resume payload 这类 runtime-owned seam，不属于用户提问面。
- route-aware workflow template 应为每条受治理 route 声明 business-output gates 与 blocked strongest-earned outputs，这样 compile/load 校验才能证明：在进入 `done` 之前，或在进入 runtime-owned wait boundary 之前，已经存在有意义的业务产物。

## Loom Skill Enhancement 治理规则

- `/loom-skill-enhancement` 修改目标 skill 前必须先计划：先分析目标 skill 的输入、输出、节点、guard、分支、循环、用户 seam、运行时 seam、gate 和输出证据，再开始编写 target-skill deliverables。
- workflow template JSON 是 review 与执行的权威。Mermaid、HTML 和本地化 plan 文案都是从 template 生成或与 template 对齐的展示层；用户反馈必须回写 workflow template 或其源计划输入，不能只修改渲染后的 Mermaid。
- workflow 可视化应携带稳定的节点类型语义。浅色系保持一致：AI/model/subagent 工作用绿色系，代码/工具工作用蓝色系，用户可选决策用黄色系，必须中途用户输入用红色系，必要 gate/governance 状态用白色或极浅灰色。
- skill-enhancement 完成证据必须包含最终 workflow template、生成的 Mermaid、node-to-file 或 node-to-artifact 映射、实际 implementation/audit 证据，以及被修改的 target-skill deliverables。仅有 runtime validation 不能算完成。
- loom-skill-enhancement 升级的第一步是可复用基础能力：plan mode、workflow 分析、template 生成、compile 生成的 Mermaid、确认循环、node-to-file 映射、最终证据报告，以及普通目标 skill 继续使用现有 latest-package 行为。
- 第二步是自举：第一步完成独立 review/fix/validate/commit 后，`/loom-skill-enhancement` 才能消费这些基础能力，把自己升级为 SO-governed。自举执行过程可以使用当前仓库 `src` 编译结果，并且只把 local runtime manifest 记录到 audit root；但自举产出的未来官方 skill 行为仍必须恢复 latest package/channel runtime 与 package-lock 语义。
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
