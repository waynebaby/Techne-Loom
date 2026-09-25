# 实现路线图

[English](../../en/architecture/implementation-roadmap.md) | [根目录](../README.md)

本页是 Techne Loom 的已批准仓库 handoff 路线图。

它的目的，是让另一个 agent 只靠公开文档也能继续推进，而不依赖隐藏的规划上下文。

## 状态快照

- 公开产品 framing 现在把 Techne Loom 定位为构建在 Agent Skills 之上的可验证语义与执行互操作层。
- `.NET` 中已经存在 `Techne.Loom.Abstractions`、`Techne.Loom.Common` 与 `Techne.Loom.SkillOrchestrator` 的公开切片。
- `SkillOrchestrator` 已经具备公开 CLI 契约、runtime、测试与对齐文档。
- `AgentOrchestrator` 已在 `.NET` 中实现，并通过 direct `ao.exe` / `ao` apphost 提供 `compile`、`run`、`resume` 与 `--guide` 命令。
- Workflow IR、compile/validation feedback、runtime binding、wait/resume、本地 MCP 治理、provenance 与 audit evidence 已经是当前公开基础。
- [Skill 互操作层](skill-interoperability.md)记录互操作定位的证据、当前产品面和边界。
- 跨宿主 target profile、adapter、loss accounting、dependency/environment portability 与 host-matrix conformance 仍属于分阶段后续工作。

## 来源与范围规则

- 历史 workflow-tracking 材料可以用于对照，但不属于公开产品合同。
- 公开行为只能由仓库代码、测试、package contract 与已编写文档定义。
- 不要把任何非公开来源的实现或文档复制到本仓库。
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

## 表达式 Runtime 路线图

- 当前 .NET 路线：通过规范 root `runtimeBinding` 与 `expressionBinding` 合同，由 Roslyn 编译 C# 表达式。所有 predicate compile 都输出 `detailedCompileFeedbackV1`。
- Adapter 路线：Node.js 与 Python 可以提供生态 adapter，但宿主语言不会自动成为表达式语言。任何未来 adapter 都必须先实现同一结构化 compile-feedback 合同。
- 未来第四条路线：用 Rust 实现跨平台 Loom Runtime Core，以 CEL 作为规范表达式语言。它不是执行 Rust 代码，必须复用 `ExpressionDefinition`、`requiredExpressionCapabilities`、`compileFeedbackContract` 与 `ExpressionCompileFeedback`。
- Rust+CEL 六个里程碑：(1) 文档先行，(2) 原型验证，(3) 合同冻结，(4) runtime 实现，(5) CLI 发布，(6) .NET adapter 集成。跨语言翻译仍由 skill 负责，并保留 source、translated source、tool、review 与 compile evidence。

## 当前与下一步切片

### 已完成或接近完成

- 根治理规则和双语 README landing page。
- 公开 `.NET` 契约层与公共运行时层。
- SO runtime、CLI 输出契约、sidecar JSON 契约以及聚焦测试。
- AO runtime、direct apphost CLI surface（`ao.exe` / `ao` 的 compile、run、resume 与 guide 命令）以及控制载荷契约。
- 带有显式 states、transitions、routes、seams、gates、ownership 与 output evidence 的 Workflow IR。
- 磁盘上的 run/resume、本地 MCP descriptor binding、provenance 与 audit artifact continuity。
- 双语互操作架构页与社区证据页。

### 推荐下一切片

- 定义 `loom-target-profile` 与 host capability 字段，覆盖 paths、frontmatter、tools、hooks、permissions 和 context mode。
- 先构建只读 host adapter 与机器可读 semantic loss report，再考虑写回转换。
- 将 activation、script、MCP、permission、runtime 与 resume probes 扩展为固定的跨宿主 conformance corpus。
- 增加 dependency/environment contract 与能够在宿主无法表达要求时 fail closed 的 policy IR。
- 准备 signed package、SBOM、publisher trust 与 revocation 证据，不再只依赖 prose provenance。
- 继续 solution 级 CI/build/test/pack hardening，并准备 Node.js/Python schema-facing 对齐面。

## Review And Commit 节奏

- 把每个 major slice 视为 review gate。
- 每完成一个 major slice，都先执行由当前运行环境或 agent 支持的 review、validation 与 commit loop，再进入下一个切片。
- 默认规划规则是：单次切片尽量控制在 50 个变更文件以内。
- 即使不到 50 个文件，只要触及协议、schema、包接缝或运行时控制行为，也要立刻 review。

## 给另一个 Agent 的交接清单

1. 阅读 `AGENTS.md`。
2. 阅读本路线图。
3. 阅读 `guides/ao-guide.md` 与 `guides/so-guide.md`。
4. 先看 `git status`，明确下一切片的 scope。
5. 让下一切片保持在可证据化 review 的规模内。
6. 在进入下一切片前，完成一次由当前运行环境或 agent 支持的 review、validation 与 commit loop。

## 不能回退的规则

- 保持 AO 与 SO 在打包、调用方式和心智模型上的独立。
- 保持 `Abstractions` 和 `Common` 不带私有云/AI 产品假设。
- 保持 workflow file 与 CLI sidecar 契约显式、machine-first。
- 保持对外文档双语且路径镜像。
