# Techne Loom

[English](README.md)

<!-- release-notes:start -->
---

## 🚀 发布说明 · `v0.2.62` · 2026 年 6 月

> [!NOTE]
> **稳定版本 — 由发布工作流自动同步。**
> 安装最新 stable：`dotnet add package Techne.Loom.SkillOrchestrator`
> 完整包列表 → [`packages.released.zh-CN.md`](packages.released.zh-CN.md)

### ✨ 通道亮点

| 领域 | 变更内容 |
| --- | --- |
| 🔄 **版本同步** | 这个区块会由发布工作流重写，确保这里展示的版本号始终对应最新发布的稳定包集合 |
| 📦 **回退资产** | GitHub release 别名会持续提供稳定的 `*.latest.nupkg` 下载地址，便于 NuGet feed 不可用时回退 |
| 🔎 **包发现** | NuGet.org 与 [`packages.released.zh-CN.md`](packages.released.zh-CN.md) 仍然是安装命令和精确稳定版本查询的事实来源 |

### 📦 本次发布的包

```
Techne.Loom.Abstractions          0.2.62
Techne.Loom.Common                0.2.62
Techne.Loom.AgentOrchestrator     0.2.62
Techne.Loom.SkillOrchestrator     0.2.62
```

> 这个区块会在每次 main 分支发布后自动更新。
> 请查阅 [NuGet.org](https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator) 或 [stable 回退发布页](https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-stable-latest) 获取最新版本号。

### 🔭 即将推出

- 带版本元数据的离线 `so.dll --guide` 与 `ao.dll --guide` 指南界面
- workflow、控制状态与提示负载的显式公共契约
- 与 .NET 系列并行的 Node.js 和 Python 包脚手架
- 更清晰的 AO / SO CLI resume 流程示例（含 `transition_id` 和 `correlation_key`）

---
<!-- release-notes:end -->






## 把两件常被混在一起的 agent 工作，拆成两个明确产品

