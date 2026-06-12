# 参考

[English](../../en/reference/README.md) | [根目录](../README.md)

参考页记录的是当前公开契约面，而不只是理想化设计说明。

如果另一个 agent 需要继续实现，应在动代码前把这一节和架构路线图、产品 guide 一起阅读。

## 分区

- [Workflow 术语](../architecture/workflow-terminology.md)
- [CLI 参考](cli.md)
- [MCP 参考](mcp.md)
- [Skills 输入输出参考](skills.md)
- [配置](configuration.md)
- [Workflow Schema](workflow-schema.md)
- [退出码](exit-codes.md)
- [Package 参考](packages/)
- [产品 Guide](products/)

## 延续实现时的阅读顺序

1. [产品 Guide](products/)
2. [Workflow 术语](../architecture/workflow-terminology.md)
3. [Workflow Schema](workflow-schema.md)
4. [CLI 参考](cli.md)
5. [MCP 参考](mcp.md)
6. [Skills 输入输出参考](skills.md)
7. [配置](configuration.md)
8. [退出码](exit-codes.md)

## 解读规则

- 如果文档、代码、测试之间有冲突，当前已 review 的代码和测试优先。
- 这一节的作用，就是把这种冲突尽量收缩到最小并显式暴露出来。
