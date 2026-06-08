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

## 当前公开契约分层

### Workflow file

- 当前持久化 workflow file 使用 camelCase 属性名。
- 多态 task node 通过 `$kind` 进行区分。
- 嵌套 `context`、command parameters 以及 side-loaded object 值在 round-trip 后，应该仍然表现为 dictionary/list，而不是裸 `JsonElement`。

### SO 控制载荷

- `<so_property>` 中的外层 JSON envelope 当前使用 camelCase 字段：`type`、`timestampUtc`、`payload`。
- 该 envelope 内部的公开 payload 使用 snake_case 作为稳定的 caller-facing 字段风格，例如 `workflow_file`、`event_log_file`、`current_node_id`、`required_inputs`、`memory_for_next_step`。

### SO resume envelope

- `so resume --result-file` 当前期望读取一个带 `transition_id`、可选 `correlation_key` 和 `payload` 的 JSON 对象。

## 当前 runtime 保证

- 来自 workflow 文件、context 文件和 result sidecar 的嵌套 JSON 对象，会在求值前规范化成 runtime dictionary/list。
- 模型里存在但 runtime 尚未实现的语义必须显式失败，而不是静默降级。
- workflow file 与 CLI sidecar 相关，但它们不是同一个无差别 JSON 面。

## 当前范围边界

- 当前公开 SO runtime 只支持一套完整物化的 workflow file 契约。
- 它还没有把每一种 CLI sidecar 都单独暴露成独立 schema artifact。
- AO control payload 目前仍是文档化目标，等 AO 公开 runtime 落地后再真正成形。

schema 与具体示例放在参考文档中，并会与公开 package 一起实现。
