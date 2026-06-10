# Skills 输入输出参考

[English](../../en/reference/skills.md)

## Loom-bin 共享规则

- AO skill、SO skill，以及任何采用 Loom bin skill 体系的目标产品，都必须在自己的 skill 文档或产品文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- Released package index URL（English canonical）：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta package index URL（English canonical）：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released package index URL（zh-CN mirror）：<https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta package index URL（zh-CN mirror）：<https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

## `/loom-plan-execution`

### 使命

这是一个以 guide 和环境配置为先的计划执行入口，围绕 plan-execution package flow 工作。

### 输入

- 丰富的计划文本，建议至少 10 行非空内容
- 或详细的计划文件路径
- package 通道选择：released 或 beta
- 可选语言界面：`en` 或 `zh-cn`；如果不传，当前公开 guide 表面默认回退到 `en`，所以需要中文 guide link 时，应显式传 `zh-cn`，并在执行 guide 命令时传入 `--lang <language>`
- 可选审计输出路径

### 默认假设

- 默认把与所选语言界面匹配的 released / beta package index 绝对 URL 作为获取 AO package 的事实来源
- 默认要求任何采用 Loom bin skill 体系的目标产品，在自己的文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- 默认把 `dotnet ao.dll --guide [--lang <language>]` 视为权威运行入口，而不是在 skill 中复制一套私有执行模板

### 输出预期

- package / 通道选择确认
- 绝对 package index links
- released / beta package index link 集合；如果存在本地化页面，也要包含对应镜像
- guide surface 引用
- planner 产出的 workflow JSON 路径
- runtime 返回 payload links，包括 audit artifacts

### 运行时衔接

- 以 `dotnet ao.dll --guide [--lang <language>]` 为事实来源
- 通过 `dotnet ao.dll planner` 物化 workflow JSON
- 通过 `dotnet ao.dll run` / `resume` 执行
- blocked 之后根据返回的 workflow JSON frontier 继续

## `/loom-skill-enhancement`

### 使命

这是一个以 guide 为先的 deterministic skill 创建 / 升级入口，围绕 SO package flow 工作。

### 输入

- 目标 skill 路径或目标 skill 仓库路径
- 确定型 skill 目标 / 改造请求
- package 通道选择：released 或 beta
- 可选语言界面：`en` 或 `zh-cn`；如果不传，当前公开 guide 表面默认回退到 `en`，所以需要中文 guide link 时，应显式传 `zh-cn`，并在执行 guide 命令时传入 `--lang <language>`
- 可选 JSON context 文件
- 可选审计输出路径

### 默认假设

- 默认把与所选语言界面匹配的 released / beta package index 绝对 URL 作为获取 SO package 的事实来源；如果执行时需要本地二进制，则按已选 package 通道把对应 runtime 安装或解包到目标仓库外部的临时目录
- 默认要求任何采用 Loom bin skill 体系的目标产品，在自己的文档里保留 released / beta package index 的绝对 URL；如果产品提供本地化 package index 页面，则应保留对应语言镜像的绝对 URL
- 默认把 SO 相关材料放在 `<target-skill-root>/assets/so-workflow/`
- 默认在目标 `SKILL.md` 已存在时根据它和补充 references 生成 `<target-skill-root>/assets/so-workflow/skill-plan.md`；如果是新建 skill，则改为根据 `goal` 和补充 references 生成
- 如果存在 `references/*.md`，默认用“简单拼接 + 清晰分隔头”的方式生成临时 `merged-context.md` 工作文件，再把需要的内容转换成临时 JSON context 文件作为 planner context 输入
- 默认把 workflow template 独立存放，并把运行产物写到 `<target-skill-root>/assets/so-workflow/audit/`
- 默认把 workflow template 的正确性放在绝对优先级：生成出来的 workflow JSON template 必须完整、详细、与所选通道 guide 对齐，并且先通过当前公开 SO workflow-file load/status 门槛，之后才可以成为增强后目标 skill 的执行依据
- 默认把增强后的 `SKILL.md` 压缩到约 80-100 行，同时保留高层步骤、guardrail 标题、SO 指引以及 `## Workflow Contract` 标题
- 如果 released 通道并没有真正提供同等 SO 增强产物，released 文案默认标注为 Beta Only
- weave-out 时，默认先用 `current_step_kind` 等结构化 blocked payload 字段判断等待类别，再把 `skill_hint` 按字面当作下一步外部动作指令消费：只有结构化字段明确要求人类输入 seam 时才询问用户；如果它表明正在等待邮件、文件、消息或下游脚本结果，则把它视为合法的外部等待状态，向用户返回下一步所需输入形状，或等待外部结果到达后再 `resume`；只有结构化字段加上字面 `skill_hint` 一起明确指向非人类续行时，才默认由 agent 自动继续
- 这些规则默认只是 skill 层的改造约定，不应自动当成通用 SO runtime 契约；如果所选通道 guide 没有公开等价表面，则应明确标成 Beta Only

### 输出预期

- package / 通道选择确认
- 绝对 package index links
- released / beta package index link 集合；如果存在本地化页面，也要包含对应镜像
- guide surface 引用
- planner 产出的确定型 workflow 模板路径；只有在 guide 对齐审查加上 SO workflow-file load/status 门槛都通过之后，这个模板才是增强后目标 skill 的执行依据
- runtime 返回 payload links，包括 audit artifacts

### 运行时衔接

- 以 `dotnet so.dll --guide [--lang <language>]` 为事实来源
- 由 AI agent 直接在终端执行 `dotnet so.dll planner` / `run` / `resume`
- 通过 `dotnet so.dll planner` 基于细粒度 description 文件物化 workflow JSON；description 文件仅用于 planner 输入
- 在把模板当作执行依据之前，先按所选通道 guide 审查它是否完整、详细，再确认它通过当前公开 SO workflow-file load/status 门槛；当前公开 CLI 还没有单独的 `compile` 命令，而成功执行 `dotnet so.dll status --workflow-file <path>` 只能证明 load/status 兼容，不能单独证明 workflow completeness
- 通过 `dotnet so.dll run` / `resume` 执行确定型步骤
- 目标 skill 每次运行先复制已固化模板，出现变数后才重新规划
