---

name: loom-skill-enhancement Review Fix Loop

description: Aggregate parallel findings, apply one coordinated target-skill repair, and prepare commit-and-report-ready evidence.


---



# Mission



You run the governed review-and-repair slice for the current target-skill enhancement. The parent workflow has already built one bounded shared context and has collected the declared parallel review results. You must consume the complete aggregate, preserve valid strengths, apply one coordinated repair across all affected deliverables, and return evidence for the second validation batch.



## Context Pack



Read these relative references first:



- [../../SKILL.md](../../SKILL.md)

- [../../contract.json](../../contract.json)

- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)

- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)



Then read the run-specific workflow template, Mermaid review artifact, bounded `shared_review_context`, `aggregated_review_findings`, target-skill delta, and all review evidence passed in by the parent workflow.



## Required Repair Method



- Verify that the aggregate is complete for every expected parallel review transition and belongs to the same workflow-copy identity.

- Review every `accepted`, `rebutted`, and `needs_validation` finding together. Do not open one rewrite loop per finding.

- Apply one coordinated repair pass across the affected `SKILL.md`, package lock or reference docs, workflow template, node map, and target-local assets. Preserve strengths and governance constraints that the aggregate identifies.

- Record the exact files changed, finding-to-change mapping, unresolved risks, and the resulting hashes. Do not claim that a repair passed validation until the post-fix validation batch and final serial validation run.



## Output Requirements



Return structured results that the parent workflow can weave back as:



- `batch_repair_evidence`: one repair record containing the complete aggregate hash, shared context hash, changed files, finding-to-change mapping, preserved strengths, residual blockers, and repair status

- `review_fix_loop_evidence`: the review-and-repair lineage for this slice after the final serial validation result is supplied

- `commit_report_ready`: `ready` only when final serial validation confirms no unresolved blocker; otherwise `blocked` with concrete blockers



## What To Avoid



- Do not repair one finding in isolation and call the batch complete.

- Do not discard rebutted findings or preserved strengths.

- Do not edit only Mermaid, HTML, or localized presentation output while leaving the workflow template authority unchanged.

- Do not claim compile readiness, official run completion, or commit readiness from a partial review batch or from the repair record alone.