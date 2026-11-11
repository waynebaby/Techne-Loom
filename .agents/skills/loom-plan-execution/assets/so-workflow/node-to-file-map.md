# AO Governance Node To File Map

All checked-in document paths in this map are relative to the root of the skill being enhanced `.agents/skills/loom-plan-execution`. Absolute paths, `..` traversal, repository-global `docs/` paths, and another skill root are invalid map targets. Runtime-owned outputs use explicit placeholders and are not checked-in document ownership.
- Bound AO package runtime version: `0.5.4`.

## Priority 1: Skill Being Enhanced

Map the skill being enhanced's `SKILL.md` first. Treat it as the first mapped and primary user-facing deliverable; record its exact root-relative path, exact owning node(s), requested changes, and validation evidence before mapping supporting documents or workflow assets. The first data row below must map it.

| Workflow node or transition | Governed artifact or evidence |
| --- | --- |
| **Priority 1: Skill Being Enhanced's `SKILL.md`** (`transition.collect_governance_scope`, `transition.review_ao_skill_wording`) | The root `SKILL.md` is the primary user-facing deliverable; map and update it first, then record its validation evidence and include it in completion. |
| `transition.collect_governance_scope` | `SKILL.md`, `contract.json` (the separate AO business contract), `assets/so-workflow/contract.json` (the incomplete SO governance catalog; historical full-template compile succeeded with runtime `0.4.15-beta`, but the runtime host seam remains unverified), and `assets/so-workflow/reference/ao/runtime-contracts.md` |
| `transition.review_ao_skill_wording` | `SKILL.md`, `assets/so-workflow/reference/ao/runtime-behavior.md`, and `assets/so-workflow/skill-review-summary.schema.json`; the agent review summary references the complete external report and does not attest approval or evidence authenticity. |
| `transition.review_ao_workflow_designer` | `assets/agents/loom-plan-execution-workflow-designer.agent.md`, its seven role-contract documents, `assets/agents/loom-plan-execution-workflow-designer-contract-reviewer.agent.md`, `assets/so-workflow/designer-review-summary.schema.json`, and `assets/so-workflow/designer-review-report.schema.json`. The summary is agent-owned; validate the same-instance report and citations before continuation, and keep the seam blocked until a deployable verifier exists. |
| `transition.refresh_governed_template` | `assets/so-workflow/so-template.json`, `assets/so-workflow/contract.json` (SO catalog), `assets/so-workflow/template-refresh.schema.json`, `contract.json` (separate AO business contract), `reference/producer-output-evidence-contract.md`, and runtime-owned `<execution-output-root>/workflow-design/static-contract-review.json#phaseDesignEvidence`; the enhancing skill dispatches its own exact SO workflow designer. |
| `transition.compile_template` | Runtime-owned compile audit artifacts, `assets/so-workflow/so-template.json`, and `assets/so-workflow/compile-feedback-summary.schema.json`; the bounded status is derived from the complete external SO feedback and is not independent proof of authenticity or approval. |
| `transition.publish_completion_manifest` | runtime-owned completion evidence referencing `assets/so-workflow/reference/document-copy-manifest.json` and the checked-in target deliverables |

| `mermaid_delivery` artifact handoff | `reference/mermaid-artifact-delivery.md` defines verified runtime paths, workspace mirrors, link states, HTML preview, card handling, and fail-closed reporting |

## Document Ownership

The workflow-designer entrypoint and all of its [required contract documents](../agents/loom-plan-execution-workflow-designer.agent.md#required-contract-documents) are one role contract owned by the designer review/design transitions above. Review, validate, and distribute every linked `assets/agents/workflow-designer-*.md` document together with the entrypoint; the split does not change workflow behavior or output requirements.

The AO reference copies under `assets/so-workflow/reference/ao/` are complete guide pages extracted from the exact bound AO package. They are refreshed with the bound AO package version and their source hashes are recorded in `assets/so-workflow/reference/document-copy-manifest.json`. The fresh published-runtime `guide_path` remains the authority for version-specific behavior.