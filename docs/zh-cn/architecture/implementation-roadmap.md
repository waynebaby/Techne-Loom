# 实现路线图

[English](../../en/architecture/implementation-roadmap.md)

本页是 Techne Loom 的已批准仓库 handoff 路线图。

它的目的，是让另一个 agent 只靠公开文档也能继续推进，而不依赖隐藏的规划上下文。

## 状态快照

- 仓库 framing、根执行规则以及旗舰双语 README 切片已经完成。
- `.NET` 中已经存在 `Techne.Loom.Abstractions`、`Techne.Loom.Common`、`Techne.Loom.SkillOrchestrator` 的公开切片。
- `SkillOrchestrator` 已经具备公开 CLI 契约、runtime、测试与对齐文档。
- `AgentOrchestrator` 现已在 `.NET` 中实现，提供 `dotnet ao.dll planner`、`dotnet ao.dll compile`、`dotnet ao.dll run`、`dotnet ao.dll resume` 与 `dotnet ao.dll --guide` 命令。
- `/docs` 大树已经存在，但部分页面仍在从“骨架”深化为“可交接规格”。

## 来源与范围规则

- 从原私有项目中挑选出来的 workflow-tracking 材料可以作为历史输入。
- 但不要把任何私有来源材料当成公开产品的最终定义。
- 不要把 `Clarios.*` 项目原样开源。
- 在 `Abstractions` 与 `Common` 层保持公开核心的协议中立、产品中立。

## 产品拆分

| 产品 | 角色 | 当前仓库状态 |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | 公开 workflow/task-tracking 契约 | `.NET` 已实现 |
| `Techne.Loom.Common` | host 无关运行时辅助 | `.NET` 已实现 |
| `Techne.Loom.SkillOrchestrator` | 确定性的 skill 执行与跟踪 | `.NET` 已实现 |
| `Techne.Loom.AgentOrchestrator` | 面向 CLI / package 契约的探索式编排 | `.NET` 已实现 |

AO 与 SO 是生态位不同的独立产品，不能再被叙述成谁是宿主、谁是子 runtime。

## 已批准阶段图

1. 仓库 framing
   公开 mono-repo 骨架、双语文档布局、来源澄清、根执行规则。
2. 核心契约抽取
   公开 workflow 模型、engine/store/dispatcher 契约、命名空间清理、依赖瘦身。
3. 公共运行时拆分
   序列化、时钟、ID、in-memory/file-backed store、表达式求值、可视化 plumbing。
4. Skill 可执行产品
   确定性 workflow 执行、本地工具执行、wait/resume 处理、稳定 CLI 契约。
5. Agent 可执行产品
   基于 CLI / package 契约的探索式编排、可变 workflow + append-only event/snapshot log、在控制 seam 处 weave out，并通过 blocked 协议载荷显式返回控制信息。
6. 协议与跨语言准备
   canonical workflow/control 契约、transport-neutral 边界、Node.js/Python 对齐面。
7. OSS hardening
   CI、打包元数据、测试、示例、文档完成度、发布卫生。

## 当前与下一步切片

### 已完成或接近完成

- 根治理规则和双语 README landing page。
- 公开 `.NET` 契约层。
- 公开公共运行时层。
- SO runtime、CLI 输出契约、sidecar JSON 契约以及聚焦测试。
- AO runtime、CLI surface（`dotnet ao.dll planner`、`dotnet ao.dll compile`、`dotnet ao.dll run`、`dotnet ao.dll resume`、`dotnet ao.dll --guide`）以及控制载荷契约。

### 推荐下一切片

- 扩展 solution 级 CI/build/test/pack 行为。
- 继续深化仍处于 skeleton 状态的文档页。
- 扩大 visualization 与 workflow progression 测试。
- 准备 Node.js/Python 保留 package 和 schema-facing 示例。

## Review And Commit 节奏

- 把每个 major slice 视为 review gate。
- 每完成一个 major slice，就先跑 `cto-review-and-commit`，再进入下一个切片。
- 默认规划规则是：单次切片尽量控制在 50 个变更文件以内。
- 即使不到 50 个文件，只要触及协议、schema、包接缝或运行时控制行为，也要立刻 review。

## 给另一个 Agent 的交接清单

1. 阅读 `AGENTS.md` 与 `AGENTS.zh-CN.md`。
2. 阅读本路线图。
3. 阅读 `reference/products/ao-guide.md` 与 `reference/products/so-guide.md`。
4. 先看 `git status`，明确下一切片的 scope。
5. 让下一切片保持在可证据化 review 的规模内。
6. 在进入下一切片前先跑 `cto-review-and-commit`。

## 不能回退的规则

- 保持 AO 与 SO 在打包、调用方式和心智模型上的独立。
- 保持 `Abstractions` 和 `Common` 不带私有云/AI 产品假设。
- 保持 workflow file 与 CLI sidecar 契约显式、machine-first。
- 保持对外文档双语且路径镜像。
