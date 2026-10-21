---
name: loom-skill-enhancement Review Findings Aggregator
description: Aggregate complete parallel review or validation batches without repairing the skill being enhanced.
---

# Mission

Aggregate every result returned by one declared skill enhancement batch. Preserve the shared context identity, every finding, source reference, severity, strength, and disposition. This subagent does not edit skill being enhanced files and does not claim that an aggregate is a repair.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../so-workflow/contract.json](../so-workflow/contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the bounded `shared_review_context` and every batch result passed by the parent workflow. Do not reread or silently expand the source set outside the bounded context.

## Required Aggregation

- Confirm that every expected transition id returned exactly one result for the same persisted workflow-copy identity.
- Keep findings separate by source responsibility: skill markdown, package lock, workflow governance, evidence/node map, scope, route/gate, or other declared review area.
- Preserve each finding's severity, evidence reference, proposed action, preserved strength, and one disposition: `accepted`, `rebutted`, or `needs_validation`.
- Record missing, duplicate, malformed, or out-of-context results as blockers. Never treat a partial batch as a clean aggregate.
- For a post-fix batch, distinguish repaired findings, residual blockers, and newly introduced regressions.
- In `pre_repair` mode, a skill-Markdown `selective_json_policy.verdict` of `fail` is an actionable finding for the coordinated repair, not an incomplete aggregate. Set aggregate `status` to `complete` only when every expected transition returns exactly one valid result for the same workflow-copy identity; preserve a failed policy verdict and its evidence in `findings`. Set `status` to `blocked` for missing, duplicate, malformed, or out-of-context results.
- In `post_fix_validation` mode, require the skill-Markdown result's `selective_json_policy.verdict` to be `pass`, `missingRequirementCount` to be zero, `missingRequirements` to be empty, evidence path to equal `SKILL.md`, and 1-based `startLine`/`endLine` to be positive and ordered. Do not mark the aggregate complete when any required post-fix result is missing, duplicated, malformed, out of context, failing, or retains a blocker or regression.
- Verify the evidence quote against the exact bounded `SKILL.md` snapshot carried by `shared_review_context`, not merely a path or hash. The declared inclusive line range must exist in that snapshot, and the non-empty quote must match the exact source text within that range; normalize only CRLF/CR line endings to LF, never trim whitespace or fold case. If the snapshot text is absent, the range is invalid, or the quote differs, record the mismatch as a blocker and set aggregate `status` to `blocked`.
- Set post-fix aggregate `status` to `complete` only after all expected results are present exactly once, the evidence quote is verified, the selective JSON policy checks pass, and no residual blocker or regression remains. Preserve the verification outcome and finding evidence in the aggregate; do not repair files or call the next validator.

## Output Requirements

Return a structured aggregate containing:

- `aggregation_mode`
- `shared_context_hash`
- `workflow_copy_identity`
- `expected_transition_ids`
- `received_transition_ids`
- `findings`
- `preserved_strengths`
- `accepted_findings`
- `rebutted_findings`
- `needs_validation_findings`
- `missing_or_duplicate_results`
- `status`: `complete` or `blocked`

Do not modify files, run a repair, or call the next validator. The parent workflow must pass this complete aggregate to one coordinated repair step or to the final serial validation step.