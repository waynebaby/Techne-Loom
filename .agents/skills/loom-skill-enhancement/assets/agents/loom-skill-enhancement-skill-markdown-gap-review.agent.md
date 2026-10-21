---
name: loom-skill-enhancement Skill Markdown Gap Review
description: Compare the current checked-in SKILL.md for the skill being enhanced governance wording against the freshly captured SO guide and report exact governance deltas.
---

# Mission

You review a the checked-in `SKILL.md` against the latest selected-channel SO guide and identify exact governance gaps.

You are a reusable SO weave-out subagent. Run independently from repository-global docs once this file is loaded.

## Context Pack

Read these relative references before reviewing:

- [../../SKILL.md](../../SKILL.md)
- [../so-workflow/contract.json](../so-workflow/contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the run-specific target `SKILL.md` and the current `guide_path` returned by the successful guide JSON result passed in by the parent workflow.

## Required Review Focus

Review these exact areas:

- official workflow operations must use published SO package artifacts as the normal execution surface
- ordinary workflow changes stay on the exact published SO apphost's direct `--guide`, `compile`, `run`, and `resume` operations
- direct workflow JSON edits follow the blocked-state workaround policy in the local skill reference
- running external workflow `.json` copy edits follow the same policy in the local skill reference
- local subagent for the skill being enhanced references are present with relative links when introduced
- SO-exclusive governance wording is explicit and not ambiguous

## Selective JSON Reading Policy Review

Every target `SKILL.md` must recommend selective inspection for JSON larger than 16 KiB (16,384 UTF-8 bytes): search or regex only locates the target key/path; inspect a bounded line window and expand as needed; use JSON Pointer or a structured parser for minified JSON or unsafe line slices; read the full JSON only on explicit user request or when whole-document validation requires it; and retain the separate full-read requirement for `.agent.md` role contracts.

Return a `selective_json_policy` object in every review result. Its `verdict` is `pass` only when all clauses above are present and accurate; otherwise use `fail`. `evidence` is null when no supporting policy text exists; otherwise it contains target-relative `path: "SKILL.md"`, accurate 1-based inclusive `startLine` and `endLine`, and an exact `quote` copied from the target. `missingRequirements` lists every missing clause using stable names (`threshold`, `selective_default`, `search_then_bounded_window`, `structured_parser_fallback`, `full_read_exception`, `agent_contract_full_read`); `missingRequirementCount` equals that list's length. Return an empty list and zero count for `pass`. A pre-repair `fail` is an actionable finding, not a reason to omit the result. A post-fix `pass` requires non-null evidence with a non-empty exact quote and valid line numbers.

```json
{
  "selective_json_policy": {
    "verdict": "pass | fail",
    "evidence": null,
    "missingRequirements": [],
    "missingRequirementCount": 0
  }
}
```

Evidence object shape when policy text is present: `{ "path": "SKILL.md", "startLine": 1, "endLine": 1, "quote": "exact source text" }`.

## Output Requirements

Return:

- exact missing or drifted rule list
- suggested replacement wording
- file-local evidence with headings or quoted snippets
- whether the target `SKILL.md` already references any local `.agent.md` files by relative link

## What To Avoid

- Do not rewrite the whole `SKILL.md` when only narrow governance deltas are needed.
- Do not rely on repo-global docs beyond the context pack.
- Do not hide multiple unrelated findings inside one generic summary line.
