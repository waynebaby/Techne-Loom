# Workflow Schema 参考

[English](../../en/reference/workflow-schema.md) | [根目录](../README.md)

canonical workflow schema 描述 SO 要执行的持久化 workflow file，以及未来 AO/SO 兼容 runtime 应能读取的结构。

像 **pattern**、**strand**、**weave out**、**weave back** 这样的 repo 级解释术语定义在 [Workflow 术语](../../en/architecture/workflow-terminology.md) 中。

## 核心元素

- instance 标识与版本
- node 集合与起始节点
- 当前节点与状态
- context map
- 历史记录条目
- wait group 与过期元数据
- artifact 引用

## 当前 Workflow File 形状

- 当前 workflow file 使用 camelCase 属性名。
- task node 存在一个以 node id 为 key 的 map 中。
- 多态条目通过 `$kind` 区分，例如 `state`、`command`、`expr`、`tbr`。
- transition 条目使用 `stepKind` 和 owned-input 元数据作为分析与可视化的稳定语义输入。Mermaid renderer 会从这些字段同时推导浅色节点背景与稳定 emoji 标签：`🔎` AI/model/subagent 工作用绿色，`⚙️` 代码/工具工作用蓝色，`💬` user-owned 的可选分支决策用黄色，`🚧` 必须用户输入用红色，`❓` 一般条件分支用琥珀黄/浅黄，`📜` gate/governance 状态用白色或极浅灰色。
- 每个 `state` 节点都必须声明非空的 `workflowPhase`，其中应包含稳定的阶段编号和简短业务名称，例如 `02 CK1 概念方案评审`；compile 会据此对 Mermaid 泳道分组。
- `name` 应同时包含检查点代号和业务短名称，例如 `CK1 - Concept direction review`。`description` 说明业务目的和关键产出，不要让读者只看到无法理解的代号。
- `context` 是自由形状的，并且允许嵌套对象和数组。
- `activeWaitGroups` 是持久化 runtime state 的一部分，不是隐藏的进程内临时内存。

## 受治理 Workflow 身份

`templateKind: so-governed-target-skill` 的 workflow 根部必须声明 `taskType`、`workflowKind`、`caseId` 和 `runId`。支持的组合如下：

| `taskType` | `workflowKind` | 范围 |
| --- | --- | --- |
| `skill_enhancement` | `so_self_bootstrap` | SO 自举增强 |
| `skill_enhancement` | `target_skill_enhancement` | 对其他 skill being enhanced 的增强 |
| target-specific task type | `target_skill_business` | skill being enhanced 的业务 workflow |

`caseId` 把一个业务案例的全部 evidence 关联起来。`runId` 把一次新的外部执行链关联起来，并且必须在 compile、run、resume、audit 和 completion evidence 中保持不变。checked-in template 可以使用 `template:` run 标记；物化和第一次对新的 `ReadyToStart` 副本执行 `run` 时，会把它替换成生成的 `run-<guid>`。business workflow for the skill being enhanced 不得发布 skill enhancement output family，也不得调用 `assets/agents/loom-skill-enhancement-*` subagent。

## Workflow 文件语言



Workflow 定义文件是 AO、SO 以及受 受 Loom Skill Orchestrator 治理的 skill being enhanced 的规范英文信息载体。workflow 自己拥有的 schema key、node 和 transition 名称/描述、workflow phase、expression、hint、failure guidance、evidence reference 以及 control metadata 必须使用英文。用户/业务 payload 可以保留来源语言，面向用户的输出可以使用请求语言；本地化属于展示层，不能改变 workflow key 或控制语义。
## 获取当前 runtime 的 Workflow 示例

本页不再放手写的 JSON workflow 示例。静态示例可能因为 runtime 增加必填字段或改变序列化方式而失效。上一版示例不能直接通过 compile：当前编译器要求每个 state node 都有非空的 `workflowPhase`，并且 runtime 会把表达式字符串序列化成结构化的 `ExpressionDefinition` 对象。

请直接从实际使用的同一份 runtime 获取当前可接受的结构：

1. 根据 runtime 的启动描述选择可执行文件：
   - `.NET CLI 模式`：`dotnet so.dll`
   - Windows self-contained 模式：`.\so.exe`
   - Unix self-contained 模式：`./so`
2. 先查看这份 runtime 实际提供的命令：

```powershell
dotnet so.dll --help
# Windows self-contained runtime 使用：
.\so.exe --help
```

3. 对 skill 目录之外的一份已有 workflow file 执行 `compile`：

```powershell
dotnet so.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
# Windows self-contained runtime 使用：
.\so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
```

`compile` 只校验已有文件，不会凭空创建 workflow。请在返回的 audit step 目录中读取 `workflow.json`、`workflow.compile-feedback.json`、`workflow.mermaid.md`、`workflow.html`、`workflow.analysis.json` 和 `workflow.dataflow.json`。feedback JSON 保存结构化编译计数和诊断；`workflow.json` 是同一份 runtime 实际接受的序列化结构。通常目录形状是 `{external-audit-root}/wf-<workflow-id>/step-<sequence>-compiled/`。

