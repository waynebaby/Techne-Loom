# Skills 输入输出参考

[English](../../en/reference/skills.md) | [根目录](../README.md)

如果你想先看操作者视角的 usage、demo 和入口选择，请先阅读 [使用 Techne Loom Skills](../guides/skill-usage.md)。

## 语言策略

- `.agents/skills/*/reference/` 下的 skill 本地 reference 文档必须只使用英文，以保证离线执行与维护一致性
- 仓库文档 `docs/en` 与 `docs/zh-cn` 必须保持中英双语镜像，用于公开文档表面
- 如果 skill 需要本地化说明，应放在 `docs/` 双语文档中，而不是在 skill 本地 `reference/` 目录新增非英文版本

## Loom-bin 共享规则

- Loom Agent Plan-Execution Orchestrator skill、SO skill，以及任何采用 Loom bin skill 体系的目标产品，都必须在自己的 skill 文档或产品文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- Loom Agent Plan-Execution Orchestrator skill、SO skill，以及任何采用 Loom bin skill 体系的目标产品，都必须在包获取指引里把 NuGet.org 视为一等“最新包来源”，同时保留 released / beta package index 的绝对 URL 与 GitHub asset fallback links
- Released package index URL（English canonical）：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL（English canonical）：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL（zh-CN mirror）：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL（zh-CN mirror）：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## Workflow 文件语言



在 AO、SO 以及受 Loom Skill Orchestrator 治理的 skill being enhanced 中，workflow 定义文件是规范英文信息载体。workflow 自己拥有的 schema key、node 和 transition 名称/描述、workflow phase、expression、hint、failure guidance、evidence reference 以及 control metadata 必须使用英文。用户/业务 payload 可以保留来源语言，面向用户的输出可以使用请求语言；本地化属于展示层，不能改变 workflow key 或控制语义。
## Runtime 选择

AO 与 SO 只发布自包含的 product+RID runtime packages。使用 owning skill 绑定的精确版本；宿主根据操作系统、CPU 架构和 Linux libc 选择一个受支持的 RID。获取包时不探测 `dotnet` host，也不选择 DLL 模式。

- 只复用标准 NuGet global-packages cache 中通过校验的精确包；否则校验精确 registration 响应中的 `catalogEntry.packageHash`。
- GitHub 回退只能使用同版本 Release asset，且对应 `.sha512` sidecar 必须通过校验。不要使用浮动包别名。
- 解压前校验 package ID、版本、RID、nuspec、`runtime.json`、压缩包路径和大小、apphost 与英文 guide 文件。
- 安全解压到 skill 目录之外，并在 Windows 直接运行 `ao.exe --guide`/`so.exe --guide`，在 Unix 直接运行 `ao --guide`/`so --guide`。这是后续工作的第一条 runtime 命令。
- MCP 仅用于后续步骤的可选支持，不会阻止获取 package、运行 guide 或官方 CLI run/resume。

## `/loom-plan-execution`

### /loom-plan-execution 使命

这是一个以 guide 和环境配置为先的计划执行入口，围绕 plan-execution package flow 工作。

本 skill 也遵循 Loom Agent Plan-Execution Orchestrator 的治理契约：AO 是该 skill 唯一正式执行 authority；Windows 使用直接 `ao.exe run`/`ao.exe resume`，Unix 使用 `ao run`/`ao resume`。

### /loom-plan-execution 输入

- 丰富的计划文本，建议至少 10 行非空内容
- 或详细的计划文件路径
- package 通道选择：released 或 beta
- guide 表面只提供英文：Windows 直接运行 `ao.exe --guide`，Unix 直接运行 `ao --guide`；解析返回的 `version`、`docs_root`、`guide_path`，并读取对应文件
- 可选 runtime source mode：默认是 `package-channel`；当你正在当前仓库里调试这个 skill 并且明确要求使用当前源码输出时，可显式传 `repo-src-debug`
- 可选审计输出路径

