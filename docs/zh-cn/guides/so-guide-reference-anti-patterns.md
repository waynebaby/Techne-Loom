# SkillOrchestrator Guide：Anti-Patterns

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-anti-patterns.md) | [根目录](../README.md)

版本：0.3.288
构建：已发布的 0.3.288 包

## Anti-Patterns

- 让调用方只能从 prose 推测下一步动作。
- 把 memory 藏在 prompt 里，而不是 workflow context 里。
- 不经编译就直接运行简写命令，而不生成持久化 workflow。
- 把 wrapped command output 和 SO 边界载荷混成一条不可分辨的纯文本流。
- 当 runtime 版本已经由 CI/CD 管理的 skill package version block 或 checked-in runtime lock 绑定时，受治理 skill 仍然要求用户再选 package / 通道。
