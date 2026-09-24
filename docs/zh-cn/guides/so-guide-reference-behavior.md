# SkillOrchestrator Guide：Behavior And Responsibilities

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-behavior.md) | [根目录](../README.md)

<!-- guide-version:start -->
版本：0.3.321
构建：已发布的 0.3.321 包
<!-- guide-version:end -->





## Behavior

当步骤本地且确定时，SO 直接执行：

- `ToolCall`
- `StateUpdate`
- `ArtifactEmit`
- `MemoryRead`
- `MemoryWrite`

当 `MemoryRead` 被用于 re-enhancement 或 governance review 阶段去检查 checked-in skill being enhanced 资产时，它必须读取真实文件快照，而不是占位式 context copy，并且每一个被检查的资产路径都必须留在声明的 asset root of the skill being enhanced 之下。

遇到这些外部拥有的步骤时，SO 会 weave out，并返回指导：

- `ModelThink`
- `McpCall`
- `SubagentCall`
- `AskUser`
- `WaitResume`

`ConditionBranch` 在 workflow 中保持显式，并由 SO 内部做确定性求值。

当前公开 runtime 支持说明：

- v1 完整支持的 transition-group 策略是 `FirstSuccess`。
- `FirstResponse` 与 `All` 仍保留在模型层中，但当前公开 runtime 在多 ready transition 场景下会显式失败，而不是假装支持。

## Responsibilities

### Caller

- 提供待校验的 workflow JSON。
- 如需下载本地运行时，遵循[平台检测步骤](../reference/runtime/platform-detection.md)：host 预检成功后，校验并使用精确版本的 SO IL bundle（`Techne.Loom.SkillOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`）；如果 host 缺失或无法启动 CLI，则为检测出的 RID 校验并使用一个精确版本的 `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package。
- 每次启动新的正式 `run` 前，都要先把 checked-in source template 复制到运行时 temp 或 execution-output 目录；当 workflow 之后进入 blocked，`resume` 必须继续作用于同一份已持久化的 runtime copy。
- 当 SO weave out 时执行外部动作。
- 用结构化 weave-back envelope 恢复 SO。
- 把 `<so_property>` 视为权威 SO 控制载荷。
- 把 `<wrapped_exec>` 视为面向 shell 的流式 wrapper 输出表面。
- 在 resume sidecar JSON 中使用 `transition_id`、`correlation_key` 和 `payload`。
- 让 runtime workflow copy、event sidecar 和 audit 输出都位于 skill-owned 目录之外。
- 每次 `dotnet so.dll` CLI call 后，think-out-loud 更新都必须先输出当前已验证 Mermaid artifact 的 Markdown link，并紧接着用 `text` 围栏重复同一个规范化 `/` 路径；然后按 Mermaid、HTML、Analysis、Dataflow 的顺序重复这四组 link 与路径围栏。四组内容之后，使用当前交互语言输出 `## 执行信心: x%` 和一句简短原因。只能使用本次调用或最近一次已验证连续状态中的路径；`not_emitted` 时说明 render 未变化，`delivery_failed` 时说明失败与下一步，不得猜测或输出无效 link。Mermaid card 或 notification 只能补充这段固定内容，不能替代它。所有进度、阻塞、错误和完成消息都要用当前交互语言说人话；不要把 `FPx`、`xxx_preflight_xxx`、节点 ID、gate ID 或内部字段名作为面向用户的主语，精确标识只放在技术细节里。
- 把 `workflow.analysis.json` 视为 machine-readable 摘要，用来审阅输入、输出族、分支、循环、用户 seam、运行时 seam、gate 与图灵完备控制风险。
- 只有在明确确认 audit 输入未变化时，才能使用 `dotnet so.dll copy-audit-step`。它的 `audit-reuse.json` 会把复制产物标记为 `artifact_origin: verified-copy` 与 `official_execution_evidence: false`；复制产物不能替代 `run`、`resume`、事件日志、gate 或 guide evidence。

### Author

- 显式编码 step kind。
- 当下一步需要上下文提炼时，定义 memory extraction 提示。
- 保证本地确定性步骤没有隐藏侧通道。

### Outer-agent

- 字面消费 `skill_hint`。
- 在阻塞 seam 与对应的 resume handoff 之间保留 `memory_for_next_step`。
- 不要超出当前阻塞步骤契约进行即兴发挥。