### /loom-plan-execution 默认假设
- 使用与当前 CI/CD skill 版本区块匹配的 released 或 beta package index 绝对 URL 作为获取指南
- 获取 AO package 前，按[平台检测步骤](runtime/platform-detection.md)检测唯一 RID，并且只获取精确版本的 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package
- 优先复用标准 NuGet cache 中通过校验的精确包；下载时校验 registration hash 或同版本 GitHub `.sha512` sidecar
- 用户明确要求在本仓库调试时，才可使用 `repo-src-debug` 构建 AO 源项目；它不能替代正式的 package runtime
- 需要集成 Loom runtime 的产品应保留 released/beta package index 绝对 URL 和对应的本地化镜像
- 把直接 apphost 返回的 fresh `--guide` 作为规划和后续工作的版本权威
- 本 skill 的正式执行 authority 是 AO；AO workflow 使用落盘、sessionless 的状态
- 官方执行仅使用 Windows `ao.exe run`/`ao.exe resume` 或 Unix `ao run`/`ao resume`
- apphost `compile`、`--guide`、`prompt-plan` 和 `prompt-replan` 仅用于准备或校验，不是正式运行
- plan、workflow copy、session state、audit 和中间文件都放在 skill 目录之外
- 保持 checked-in plan 和 workflow snapshot 不变；业务产出优先，AO artifact 不能替代业务交付物
### /loom-plan-execution 输出预期
- AO 绑定版本及推导出的 released/beta channel
- released/beta package index 绝对链接及本地化镜像
- 生效的 runtime source；只有明确调试本仓库时才使用 `repo-src-debug`
- fresh guide surface 引用
- 精确 package ID/版本/RID/hash、压缩包校验、解压结果、apphost 路径和 guide evidence
- 可选的外部 workflow JSON 或 `WorkflowInstance` 路径，以及 AO apphost 校验结果
- runtime 返回 payload、event log 和 audit artifact 链接
- skill 目录之外的 external workflow、session 和 audit 路径
- 明确区分不可变的 checked-in plan/snapshot 与外部 AO runtime 状态
- think-out-loud 中的 package identity 和已验证 Mermaid/HTML/Analysis/Dataflow artifact
- 以 AO workflow 和审计产物为依据的 history、checklist、run map、evidence 与完成报告
### /loom-plan-execution 运行时衔接
- Windows 直接运行 `ao.exe --guide`，Unix 运行 `ao --guide`；先解析 JSON 并读取 `guide_path`，再继续后续工作
- 只有用户明确要求调试当前仓库时，才构建 AO 源项目用于 `repo-src-debug`
- package-channel 执行只获取并校验精确 AO product+RID package；安全解压后，guide、compile、prompt-plan、prompt-replan、run 和 resume 都使用同一个 apphost
- 先准备 objective/context 输入；`prompt-plan` 返回 AO planner 文本和用于 WorkflowInstance 编写的 typed blocks
- `consumption_requirement = required` 的 blocks 是必需输入；`optional` blocks 仅供参考
- 在 skill 目录外编写 WorkflowInstance，用 apphost compile 校验；需要时让 run 使用同一实例
- AO blocked 后，使用 `prompt-replan` 取得结构化上下文并修改同一 workflow instance，再使用相同 apphost resume
- 官方 skill run 只使用 direct apphost `run`/`resume`，并保持 workflow copy 和持久状态不变直到完成
- blocked run 要保留失败、event 和 audit evidence；输出放在 skill 目录之外
- compile 和 audit 不覆盖已有文件；每次 AO 进度更新只展示已验证的 Mermaid 和 HTML 路径
## `/loom-skill-enhancement`

### /loom-skill-enhancement 使命

这是一个以 guide 为先的 deterministic skill 创建 / 升级入口，围绕 Loom Skill Orchestrator package flow 工作。

当目标 skill 已经暴露出 Loom Skill Orchestrator governance 信号时，这个 skill 必须在一次增强过程中把它升级成一个排他采用 Loom Skill Orchestrator 治理的 skill，而不是停留在一般性的 Loom Skill Orchestrator 支持补充或文档补全。

### /loom-skill-enhancement 输入

- 目标 skill 路径或目标 skill 仓库路径
- 确定型 skill 目标 / 改造请求
- 本次增强中必须创建或修改的目标 skill 变更项
- runtime 版本依据：复用 checked-in 的 `assets/so-workflow/so-package-lock.json` 与当前 skill package version block，需要区分 released 或 beta 时再从这个绑定版本推导
- guide 表面只提供英文：Windows 直接运行 `so.exe --guide`，Unix 运行 `so --guide`；解析 JSON 中的 `version`、`docs_root`、`guide_path` 并读取返回文件
- 可选 JSON context 文件
- 可选审计输出路径

