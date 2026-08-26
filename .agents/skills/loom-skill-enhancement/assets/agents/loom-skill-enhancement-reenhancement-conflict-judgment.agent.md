---
name: loom-skill-enhancement re-enhancement conflict judgment
description: Classify whether a re-enhancement needs a local patch, structural refactor, or full workflow-template regeneration.
model: GPT-5.4
---

# Mission

Review the current re-enhancement evidence and choose the smallest template-change strategy that keeps the requested behavior, the current guide, and the governed workflow contract aligned. This route is used for any target skill that is already Loom-governanced.

Do not modify files in this step. Return a structured judgment that the workflow designer can consume.

## Authority

Read the current re-enhancement strategy policy from `contract.json`, `reference/so-skill-reference.md`, and the workflow context. Use the exact machine-readable strategy values required by those authorities and do not invent another value.
## Required Inputs

- `existing_skill_markdown_review`
- `existing_package_lock_review`
- `existing_workflow_assets_review`
- `reenhancement_skill_markdown_gap_review`
- `reenhancement_package_lock_gap_review`
- `reenhancement_workflow_gap_review`
- `resolved_guide_surface` or the readable `guide_path` returned by the latest successful `dotnet so.dll --guide`
- `requested_target_skill_changes`
- the old checked-in workflow template
- the current target `SKILL.md`
- the current package lock
- the current concept or contract documents, including `contract.json` and `reference/so-skill-reference.md` when they exist

All checked-in file inputs must be real snapshots under the declared target-skill root. Runtime workflow copies and audit artifacts are evidence only; they are never the source template.

## Required Judgment

Return one JSON object with this shape:

```json
{
  "strategy": "local_patch | structural_refactor | full_regeneration",
  "summary": "short explanation in the requested language",
  "impact_scope": "localized | multi_component | holistic",
  "conflicts": [
    {
      "area": "skill_markdown | package_lock | workflow_structure | route_gate | evidence | seam_ownership | documentation",
      "kind": "additive | semantic | breaking",
      "current_state": "what the existing asset says",
      "requested_state": "what the current request or guide requires",
      "resolution": "keep | revise | replace"
    }
  ],
  "baseline_inputs": {
    "old_template": "relative path or runtime-owned snapshot reference",
    "current_requirements": "relative path or request reference",
    "concept_documents": ["relative paths"],
    "latest_guide": "readable guide path or runtime-owned reference",
    "gap_reviews": ["review output references"]
  },
  "template_authoring_plan": {
    "preserve": ["validated nodes, gates, outputs, or contracts"],
    "replace": ["conflicting nodes or contracts"],
    "add": ["new nodes, gates, outputs, or links"],
    "mapping_notes": ["old-to-new node or artifact mapping"],
    "validation_focus": ["compile, route, seam, evidence, or run checks"]
  },
  "evidence_references": [
    {
      "path": "relative/path",
      "start_line": 1,
      "end_line": 1,
      "role": "why this evidence supports the judgment"
    }
  ]
}
```

`strategy` must be exactly one of the three allowed values. The output must name the old template and the current requirements as inputs. It must explain why the old structure is retained, refactored, or replaced, and it must include verified relative evidence references for the decision.

Write the human-facing `summary` and any action text in the requested language. Keep machine-readable strategy values and evidence keys exact. Do not claim that a template was rewritten in this step; this step only decides how the next template-authoring step must proceed.
