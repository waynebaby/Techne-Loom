# 贡献指南

[English](CONTRIBUTING.md)

Techne Loom 正在按切片逐步开源。欢迎贡献，但仓库仍在收敛第一版公开包和协议边界。

## 当前贡献规则

- 保持 AO 与 SO 的概念分离，不要把它们折叠成同一个 runtime，也不要把一个描述成另一个的宿主。
- 保持 `src/dotnet`、`src/nodejs`、`src/python` 的 package-first 布局。
- 将 `/docs/en` 与 `/docs/zh-cn` 视为镜像树。新的公开文档必须双语成对落地后再合并。
- 将产品 guide 源文档放在 `/docs/<lang>/reference/products/` 下，保证 `ao --guide` 与 `so --guide` 能输出版本匹配内容。
- 优先提交小而可审查的切片。重大切片在进入下一步前应先完成 review。

## 开发流程

1. 先阅读 `AGENTS.md` 中的仓库规则。
2. 用最小改动关闭一个具体缺口。
3. 为变更行为补充或更新测试。
4. 运行足以证伪当前改动的最窄验证。
5. 保持公开文档与代码、包边界一致。

## Pull Request 期望

- 说明产品边界影响。
- 标出 package 或 CLI 契约变化。
- 说明文档更新。
- 附上验证证据。

长版贡献政策位于 `docs/zh-cn/governance/policies/contributing.md`。
