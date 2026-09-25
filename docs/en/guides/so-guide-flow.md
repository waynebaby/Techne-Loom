# SkillOrchestrator Flow

[中文](../../zh-cn/guides/so-guide-flow.md) | [Hub](so-guide.md) | [Reference](so-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.323-beta
Build: published package 0.3.323-beta
<!-- guide-version:end -->





## Purpose

Use this page for the shortest governed execution path through SkillOrchestrator. The fixed `so-guide.md` page is the guide hub. Use [SO Guide Reference](so-guide-reference.md) for complete contracts, governance rules, examples, and anti-patterns.

## Flow

The route below separates the **enhancing skill** from the **skill being enhanced**. `compile` proves structure; the same external workflow copy must continue through `run` and every required `resume`.

Legend: `🧭` intake/navigation, `📜` contract, `🔎` inspection, `📝` drafting, `⚙️` runtime execution, `💬` review, `🚧` blocked/boundary, `🔁` continuation, `🧾` evidence, `✅` completion, `❓` decision.

```mermaid
flowchart TD
    A["🧭 Bind exact SO version"] --> B["📜 Acquire exact SO RID package"]
    B --> C["⚙️ Run fresh so.exe --guide"]
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

- Detect one supported RID from OS, architecture, and Linux libc; acquire only the exact published SO package for that RID.
- Verify package identity, version, SHA-512, nuspec, manifest, ZIP safety, apphost, and English guide files before extraction.
- Run `so.exe --guide` on Windows or `so --guide` on Unix as the first runtime operation. Verify the returned version and readable contained guide paths.
- Use the same extracted apphost for schema/demo, compile, run, and resume on one external workflow copy.
- MCP is optional for later steps only; it is not a package, guide, compile, run, or resume prerequisite.
- Keep the checked-in template immutable and keep runtime copies, event logs, and audit artifacts outside skill folders.
- Workflow-owned schema and control metadata use English; user/business payloads may keep their source language.

## CLI Quick Reference

```powershell
.\so.exe --guide
.\so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
.\so.exe run --workflow-file <external-workflow.json> --context-file <context.json> --audit-output <external-audit-root>
.\so.exe resume --workflow-file <external-workflow.json> --result-file <result.json>
```

On Unix, invoke `./so` with the same arguments. `--guide` and `compile` prepare or validate; only public `run` and `resume` count as official SO workflow execution.
## Blocked Return

Read `current_step_kind`, `skill_hint`, `required_inputs`, `workflow_file`, `event_log_file`, and verified audit links. For user-owned input, ask only for the declared decision or value. For runtime-owned facts, return structured data through the matching resume path. Preserve the same external workflow copy.

## Skill-Belonging Completion

A run for a skill under Loom Skill Orchestrator governance is not complete at guide refresh, template authoring, compile, or a blocked return. It needs skill deliverable changes, review-fix evidence, route and gate evidence, and the same-copy public run/resume chain through final completion.

## Continue

- [SO Guide Hub](so-guide.md)
- [SO Complete Reference](so-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)
