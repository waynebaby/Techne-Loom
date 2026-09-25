# Contract Context 参考

[English](../../en/architecture/contract-context-reference.md) | [根目录](../README.md)

本页定义 AO、SO 与 受 Loom Skill Orchestrator 治理的 skill being enhanced 共享的 B+ contract context provider。

## 含义

目标 skill 的 `assets/so-workflow/contract.json` 是静态业务字典。它不会被编译进通用 workflow schema，AO 和 SO 也不会把它解释成 CDD、研究或其他领域的业务逻辑。

- `compile` 只校验 workflow 结构以及 contract binding/reference 语法。
- SO 在每个引用 transition 执行前读取 contract。AO 在当前 boundary 写入 planning context 前，读取并合并当前 boundary 候选 transition 的引用。
- 业务步骤负责解释 fragment，并决定业务输出或下一步行动。
- runtime 状态与静态 contract 分开保存。

## Binding

workflow 可以声明 root binding：

```json
{
  "contractBinding": {
    "path": "assets/so-workflow/contract.json",
    "format": "json"
  }
}
```

transition 只声明自己需要的 fragment：

```json
{
  "contractRefs": [
    "/inputs/request",
    "/outputs/result"
  ]
}
```

路径在声明的 target asset root 下解析。受治理 workflow 拒绝绝对路径和 `..` traversal。

## Runtime 读取

引用步骤开始前，provider 会：

1. 读取 contract 文件的一份字节快照。
2. 对同一份快照做 JSON parse。
3. 解析每个 `contractRefs` JSON Pointer。
4. 应用字节数、深度、数组和 object property 限制。
5. 只把 bounded fragment 返回给步骤。
6. 把路径、读取时间、可选 SHA-256、缓存状态、返回字节数和 refs 写入 runtime-owned audit/context metadata。

AO 在 planning boundary 使用相同 provider。它会合并当前 state 候选 transition 的 refs，让分支选择使用一份 bounded context；这不表示每个候选 transition 都会执行。

没有 `contractRefs` 的步骤不会读取 contract。

## 手动修改

workflow 运行期间允许编辑 contract 文件。provider 使用 path-plus-hash 缓存：内容未变时可以复用，内容变化后会在下一个引用步骤前重新读取。合法的手动修改不会让 workflow 失败。文件缺失、JSON 错误、pointer 无效、路径越界或超过限制时 fail closed，并返回修复指引。

`resume` 保持同一条 workflow lineage，但下一次引用步骤读取当前 contract；已经完成的步骤不会重放。

## 步骤上下文

业务步骤收到的 `contract_context` envelope 只包含 fragments。runtime-owned 元数据单独保存。bounded 的当前 workflow context 通过 runtime 既有 context 通道提供，不能与静态 contract 混淆。

## 诊断

诊断读取支持 workflow binding 或显式 contract 文件：

```powershell
.\so.exe inspect-contract-fragment --workflow-file <workflow> --json-pointer <pointer>
.\so.exe inspect-contract-fragment --contract-file <contract> --json-pointer <pointer>
```

诊断输出包含 fragment 和读取 metadata。诊断读取不能替代正式 `run`/`resume` 执行。

## Enhanced Gate

每个 skill being enhanced 都必须把自己的 contract 放在 `assets/so-workflow/contract.json`。enhancement workflow 把它作为 `current_contract` 读取，检查 `name`、`inputs`、`outputs`、`default_assumptions` 四个最小面，保存 evidence，并让 workflow refs 与 contract 对齐。enhancement skill 自己的治理 contract 绝不能复制到 skill being enhanced。
