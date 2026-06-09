# 快速开始

[English](../../en/getting-started/README.md)

如果你想用最短路径从仓库 clone 到第一个可运行 workflow，请从这里开始。

## 推荐阅读顺序

- [安装与运行](install.md)
- [第一个 Workflow](first-workflow.md)

首个公开版本是 `.NET` 优先。Node.js 与 Python 目录已经预留、文档已对齐 schema，但 v1 还不是可运行实现。

## 当前“可运行”到底指什么

当前已 review 的 SO 切片有两条真正可跟跑的入口：

1. `so ls <path>`
说明：
这是最快的端到端 smoke path。它会把 shorthand 输入编译成 workflow，运行 wrapped command-line listing，然后输出 `<so_property>` 结果块。

2. `so run --workflow-file ...`
说明：
这是作者定义 workflow 的通用路径。需要你显式控制 step kind、boundary 行为和 resume envelope 时就用它。

这两条路径都在 [第一个 Workflow](first-workflow.md) 里给了具体示例。
