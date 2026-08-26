# Loom-Governanced Skill Run Example

[中文](../../zh-cn/examples/so-enhanced-skill-run.md) | [Root](../README.md)

This example shows a generalized Loom-governanced target-skill run where the value comes from route discipline, not from domain-specific implementation details.

> [!NOTE]
> This page intentionally hides product-domain details, vendor details, repository-specific anchors, and local file names. The point is to show how SO kept a large run structurally correct from intake to completion.

## Read With

- [SkillOrchestrator Guide](../guides/so-guide.md)
- [Skills Reference](../reference/skills.md)
- [Workflow Terminology](../architecture/workflow-terminology.md)
- [Skill-Driven Workflow Example](skill-driven-workflow.md)

## Scenario At A Glance

The run starts with a broad engineering request and must end with an implementation-ready output package that is traceable, reviewable, resumable, and auditable.

Without governance, this class of run usually drifts in one of four ways:

- scope is accepted too loosely
- branch exploration becomes uneven
- synthesis happens before enough evidence exists
- completion is declared without a real audit trail

Loom Skill Orchestrator governance prevents that drift by forcing the run through explicit gates.

## Route Map

Legend: `🧭` intake, `🚧` blocked or repair state, `🔎` branch analysis, `📝` synthesis, `🧾` evidence handoff, `❓` decision gate.

```mermaid
flowchart TD
    A[🧭 Request intake] --> B{❓ Minimum inputs present?}
    B -- No --> B1[🚧 Blocked seam\nAsk for missing scope]
    B -- Yes --> C[🔎 Preflight and environment confirmation]
    C --> D[🔎 Input normalization]
    D --> E[🔎 Structured branch fan-out]
    E --> E1[🔎 First-principles branch]
    E --> E2[🔎 Reference branch A]
    E --> E3[🔎 Reference branch B]
    E1 --> F[🚧 Critique and conflict review]
    E2 --> F
    E3 --> F
    F --> G{❓ Enough evidence to synthesize?}
    G -- No --> E
    G -- Yes --> H[📝 Authoritative synthesis]
    H --> I{❓ Validation passed?}
    I -- No --> J[🚧 Repair and re-validate]
    J --> H
    I -- Yes --> K[🧾 Official evidence handoff]
    K --> L[🧾 Completed governed run]

    classDef intake fill:#E0F2FE,stroke:#0284C7,color:#0C4A6E;
    classDef branch fill:#FEF3C7,stroke:#B45309,color:#78350F;
    classDef review fill:#FFEDD5,stroke:#EA580C,color:#9A3412;
    classDef synth fill:#DCFCE7,stroke:#15803D,color:#14532D;
    classDef evidence fill:#EDE9FE,stroke:#6D28D9,color:#4C1D95;
    classDef decision fill:#F1F5F9,stroke:#64748B,color:#334155;

    class A intake;
    class C,D,E,E1,E2,E3 branch;
    class B1,F,J review;
    class H synth;
    class K,L evidence;
    class B,G,I decision;
```

## Why The Route Stayed Correct

Legend: `👤` caller action, `⚙️` runtime action, `🔎` branch analysis, `❓` validation gate, `🧾` audit evidence.

```mermaid
sequenceDiagram
    participant Caller as 👤 Caller / Outer Agent
    participant SO as ⚙️ SO Runtime
    participant Branches as 🔎 Branch Analysis
    participant Validator as ❓ Validation Gate
    participant Audit as 🧾 Audit + Evidence

    Caller->>SO: 👤 run with normalized runtime workflow copy
    SO->>SO: ⚙️ gate inputs and confirm preflight
    SO->>Branches: ⚙️ dispatch structured branch fan-out
    Branches-->>SO: 🔎 branch outputs + critique payloads
    SO->>Validator: ⚙️ submit synthesis candidate
    Validator-->>SO: ❓ pass or fail
    alt validation fails
        SO-->>Caller: ⚙️ blocked repair route + current workflow state
        Caller->>SO: 👤 resume with structured fix evidence
        SO->>Validator: ⚙️ re-check
    end
    Validator-->>SO: ❓ validated output
    SO->>Audit: ⚙️ emit event log, workflow backup, Mermaid, HTML
    Audit-->>Caller: 🧾 official completion evidence
```

## Stage Narrative

### 1. Input gating

SO stopped the run until the minimum inputs were confirmed. That did three jobs early:

- unclear scope became explicit
- missing assumptions became real defaults or decisions
- the run got a stable request context before heavier analysis began

### 2. Preflight and environment confirmation

SO required the environment and execution context to be confirmed before analysis moved forward. This was not only a tooling check. It protected the route from starting on an unverified foundation.

### 3. Input normalization

Raw upstream material was converted into normalized working artifacts before deeper analysis began. That kept later stages aligned on one canonical input set instead of a mixture of partial raw sources.

### 4. Structured branch fan-out

The run split into one first-principles branch, multiple reference-style branches, and critique passes. SO made those branches land in comparable shapes before synthesis could continue.

### 5. Critique before synthesis

SO did not let branch generation masquerade as final architecture. Each branch had to survive critique first, so weak assumptions and unresolved conflicts were surfaced before they hardened into the final route.

### 6. Authoritative synthesis

Synthesis only opened after branch evidence and critique already existed. That made the final output evidence-driven and conflict-aware instead of optimistic.

### 7. Validation gate

Validation was a real gate, not a courtesy step. The run could not close merely because a document existed. The authoritative output had to meet the structure and completeness bar first.

### 8. Official evidence handoff

Completion required more than prose saying “done”. The run had to end with an official evidence set so the route could be reconstructed later without depending on fragile chat memory.

## What SO Produced

| Surface | What it contributed | Why it mattered |
| --- | --- | --- |
| Workflow state | Durable stage-aware control path | Prevented silent drift |
| Event log | Step-by-step execution record | Made the run auditable |
| Mermaid + HTML renders | Point-in-time workflow visualization | Made progress and recovery explainable |
| Workflow analysis JSON | Inputs, outputs, branches, loops, seams, gates, and control-risk summary | Made the route reviewable before execution |
| Blocked seams | Structured pauses with resume requirements | Kept recovery deterministic |
| Completion evidence | Official done-state handoff | Distinguished real completion from apparent completion |

## Evidence Shape

A successful governed run ends with a small but meaningful evidence bundle:

- current workflow state or terminal workflow state
- append-only event log
- point-in-time Mermaid Markdown and HTML renders
- workflow JSON backup per audited step
- workflow analysis JSON per audited step
- validation pass signal tied to the authoritative output

That bundle is why the run is not only complete, but reviewable and resumable.

## Why This Pattern Repeats Well

This route is reusable for any skill that must:

- transform ambiguous inputs into a structured output package
- compare multiple design paths before choosing one
- preserve critique and conflict resolution
- validate a primary output before closure
- end with auditable completion evidence

The real reusable artifact is not the domain content. It is the governed execution shape.

## Positioning Guidance

Use this example as:

- a workflow-governance example
- a traceability example
- a structured synthesis example
- a completion-discipline example

Do not use it as a domain design reference, because the domain-specific details are intentionally abstracted away.

## Takeaways

- SO adds the most value by controlling route quality, not merely by accelerating content generation.
- The run stays correct because progress is earned stage by stage.
- Branching, critique, synthesis, validation, and evidence closure belong to one official path.
- A governed run is stronger precisely because it cannot bypass structure.

## Continue Reading

- Return to [Examples](README.md)
- Read the runtime contract in [SkillOrchestrator Guide](../guides/so-guide.md)
- Read the skill-layer rules in [Skills Reference](../reference/skills.md)
