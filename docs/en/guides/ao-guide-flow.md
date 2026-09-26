# Loom Agent Plan-Execution Orchestrator Flow

[中文](../../zh-cn/guides/ao-guide-flow.md) | [Hub](ao-guide.md) | [Reference](ao-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.325-beta
Build: published package 0.3.325-beta
<!-- guide-version:end -->






## Purpose

Use this page for the shortest operational path through Loom Agent Plan-Execution Orchestrator. The fixed `ao-guide.md` page is the guide hub. Use [AO Guide Reference](ao-guide-reference.md) when a contract, example, or anti-pattern needs full detail.

## Flow

AO is the plan-execution route for uncertainty. It preserves one external workflow instance while the caller plans, probes, weaves out, and weaves back.

Legend: `🧭` intake, `📜` contract, `🔎` planning/research, `⚙️` runtime, `🚧` blocked boundary, `🔁` resume, `🧾` evidence, `✅` completion, `❓` decision.

```mermaid
flowchart TD
    A["🧭 Classify business outcome"] --> B["📜 Acquire exact AO RID package"]
    B --> C["⚙️ Run fresh ao.exe --guide"]
    C --> D["🔎 Generate plan or reuse authored WorkflowInstance"]
    D --> E["⚙️ Compile the same external workflow"]
    E --> F["⚙️ Run the same workflow instance"]
    F --> G{"❓ Did AO weave out?"}
    G -- "Yes" --> H["🚧 Preserve boundary reason, frontier, and required inputs"]
    H --> I["🔎 Perform the concrete outside action"]
    I --> J["🔁 Resume with transition_id, correlation_key, and payload"]
    J --> F
    G -- "No" --> K{"❓ Are business deliverables verifiable?"}
    K -- "No" --> D
    K -- "Yes" --> L["🧾 Record workflow, event, and audit evidence"]
    L --> M["✅ Completed plan-execution run"]

    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef contract fill:#f8fafc,stroke:#94a3b8,color:#334155;
    classDef research fill:#dcfce7,stroke:#16a34a,color:#14532d;
    classDef runtime fill:#dbeafe,stroke:#2563eb,color:#1e3a8a;
    classDef blocked fill:#fee2e2,stroke:#dc2626,color:#7f1d1d;
    classDef decision fill:#fef3c7,stroke:#a16207,color:#713f12;
    classDef evidence fill:#ede9fe,stroke:#7c3aed,color:#4c1d95;
    class A intake;
    class B contract;
    class D,I research;
    class C,E,F,J runtime;
    class H blocked;
    class G,K decision;
    class L,M evidence;
    subgraph legend["Legend"]
        Z1["🧭 intake"]
        Z2["🔎 plan / research"]
        Z3["⚙️ runtime"]
        Z4["🚧 blocked boundary"]
        Z5["🔁 structured resume"]
        Z6["✅ completion"]
    end
    class Z1 intake;
    class Z2 research;
    class Z3 runtime;
    class Z4 blocked;
    class Z5 runtime;
    class Z6 evidence;
```

## Runtime Checklist

- Detect one supported RID from OS, architecture, and Linux libc; acquire only the exact published AO package for that RID.
- Verify package identity, version, SHA-512, nuspec, manifest, ZIP safety, apphost, and English guide files before extraction.
- Run `ao.exe --guide` on Windows or `ao --guide` on Unix as the first runtime operation. Verify the returned version and readable contained guide paths.
- Use the same extracted apphost for schema/demo, compile, prompt-plan, prompt-replan, run, and resume.
- Keep the external WorkflowInstance, runtime state, event log, and audit output outside skill folders.
- No installed .NET host, DLL mode, resolver descriptor, or cross-mode fallback is required.
- Workflow-owned schema and control metadata use English; user/business payloads may keep their source language.

## CLI Quick Reference

```powershell
.\ao.exe --guide
.\ao.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
.\ao.exe run --workflow-file <external-workflow.json> --context-file <context.json> --audit-output <external-audit-root>
.\ao.exe resume --workflow-file <external-workflow.json> --result-file <result.json>
```

On Unix, invoke `./ao` with the same arguments. `--guide`, `compile`, `prompt-plan`, and `prompt-replan` prepare or validate; only `run` and `resume` are official AO skill runs.
## Blocked Return

Read the structured blocked payload. Preserve `session_id`, `workflow_file`, `workflow_instance_file`, `event_log_file`, `current_node_id`, and the latest transition data. Use `prompt-plan` for the first authored graph and `prompt-replan` only when a later frontier or `tbr` path must be redesigned. Resume with `transition_id`, optional `correlation_key`, and a structured `payload`.

## Continue

- [AO Guide Hub](ao-guide.md)
- [AO Complete Reference](ao-guide-reference.md)
- [Workflow Schema](../reference/workflow-schema.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)
