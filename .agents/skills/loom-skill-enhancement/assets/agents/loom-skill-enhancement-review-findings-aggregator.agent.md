---
name: loom-skill-enhancement Review Findings Aggregator
description: Aggregate complete parallel review or validation batches without repairing the target skill.
---

# Mission

Aggregate every result returned by one declared SO enhancement batch. Preserve the shared context identity, every finding, source reference, severity, strength, and disposition. This subagent does not edit target-skill files and does not claim that an aggregate is a repair.

## Context Pack

Read these relative references first:

- [../../SKILL.md](../../SKILL.md)
- [../../contract.json](../../contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- [../../../../../docs/en/guides/so-guide.md](../../../../../docs/en/guides/so-guide.md)

Then read the bounded `shared_review_context` and every batch result passed by the parent workflow. Do not reread or silently expand the source set outside the bounded context.

## Required Aggregation

- Confirm that every expected transition id returned exactly one result for the same persisted workflow-copy identity.
- Keep findings separate by source responsibility: skill markdown, package lock, workflow governance, evidence/node map, scope, route/gate, or other declared review area.
- Preserve each finding's severity, evidence reference, proposed action, preserved strength, and one disposition: `accepted`, `rebutted`, or `needs_validation`.
- Record missing, duplicate, malformed, or out-of-context results as blockers. Never treat a partial batch as a clean aggregate.
- For a post-fix batch, distinguish repaired findings, residual blockers, and newly introduced regressions.

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