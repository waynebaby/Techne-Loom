# Released 0.3.282 Demo（已发布版本示例）

[English](Readme.md) | [Demo 索引](../README.zh-CN.md) | [仓库根目录](../../../README.zh-CN.md)

> [!IMPORTANT]
> 这是 `loom-enhanced-research` 的当前 released demo 快照，不是历史时间线。它是从既有治理快照迁移而来的 target-skill 副本，workflow authority 按 released Skill Orchestrator 0.3.282 的精确契约校验。

## 概览

| 项目 | 内容 |
| --- | --- |
| 目标 | 把研究 skill 从既有治理快照迁移到 released 0.3.282 runtime 契约 |
| Runtime | 精确版本 `0.3.282` 的 `Techne.Loom.SkillOrchestrator.Runtime.win-x64` |
| 主要结果 | emitter-aware workflow、MCP-first 入口、canonical resume projection 与迁移工具 |
| 完成规则 | 同一份外部 workflow copy 必须通过 public `run` 和 `resume`，直到最终 `Done` |

## 迁移记录

1. 将既有治理 target-skill 样本迁移到本 released 目录。
2. 将 workflow identity 调整为 `research_generation` 与 `target_skill_business`。
3. 使用精确 runtime 预检、本机 stdio MCP、有界片段检查和 fresh guide 重建入口。
4. 不再把普通 `ToolCall/noop` 的字面量 updates 当作 producer；字面量写入使用 `StateUpdate` 语义。
5. 外部结果使用顶层 `result` 的 canonical projection，required sibling fields 保持在 payload 顶层。
6. 四个迁移工具记录 dry-run candidate、hash、歧义发现、rollback、producer audit 与幂等性。

## 检查入口

- [loom-enhanced-research/SKILL.md](loom-enhanced-research/SKILL.md)
- [loom-enhanced-research/contract.json](loom-enhanced-research/contract.json)
- [loom-enhanced-research/assets/so-workflow/so-template.json](loom-enhanced-research/assets/so-workflow/so-template.json)
- [loom-enhanced-research/assets/so-workflow/so-package-lock.json](loom-enhanced-research/assets/so-workflow/so-package-lock.json)
- [loom-enhanced-research/assets/so-workflow/reference/runtime-semantic-migration.md](loom-enhanced-research/assets/so-workflow/reference/runtime-semantic-migration.md)
- [loom-enhanced-research/assets/so-workflow/reference/migration-script-playbook.md](loom-enhanced-research/assets/so-workflow/reference/migration-script-playbook.md)
- [loom-enhanced-research/assets/so-workflow/scripts](loom-enhanced-research/assets/so-workflow/scripts/)

## 验证形状

只有以下证据都存在时，才能认为这个 released demo 有效：

- 精确 0.3.282 guide 与 runtime 证据；
- 针对同一外部 workflow copy 的 MCP startup 证据；
- workflow compile 通过，并有可读的 analysis/dataflow 产物；
- 非空的 research、review、draft、migration 与 decision 证据；
- public `run` 和每次 `resume` 始终使用同一份 workflow 文件；
- terminal business-output gate 通过并到达最终 `Done`。

运行契约请查看 [SO guide](../../../docs/zh-cn/guides/so-guide.md) 和 [迁移参考](loom-enhanced-research/assets/so-workflow/reference/runtime-semantic-migration.md)。