Mermaid Markdown 保留完整图形代码围栏，并在图后按阶段生成业务说明表，字段来自 state 的 `workflowPhase`、`name`、`id` 和 `description`。浅色节点和图例会明确指定深色文字。

成功 compile 生成的 HTML 是审计报告，不只是流程图预览。它汇总本次 compile 的产品/运行时和 workflow 来源信息、计数与诊断、控制流和数据归属分析、门禁、产物映射及逐转移数据流证据；不会推断运行时执行结果。compile 失败时只写反馈和 workflow JSON，不生成占位 Mermaid 或 HTML。

如果 workflow 已经由 runtime 保存，请使用同一可执行文件执行 `inspect-workflow --workflow-file <external-workflow.json>` 读取它。不要把 `--guide` 返回的 JSON 当成 workflow 示例；`--guide` 返回的是 guide 路径，不是 workflow file。不要把本页的静态 JSON 复制到新的运行中。
### 同时导出 Schema 与 Demo

如果要从同一份 runtime 获取当前 schema 合同和可以编译的 demo，请使用专用输出参数：

```powershell
dotnet so.dll --schema-demo-output <external-output-directory>
# Windows self-contained runtime 使用：
.\so.exe --schema-demo-output <external-output-directory>
```

这个命令会在指定目录中一次性写出完整文件集：`workflow.schema.json`、`workflow.demo.json`、`workflow.model.cs`、`workflow.demo.cs` 与 `workflow.demo.verify.cs`。builder 和 verifier 是由内置 Roslyn host 执行的普通 `.cs` 文件，不需要 project 文件或额外 C# runtime；命令不会修改 workflow。请用同一份 runtime 校验生成的 demo：

```powershell
dotnet so.dll compile --workflow-file <external-output-directory>\workflow.demo.json --audit-output <external-audit-root>
```

更新本文档时，应以生成的 schema 合同和成功的 compile 结果为依据。

- 公开模型比当前公开 SO runtime 的真实实现更宽。
- 当前已 review 切片完整支持的 group strategy 是 `firstSuccess`。
- 对不支持的 multi-transition 策略场景，runtime 会显式失败，而不是静默降级。
- `dotnet so.dll compile`、`run` 与 `resume` 会在 audit step 目录下写出 `workflow.analysis.json`。该 artifact 从 workflow file 推导，汇总所需输入、发布的输出族、branch、loop、用户 seam、运行时 seam、gate 与图灵完备控制风险。
- `dotnet so.dll copy-audit-step` 只复制明确验证过且未变化的 audit artifact，把源文件哈希写入 `audit-reuse.json`，不会推进 workflow 状态或创建官方 runtime evidence。
- `dotnet so.dll compile` 会拒绝任何缺少 `workflowPhase`、或把它写成 null、空字符串、纯空白的 state 节点。错误输出应指出 state node id、`workflowPhase` 字段路径，以及修复建议，明确说明这个字段的含义是“该节点处于整个 workflow 的哪个阶段”。

## Sidecar 分层

- workflow file 不等于 CLI control payload。
- `<so_property>` 承载公开控制元数据，也是当前 weave-out surface 之一。
- `dotnet so.dll resume --result-file` 会读取一个独立的结构化 JSON envelope。这个 envelope 就是当前的 weave-back sidecar。
- workflow file 旁边的 `.events.jsonl` 保存 append-on-growth 的事件历史。
- 同目录的 `.events.jsonl.meta.json` 保存 workflow `instance_id`；lineage 记录缺失、格式错误或不匹配时，必须先重写 event sidecar，再追加新的 history。

SO 的 blocking payload 与 AO 的 control payload 是建立在共享低层约定之上的独立产品契约。


## ExpressionDefinition 表达式定义

workflow 根部声明 `runtimeBinding` 与 `expressionBinding`。当前 .NET binding 是 C# + Roslyn，并使用 `compileFeedbackContract: "detailedCompileFeedbackV1"`。谓词字段使用带 `kind`、`source`、`entryPoint`、`resultType` 的结构化 `ExpressionDefinition`；只有显式 C# binding 存在时才读取字符串 shorthand，写出时始终变为对象。使用 `context.Get<T>("path")` 等同步只读 context API。legacy 非 C# 语法与异步构造非法，必须 fail closed。compile 结果必须使用结构化 `ExpressionCompileFeedback`，不能只透传 compiler 原文。

Rust+CEL 被记录为未来第四条 runtime 路线，复用同一 root binding、expression definition 与详细 feedback contract；它不是执行 Rust 表达式。Node.js 与 Python adapter 在实现同一 feedback contract 前不得标记为 supported。
