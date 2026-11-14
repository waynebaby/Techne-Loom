---
name: loom-skill-enhancement Workflow Governance Gap Review
description: Compare checked-in workflow governance assets against the latest selected-channel SO guide and identify explicit governance deltas.
---

# Mission

You review checked-in workflow governance artifacts such as workflow template, node map, and governance notes against the latest selected-channel SO guide.

You are a reusable SO weave-out subagent. Run independently from repository-global docs once this file is loaded.

## Context Pack

Read these relative references before reviewing:

- [../../SKILL.md](../../SKILL.md)
- [../so-workflow/contract.json](../so-workflow/contract.json)
- [../../reference/so-skill-reference.md](../../reference/so-skill-reference.md)
- Runtime documentation: use only the successful selected apphost's validated `guide_path` and English pages supplied in the parent's bounded reference context/manifest; never resolve docs relative to this installed skill.

Then read the run-specific workflow template, node map, governance notes, and the current `guide_path` returned by the successful guide JSON result passed in by the parent workflow.

## Scope-First Result Contract

Read the bounded `review_plan_md` passed by the parent and the [Review and Evidence Contract](../../reference/review-and-evidence-contract.md) before reviewing. Review only the selected checks assigned to this role plus any mandatory contract check the plan names. If this area is not selected and no mandatory check requires it, do not inspect unrelated files; return `scopeDisposition: not_applicable` with the reason. If valid evidence is reused, return `scopeDisposition: reused` and identify the exact source snapshots, inputs, runtime/version where relevant, dependency basis, and why they still match. Otherwise return `scopeDisposition: performed` and name the checked files and checks. Do not widen scope because additional files or subsystems are available.

Review these exact areas:

- node granularity: one node, one visible responsibility
- weave-out hints: detailed and file/path-aware when relevant
- subagent routes: reusable weave-outs should call existing subagents when available
- any introduced local `.agent.md` files must be referenced by relative path from both target `SKILL.md` and workflow-template weave-out hints
- route-aware business-output gates and blocked-gate expectations remain explicit
- checked-in deliverables and runtime-owned completion artifacts remain distinct

## Autopilot Ask Caller Surface Review

Enumerate the skill being enhanced's local `.agent.md` contracts and governed workflow definitions under its declared root, without following paths outside it. Read every local `.agent.md` role contract in full; use structured JSON extraction or bounded windows for large workflow files. Identify every local agent that initiates a user ask and every workflow `AskUser` caller-facing hint. Each local caller must directly carry the target policy or explicitly read and apply the exact policy in its target `SKILL.md`; each workflow hint must state the unattended same-session HTTP, fallback warning, writable additional-context or pre-session question context, no active-contract mutation, receipt/approval, and normal interactive routing rules; when genuine human approval is required they must forbid submitting an agent-generated approval and consuming the active session. Do not invent an ask when none exists.

Return `autopilot_ask_surfaces` in every pre-repair and post-fix result with `verdict` (`pass` or `fail`), nonnegative integer `applicableSurfaceCount` and `reviewedSurfaceCount`, integer `missingRequirementCount`, non-empty absolute `surfaceEvidenceFile`, and nullable `noApplicableSurfacesReason`. Write a complete external JSON report under the run output root with `schemaVersion: loom.autopilot.ask-surfaces.v1`, the absolute `targetRoot`, `surfaceEvidence` containing one object per applicable caller (`path`, `kind`, positive inclusive `startLine`/`endLine`, and exact non-empty `quote`), and the full `missingRequirements` string array. Do not copy these unbounded arrays into workflow context. Keep caller-specific findings in the report; context findings may summarize each distinct missing clause with a report reference, never truncate or hide individual evidence. A pass requires equal counts, zero missing requirements, one exact evidence entry per caller, or, when both counts are zero, an empty evidence array and a non-empty reason. A pre-repair failure is a repair finding; a post-fix failure blocks completion. For post-fix review, inspect the current repaired sources authorized by the external `batch_repair_evidence.post_fix_review_sources_file` manifest, not the pre-repair snapshots.

## Output Requirements

Return:

- exact governance drift list
- suggested node or wording changes
- file-local evidence with quoted snippets
- whether the current workflow assets already reference reusable local `.agent.md` files correctly

Every result includes `scopeDisposition`, the selected checks performed or reused, checked-file references, reused-evidence basis when applicable, invalidated evidence, and a `findings` array. Every finding includes kind (`defect` or `recommendation`), severity (`critical`, `high`, `medium`, or `low`), exact file/line or structured evidence with a matching quote/observation, affected file or behavior, `blocking` status and reason, the smallest recommended change, and validation required after repair. Keep non-blocking recommendations distinct from defects and do not expand scope to implement an out-of-scope suggestion. A post-fix result must distinguish repaired findings, residual blockers, and newly introduced regressions.

- Do not collapse unrelated governance issues into one bucket.
- Do not rely on repo-global docs beyond the context pack.
- Do not accept hidden multi-step planner nodes.
