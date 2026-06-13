# 使用 Techne Loom Skills

[English](../../en/guides/skill-usage.md) | [根目录](../README.md)

这是一份面向操作者的 Techne Loom skill 使用入口文档。

如果你要看 package contract、runtime wire 细节或完整输入输出参考，请在读完这页后继续看产品 guide 和 skills reference。这一页先回答更直接的问题：该用哪个 skill、该给它什么输入、以及什么才算正式运行面。

## 先选对入口

| 场景 | 应该使用 | 先读什么 | 正式运行面 |
| --- | --- | --- | --- |
| 路线还不清晰，需要探索式编排 | `/loom-plan-execution` | `packages.released.zh-CN.md` 或 `packages.beta.zh-CN.md`，再读 `dotnet ao.dll --guide` | `dotnet ao.dll run` 与 `dotnet ao.dll resume` |
| 你要创建或升级一个确定型 skill | `/loom-skill-enhancement` | `packages.released.zh-CN.md` 或 `packages.beta.zh-CN.md`，再读 `dotnet so.dll --guide` | 增强流程会用到 `dotnet so.dll compile`、`run`、`resume` |
| 你已经有一个 SO-enhanced target skill，想日常使用它 | 目标 skill 本身 | 目标 `SKILL.md` 与 `assets/so-workflow/so-package-lock.json` | 面向 runtime workflow copy 的 `dotnet so.dll run` 与 `dotnet so.dll resume` |

## 共享准备规则

1. 在开始前先选 package 通道。稳定通道从 [packages.released.zh-CN.md](../../../../packages.released.zh-CN.md) 开始。Development 通道从 [packages.beta.zh-CN.md](../../../../packages.beta.zh-CN.md) 开始。
2. 如果需要下载本地 runtime，不要只恢复主 runtime 包，必须恢复完整 runtime bundle。AO 使用 `Techne.Loom.AgentOrchestrator`、`Techne.Loom.Common`、`Techne.Loom.Abstractions`。SO 使用 `Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common`、`Techne.Loom.Abstractions`。
3. 除非用户明确指定其他输出根目录，否则 compile artifacts、audit artifacts、runtime workflow copy、session 目录和 event sidecar 都必须放在 checked-in skill 目录之外。
4. NuGet.org 是一等“最新包来源”。GitHub release assets 只作为 fallback 路径保留。

## `/loom-plan-execution`

当外层 agent 仍需要探索、澄清、比较 frontiers，或在路线尚未稳定时委派聚焦工作，请使用 `/loom-plan-execution`。

### AO Skill 输入

- 至少 10 行非空内容的丰富计划，或详细计划文件路径
- 可选语言界面：`en` 或 `zh-cn`
- 可选 audit 输出根目录

### 它会做什么

- 先把调用方导向正确的 package index
- 在执行前把 `dotnet ao.dll --guide` 当作权威来源
- 可以在编写 WorkflowInstance 文件前显式调用 `dotnet ao.dll prompt-plan` 获取 AO 管理的 planner prompt blocks，也可以在 blocked WorkflowInstance seam 需要改写前显式调用 `dotnet ao.dll prompt-replan` 获取 AO 管理的 replanner prompt blocks
- 把 AO 作为该 skill 唯一正式 execution authority
- 返回 `session_id`、`workflow_file`、`event_log_file`、blocked frontier 细节等控制态数据

### AO 示例

```text
/loom-plan-execution
Channel: beta
Language: zh-cn
Plan:
1. 先确认失败 CLI 行为。
2. 比较最可能的 owner 路径。
3. 用最窄验证动作确认修复方向。
4. 如果必须有人类输入，则在明确 weave-out 处停止。
...
```

### 什么算正式运行

- `dotnet ao.dll run`
- `dotnet ao.dll resume`

`dotnet ao.dll --guide` 与 `dotnet ao.dll compile` 只是准备或校验表面，不算正式 skill run。

## `/loom-skill-enhancement`

当你要创建确定型 skill、把现有 skill 升级成 SO-governed skill，或把已经 SO-enhanced 的 skill 推进到 SO-exclusive governance 时，请使用 `/loom-skill-enhancement`。

### SO Enhancement 输入

- 目标 skill 路径或目标仓库路径
- 确定型目标或升级请求
- package 通道：`released` 或 `beta`
- 可选语言界面：`en` 或 `zh-cn`
- 可选 JSON context 文件
- 可选 audit 输出根目录

### 它会产出什么

- `<target-skill-root>/assets/so-workflow/skill-plan.md`
- `<target-skill-root>/assets/so-workflow/` 下的 checked-in workflow template
- `<target-skill-root>/assets/so-workflow/so-package-lock.json`
- 更新后的目标 `SKILL.md`，显式引用 lock 文件并说明 SO 治理模型

### SO Enhancement 示例

```text
/loom-skill-enhancement
Channel: beta
Language: zh-cn
Target: .github/skills/my-target-skill
Goal: 把这个 skill 升级为 SO-exclusive governed skill，并固化 checked-in workflow template 与 locked runtime bundle
```

### 增强后什么算正式运行

- 增强过程本身可能会调用 `dotnet so.dll compile`、`dotnet so.dll run`、`dotnet so.dll resume`
- 一旦目标 skill 进入 SO-exclusive governed 状态，只有 `dotnet so.dll run` 与 `dotnet so.dll resume` 才算正式 target-skill run

direct CLI 片段、MCP 调用或 prose explanation 本身都不会自动变成正式运行。

## 如何使用 SO-Enhanced Target Skill

SO-enhanced target skill 不能再按“普通 prompt skill”来使用。

### 日常运行顺序

1. 先读目标 `SKILL.md`。
2. 再读 `assets/so-workflow/so-package-lock.json`，并从 NuGet 恢复精确锁定的 SO runtime bundle。
3. 保持 checked-in workflow template 干净，把它复制成 skill 目录外部的 runtime workflow copy。
4. 执行 `dotnet so.dll run --workflow-file <runtime-copy-path>`。
5. 如果 SO blocked，就按 `skill_hint` 行动，保留 `memory_for_next_step`，再用 `dotnet so.dll resume --workflow-file <runtime-copy-path> --result-file <path>` 续跑。

### 最小示例

```text
先读 SKILL.md -> 再读 assets/so-workflow/so-package-lock.json -> 恢复精确锁定的 SO runtime bundle -> 复制 checked-in template -> 执行 dotnet so.dll run -> 跟随 blocked seam -> 执行 dotnet so.dll resume
```

### 不要这样做

- 不要在同一通道内悄悄漂到更高的 SO 包版本
- 不要只恢复 `Techne.Loom.SkillOrchestrator`
- 不要把 `run` 或 `resume` 直接指回 checked-in source template
- 一旦目标 skill 进入 SO-exclusive governed 状态，不要把 direct CLI 或 direct MCP 执行当成平级正式运行面

## 继续深入阅读

- [Agent 集成](agent-integration.md)
- [Skill 集成](skill-integration.md)
- [AgentOrchestrator Guide](../reference/products/ao-guide.md)
- [SkillOrchestrator Guide](../reference/products/so-guide.md)
- [Skills 输入输出参考](../reference/skills.md)
- [SO 增强 Skill 运行示例](../examples/so-enhanced-skill-run.md)
