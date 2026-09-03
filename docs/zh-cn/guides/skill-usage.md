# 使用 Techne Loom Skills

[English](../../en/guides/skill-usage.md) | [根目录](../README.md)

这是一份面向操作者的 Techne Loom skill 使用入口文档。

如果你要看 package contract、runtime wire 细节或完整输入输出参考，请在读完这页后继续看产品 guide 和 skills reference。这一页先回答更直接的问题：该用哪个 skill、该给它什么输入、以及什么才算正式运行面。

## 先选对入口

| 场景 | 应该使用 | 先读什么 | 正式运行面 |
| --- | --- | --- | --- |
| 路线还不清晰，需要探索式编排 | `/loom-plan-execution` | `packages.released.zh-CN.md` 或 `packages.beta.zh-CN.md`，再读 Loom Agent Execution Orchestrator 的 `dotnet ao.dll --guide` | `dotnet ao.dll run` 与 `dotnet ao.dll resume` |
| 你要创建或升级一个确定型 skill | `/loom-skill-enhancement` | `packages.released.zh-CN.md` 或 `packages.beta.zh-CN.md`，再读 Loom Skill Orchestrator 的 `dotnet so.dll --guide` | 增强后正式 target-skill run 只有 `dotnet so.dll run` 与 `dotnet so.dll resume`；`compile` 只是校验 |
| 你已经有一个 Loom-governanced target skill，想日常使用它 | 目标 skill 本身 | 目标 `SKILL.md` 与 `assets/so-workflow/so-package-lock.json` | 面向 runtime workflow copy 的 `dotnet so.dll run` 与 `dotnet so.dll resume` |

## 共享准备规则

