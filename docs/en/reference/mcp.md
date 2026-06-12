# MCP Reference

[中文](../../zh-cn/reference/mcp.md) | [Root](../README.md)

## AgentOrchestrator MCP Surface

AO does not expose a public MCP tool surface in this repository slice.

Use the CLI/package contract for AO instead.

### AO parameter-boundary note

- AO `compile`, `run`, and `resume` are CLI/package parameters in this project
- this project no longer exposes an AO MCP host or AO MCP tools

## SkillOrchestrator MCP Surface

SO does not currently expose a public MCP tool surface in this repository slice.

Use the CLI/package contract for SO instead.

### SO parameter-boundary note

- SO `compile` is a CLI/package parameter for validating an existing workflow JSON and emitting Mermaid/HTML outputs
- this project does not expose a public SO MCP surface
