# SkillOrchestrator Flow

[English](../../en/guides/so-guide-flow.md) | [Hub](so-guide.md) | [Reference](so-guide-reference.md) | [根目录](../README.md)

版本：draft
构建：repository source

## 用途

这页只保留 SkillOrchestrator 的最短治理执行路径。固定的 `so-guide.md` 是 guide hub；需要完整契约、治理规则、示例和反模式时，请阅读 [SO Guide 完整参考](so-guide-reference.md)。

## 流程

1. 从所属 skill 的 version block 和 package lock 绑定精确 SO 版本。
2. 在继续后续工作前，恢复并校验完整的已发布 SO runtime bundle。
3. 运行不带参数的 `dotnet so.dll --guide`，解析 JSON 结果并读取返回的 guide。
4. 如果是 enhancement 或 re-enhancement，检查 target skill 的 `SKILL.md`、package lock、workflow assets 和当前 guide 差异。
5. 根据这些资产和 fresh runtime evidence，只构建一次有界且可哈希的 shared review context。
6. 让独立的差异审查或规划审查都引用这份 context，并作为完整的 `ConcurrencyStrategy.All` 批次运行。
7. 等所有结果返回后统一汇总，再做一次协调修复；不要按 finding 一个个重写。
8. 修复后再运行第二个并行验证批次，汇总结果，然后按顺序执行 JSON、图/dataflow、compile、schema/demo 和 runtime 校验。
9. 复制一份 external runtime workflow instance，再对同一实例运行 `dotnet so.dll run`；每次 blocked seam 都使用 `dotnet so.dll resume`，直到形成最终完成证据。

## Runtime 检查

- 精确版本的已发布 bundle 通过启动和 dependency-closure 检查。
- 在规划或修改 target skill 前，fresh `--guide` 结果可读取。
- 正式执行时不修改 checked-in template。
- runtime copy 和 audit artifact 保持在 skill 目录之外。
- `compile` 只做校验；`run` 和 `resume` 才是正式执行路径。
- workflow file 中 workflow 自有的 schema 和控制元数据使用英文。
- 用户和业务 payload 可以保留来源语言。

## CLI 速查

```powershell
dotnet so.dll --guide
dotnet so.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
dotnet so.dll run --workflow-file <external-workflow.json> --context-file <context.json> --audit-output <external-audit-root>
dotnet so.dll resume --workflow-file <external-workflow.json> --result-file <result.json>
```

`--guide` 和 `compile` 用于准备或校验；只有公开的 `run` 与 `resume` 算作 SO 正式 workflow 执行。

## Blocked 返回

读取 `current_step_kind`、`skill_hint`、`required_inputs`、`workflow_file`、`event_log_file` 和 audit artifact links。需要用户输入时，只询问已经声明的决定或值；runtime-owned facts 则通过对应的 resume 路径返回结构化数据。保持同一份 external workflow copy。

## Target-Skill 完成

guide refresh、template authoring、compile 或 blocked 返回都不算 governed target-skill 完成。必须有 target-skill deliverable 变更、review-fix evidence、route 和 gate evidence，并在同一份 copy 上完成公开 run/resume 链路。

## Reference 章节



reference 索引已经按章节拆开，调用方可以只读取当前需要的契约。



- [Contracts](so-guide-reference-contracts.md)

- [Behavior And Responsibilities](so-guide-reference-behavior.md)

- [Governance](so-guide-reference-governance.md)

- [Examples](so-guide-reference-examples.md)

- [Anti-Patterns](so-guide-reference-anti-patterns.md)




## 继续阅读

- [SO Guide Hub](so-guide.md)
- [SO Guide 完整参考](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow 术语](../../en/architecture/workflow-terminology.md)
