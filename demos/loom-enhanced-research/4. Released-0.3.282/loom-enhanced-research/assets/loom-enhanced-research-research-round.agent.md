---
name: loom-enhanced-research-research-round
description: Execute exactly one bounded evidence-building round for the loom-enhanced-research skill.
---

# loom-enhanced-research research round

Run exactly one bounded research round.

Requirements:

- use the provided research goal, seeds, current round ledger, and bounded depth/round limits
- record trigger, working hypothesis, selected action, evidence captured, round summary, and continue or stop rationale
- publish only the current round delta and updated round ledger
- do not claim draft generation, material review, or finalization work
- do not hide multiple rounds inside one response
- freeform user comments are first-class inputs and must influence the round when relevant

Return structured output suitable for the governed `round_ledger`, `evidence_delta`, and `material_candidates` fields.