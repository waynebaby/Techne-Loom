# AO 实现交接

[English](../../en/guides/ao-implementation-handoff.md)

这份指南的目标，是让 AO 即使在当前提交里还没有带上 runtime，也能在另一台机器上继续推进实现。

## 当前 Ground Truth

- 已提交的 AO runtime 还没有落地。
- 权威的公开设计契约是 [AgentOrchestrator Guide](../reference/products/ao-guide.md)。
- 目标 runtime 路线是官方 `ModelContextProtocol` C# SDK + `MCP/stdio`。
- 设计上必须保留结构化的 sampling-planner 路径，不能把 planner 请求藏进 prose。

## 必须保持不变的约束

- AO 和 SO 是两个独立产品，不能把 AO 写成 SO 的父运行时。
- AO 面向探索和边界返回；SO 保持确定性和逐步执行。
- AO 的调用方必须用结构化数据恢复，而不是自由叙述总结。
- AO 的控制输出必须继续保持 machine-first：workflow path、event log path、status、boundary reason、next frontier，以及可选的 sampling request。

## 下一批 AO 实现应从这里继续

1. 在 `src/dotnet/Techne.Loom.AgentOrchestrator` 下创建 AO host 入口。
2. 先接官方 MCP server/session 路径，再叠加产品自己的 orchestration 逻辑。
3. 实现 `run` 和 `resume` surface，并持久化可变 workflow file 与 append-only event log。
4. 为 clarification、delegation、tool probing 和 sampling request 输出结构化 boundary payload。
5. 确保 workflow 和 event 产物足够稳定，能跨轮恢复。

## 下一批 AO slice 的最低完成线

- AO 项目的 `dotnet build` 必须通过。
- AO 返回 machine-readable boundary payload，而不是 placeholder 文本。
- AO 会持久化 workflow 与 event-log 路径。
- AO 能从结构化 result file 恢复。
- AO 文档明确说明 sampling request 会出现在控制载荷的什么位置。

## 推荐验证方式

- 加一个 smoke test：启动 AO、强制产生 boundary、用 result file 恢复，并验证 workflow 与 event-log 连续性。
- 加一个测试，断言 sampling/planner 请求以结构化数据形式发出。
- 任何新的 boundary 字段进入实现时，都要同步校准 AO guide。
