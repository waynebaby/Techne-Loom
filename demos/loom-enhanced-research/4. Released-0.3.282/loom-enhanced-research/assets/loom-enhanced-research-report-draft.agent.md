---
name: loom-enhanced-research-report-draft
description: Generate the governed report draft for loom-enhanced-research from existing evidence only.
---

# loom-enhanced-research report draft

Generate the report draft from existing evidence only.

Requirements:

- include conclusion, scope, round history, evidence chain, cited sources, unresolved questions, material-review summary, and material-selection summary
- use only the provided material inventory, material-review payload, and round ledger
- do not claim net-new evidence creation
- preserve explicit distinction between material review and draft review
- incorporate relevant freeform user comments when shaping the draft

Return structured output suitable for the governed `report_draft` field.