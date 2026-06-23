# loom-enhanced-research SO workflow plan

## Goal

Make loom-enhanced-research SO-governed without changing its core research semantics.

The governed workflow must preserve these invariants:

- intake comments are first-class workflow input
- material review is distinct from draft review
- only the research loop may create new evidence
- reselection and cherry-pick loops must not pretend to create new evidence
- ordinary governance stays on the SO CLI path
- direct workflow JSON edits are blocked-state-only emergency workarounds

## Governance state

- Target state today: not already SO-enhanced
- This is the first SO-governed target-skill workflow for loom-enhanced-research
- Bound SO runtime version authority: `0.2.118-beta`

## Runtime-proof policy

The workflow starts with an explicit runtime-proof gate before any intake, planning, research, drafting, or review work.

Primary route:

1. Attempt published-package runtime preflight
2. If startup-contract checks pass, capture a fresh guide from the published runtime
3. Continue on the normal governed path

Blocked exception route for this enhancement pass:

1. Record published-package preflight failure
2. Record that the failure was specifically a missing `so.deps.json` in the extracted published bundle
3. Record the explicitly approved repo-src debug workaround
4. Record the local workaround runtime path:
   - `src/dotnet/Techne.Loom.SkillOrchestrator/bin/Release/net9.0`
5. Record the fresh guide exported from that workaround runtime:
   - `.temp/exec-20260623_141235-loom-skill-enhancement-repo-src-debug-result/so-guide.en.md`
6. Continue only after the workaround guide evidence is present

This exception route is evidence-only and enhancement-pass-only. It must not become the routine runtime path for future governed runs.

## Inputs

- `research_goal`
- `seed_query`
- `seed_urls`
- `max_depth`
- `max_rounds`
- `output_root`
- `evidence_policy`
- `demo_mode`
- `user_language`
- `intake_comments`

## Outputs

- `round_ledger`
- `evidence_ledger`
- `material_inventory`
- `continuation_payload`
- `report_draft.md`
- `final_report.md`
- `completion_manifest.md`

## Workflow phases

### 1. Runtime proof

- `state.start`
- `state.runtime_path_decision`
- `state.runtime_guide`
- `state.runtime_exception`

Outputs:

- package preflight result
- resolved runtime version
- runtime bundle package set
- resolved guide surface
- exception evidence when applicable

### 2. Intake

- `state.intake`

Rule:

- intake comments are required as a first-class preserved field even when other inputs are already known

### 3. Setup

- `state.setup`

Actions:

- initialize run artifacts
- initialize ledgers
- initialize material inventory structure
- initialize draft/report output targets

### 4. Bounded research loop

- `state.research_round`
- `state.research_continue_decision`

Rule:

- this is the only loop that may publish new evidence

### 5. Material inventory and review

- `state.material_inventory`
- `state.material_review`
- `state.material_review_decision`

Rule:

- this stage reviews gathered evidence, not the report draft

### 6. Draft generation

- `state.draft`

Rule:

- draft generation may synthesize from existing evidence and user comments but must not claim net-new evidence creation

### 7. Draft review

- `state.draft_review`
- `state.draft_review_decision`

Rule:

- this stage reviews the written draft, not the raw materials

Branches:

- finalize
- more research
- re-select materials

### 8. Final publication

- `state.publish_final`
- `state.done`

Outputs:

- final Markdown report
- final round ledger
- runtime-owned completion manifest referencing governed source assets and produced report outputs

## Branch and loop rules

- `material_review_decision == more_research` returns to the research loop
- `material_review_decision == draft` proceeds to draft generation
- `draft_review_decision == finalize` publishes the final report
- `draft_review_decision == more_research` returns to the research loop
- `draft_review_decision == reselect_materials` returns to material review
- material reselection must not publish a new evidence delta
- only research-round execution may publish new evidence

## Acceptance rules

The governed workflow is acceptable only if all of these stay true:

- runtime proof is a hard front gate
- the exception route records failed published-package preflight evidence explicitly
- the exception route records explicit user approval of the repo-src workaround explicitly
- direct workflow JSON edits remain blocked-state-only emergency workarounds
- only the research loop creates new evidence
- material review and draft review remain distinct
- reselection never claims new evidence