![Status](https://img.shields.io/badge/status-open%20source%20design%20in%20progress-F59E0B)
![Architecture](https://img.shields.io/badge/architecture-AO%20%2B%20SO-2563EB)
![Runtime](https://img.shields.io/badge/.NET-first-512BD4)
![Packages](https://img.shields.io/badge/packages-NuGet%20%7C%20npm%20%7C%20PyPI-111827)
![Docs](https://img.shields.io/badge/docs-bilingual-0EA5E9)

> [!IMPORTANT]
> Techne Loom 不是在继续堆一个“全都能干一点”的 agent 框架。
> 它的核心设计是：把 **面向未知的顶层编排** 和 **面向确定执行的 skill 跟踪** 明确拆开。

Techne Loom 背后的核心判断很直接：大多数 agent 系统把两种完全不同的工作混在了一起。

1. 在地图还不完整的时候，边探索边决定路线。
2. 当一个 skill 已经进入执行阶段时，严格地知道下一步该干什么，并且别走偏。

Techne Loom 给这两件事分别命名、分别建产品、分别设计包、文档和调用方式。

## Workflow 术语基线

整个 repo 现在统一用一套编织术语来解释 AO 和 SO 的 workflow 行为。

- **weave out**：runtime 把控制权或工作向外交出，并等待结构化继续。
- **weave back**：外界带着结构化结果回到原来的执行线，让流程得以 resume。
- **strand**：一条当前执行线；在仓库文档里用它代替容易和 `.NET` 线程混淆的 `thread`。
- **seam**：控制权跨所有者转移的概念接缝；协议层后续会通过 `boundary_reason`、`current_step_kind` 这类字段把它显式表达出来。
- **boundary**：正式协议术语，保留给 machine-readable 的阻塞/返回控制态，例如 `boundary_reason` 或 `type: "boundary"`。

完整术语表见：

- [`docs/en/architecture/workflow-terminology.md`](docs/en/architecture/workflow-terminology.md)
- [`docs/zh-cn/architecture/workflow-terminology.md`](docs/zh-cn/architecture/workflow-terminology.md)

后续 AO / SO 文档都会按这套术语解释流程；如果某个当前实现字段名和术语不同，文档会同时写出术语和真实字段名。

## 为什么要做这个

很多基于 prompt 的编排，看起来很灵活，但一旦进入复杂场景就开始漂移。

- 顶层 agent 会过度依赖眼前还记得的局部上下文。
- skill 会把状态偷偷塞进 prompt、memory 和工具输出里。
- 工具调用、模型思考、人类输入、subagent 协作最后全挤在一个模糊表面上。
- 一旦你需要可恢复、可审计、可回放、可发布复用，这种体系就会越来越难信任。

Techne Loom 就是针对这个失败模式而设计的。

## 两个产品，不是一个产品的两种模式

| 产品 | 它是什么 | 它不是什么 | 主要接口 |
| --- | --- | --- | --- |
| `AgentOrchestrator` (`ao`) | 面向总 agent 的探索式编排产品 | 不是确定型 skill 执行器 | CLI / package 契约 |
| `SkillOrchestrator` (`so`) | 面向 skill 的确定型 workflow 跟踪与下一步约束产品 | 不是开放式规划器 | 本地 CLI 与包契约 |

```mermaid
flowchart LR
    subgraph AO[AgentOrchestrator]
        A1[用户目标]
        A2[局部上下文]
        A3[可变工作流]
        A4[控制态输出]
        A1 --> A3
        A2 --> A3
        A3 --> A4
    end

    subgraph SO[SkillOrchestrator]
        S1[Workflow JSON]
        S2[确定型执行循环]
        S3[阻塞或结束输出]
        S1 --> S2
        S2 --> S3
    end
```

这个拆分不是包装词，而是方法论本体。

- **AO** 负责继续探索、试探、澄清、修正路线。
- **SO** 负责让一个 skill 一旦进入执行，就不再迷路。

两者可以共享低层约定，但它们不是一个父子运行时体系。

## 这套方式和常见做法有什么不同

| 问题 | 常见结果 | Techne Loom 的回答 |
| --- | --- | --- |
| 顶层 agent 在未知环境里规划 | 一边 improvisation，一边把状态搞丢 | AO 维护活的 workflow 和 append-only 事件历史 |
| skill 同时穿插工具、模型、MCP、subagent | skill 只能靠脆弱上下文硬撑 | SO 跑持久化 workflow，并在阻塞点返回严格下一步契约 |
| 跨生态复用 | 逻辑被锁死在单一 runtime 或仓库内部 | 每个项目单元都设计成可发布 package |
| 文档给人看但不能直接驱动生成 | 规范和实际调用脱节 | AO/SO 从一开始就为内建 guide surface 设计 |

## 核心承诺

Techne Loom 想做的，不是让 agent 看起来更聪明。
而是让 agent 系统在面对不确定性时，仍然具备可控的结构。

- **探索必须显式。**
- **执行必须可恢复。**
- **下一步提示必须足够严格。**
- **memory 必须进入 workflow context，而不是靠“氛围记忆”。**
- **每个项目单元都应该能作为包发布，而不是埋在仓库里做内部胶水。**

## 从第一天起就是 package-first

仓库会按生态维护平行包系。

| 角色 | NuGet | npm | PyPI |
| --- | --- | --- | --- |
| 抽象层 | `Techne.Loom.Abstractions` | `@techne-loom/abstractions` | `techne-loom-abstractions` |
| 公共层 | `Techne.Loom.Common` | `@techne-loom/common` | `techne-loom-common` |
| Agent 编排 | `Techne.Loom.AgentOrchestrator` | `@techne-loom/agent-orchestrator` | `techne-loom-agent-orchestrator` |
| Skill 编排 | `Techne.Loom.SkillOrchestrator` | `@techne-loom/skill-orchestrator` | `techne-loom-skill-orchestrator` |

这不是“一个核心运行时外面套三层壳”。
这是按角色平行展开的 package matrix。

未来 Node.js 与 Python 包的命名目前都还只是**规划态**，对应调用方式也同样只是规划：

- Node.js：通过 package 入口调用，例如 `npx @techne-loom/agent-orchestrator` 与 `npx @techne-loom/skill-orchestrator`
- Python：通过模块入口调用，例如 `python -m techne_loom_agent_orchestrator` 与 `python -m techne_loom_skill_orchestrator`

这些非 .NET 调用面目前在本仓库中**尚未实现**。

> [!NOTE]
> 在开始配置或执行前，先选择 package 通道：
>
> - 稳定通道：[`packages.released.zh-CN.md`](packages.released.zh-CN.md)
> - Beta / development 通道：[`packages.beta.zh-CN.md`](packages.beta.zh-CN.md)
> - English stable：[`packages.released.md`](packages.released.md)
> - English beta：[`packages.beta.md`](packages.beta.md)

## 快速使用

如果你现在是从操作者视角评估 Techne Loom，而不是先完整阅读 contracts，请从这里开始。

| 你要做什么 | 应该使用 | 先读什么 | 正式运行面 |
| --- | --- | --- | --- |
| 让顶层 agent 在不确定路线下继续探索 | `/loom-plan-execution` | [使用 Techne Loom Skills](docs/zh-cn/guides/skill-usage.md)，再读 [AO Guide](docs/zh-cn/reference/products/ao-guide.md) | `dotnet ao.dll run` / `dotnet ao.dll resume` |
| 创建或升级一个确定型 skill | `/loom-skill-enhancement` | [使用 Techne Loom Skills](docs/zh-cn/guides/skill-usage.md)，再读 [SO Guide](docs/zh-cn/reference/products/so-guide.md) | 增强流程会用到 `dotnet so.dll compile` / `run` / `resume` |
| 运行一个已经 SO-enhanced 的 target skill | 目标 skill 及其 lock file | [使用 Techne Loom Skills](docs/zh-cn/guides/skill-usage.md)，再读 [SO 增强 Skill 运行示例](docs/zh-cn/examples/so-enhanced-skill-run.md) | 面向 runtime workflow copy 的 `dotnet so.dll run` / `dotnet so.dll resume` |

有三条规则需要先记住：

1. 执行前先选 package 通道。
2. 恢复完整 AO 或 SO runtime bundle，不要只恢复主 runtime 包。
3. 把 runtime workflow copy、session state 与 audit artifacts 放在 checked-in skill 文件夹之外。

## AO 一句话解释

AO 是给总 agent 用的：当路线还不清晰时，它负责边探索边细化 workflow，并在每个关键控制 seam 处 weave out；协议层需要显式表达时，再通过 blocked AO payload 里的 `boundary_reason` 等字段呈现控制态信息。

它输出的重点不是长篇自然语言，而是：

- 成功/失败
- session_id
- 当前 workflow 文件
- 当前节点编号
- 事件日志路径
- 下一步候选或待满足条件

按这套术语，AO 会在需要外界继续判断或执行时 **weave out**，并通过 blocked AO payload 里的 `boundary_reason`、`weave_out_request` 等字段把这个 seam 显式表达出来；调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet ao.dll resume` 结果 envelope **weave back**。

## SO 一句话解释

SO 是给 skill 用的：当 skill 不该继续 improvisation 时，它把执行重新拉回一个显式 workflow 上。

SO 设计成 `run-until-blocked-or-finished`。
也就是说，每次调用它，它都会尽量推进，直到：

- 整个流程完成
- 或者遇到必须由外界参与的 seam

在阻塞点，它应该稳定返回：

- 当前 workflow 文件
- 当前节点编号
- 当前 step 类型
- 严格下一步提示
- `memory_for_next_step`
- 继续执行所需输入

这里最关键的是 `memory_for_next_step`。
SO 的设计目标之一，就是把相关 memory/context 写回 workflow state，并在每次阻塞返回时显式带出来，降低 skill 在外界继续执行时走偏的概率。

按这套术语，SO 只有在遇到外部拥有的 seam 时才会 **weave out**，并通过 blocked `<so_property>` payload 里的 `current_step_kind` 等字段显式表达这个 seam；之后调用方再通过携带 `transition_id`、`correlation_key`、`payload` 的 `dotnet so.dll resume` 结构化输入 **weave back**。

## 内建 Guide Surface

Techne Loom 的目标不是让使用者“自己翻仓库猜该怎么接”。

AO 和 SO 都会围绕内建 guide surface 来设计：

- `dotnet ao.dll --guide`
- `dotnet so.dll --guide`

这些 guide 不是普通 help，而是版本绑定、可离线、可直接给用户或模型消费的规范输出。

也就是说，未来应该能直接支持这样的使用方式：

> 根据 `dotnet so.dll --guide` 里面描述，为我写一个 xxx 功能的 skill。

## 当前已经确定的仓库规则

- 根文档默认双语。
- 根 `README.md` 与 `README.zh-CN.md` 是旗舰 landing page。
- 根 `AGENTS.md` 与 `AGENTS.zh-CN.md` 负责仓库执行规则。
- 每个大切片做完后，都要先走 review-and-commit，再进入下一个切片。

当前仓库执行规则见：

- [AGENTS.md](AGENTS.md)
- [AGENTS.zh-CN.md](AGENTS.zh-CN.md)

## 当前阶段

这个仓库目前正处于“开源化落地”的分阶段推进里：

1. 先锁死根规则与文档节奏。
2. 先把双语 landing page 做出来。
3. 再补 docs 和 AO/SO guide 源文档。
4. 再搭平行 package 骨架。
5. 最后逐步抽出公开契约与运行时实现。

所以这份 README 的语气是刻意强定位的，而实现会按切片稳步跟上。

## 接下来会看到什么

- `/docs` 下的双语文档树
- AO / SO 的专门 guide 源文档
- `.NET`、Node.js、Python 三个生态的 package 骨架
- 更明确的 workflow、control、guide 契约
- 探索式编排和确定型 skill 执行的公开分层

## 方法论底色

Techne Loom 并不是靠假装“不确定性不存在”来赢。
它试图通过给“不确定性”和“确定执行”两件事分别配工具来赢。

这就是整个项目最核心的出发点。
