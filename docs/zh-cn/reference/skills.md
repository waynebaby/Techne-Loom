# Skills 输入输出参考

[English](../../en/reference/skills.md)

## `/loom-plan-execution`

### 使命

这是一个以 guide 和环境配置为先的计划执行入口，围绕 plan-execution package flow 工作。

### 输入

- 丰富的计划文本，建议至少 10 行非空内容
- 或详细的计划文件路径
- package 通道选择：released 或 beta
- 可选审计输出路径

### 输出预期

- package / 通道选择确认
- 绝对 package index links
- guide surface 引用
- planner 产出的 workflow JSON 路径
- runtime 返回 payload links，包括 audit artifacts

### 运行时衔接

- 以 `dotnet ao.dll --guide` 为事实来源
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
- 可选审计输出路径

### 输出预期

- package / 通道选择确认
- 绝对 package index links
- guide surface 引用
- planner 产出的确定型 workflow 模板路径
- runtime 返回 payload links，包括 audit artifacts

### 运行时衔接

- 以 `dotnet so.dll --guide` 为事实来源
- 通过 `dotnet so.dll planner` 物化 workflow JSON
- 通过 `dotnet so.dll run` / `resume` 执行确定型步骤
- 目标 skill 每次运行先复制已固化模板，出现变数后才重新规划
