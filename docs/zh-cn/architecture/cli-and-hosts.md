# CLI 与宿主边界

[English](../../en/architecture/cli-and-hosts.md)

AO 与 SO 之所以暴露不同的 host 模型，是因为它们解决的是不同问题。

## AgentOrchestrator

- 规范接口：`MCP/stdio`
- 轻量 CLI：用于回放、调试和文件驱动测试
- 主要职责：输出控制态决策、当前 workflow 路径和边界元数据
- 目标实现路径：官方 `ModelContextProtocol` C# SDK，优先使用主包 `ModelContextProtocol` 提供 hosted stdio server 行为。
- 规划方向：保留 sampling/planner 路线，而不是用自定义 transport 层替代 MCP。
- 当前仓库状态：guide 与 scaffold 已存在，但公开 AO runtime 尚未真正实现。

## SkillOrchestrator

- 规范接口：本地 CLI 与 package 契约
- 主要职责：编译或加载 workflow，执行 SO 自己拥有的步骤，并在需要外部工作时返回严格 payload
- 支持简写调用，但必须先编译成持久化 workflow，再执行
- 当前 CLI 分层：
  - 面向 shell 的 wrapped command output 放在 `<wrapped_exec>` 块里
  - SO 自己的控制元数据放在独立的 `<so_property>` 块里
  - workflow state 持久化到 workflow file，事件历史落在 `.events.jsonl` sidecar

## 宿主边界规则

不要把 AO 当成 SO 的外壳，也不要把 SO 当成 AO 的子 runtime。它们可以共享低层契约，但必须独立打包、独立调用。

## 继续实现时的实践规则

如果另一个 agent 要继续推进实现：

- 把 AO 文档视为下一批产品切片的主要目标
- 把 SO 文档和测试视为当前公开基线，不要随意破坏
- 不要把 MCP-specific 代码重新塞回 `Abstractions` 或 `Common`
