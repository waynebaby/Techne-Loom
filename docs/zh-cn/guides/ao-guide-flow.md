# Loom Agent Execution Orchestrator Flow

[English](../../en/guides/ao-guide-flow.md) | [Hub](ao-guide.md) | [Reference](ao-guide-reference.md) | [根目录](../README.md)

版本：0.3.283-beta
构建：已发布的 0.3.283-beta 包

## 用途

这页只保留 Loom Agent Execution Orchestrator 的最短操作路径。固定的 `ao-guide.md` 是 guide hub；需要完整契约、示例或反模式时，请阅读 [AO Guide 完整参考](ao-guide-reference.md)。

## 流程

1. 先判断请求需要业务交付物，还是只需要校验 runtime。
2. 从所属 skill 或 package source 绑定精确 AO 版本；需要区分时再从版本推导 package channel。
3. 准备一份有效 runtime：使用 package resolver 和精确 bundle；只有在明确进行仓库调试时，才使用 repository debug mode。
4. 运行不带参数的 `dotnet ao.dll --guide`，解析 `version`、`docs_root`、`guide_path`，并读取返回的 guide。
5. 创建或复用一份位于 skill 目录之外的 external workflow instance，并保持 session、workflow 和 audit 路径在 skill 目录之外。
6. 对同一份 external workflow 运行 `dotnet ao.dll compile`。
7. 对同一实例运行 `dotnet ao.dll run`。如果返回 blocked，就执行要求的外部动作，并用结构化结果恢复同一实例。
8. 持续执行，直到 AO runtime 完成，并且请求的业务交付物可以核验。

## Runtime 检查

- .NET host 或选定的 self-contained RID runtime 通过启动前置检查。
- `.NET CLI 模式`下必须存在 `ao.dll`、`ao.deps.json` 和 `ao.runtimeconfig.json`。
- Windows PowerShell 5.1 下的 package-channel 解包必须使用 ZIP 安全方式。
- 从 `--guide` 到 `compile`、`run`、`resume`，始终复用同一 launch descriptor、版本和 RID。
- workflow file 中 workflow 自有的 schema 和控制元数据必须使用英文，作为信息载体。
- 用户和业务 payload 可以保留来源语言。

## CLI 速查

```powershell
dotnet ao.dll --guide
dotnet ao.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
dotnet ao.dll run --objective-file <objective.md> --session-dir <session-dir> --instance-file <external-workflow.json> --audit-output <external-audit-root>
dotnet ao.dll resume --session-dir <session-dir> --session-id <id> --result-file <result.json>
```

`--guide`、`compile`、`prompt-plan` 和 `prompt-replan` 用于准备或恢复；只有 `run` 与 `resume` 是 AO 的正式 skill run。

## Blocked 返回

读取结构化 blocked payload，保留 `session_id`、`workflow_file`、`workflow_instance_file`、`event_log_file`、`current_node_id` 和最新 transition 数据。第一次创建图时使用 `prompt-plan`；只有后续 frontier 或 `tbr` 路径需要重设计时才使用 `prompt-replan`。恢复时传入 `transition_id`、可选的 `correlation_key` 和结构化 `payload`。

## 角色

- **Caller：**提供 objective，执行外部动作，保持 session 连续，并用结构化数据恢复。
- **Author：**保持 workflow graph、控制字段、证据路径和所有权清晰。
- **Outer agent：**判断 frontier，并在 blocked 返回之间保留上下文。

## Reference 章节



reference 索引已经按章节拆开，调用方可以只读取当前需要的契约。



- [Contracts](ao-guide-reference-contracts.md)

- [Plan And Replan](ao-guide-reference-plan-replan.md)

- [Behavior And Responsibilities](ao-guide-reference-behavior.md)

- [Examples](ao-guide-reference-examples.md)

- [Anti-Patterns](ao-guide-reference-anti-patterns.md)




## 继续阅读

- [AO Guide Hub](ao-guide.md)
- [AO Guide 完整参考](ao-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow 术语](../../en/architecture/workflow-terminology.md)
