# MCP 参考

[English](../../en/reference/mcp.md) | [根目录](../README.md)

## AgentOrchestrator MCP 表面

当前仓库切片里，AO 不再公开 MCP tool 表面。

AO 请使用 CLI / package 契约。

### AO 参数边界说明

- AO 的 `compile`、`run`、`resume` 在本项目里都属于 CLI/package 参数面
- 本项目不再公开 AO MCP 宿主或 AO MCP tools

## SkillOrchestrator MCP 表面

当前仓库切片里，SO 还没有公开 MCP tool 表面。

SO 请使用 CLI / package 契约。

### SO 参数边界说明

- SO 的 `compile` 属于 CLI/package 参数，用于校验已有 workflow JSON 并输出 Mermaid/HTML
- 本项目不公开 SO MCP 表面