### /loom-skill-enhancement 默认假设
- 使用与绑定 SO 版本匹配的 released/beta package index 绝对 URL 作为获取指南
- 每次增强前，从 `assets/so-workflow/so-package-lock.json` 读取精确版本；检测一个 host RID；获取、校验并安全解压对应的 SO package；直接运行 apphost `--guide` 后再编辑或收集后续输入
- 优先复用标准 NuGet cache 中通过校验的精确包；否则校验 NuGet registration SHA-512 或同版本 GitHub `.sha512` sidecar。不要添加固定 bootstrap script 或 Loom 专用缓存
- 把稳定模板、lock、reference 和 map 放在目标 skill 的 `assets/so-workflow/`；plan 和可变运行文件放在外部 execution output 根目录
- checked-in workflow template 保持不可变；每次新运行先复制到一个外部 workflow file，所有 resume 继续使用该副本和同一持久状态
- package lock 只记录精确 SO 版本；RID、apphost、hash 和解压路径是临时 runtime evidence
- 不要把 `Common` 或 `Abstractions` 当作运行时包获取；发布的 SO RID package 已包含自包含 runtime
- 只有后续步骤确实需要 workflow 内容时才检查有界片段；MCP 和 fragment inspection 都不是 package 或 guide 的前置条件
- 后续步骤可以按需使用绑定当前 apphost identity 的本地 MCP；compile/run/resume 仍通过同一个 direct apphost
- 所有 CLI 输入都先准备成完整、关闭的文件；可变输出放在 skill 目录之外
- workflow 设计必须保留 guide 对齐、业务输出 gates、seam ownership 和逐步 boundary check
- `AskUser` 只请求用户拥有的选择；runtime facts 放在 runtime-owned output
- re-enhancement 必须在 `local_patch`、`structural_refactor` 和 `full_regeneration` 中明确选择，并对照 fresh guide
- 使用同一个精确发布 apphost 生成 schema/demo、compile、run 和 resume；compile 仅用于校验，不代表完成
- SO 治理下的正式运行仅使用 Windows `so.exe run`/`so.exe resume` 或 Unix `so run`/`so resume`
- package、校验、解压、启动或 guide 检查失败时停止并保留失败证据；不能改用 repository build 或其他包
- 不要把多步计划藏在一个 workflow node 中；拆分为明确、可审查的步骤
### /loom-skill-enhancement 输出预期
- released/beta package index 链接和本地化镜像
- 精确 SO lock 版本的 fresh guide 结果与返回路径
- 精确 package ID/版本/RID、校验 hash、压缩包检查、解压结果和 apphost 路径
- checked-in package lock 路径及引用该锁的 runtime evidence
- workflow template、治理 validation contract、route-aware business-output gates 和 seam ownership evidence
- runtime-owned compile feedback、Mermaid、HTML、analysis 与 dataflow 输出
- 外部 workflow copy、event log、resume 链路、boundary-check trail 和 audit 链接
- review、repair、post-fix validation evidence 与 completion manifest
- 明确区分 checked-in source deliverables、runtime 临时文件和 runtime manifest
- 正式完成时提供同一 apphost 命令链与最终 workflow 状态
- 只有后续步骤实际使用 MCP 时才提供对应可选证据
- 确认 workflow node 没有隐藏多步计划
### /loom-skill-enhancement 运行时衔接
- Windows 直接运行 `so.exe --guide`，Unix 运行 `so --guide`，作为第一个 SO runtime 操作；校验 fresh JSON 与可读 guide path 后再继续
- 使用宿主已有工具获取并校验 lock 中的精确 SO product+RID package；不需要 runtime resolver 或固定 bootstrap script
- 直接使用解压后的同一个 `so.exe`/`so` 执行 compile、run 和 resume；保持 apphost、外部 workflow copy 与持久状态一致
- 每次新运行前把 checked-in template 复制为新的外部 workflow copy；执行时不修改源模板
- Compile 只校验模板；完整交付还需在同一 workflow copy 上通过 direct apphost run/resume 到 `Done`
- 后续步骤确实需要时才使用绑定当前 apphost 的本地 MCP；guide 和官方执行不依赖 MCP 注册或 fragment inspection
- 保留 route-aware business outputs、用户/runtime seam ownership、boundary checks、失败历史、event log 与已验证 audit 链接
- package、hash、manifest、archive、解压、apphost 启动或 guide 检查失败时停止；不能把失败说成成功，也不能切换到其他 runtime
