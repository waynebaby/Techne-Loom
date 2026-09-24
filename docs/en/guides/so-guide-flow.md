# SkillOrchestrator Flow

[中文](../../zh-cn/guides/so-guide-flow.md) | [Hub](so-guide.md) | [Reference](so-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.320-beta
Build: published package 0.3.320-beta
<!-- guide-version:end -->




## Purpose

Use this page for the shortest governed execution path through SkillOrchestrator. The fixed `so-guide.md` page is the guide hub. Use [SO Guide Reference](so-guide-reference.md) for complete contracts, governance rules, examples, and anti-patterns.

## Flow

The route below separates the **enhancing skill** from the **skill being enhanced**. `compile` proves structure; the same external workflow copy must continue through `run` and every required `resume`.

Legend: `🧭` intake/navigation, `📜` contract, `🔎` inspection, `📝` drafting, `⚙️` runtime execution, `💬` review, `🚧` blocked/boundary, `🔁` continuation, `🧾` evidence, `✅` completion, `❓` decision.

```mermaid
flowchart TD
    A["🧭 Bind exact SO version"] --> B["📜 Restore complete published bundle"]
    B --> C["⚙️ Run fresh dotnet so.dll --guide"]
    C --> D["🔎 Inspect the skill being enhanced\nSKILL.md, lock, workflow assets"]
    D --> E["📝 Plan inputs, outputs, routes, gates, seams, evidence"]
    E --> F["📝 Author or refresh workflow template"]
    F --> G["⚙️ Compile external candidate"]
    G --> H{"❓ Did compile and static review pass?"}
    H -- "No" --> I["🚧 Repair template and rerun review"]
    I --> F
    H -- "Yes" --> J["💬 Confirm route and review findings"]
    J --> K["🧾 Copy one external runtime workflow"]
    K --> L["⚙️ Run the same workflow copy"]
    L --> M{"❓ Did the run reach an external seam?"}
    M -- "Yes" --> N["🚧 Preserve blocked payload and required inputs"]
    N --> O["🔁 Resume the same workflow copy"]
    O --> L
    M -- "No" --> P{"❓ Is terminal business evidence present?"}
    P -- "No" --> I
    P -- "Yes" --> Q["✅ Final governed completion"]

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
    subgraph legend["Legend"]
        Z1["🔎 inspect / research"]
        Z2["⚙️ runtime action"]
        Z3["💬 review / discussion"]
        Z4["🚧 blocked / boundary"]
        Z5["🧾 evidence"]
        Z6["✅ completion"]
    end
    class Z1 inspect;
    class Z2 runtime;
    class Z3 review;
    class Z4 blocked;
    class Z5 evidence;
    class Z6 done;
```

## Runtime Checklist

- Framework-dependent mode uses only a resolver-generated bundle containing `so.dll`, generated `so.deps.json`, `so.runtimeconfig.json`, flattened dependency assets, and the exact package closure; a raw product `.nupkg` or `lib/net9.0` extraction is not runnable, and launch must use `dotnet exec --depsfile ... --runtimeconfig ... so.dll`.
- Self-contained mode uses only the exact RID runtime package and its native entry point.
- The fresh `--guide` result is readable before planning or skill-being-enhanced edits.
- The checked-in template remains immutable during official execution.
- Runtime copies and audit artifacts stay outside skill folders.
- `compile` is validation only; `run` and `resume` are the official execution path.
- Workflow-owned schema and control metadata use English.
- User and business payload values may keep their source language.

## CLI Quick Reference

```powershell
dotnet so.dll --guide
dotnet so.dll compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
dotnet so.dll run --workflow-file <external-workflow.json> --context-file <context.json> --audit-output <external-audit-root>
dotnet so.dll resume --workflow-file <external-workflow.json> --result-file <result.json>
```

`--guide` and `compile` prepare or validate the route. Only public `run` and `resume` count as official SO workflow execution.

## Blocked Return

Read `current_step_kind`, `skill_hint`, `required_inputs`, `workflow_file`, `event_log_file`, and verified audit links. For user-owned input, ask only for the declared decision or value. For runtime-owned facts, return structured data through the matching resume path. Preserve the same external workflow copy.

## Skill-Belonging Completion

A run for a skill under Loom Skill Orchestrator governance is not complete at guide refresh, template authoring, compile, or a blocked return. It needs skill deliverable changes, review-fix evidence, route and gate evidence, and the same-copy public run/resume chain through final completion.

## Continue

- [SO Guide Hub](so-guide.md)
- [SO Complete Reference](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)
