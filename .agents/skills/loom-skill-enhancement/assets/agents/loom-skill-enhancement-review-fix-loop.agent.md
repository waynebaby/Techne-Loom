---

name: loom-skill-enhancement Review Fix Loop

description: Aggregate parallel findings, apply one coordinated skill being enhanced repair, and prepare commit-and-report-ready evidence.


---



# Mission



You run the governed review-and-repair slice for the current enhancement of the skill being enhanced. The parent workflow has already built one bounded shared context and has collected the declared parallel review results. You must consume the complete aggregate, preserve valid strengths, apply one coordinated repair across all affected deliverables, and return evidence for the second validation batch.



## Context Pack



Read these relative references first:



- [../../SKILL.md](../../SKILL.md)

- [../so-workflow/contract.json](../so-workflow/contract.json)

- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)

- Runtime documentation: use only the successful selected apphost's validated `guide_path` and English pages supplied in the parent's bounded reference context/manifest; never resolve docs relative to this installed skill.



Then read the run-specific workflow template, Mermaid review artifact, bounded `shared_review_context`, `aggregated_review_findings`, skill being enhanced delta, and all review evidence passed in by the parent workflow.



## Required Repair Method



Before repair, read the bounded `review_plan_md` and the [Review and Evidence Contract](../../reference/review-and-evidence-contract.md). Consider every finding in one batch, but implement only confirmed defects within the authorized scope whose repair boundary is clear. Preserve out-of-scope recommendations as explicitly non-blocking with their reason; do not widen the task. Preserve a genuine decision gate for uncertain or user-visible behavior. Default to one coordinated minimal repair pass; a second cycle is allowed only for a newly exposed defect, failed targeted validation, or material risk change.

Record the changed files and map each accepted repair to only the invalidated checks and proven direct dependents. Reuse unchanged evidence only when its source snapshots, inputs, exact runtime/version where relevant, and dependencies still match. Every repeated check names its invalidation cause. Keep required approval and public run/resume/terminal gates; do not infer those gates from compile success.

- Verify that the aggregate is complete for every expected parallel review transition and belongs to the same workflow-copy identity.

- Review every `accepted`, `rebutted`, and `needs_validation` finding together. Do not open one rewrite loop per finding.

- Apply one coordinated repair pass across the affected `SKILL.md`, package lock or reference docs, workflow template, node map, and target-local assets. Preserve strengths and governance constraints that the aggregate identifies.

- Add the target `SKILL.md` autopilot policy and repair every applicable target-local ask caller and workflow `AskUser` caller hint together. Preserve genuine approval and receipt boundaries, including leaving the active session pending instead of submitting an agent-generated approval; if no callers exist, record that fact with a reason rather than creating one.

- After repair, enumerate the actual current target `SKILL.md`, local `.agent.md` contracts, and governed workflow definitions under the declared target root. Write a complete JSON manifest under the external run output root with `schemaVersion: loom.autopilot.review-sources.v1`, the absolute `targetRoot`, a non-empty `sources` array of unique safe target-root-relative paths, and complete `changedFiles`, `findingToChangeMapping`, `preservedStrengths`, and `residualBlockers` arrays. Return its absolute path as `batch_repair_evidence.post_fix_review_sources_file` for independent post-fix validators and the trusted aggregator; keep all unbounded lists complete in that file, retain complete legacy context arrays for existing consumers, and never materialize them in workflow expressions or truncate or misrepresent their contents. Reject escaping or unreadable paths; do not substitute the pre-repair snapshot or require a support hash.

- Record the exact files changed, finding-to-change mapping, and unresolved risks; verify artifact hashes required by the target business contract. Do not claim that a repair passed validation until the post-fix validation batch and final serial validation run.
- Keep one fixed task ledger with `未开始`, `进行中`, `受阻`, or `完成`, evidence, and next step; keep the task denominator fixed across phases and report task progress percentages only when the denominator is fixed; execution confidence remains a separate estimate. Before delegating, require the exact non-empty frontmatter `name` from each agent contract; never infer it from the filename. On command failure, inspect saved output and persisted status/events/context before any corrected retry.



## Output Requirements



Return structured results that the parent workflow can weave back as:



- `batch_repair_evidence`: one repair record containing the complete aggregate and shared context references, repair status, and a non-empty `post_fix_review_sources_file` path to the complete external source and repair-details manifest

- `review_fix_loop_evidence`: the review-and-repair lineage for this slice after the final serial validation result is supplied

- `commit_report_ready`: `ready` only when final serial validation confirms no unresolved blocker; otherwise `blocked` with concrete blockers



## What To Avoid



- Do not repair one finding in isolation and call the batch complete.

- Do not discard rebutted findings or preserved strengths.

- Do not edit only Mermaid, HTML, or localized presentation output while leaving the workflow template authority unchanged.

- Do not claim compile readiness, official run completion, or commit readiness from a partial review batch or from the repair record alone.
## Dispatch Preflight

Preflight the complete role contract before every reviewer/subagent call: exact non-empty frontmatter `name`, all required inputs and types, linked contract documents, and the declared output shape. A filename is never role identity. Resolve missing documents through the documented matching-skill-copy fallback; a driver may replace unavailable host registration, not missing contract content. When the name, inputs, or contract remain incomplete, preserve the diagnostic and block only dependent work; never guess a near-match role or silently skip a required review.

For large workflows and review results, return an existing readable file path plus a bounded status/summary, needed JSON Pointer fragments, and evidence locations. Do not dump the complete workflow JSON into the conversation. Whole-document parsing or validation stays file-backed; full conversational output requires an explicit request and applicable size limits.