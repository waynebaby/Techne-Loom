# Loom Agent Execution Orchestrator Guide

[English](../../en/guides/ao-guide.md) | [根目录](../README.md)

版本：0.3.283-beta
构建：已发布的 0.3.283-beta 包

## Guide 输出

运行不带参数的 `dotnet ao.dll --guide`。它会返回与当前版本匹配的英文 guide 的 `version`、`docs_root` 和 `guide_path` 实际路径 JSON。

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## 信息 Hub

这个固定的 `guide_path` 入口刻意保持简短。先读本页，再根据需要阅读执行 flow 和完整 reference。

- [AO Flow](ao-guide-flow.md)
- [AO Guide 完整参考](ao-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow 术语](../../en/architecture/workflow-terminology.md)

## 产品定位

Loom Agent Execution Orchestrator 面向不确定环境下的探索式工作。它保存 workflow 状态，在外部 seam 处返回结构化 blocked 控制数据，并通过结构化 resume 结果继续执行。

## 核心流程

1. 绑定精确 AO 版本，准备有效的已发布 runtime。
2. 运行不带参数的 `dotnet ao.dll --guide`，并读取返回的 guide 路径。
3. 创建或复用一份位于 skill 目录之外的 external workflow instance，把 runtime state 和 audit output 保持在 skill 目录之外。
4. 对同一份 external workflow 执行 compile，再执行 run。
5. 返回 blocked 后执行要求的外部动作，并用结构化数据恢复同一实例。
6. 只有 runtime 完成且请求的业务交付物可以核验时才停止。

## 正式入口

- `dotnet ao.dll run` 和 `dotnet ao.dll resume` 是 AO 的正式 skill run。
- `--guide`、`compile`、`prompt-plan` 和 `prompt-replan` 用于准备或恢复。
- 在整个 run/resume 链路中保持同一版本、launch descriptor 和 workflow instance。

## Workflow 文件语言

Workflow 定义文件是 AO、SO 以及受 Loom 治理 target skill 的规范英文信息载体。workflow 自有的 schema 和控制元数据使用英文；用户/业务 payload 和面向用户的本地化输出可以保留来源或请求语言。

## 内容边界

完整的操作流程、契约、职责、示例和反模式位于 [AO Guide 完整参考](ao-guide-reference.md)。hub 路径继续作为 `guide_path`，完整文档包会携带链接的 flow 与 reference 页面。