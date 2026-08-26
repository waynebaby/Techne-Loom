# 贡献指南

[English](CONTRIBUTING.md)

Techne Loom 正在按切片逐步开源。欢迎贡献，但仓库仍在收敛第一版公开包和协议边界。

## 当前贡献规则

- 保持 `Techne.Loom.AgentOrchestrator` 与 `Techne.Loom.SkillOrchestrator` 在概念和运行职责上的分离，不要把它们折叠成同一个 runtime，也不要把一个描述成另一个的宿主。
- 在公开文档中，将 AO 的用户侧产品名称统一写成 `Loom Agent Execution Orchestrator`，同时保留 package ID、CLI 名称和源码身份不变。
- 保持 `src/dotnet`、`src/nodejs`、`src/python` 的 package-first 布局。
- 将 `/docs/en` 与 `/docs/zh-cn` 视为镜像树。新的公开文档必须双语成对落地后再合并。
- 将产品 guide 源文档放在 `/docs/en/guides/` 和 `/docs/zh-cn/guides/` 下。发布的 runtime package 会把英文页面直接放在 `tools/<rid>/docs/en/guides/`；可执行文件本身不内嵌 guide 页面。
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
