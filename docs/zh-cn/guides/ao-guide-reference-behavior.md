# Loom Agent Execution Orchestrator Guide：Behavior And Responsibilities

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [English](../../en/guides/ao-guide-reference-behavior.md) | [根目录](../README.md)

版本：draft
构建：repository source

## Behavior

AO 应当：

- 检查当前上下文
- 扩展或细化 workflow frontier
- 在澄清、探测、委派、重规划和完成之间做选择
- 持久化决策、产物和 blocked payload 元数据
- 维护可变 workflow 文件和 append-only event/snapshot log
- 当调用方请求 prompt-plan 或 prompt-replan 支持表面时，由代码生成 AO 自有 planner / replanner prompt 文本
- 当需要外部比较、规划或类似分析时，通过显式的 blocked payload 字段表达 weave-out request，而不是把它藏进不透明 prose
- 当 resume envelope 的 `transition_id` 与当前待处理 payload 字段所记录的 blocked workflow seam 不匹配时，明确拒绝恢复
- 当会话元数据确实需要参与执行时，把它视为显式 CLI 输入，而不是依赖隐藏的宿主状态

AO 不应当：

- 冒充确定性 skill 执行器
- 把控制态藏进纯叙述文本
- 把所有决策都折叠进一次不透明的 prompt 往返
- 不要绕开文档化的 CLI 控制面去写私有胶水
- 不要把 prompt-plan 或 prompt-replan 当成与 run/resume 同级的正式 AO run surface

## Responsibilities

### Caller

- 提供目标和当前已知上下文。
- 如需下载本地运行时，遵循[平台检测步骤](../reference/runtime/platform-detection.md)：.NET 9 host 预检成功后，校验并使用精确版本的 AO IL bundle（`Techne.Loom.AgentOrchestrator`、`Techne.Loom.Common` 与 `Techne.Loom.Abstractions`）；如果 host 缺失或无法启动 CLI，则为检测出的 RID 校验并使用一个精确版本的 `Techne.Loom.AgentOrchestrator.Runtime.<rid>` package。
- 执行 AO 请求的外部动作。
- 用结构化结果恢复 AO。
- 在多轮之间保留 `session_id`。
- 保持稳定且可写的会话目录，并通过 `--session-dir` 传入。
- 让 `--session-dir` 输出和任何 `--audit-output` 都位于 skill-owned 目录之外。
- 每次 AO progress update 都要在 think-out-loud 输出中带上当前 workflow 的 Mermaid Markdown 与 HTML 路径。

### Author

- 定义控制态文件如何存储和暴露。
- 保持 AO 输出稳定且 machine-first。
- 让 weave-out request、它们当前的 wire 字段，以及对应 event log 轨迹保持可见，而不是埋进私有启发式里。

### Outer-agent

- 决定是否采纳 AO 给出的 frontier。
- 在恢复之间保留产物引用与 blocked payload 上下文。
- 把 AO 当作探索式协调者，而不是执行 SO 拥有的确定性工作的地方。
- 如果需要预编写 AO workflow file，由 outer-agent 生成满足 AO snapshot schema 的 JSON，再调用 `dotnet ao.dll compile`。
- 审计产物、中间 workflow 物化文件，以及可在对话中引用的运行输出，默认都放在运行时 temp 根、repo 根 temp 根，或用户明确指定的 execution output 根，不能默认落到 skill 文件夹里。

### Schema 与 Demo 导出

请使用同一份 runtime，把当前 workflow schema 合同和可以编译的 demo 成对写出：

```powershell
dotnet ao.dll --schema-demo-output outputs\schema-demo
# Windows self-contained runtime 使用：
.\ao.exe --schema-demo-output outputs\schema-demo
```

这个命令会一次性写出完整文件集：`workflow.schema.json`、`workflow.demo.json`、`workflow.model.cs`、`workflow.demo.cs` 与 `workflow.demo.verify.cs`。其中两个可执行示例是普通 `.cs` 文件；把它们的路径传给 `--script-file` 和 `--verify-script`，不需要 project 文件，也不需要额外安装 C# script runtime。请使用同一份 runtime 通过 `compile --workflow-file <path>` 校验生成的 demo。除非明确要求作为交付物，否则生成文件必须放在 skill 目录之外。

```guide-template
dotnet ao.dll compile \
  --workflow-file ao-plan.json \
  --audit-output outputs/audit
```

`ao-plan.json` 可以继续作为 checked-in 或交换用的 source artifact，但 `outputs/audit` 应位于 skill 文件夹之外。

```guide-template
dotnet ao.dll run \
  --objective-file objective.md \
  --context-file context.json \
  --session-dir outputs/sessions \
  --audit-output outputs/audit
```

`outputs/sessions` 和 `outputs/audit` 都必须位于 skill-owned 目录之外，避免 AO runtime state 写脏 checked-in skill assets。

```guide-template
dotnet ao.dll resume \
  --session-dir outputs/sessions \
  --session-id 20260609010101_abc12345 \
  --result-file latest-boundary-result.json
```

Resume 必须继续指向同一个外部 session 目录，而不能指向 skill 文件夹下的路径。

```guide-checklist
- 目标清晰明确
- 当调用方希望保留可复用的 AO workflow snapshot artifact 时，调用 agent 会先编写 AO workflow JSON 文件，再进入校验交接
- compile 在执行前会先产出 Mermaid Markdown 与 HTML 校验输出
- 调用方已保存 session_id
- 会话目录稳定且可写
- 会话目录和 audit 输出都位于 skill 文件夹之外
- 产物引用可持久化
- 调用方可以用结构化数据恢复
- 控制输出已持久化并可审计
- 保持文档化的 CLI 控制路径
- weave-out request 必须显式表达，不能藏在 prose 里
- 审计和中间输出默认放在 skill 文件夹之外的 temp / execution-output 根目录
- compile 不得覆盖已有 artifact 文件，必须失败
```