1. 在获取 runtime 前先执行[平台检测步骤](../reference/runtime/platform-detection.md)。受治理 skill 只能把 owning skill 的 locked exact version、CI/CD 管理的 version block 或 checked-in runtime lock 作为版本权威；direct 调用者从 package index 选择 released 或 beta。
2. 使用双模式 runtime 契约。self-contained 是默认通道：解析检测出的 RID，恢复一个精确匹配的 runtime package，再使用其 direct executable launch descriptor。`.NET CLI 模式`必须通过 `runtimeBinding` 或显式 bundle directory 选择；选择后要求可用的 `Microsoft.NETCore.App 9.x` host，并恢复精确版本的 Product、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions` IL closure。`.NET CLI` host 失败时直接 fail-closed，不切换模式。
3. self-contained 包无需预装 .NET runtime，但仍依赖目标 OS 与 ABI。不支持的 RID 必须 fail-fast；不允许跨架构或相邻版本 fallback。
4. 两种模式都必须先运行 fresh `--guide`，校验输出 JSON 中的 version 和可读取的 `guide_path`，再让 `compile`、`run`、`resume` 复用同一个 launch descriptor、精确 runtime version 与 RID。
5. 除非用户明确选择其他输出根目录，否则 compile artifacts、audit artifacts、runtime workflow copy、session 目录和 event sidecar 都必须放在 checked-in skill 目录之外。有效的精确版本缓存可以离线复用；没有有效缓存且网络不可用时，结果是阻塞。
6. 先使用 NuGet.org 精确 V3 package URL。只有精确 NuGet 包无法获取时，才使用同版本官方 GitHub release asset，并执行相同的 hash、manifest、ZIP 安全和入口校验。
## `/loom-plan-execution`

当外层 agent 仍需要探索、澄清、比较 frontiers，或在路线尚未稳定时委派聚焦工作，请使用 `/loom-plan-execution`。

### Loom Agent Execution Orchestrator Skill 输入

- 至少 10 行非空内容的丰富计划，或详细计划文件路径
- skill 的本地化 prose 和 package-index link 可以在相应页面存在时使用 `en` 或 `zh-cn`；runtime 的 `--guide` 命令本身只支持英文，并返回英文文档包的路径 JSON
- 可选 audit 输出根目录

### 它会做什么

- 先把调用方导向正确的 package index
- 在执行前把 `dotnet ao.dll --guide` 当作权威来源
- 可以在编写 WorkflowInstance 文件前显式调用 `dotnet ao.dll prompt-plan` 获取 Loom Agent Execution Orchestrator 管理的 planner prompt blocks，也可以在 blocked WorkflowInstance seam 需要改写前显式调用 `dotnet ao.dll prompt-replan` 获取 Loom Agent Execution Orchestrator 管理的 replanner prompt blocks
- 把 Loom Agent Execution Orchestrator 作为该 skill 唯一正式 execution authority
- 返回 `session_id`、`workflow_file`、`event_log_file`、blocked frontier 细节等控制态数据

### Loom Agent Execution Orchestrator 示例

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

当你要创建确定型 skill、把现有 skill 升级成 Loom-governanced skill，或把已经被 Loom Skill Orchestrator 增强过的 skill 推进到排他的 Loom Skill Orchestrator governance mode 时，请使用 `/loom-skill-enhancement`。

### Loom Skill Orchestrator Enhancement 输入

- 目标 skill 路径或目标仓库路径
- 确定型目标或升级请求
- 本次增强中必须创建或修改的目标 skill 变更项
- runtime 版本依据：复用 checked-in `assets/so-workflow/so-package-lock.json` 与当前 skill package version block，需要时再从绑定版本推导 `released` 或 `beta`
- skill 的本地化 prose 和 package-index link 可以在相应页面存在时使用 `en` 或 `zh-cn`；runtime 的 `--guide` 命令本身只支持英文，并返回英文文档包的路径 JSON
- 可选 JSON context 文件
- 可选 audit 输出根目录

### 它会产出什么

- `<execution-output-root>/plan/skill-plan.md`
- `<target-skill-root>/assets/so-workflow/` 下的 checked-in workflow template
- `<target-skill-root>/assets/so-workflow/so-package-lock.json`
- 更新后的目标 `SKILL.md`，显式引用 lock 文件、说明 Loom Skill Orchestrator 治理模型，并明确默认的受治理成功路径必须继续走公开 `dotnet so.dll run` / `resume` 直到最终 `Done`

### Loom Skill Orchestrator Enhancement 示例

下面的 `{agentskillfolder}/...` 是“外部 target skill 根目录”的 agent 中立占位写法。请把它替换成你的 agent 或宿主实际使用的 skill 文件夹；只有在明确指代“本仓库内置 skill”或“本仓库内置 manifest catalog”时，才使用 `.agents/skills/...`。

```text
/loom-skill-enhancement
Channel: beta
Language: zh-cn
Target: {agentskillfolder}/my-target-skill
Goal: 把这个 skill 升级为处于排他 Loom Skill Orchestrator governance 下的 Loom-governanced skill，并固化 checked-in workflow template 与 locked runtime bundle
Requested target skill changes:
- 刷新 SKILL.md 治理文案
- 创建或刷新 <execution-output-root>/plan/skill-plan.md
- 创建或刷新 checked-in workflow template
- 创建或重写 assets/so-workflow/so-package-lock.json
```

三个具体调用路径见 [Loom Skill 增强调用示例](../examples/skill-enhancement-calls.md)。

Workflow template 治理基线：

- workflow template 必须使用显式的受治理步骤、guards、seams 与可复核输出
- workflow template 绝不能包含任何目的或意图上表示 `run a multistep plan` 的节点
- 审查 workflow template 时，还必须查找任何把多步指令或宽泛 agent prompt 塞进单个节点的写法，并在可行时拆成更小的受治理节点

## 受治理的 SO 入口

对于每个由 Loom Skill Orchestrator 治理的 target skill 校验，包括 `/loom-skill-enhancement` 自举，精确的发布 runtime 必须先为同一份外部 workflow copy 返回由 resolver 生成的 launch descriptor。

1. 使用该 descriptor 通过选定 runtime 生成所需的 VS Code `mcp.json` 和 Claude `.mcp.json`。resolver 决定使用 self-contained executable 还是 framework-dependent DLL；workflow 文本不得自行选择。
2. 尝试注册生成的配置，完成 `initialize` 和 `notifications/initialized`，再用有界参数调用 `so_inspect_workflow_fragment`。
3. 成功后保存 `mcp_registration_attempt_evidence.status=ready`，设置 `governance_entry_transport=mcp_stdio`，并返回带有相同 descriptor 与 workflow 身份的 `mcp_startup_evidence`。
4. 如果 MCP 在成功派发命令前无法提供，就保存 `mcp_registration_attempt_evidence.status=failed`、`mcp_attempted=true`，并且只能使用一个允许原因：`mcp_transport_unavailable`、`mcp_handshake_unsupported` 或 `mcp_tool_unavailable`。然后使用同一个 descriptor 执行有界的 `inspect-workflow-fragment` CLI backup，并设置 `governance_entry_transport=cli`。
5. MCP 启动后的应用错误或命令错误不能触发 backup。保留保存的 workflow 失败边界。
6. 只有某一种传输方式生成 `mcp_startup_evidence` 后，workflow 才能捕获 `--guide`，再继续规划、编写、校验、compile、run 或 resume。

### 增强后什么算正式运行

- 增强过程本身可能会把 `dotnet so.dll compile` 用作治理完成前的校验步骤
- 当增强过程实际执行 target-skill workflow 时，正式 target-skill 运行面是 `dotnet so.dll run` 与 `dotnet so.dll resume`
- 一旦目标 skill 进入排他的 Loom Skill Orchestrator governance 状态，只有 `dotnet so.dll run` 与 `dotnet so.dll resume` 才算正式 target-skill run
- 如果某次创建或 re-enhancement 切片停在 guide 刷新、checked-in 资产更新和 compile 校验通过，那么正确状态应表述为进行中或阻塞中的 enhancement 切片，而不是治理完成

direct CLI 片段、MCP 调用或 prose explanation 本身都不会自动变成正式运行。

## 如何使用 Loom-governanced Target Skill

一旦目标 skill 已经切换成 Loom Skill Orchestrator governance 类型，就应把它视为 Loom-governanced target skill，而不再按“普通 prompt skill”来使用。

### 日常运行顺序

1. 先读目标 `SKILL.md`。
2. 再读 `assets/so-workflow/so-package-lock.json`，并从 NuGet 恢复精确锁定的 Loom Skill Orchestrator runtime bundle。
3. 保持 checked-in workflow template 干净，把它复制成 skill 目录外部的 runtime workflow copy。
4. 使用 runtime-owned launch descriptor 生成 MCP 配置并尝试注册、握手和片段检查。如果 MCP 在成功派发前无法提供，就使用同一个 descriptor 执行有界 CLI backup，并保留 `mcp_startup_evidence`；不能把当前编辑器的 `mcp.json` 当作证据。
5. 执行 `dotnet so.dll run --workflow-file <runtime-copy-path>`。
6. 如果 Loom Skill Orchestrator blocked，就按 `skill_hint` 行动，保留 `memory_for_next_step`，再用 `dotnet so.dll resume --workflow-file <runtime-copy-path> --result-file <path>` 续跑。

### 最小示例

```text
先读 SKILL.md -> 再读 assets/so-workflow/so-package-lock.json -> 恢复精确锁定的 Loom Skill Orchestrator runtime bundle -> 复制 checked-in template -> 启动并使用 dotnet so.dll mcp stdio -> 捕获 guide -> 执行 dotnet so.dll run -> 跟随 blocked seam -> 执行 dotnet so.dll resume
```

### 不要这样做

- 不要在同一通道内悄悄漂到更高的 Loom Skill Orchestrator 包版本
- 不要只恢复 `Techne.Loom.SkillOrchestrator`
- 不要把 `run` 或 `resume` 直接指回 checked-in source template
- 一旦目标 skill 进入排他的 Loom Skill Orchestrator governance 状态，不要把 direct CLI 或 direct MCP 执行当成平级正式运行面

对于已经 Loom-governanced 的 target skill，稳定状态的话术应写成：该 target skill 已是 Loom-governanced target skill，且它的 official execution surface 是面向 runtime workflow copy 的公开 `dotnet so.dll run` 与 `dotnet so.dll resume` 路径。compile-only 或 compile 校验通过只应被视为 enhancement 的中间里程碑，不应作为正常治理完成话术。

如果仓库里同时保留 demo 时间线或 recorded-slice 叙事页面，应把这些页面视为历史记录，而不是当前完成合同的 authority。当前 authority 仍然是 target skill 已检入的 `SKILL.md`、`contract.json` 与 `assets/so-workflow/` 表面。

## 继续深入阅读

- [Agent 集成](agent-integration.md)
- [Skill 集成](skill-integration.md)
- [Loom Agent Execution Orchestrator Guide](ao-guide.md)
- [SkillOrchestrator Guide](so-guide.md)
- [Skills 输入输出参考](../reference/skills.md)
- [Loom Skill 增强调用示例](../examples/skill-enhancement-calls.md)
- [Loom 治理 Skill 运行示例](../examples/so-enhanced-skill-run.md)
