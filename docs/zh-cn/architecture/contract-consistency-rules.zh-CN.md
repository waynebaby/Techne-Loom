# Contract 一致性规则

[English](../../en/architecture/contract-consistency-rules.md) | [根目录](../README.md)

这些规则保证 target contract、workflow binding 与 runtime context 一致，同时不把 AO 或 SO 变成领域解释器。

## 每层只有一个权威

- `assets/so-workflow/contract.json` 是 target 的业务 contract。
- workflow JSON 是执行路径和 gate contract。
- AO/SO runtime 是 bounded reader、缓存、路由和持久化权威。
- 业务步骤是领域解释器。

不要把完整 contract 复制到 workflow JSON，也不要创建第二份可变业务真相。

## Target Contract 必须存在

每个 Loom 治理的 enhanced target skill 都必须有 `assets/so-workflow/contract.json`。它必须是 JSON object，包含非空 `name`，以及 object 类型的 `inputs`、`outputs`、`default_assumptions`。可以增加领域自定义字段。

enhancement workflow 会在 planning 和 authoring 前读取该文件，把它作为 bounded reference pack 的 `current_contract`，并记录路径、parse 结果和可选 source hash。初始 alignment gate 是强制的。B+ 允许在该 gate 之后修改 contract，后续引用步骤读取最新内容。

## Workflow Binding

root `contractBinding` 指向 target contract；步骤的 `contractRefs` 是该文件里的 JSON Pointer。Compile 只检查字段形状、相对路径、保留字段和 pointer 语法，不校验任意领域字段。

## Runtime 一致性

每个引用步骤都会读取一份字节快照，并从同一份快照完成 parse、可选 hash 与 pointer projection。path-plus-hash 缓存可以复用未变化内容；文件变化后重新读取。文件缺失、格式错误、pointer 无效、路径越界或超过限制时 fail closed。

## Context 分离

步骤通过 `contract_context` 获得 bounded fragments。runtime 读取 metadata 放在 runtime-owned audit/context metadata 中。当前 workflow context 另行 bounded 处理。contract fragment 不能静默覆盖普通业务 context key。

## AO/SO 对齐

AO 与 SO 共用 Common provider 和限制，但编排职责独立：

- SO 在有引用的确定性 transition 前注入 fragment。
- AO 把 fragment 注入当前 boundary/planning workflow context。
- 两个 runtime 都不决定 target 领域如何解释 contract。

## 验证

B+ 变更只有在以下条件都满足时才完整：

- AO/SO 生成的 schema/demo 与 binding 字段一致；
- 两份 demo 都通过对应 runtime compile；
- provider、AO、SO、resume、失败和路径边界测试通过；
- 英文/中文页面互相链接；
- enhancement workflow 在 planning 和 final Done 前都能证明 `current_contract` evidence 存在。
