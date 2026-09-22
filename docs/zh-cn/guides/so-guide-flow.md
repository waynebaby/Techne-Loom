# SkillOrchestrator Flow

[English](../../en/guides/so-guide-flow.md) | [Hub](so-guide.md) | [Reference](so-guide-reference.md) | [根目录](../README.md)

<!-- guide-version:start -->
版本：0.3.317
构建：已发布的 0.3.317 包
<!-- guide-version:end -->

## 用途

这页只保留 SkillOrchestrator 的最短治理执行路径。固定的 `so-guide.md` 是 guide hub；完整契约、治理规则、示例和反模式请阅读 [SO Guide 完整参考](so-guide-reference.md)。

## 流程

下面的路线区分 **enhancing skill** 和 **被增强的 skill**。`compile` 只证明结构；同一份 external workflow copy 必须继续通过 `run` 和所需的每次 `resume`。

图例：`🧭` 接入/导航，`📜` 契约，`🔎` 检查，`📝` 编写，`⚙️` 运行时动作，`💬` 审查，`🚧` 阻塞/边界，`🔁` 继续，`🧾` 证据，`✅` 完成，`❓` 决策。

```mermaid
flowchart TD
    A["🧭 Bind exact SO version<br/>绑定精确 SO 版本"] --> B["📜 Restore complete published bundle<br/>恢复完整已发布 bundle"]
    B --> C["⚙️ Fresh dotnet so.dll --guide<br/>读取 fresh guide"]
    C --> D["🔎 Inspect the skill being enhanced<br/>检查被增强的 skill、lock 与 workflow assets"]
    D --> E["📝 Plan inputs, outputs, routes, gates, seams<br/>规划输入、输出、route、gate 与 seam"]
    E --> F["📝 Author workflow template<br/>编写或刷新 workflow template"]
    F --> G["⚙️ Compile external candidate<br/>编译外部 candidate"]
    G --> H{"❓ Compile and review passed?<br/>compile 与审查是否通过?"}
    H -- "No<br/>否" --> I["🚧 Repair and review again<br/>修复 template 并重新审查"]
    I --> F
    H -- "Yes<br/>是" --> J["💬 Confirm route and findings<br/>确认路线与 findings"]
    J --> K["🧾 Copy external runtime workflow<br/>复制一份外部 runtime workflow"]
    K --> L["⚙️ Run the same copy<br/>运行同一份 copy"]
    L --> M{"❓ External seam reached?<br/>是否到达外部 seam?"}
    M -- "Yes<br/>是" --> N["🚧 Preserve blocked payload<br/>保存 blocked payload 与 required inputs"]
    N --> O["🔁 Resume the same copy<br/>恢复同一份 copy"]
    O --> L
    M -- "No<br/>否" --> P{"❓ Terminal evidence present?<br/>终态业务证据是否齐备?"}
    P -- "No<br/>否" --> I
    P -- "Yes<br/>是" --> Q["✅ Final governed completion<br/>最终受治理完成"]

    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef contract fill:#f8fafc,stroke:#94a3b8,color:#334155;
    classDef inspect fill:#dcfce7,stroke:#16a34a,color:#14532d;
    classDef runtime fill:#dbeafe,stroke:#2563eb,color:#1e3a8a;
    classDef review fill:#ffedd5,stroke:#ea580c,color:#9a3412;
    classDef blocked fill:#fee2e2,stroke:#dc2626,color:#7f1d1d;
    classDef decision fill:#fef3c7,stroke:#a16207,color:#713f12;
    classDef evidence fill:#ede9fe,stroke:#7c3aed,color:#4c1d95;
    classDef done fill:#dcfce7,stroke:#16a34a,color:#14532d;
    class A intake;
    class B,D contract;
    class C,E,F inspect;
    class G,K,L,O runtime;
    class I,J review;
    class N blocked;
    class H,M,P decision;
    class Q evidence;
    subgraph legend["Legend<br/>图例"]
        Z1["🔎 inspect / 检查"]
        Z2["⚙️ runtime action / 运行时动作"]
        Z3["💬 review / 审查"]
        Z4["🚧 blocked / boundary<br/>阻塞 / 边界"]
        Z5["🧾 evidence / 证据"]
        Z6["✅ completion / 完成"]
    end
    class Z1 inspect;
    class Z2 runtime;
    class Z3 review;
    class Z4 blocked;
    class Z5 evidence;
    class Z6 done;
```

## Runtime 检查

- framework-dependent 模式只能使用 resolver 生成的 bundle，其中包含 `so.dll`、生成的 `so.deps.json`、`so.runtimeconfig.json`、平铺依赖文件和精确 package closure；raw product `.nupkg` 或 `lib/net9.0` extraction 不是可运行 bundle，必须通过 `dotnet exec --depsfile ... --runtimeconfig ... so.dll` 启动。
- self-contained 模式只能使用精确 RID runtime package 及其 native entry point。
- 在规划或修改被增强的 skill 前，fresh `--guide` 结果可读取。
- 正式执行时不修改 checked-in template。
- runtime copy 和 audit artifact 保持在 skill 目录之外。
- `compile` 只做校验；`run` 和 `resume` 才是正式执行路径。
- workflow 自有 schema 和控制元数据使用英文。
- 用户和业务 payload 可以保留来源语言。

## CLI 速查

```powershell
dotnet so.dll --guide
dotnet so.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
dotnet so.dll run --workflow-file <external-workflow.json> --context-file <context.json> --audit-output <external-audit-root>
dotnet so.dll resume --workflow-file <external-workflow.json> --result-file <result.json>
```

`--guide` 和 `compile` 用于准备或校验；只有公开的 `run` 与 `resume` 算作 SO 正式 workflow 执行。

## Blocked 返回

读取 `current_step_kind`、`skill_hint`、`required_inputs`、`workflow_file`、`event_log_file` 和已验证的 audit links。需要用户输入时，只询问已经声明的决定或值；runtime-owned facts 通过对应 resume 路径返回结构化数据。保持同一份 external workflow copy。

## 被增强的 Skill 完成

受 Loom Skill Orchestrator 治理的 skill，不会在 guide refresh、template authoring、compile 或 blocked 返回时自动完成。必须有 skill deliverable 变更、review-fix evidence、route 与 gate evidence，并在同一份 copy 上完成公开 run/resume 链路。

## 继续阅读

- [SO Guide Hub](so-guide.md)
- [SO Guide 完整参考](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow 术语](../../en/architecture/workflow-terminology.md)
