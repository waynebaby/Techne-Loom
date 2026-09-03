# Loom Agent Execution Orchestrator Guide：Anti-Patterns

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [English](../../en/guides/ao-guide-reference-anti-patterns.md) | [根目录](../README.md)

版本：0.3.288
构建：已发布的 0.3.288 包

## Anti-Patterns

- 把 AO 当成通用聊天外壳。
- 返回只包含 prose、却没有 workflow、node 或 artifact 状态的数据。
- 用 AO 执行本应属于 SO 的确定性逐步 skill 逻辑。
- 没有明确理由就绕开文档化的 CLI / package 控制路径，改写成私有 wrapper。
- AO 需要 weave-out request 时，不发结构化 boundary，而是用自由叙述去暗示。
- 当 runtime 版本已经由 CI/CD 管理的 skill package version block 或 checked-in runtime lock 绑定时，受治理 skill 仍然要求用户再选 package / 通道。
