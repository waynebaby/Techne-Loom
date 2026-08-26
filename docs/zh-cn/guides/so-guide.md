# SkillOrchestrator Guide

[English](../../en/guides/so-guide.md) | [根目录](../README.md)

版本：draft
构建：repository source

## Guide 输出

运行不带参数的 `dotnet so.dll --guide`。它会返回与当前版本匹配的英文 guide 的 `version`、`docs_root` 和 `guide_path` 实际路径 JSON。

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

## 信息 Hub

这个固定的 `guide_path` 入口刻意保持简短。先读本页，再根据需要阅读治理执行 flow 和完整 reference。

- [SO Flow](so-guide-flow.md)
- [SO Guide 完整参考](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow 术语](../../en/architecture/workflow-terminology.md)

## 产品定位

SkillOrchestrator 执行确定性的 workflow 步骤，只有在 workflow 完成或到达需要外部参与的 seam 时才返回。它是受 Loom 治理 target skill 的正式执行权威。

## 核心流程

1. 绑定精确 SO 版本，恢复完整的已发布 runtime bundle。
2. 运行不带参数的 `dotnet so.dll --guide`，并读取返回的 guide 路径。
3. 检查 target skill，并规划它的输入、输出、route、gate、seam 和 evidence。
4. 使用指定的 workflow designer 创建或刷新 template。
5. compile、检查并确认 template 和 audit artifact。
6. 复制一份 external runtime workflow instance，然后在同一实例上 run/resume，直到形成最终完成证据。

## 正式入口

- `dotnet so.dll run` 和 `dotnet so.dll resume` 是 SO 的正式 workflow run。
- `--guide`、`compile`、`status` 和 inspection 命令用于准备或校验。
- guide refresh、template authoring、compile 结果或 blocked 返回本身都不构成治理完成。

## Workflow 文件语言

Workflow 定义文件是 AO、SO 以及受 Loom 治理 target skill 的规范英文信息载体。workflow 自有的 schema 和控制元数据使用英文；用户/业务 payload 和面向用户的本地化输出可以保留来源或请求语言。

## 内容边界

完整的治理流程、target-skill 规则、契约、示例和反模式位于 [SO Guide 完整参考](so-guide-reference.md)。hub 路径继续作为 `guide_path`，完整文档包会携带链接的 flow 与 reference 页面。