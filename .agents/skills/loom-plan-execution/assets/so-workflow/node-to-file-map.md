# AO Governance Node To File Map

All checked-in document paths in this map are relative to the root of the skill being enhanced `.agents/skills/loom-plan-execution`. Absolute paths, `..` traversal, repository-global `docs/` paths, and another skill root are invalid map targets. Runtime-owned outputs use explicit placeholders and are not checked-in document ownership.
- Bound AO package runtime version: `0.4.5-beta`.

## Priority 1: Skill Being Enhanced

Map the skill being enhanced's `SKILL.md` first. Treat it as the first mapped and primary user-facing deliverable; record its exact root-relative path, exact owning node(s), requested changes, and validation evidence before mapping supporting documents or workflow assets. The first data row below must map it.

| Workflow node or transition | Governed artifact or evidence |
| --- | --- |
| **Priority 1: Skill Being Enhanced's `SKILL.md`** (`transition.collect_governance_scope`, `transition.review_ao_skill_wording`) | The root `SKILL.md` is the primary user-facing deliverable; map and update it first, then record its validation evidence and include it in completion. |
| `transition.collect_governance_scope` | `SKILL.md`, `contract.json`, and `assets/so-workflow/reference/ao/runtime-contracts.md` |
| `transition.review_ao_skill_wording` | `SKILL.md` and `assets/so-workflow/reference/ao/runtime-behavior.md` |
| `transition.review_ao_workflow_designer` | `assets/agents/loom-plan-execution-workflow-designer.agent.md`, `assets/so-workflow/reference/ao/runtime-contracts.md`, and `assets/so-workflow/reference/document-copy-manifest.json` |
| `transition.refresh_governed_template` | `assets/so-workflow/so-template.json`, `contract.json`, `assets/agents/loom-plan-execution-workflow-designer.agent.md`, `reference/producer-output-evidence-contract.md`, `assets/so-workflow/reference/ao/runtime-behavior.md`, and runtime-owned `<execution-output-root>/workflow-design/static-contract-review.json#phaseDesignEvidence` |
| `transition.compile_template` | runtime-owned compile audit artifacts plus `assets/so-workflow/so-template.json` |
| `transition.publish_completion_manifest` | runtime-owned completion evidence referencing `assets/so-workflow/reference/document-copy-manifest.json` and the checked-in target deliverables |

| `mermaid_delivery` artifact handoff | `reference/mermaid-artifact-delivery.md` defines verified runtime paths, workspace mirrors, link states, HTML preview, card handling, and fail-closed reporting |

## Document Ownership

The AO reference copies under `assets/so-workflow/reference/ao/` are complete guide pages extracted from the exact bound AO package. They are refreshed with the bound AO package version and their source hashes are recorded in `assets/so-workflow/reference/document-copy-manifest.json`. The fresh published-runtime `guide_path` remains the authority for version-specific behavior.