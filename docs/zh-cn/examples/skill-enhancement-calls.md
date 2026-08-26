# Loom Skill 增强调用示例

[English](../../en/examples/skill-enhancement-calls.md) | [根目录](../README.md)

这些示例展示三种常见的 `/loom-skill-enhancement` 调用方式，并明确保持 Loom Skill Orchestrator（`dotnet so.dll`）治理约束。

> [!NOTE]
> 这些路线产出的 workflow template 必须使用显式的受治理步骤、guards、seams 与可复核输出。它们绝不能包含任何目的或意图上表示 `run a multistep plan` 的节点。还要审查是否有节点把多步指令或宽泛 agent prompt 塞在一起，并在可行时拆成更小的受治理节点。

下面的 `{agentskillfolder}/...` 是“外部 target skill 根目录”的 agent 中立占位写法。请把它替换成你的 agent 或宿主实际使用的 skill 文件夹；只有在明确指代“本仓库内置 skill”或“本仓库内置 manifest catalog”时，才使用 `.agents/skills/...`。

## 建议配套阅读

- [Skill 使用指南](../guides/skill-usage.md)
- [Skills 参考](../reference/skills.md)
- [SkillOrchestrator Guide](../guides/so-guide.md)

## 1. 增强一个已经存在的 Skill

当目标 skill 已经存在，但还没有被 Loom Skill Orchestrator 治理时，使用这条路线。

```text
/loom-skill-enhancement
Channel: released
Language: zh-cn
Target: {agentskillfolder}/existing-skill
Goal: 把这个现有 skill 升级成 Loom-governanced skill，并固化 checked-in workflow template、locked runtime bundle 与显式治理文案
Requested target skill changes:
- 刷新 SKILL.md，使其符合 Loom Skill Orchestrator 治理
- 创建 <execution-output-root>/plan/skill-plan.md
- 在 assets/so-workflow/ 下创建 checked-in workflow template
- 创建 assets/so-workflow/so-package-lock.json
```

预期路线：

- 先读所选 package index
- 从当前选定 package runtime 运行 fresh 的不带参数 `dotnet so.dll --guide`，解析其中的 `version`、`docs_root` 与 `guide_path` JSON 字段，并读取返回的 guide 路径
- 如果目标项目本身还没有安装依赖，只安装完成本次 target-skill 变更和当前 guide 对齐校验所需的最小依赖集
- 派生或刷新 `<execution-output-root>/plan/skill-plan.md`
- 编写一个没有隐藏 multistep-plan 节点意图的 deterministic workflow template
- 审查模板里是否有把多步指令或宽泛 agent prompt 捆在单个节点里的写法，并在可行时拆小
- 在任何 execution-authority 声明前先 compile

## 2. 通过 Skill Plan 创建一个 Skill

当 skill 还不存在，并且第一阶段的主要产物应该是 plan mode markdown 文件时，使用这条路线。

```text
/loom-skill-enhancement
Channel: beta
Language: zh-cn
Target: {agentskillfolder}/new-skill
Goal: 从一个 skill plan 创建新的 deterministic skill，并让第一份 plan mode outcome 保持为 markdown 文件
Requested target skill changes:
- 创建 SKILL.md
- 创建 <execution-output-root>/plan/skill-plan.md，作为第一份 plan mode outcome markdown file
- 在 assets/so-workflow/ 下创建 checked-in workflow template
- 创建 assets/so-workflow/so-package-lock.json
```

预期路线：

- 把 `<execution-output-root>/plan/skill-plan.md` 视为第一份作者产物
- 让 workflow template 在此基础上细化成显式受治理步骤
- 避免任何用通用 planner 意图隐藏开放式执行的 template 节点
- 审查草稿模板里的 bundled multistep instruction，并把它们拆成更小的受治理节点

## 3. 再次增强一个已经被 Loom Skill Orchestrator 增强过的 Skill

当目标 skill 已经被 Loom Skill Orchestrator 增强过，并且需要新一轮增强时，使用这条路线。

```text
/loom-skill-enhancement
Channel: 从已绑定的精确 runtime 版本推导 released 或 beta；正常再次增强不向用户询问 channel
Language: zh-cn
Target: {agentskillfolder}/already-enhanced-skill
Goal: 基于最新 Loom Skill Orchestrator guide 再次增强这个 skill，并收紧治理文案
Requested target skill changes:
- 刷新 SKILL.md 的治理文案
- 如果最新 guide 需要，则刷新 <execution-output-root>/plan/skill-plan.md
- 如果最新 guide 需要，则刷新 checked-in workflow template
- 保持 assets/so-workflow/so-package-lock.json 与当前 skill 绑定的 runtime 版本一致
```

必需决策与路线：

- 正常 re-enhancement 流程里不再让用户选择 released 或 beta
- 直接重新获取当前 skill build 与 checked-in package lock 已绑定的精确 package 版本
- 从当前选定 package runtime 运行 fresh 的不带参数 `dotnet so.dll --guide`，解析其中的 `version`、`docs_root` 与 `guide_path` JSON 字段，并读取返回的 guide 路径
- 如果目标项目本身还没有安装依赖，只安装完成本次 target-skill 变更和当前 guide 对齐校验所需的最小依赖集
- 强烈建议用 subagent 对当前 skill 与 workflow assets 相对照最新 guide 结果做一次复查
- 三个 gap review 完成后，必须把模板变化分类为 `local_patch`、`structural_refactor` 或 `full_regeneration`
- 对于 `structural_refactor` 或 `full_regeneration`，要把旧模板作为基线输入，并结合当前需求、概念文档、target-skill 资产和最新 guide 重新生成候选模板
- `/loom-skill-enhancement` 自身也遵循同一判断规则；self-bootstrap 不能绕过这一步
- 刷新的 workflow template 仍必须避免任何表示或暗示 `run a multistep plan` 的节点意图
- 还要审查是否有节点把多步指令或宽泛 agent prompt 捆在一起，并在可行时拆成更小的受治理节点

## 这些调用绝不能做什么

- 绝不能把 direct CLI 或 direct MCP 执行当成平级正式运行面
- 绝不能默默复用旧 package lock 来决定再次增强版本
- 绝不能让 workflow template 用一个表示或暗示 `run a multistep plan` 的节点来隐藏开放式执行

## 继续阅读

- 返回 [示例目录](README.md)
- 阅读 [Skill 使用指南](../guides/skill-usage.md)
- 阅读 [Skills 参考](../reference/skills.md)
