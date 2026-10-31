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
- Runtime documentation: use only the selected apphost's validated `guide_path` and English docs entries supplied in the bounded parent reference context/manifest; never resolve docs relative to this installed skill.

Read the bounded `shared_review_context` and every batch result passed by the parent workflow. For post-fix validation, read the complete external JSON manifest at `batch_repair_evidence.post_fix_review_sources_file` and inspect its current target-root sources; the earlier snapshot is the pre-repair baseline, not evidence of repaired content. Do not silently widen beyond the declared target-root review inventory.

## Required Aggregation

- Confirm that every expected transition id returned exactly one result for the same persisted workflow-copy identity.
- Keep findings separate by source responsibility: skill markdown, package lock, workflow governance, evidence/node map, scope, route/gate, or other declared review area. For caller-specific findings, keep complete details in the external report and use distinct clause-level context summaries with references; never truncate or hide individual evidence.
- Preserve each finding's severity, evidence reference, proposed action, preserved strength, and one disposition: `accepted`, `rebutted`, or `needs_validation`.
- Record missing, duplicate, malformed, or out-of-context results as blockers. Never treat a partial batch as a clean aggregate.
- For a post-fix batch, distinguish repaired findings, residual blockers, and newly introduced regressions.
- In `pre_repair` mode, a `fail` verdict from `selective_json_policy`, `runtime_doc_resolution_policy`, `autopilot_ask_policy`, or `autopilot_ask_surfaces` is an actionable coordinated-repair finding, not an incomplete aggregate. Preserve all policy and surface objects and their evidence in `findings`. Set `status` to `complete` only when every expected transition returns exactly one valid result for the same workflow-copy identity; missing, duplicate, malformed, out-of-context, or absent policy results block.
- In `post_fix_validation` mode, require `selective_json_policy`, `runtime_doc_resolution_policy`, and `autopilot_ask_policy` to pass with empty `missingRequirements`, zero `missingRequirementCount`, `SKILL.md` evidence, positive ordered inclusive lines, and a non-empty quote. Require `autopilot_ask_surfaces` to pass with equal nonnegative applicable and reviewed counts, zero missing count, and a non-empty `surfaceEvidenceFile` path. Inspect the complete report for an empty missing list and one evidence entry per applicable caller; when none apply, require an empty evidence array and a non-empty reason. Missing, malformed, failing, incomplete, or regressed results block.
- Validate the complete source manifest (`loom.autopilot.review-sources.v1`) and caller report (`loom.autopilot.ask-surfaces.v1`) as external JSON files under the run output root, with the same absolute target root. Verify source and repair-detail completeness, require every changed file to be represented in `sources`, and resolve paths without allowing symlink escapes from the target root. Verify each safe, readable target-relative source and every exact quote against the actual current repaired file. Enumerate all local agent contracts and governed workflow ask callers to establish exhaustive inventory and require the report counts and missing-detail count to agree with the scalar summaries. Read `.agent.md` contracts in full; structured whole-document parsing is allowed when validating a large JSON report. Each inclusive range must exist and each non-empty quote must match its source exactly; normalize only CRLF/CR line endings to LF, never trim or fold case. An absent, unsafe, omitted, unreadable, invalid, or mismatched source or report blocks. Do not use a support hash as a validity or freshness gate.
- Set `evidenceQuoteVerified`, `runtimeDocResolutionPolicyVerified`, and `autopilotAskPolicyVerified` to true only after their respective exact current-source quote comparisons, and `autopilotAskSurfacesVerified` only after exhaustive caller inventory and exact evidence verification. Otherwise set the affected flag false. Set `status` to `complete` only after all expected results, passing policies and caller review, all four true attestations, and no residual blocker or regression. These are trusted-aggregator attestations: the runtime does not independently compare external text or prevent forged resume payloads. If that trust boundary is insufficient, block pending independent verification.

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
- `runtimeDocResolutionPolicyVerified`: boolean for `post_fix_validation`; true only after exact bounded-snapshot quote verification
- `evidenceQuoteVerified`: boolean for `post_fix_validation`; true only after exact bounded-snapshot quote verification
- `autopilotAskPolicyVerified`: boolean for `post_fix_validation`; true only after exact current `SKILL.md` quote verification
- `autopilotAskSurfacesVerified`: boolean for `post_fix_validation`; true only after exhaustive caller inventory and exact current-source evidence verification

Do not modify files, run a repair, or call the next validator. The parent workflow must pass this complete aggregate to one coordinated repair step or to the final serial validation step.