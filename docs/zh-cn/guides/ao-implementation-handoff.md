# AO 实现交接

[English](../../en/guides/ao-implementation-handoff.md) | [根目录](../README.md)

这份指南的目标，是让另一台机器可以基于当前已提交的 runtime 继续推进 AO 的实现与契约加固。

## 当前 Ground Truth

- 已提交的 AO runtime 已经在 `.NET` 中落地。
- 权威的公开设计契约是 [AgentOrchestrator Guide](../reference/products/ao-guide.md)。
- 目标 runtime 路线是文档化的 CLI / package 契约。
- 设计上必须保留显式的 weave-out request 数据，不能把外部比较或规划请求藏进 prose。

## 必须保持不变的约束

- AO 和 SO 是两个独立产品，不能把 AO 写成 SO 的父运行时。
- AO 在解释层面面向探索，并在 seam 处 weave out，而显式 boundary payload 仍然是协议面；SO 保持确定性和逐步执行。
- AO 的调用方必须用结构化数据恢复，而不是自由叙述总结。
- AO 的控制输出必须继续保持 machine-first：`session_id`、`workflow_file`、`event_log_file`、`status`、`boundary_reason`、`next_frontier`，以及可选的 `weave_out_request` 数据。

## 下一批加固应从这里继续

1. 保持 AO 的 resume 契约严格，拒绝过期或不匹配的 weave-back envelope。
2. 保持文档化的 `run` / `resume` CLI envelope 与 AO 的 weave-out / weave-back 控制契约一致。
3. 保持 AO 术语与 repo 级 weave out / weave back 术语表一致。
4. 确保 workflow 和 event 产物足够稳定，能跨轮恢复。

## 下一批 AO slice 的最低完成线

- AO 项目的 `dotnet build` 必须通过。
- AO 返回 machine-readable 的 boundary payload 与 result payload。
- AO 会持久化 workflow 与 event-log 路径。
- AO 能从结构化 result file 恢复，并拒绝过期 weave-back envelope。
- AO 文档明确说明 weave-out request 数据会出现在控制载荷的什么位置。

## 推荐验证方式

- 加一个 smoke test：启动 AO、强制产生 boundary、用 result file 恢复，并验证 workflow 与 event-log 连续性。
- 加一个测试，断言 weave-out request 数据以结构化形式发出。
- 任何新的 boundary 字段进入实现时，都要同步校准 AO guide。
