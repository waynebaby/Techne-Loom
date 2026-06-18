# Loom Skill Enhancement Self-Bootstrap Slice Plan

## Scope

This file is the checked-in plan for the current self-bootstrap slice, where `/loom-skill-enhancement` is itself the target skill being rewritten.

It does not narrow the general mission of `/loom-skill-enhancement`. The skill still exists to rewrite or create any target skill so that the target becomes governed through the Loom Skill Orchestrator flow.

## Goal

Upgrade `/loom-skill-enhancement` as the current target skill so it governs itself with checked-in SO workflow assets, a locked runtime bundle, and explicit SO-exclusive governance wording.

## Mermaid

```mermaid
flowchart TD
  A[Classify governance state]:::runtime --> B{Already SO-enhanced?}:::gate
  B -- yes --> C[Ask latest released or latest beta]:::user
  B -- no --> D[Use confirmed package channel]:::gate
  C --> E[Reacquire SO runtime bundle]:::runtime
  D --> E
  E --> F[Capture fresh dotnet so.dll --guide]:::runtime
  F --> G[Analyze current SKILL.md and references]:::ai
  G --> H[Draft or refresh workflow template]:::tool
  H --> I[Compile template and collect Mermaid, HTML, and analysis]:::tool
  I --> J[Present compiled audit artifacts to user]:::user
  J --> K{Approve?}:::gate
  K -- revise --> L[Apply feedback to template]:::user
  L --> G
  K -- approve --> M[Publish blocked runtime outputs]:::runtime
  M --> N[Finalize external completion manifest]:::runtime
  N --> O[Update SKILL.md and close slice]:::tool

  classDef ai fill:#e4f6e8,stroke:#2f8f4e,color:#14532d;
  classDef tool fill:#e7f0ff,stroke:#4a6cf7,color:#1e3a8a;
  classDef user fill:#fff6d6,stroke:#e0ad00,color:#7c5c00;
  classDef gate fill:#f6f7f9,stroke:#c7cdd4,color:#4b5563;
  classDef runtime fill:#eef2ff,stroke:#818cf8,color:#3730a3;
```

## Inputs

- Current `SKILL.md`
- Current skill reference docs
- Current SO guide surface
- Current package index surface

## Required Outcomes

- Checked-in workflow template at `assets/so-workflow/so-template.json`
- Locked runtime metadata at `assets/so-workflow/so-package-lock.json`
- Updated `SKILL.md` that references the lock and SO-exclusive governance model
- A compile-valid governed workflow with explicit user-confirmed steps and audit-friendly evidence
- External compile audit artifacts for Mermaid, HTML, workflow JSON backup, and workflow analysis
- A clear separation between the generic skill mission and this self-bootstrap target-skill slice

## Plan Output Support Matrix

This matrix classifies the current self-bootstrap output requirements against the current `src`-built `dotnet so.dll --guide`, the governed-template validator, and the runtime execution surface.

| Output requirement | Current status | Why |
| --- | --- | --- |
| Checked-in workflow template at `assets/so-workflow/so-template.json` | `仅被 compile 支持` | The workflow template is the authority input for compile/load validation, but runtime does not automatically rewrite or finalize the checked-in source template. |
| Locked runtime metadata at `assets/so-workflow/so-package-lock.json` | `目前完全没下沉` | The plan requires a checked-in lock deliverable, but current runtime/validator only understand declared output families and do not guarantee a truthful package-lock payload. |
| Updated `SKILL.md` that references the lock and SO-exclusive governance model | `目前完全没下沉` | The plan requires a checked-in skill-markdown outcome, but current runtime does not have a governed business step that actually updates or validates that source file content. |
| A compile-valid governed workflow with explicit user-confirmed steps and audit-friendly evidence | `仅被 compile 支持` | Governed contract structure, seams, blocked outputs, and done reachability are compile-enforced, but business-evidence truthfulness still depends on authored workflow semantics. |
| External compile audit artifact: Mermaid Markdown | `已被 runtime 支持` | `compile` currently emits `workflow.mermaid.md` as a first-class audit artifact. |
| External compile audit artifact: HTML | `已被 runtime 支持` | `compile` currently emits `workflow.html` as a first-class audit artifact. |
| External compile audit artifact: workflow JSON backup | `已被 runtime 支持` | `compile` currently emits `workflow.json` backup as a first-class audit artifact. |
| External compile audit artifact: workflow analysis | `已被 runtime 支持` | `compile` currently emits `workflow.analysis.json` as a first-class audit artifact. |
| Final workflow template as review authority | `仅被 compile 支持` | Current SO can prove the final template is structurally valid and governed, but not that it already captured every promised business deliverable. |
| Compiled Mermaid as review evidence | `已被 runtime 支持` | Current SO compile directly produces Mermaid review evidence. |
| Workflow analysis report as review evidence | `已被 runtime 支持` | Current SO compile directly produces workflow analysis review evidence. |
| Package lock metadata as governed evidence | `目前完全没下沉` | The plan expects governed package-lock evidence, but the current workflow still does not guarantee a real checked-in lock artifact with validated content. |
| Node-to-file map as governed evidence | `目前完全没下沉` | The map is a checked-in documentation artifact today; current runtime/validator do not enforce completeness or correctness of node-to-file mapping. |
| A clear separation between the generic skill mission and this self-bootstrap target-skill slice | `目前完全没下沉` | This distinction exists in plan/governance prose, but not as a runtime or compile-enforced contract field. |

## Analysis Focus

- Inputs, outputs, branches, loops, seams, gates, and evidence
- Route-aware terminal business-output gates
- User-confirmed review loops
- Node-to-file and node-to-artifact mapping

## Bootstrap Route

1. Classify whether `/loom-skill-enhancement` is already SO-enhanced for the current pass.
2. If it is already SO-enhanced, ask exactly one two-choice latest-channel question: latest released or latest beta.
3. Reacquire the selected SO runtime bundle and record the resolved version in the checked-in package lock.
4. Run a fresh `dotnet so.dll --guide` capture from that runtime before analysis or validation.
5. Treat `/loom-skill-enhancement` as the current target skill for this slice and author the governed workflow template plus the package lock for that target.
6. Compile the template to produce Mermaid, HTML, workflow JSON backup, and workflow analysis artifacts.
7. Present the compiled audit artifacts and confirmation loop to the user for review.
8. Apply feedback to the template if needed and recompile.
9. Publish blocked runtime outputs through a dedicated blocked-governance gate, finalize an external completion manifest that references the checked-in source deliverables, and then use the checked-in source assets as the authoritative self-bootstrap deliverables.

## Evidence

- Final workflow template
- Compiled Mermaid
- Workflow analysis report
- Package lock metadata
- Node-to-file map
- Updated `SKILL.md`
