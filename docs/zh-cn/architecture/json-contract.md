# Workflow JSON 契约

[English](../../en/architecture/json-contract.md)

canonical JSON 契约是跨生态、跨调用方的可移植层。

## 契约目标

- 在不绑定具体 host 的前提下编码 workflow 结构。
- 保留显式 step kind、状态、历史、产物与等待信息。
- 同时支持 SO 的确定性执行和 AO 的控制态交换。

## 最低方向

- 一个 workflow instance 至少包含标识、节点、当前位置、上下文、历史和状态。
- step 或 transition kind 在序列化形式中保持显式。
- block 返回必须是 machine-first payload，并带 required input 契约。
- AO 的 resume 输入与 SO 的外部步骤结果都应使用结构化 envelope，而不是只靠自然语言回传。

schema 与具体示例放在参考文档中，并会与公开 package 一起实现。
