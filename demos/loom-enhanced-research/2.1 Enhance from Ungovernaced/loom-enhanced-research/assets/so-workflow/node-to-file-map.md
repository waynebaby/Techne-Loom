# loom-enhanced-research Node To File Map

## Runtime proof

- `state.start` -> `assets/so-workflow/so-template.json`
- `state.runtime_path_decision` -> `assets/so-workflow/so-package-lock.json`
- `state.runtime_guide` -> external guide capture from the selected SO runtime
- `state.runtime_exception` -> `assets/so-workflow/so-package-lock.json`

## Intake and setup

- `state.intake` -> [SKILL.md](../../SKILL.md)
- `state.setup` -> runtime-owned ledgers and artifact roots outside the skill folder

## Research loop

- `state.research_round` -> runtime-owned round ledger and evidence delta
- `state.research_continue_decision` -> runtime-owned continuation state

## Material review

- `state.material_inventory` -> runtime-owned material inventory
- `state.material_review` -> runtime-owned material review payload
- `state.material_review_decision` -> runtime-owned branch decision

## Drafting and draft review

- `state.draft` -> runtime-owned report draft
- `state.draft_review` -> runtime-owned draft review payload
- `state.draft_review_decision` -> runtime-owned branch decision

## Finalization

- `state.publish_final` -> runtime-owned final report and completion manifest
- `state.done` -> references governed source assets and final runtime-owned outputs

## Governed source assets

- Workflow plan -> `assets/so-workflow/skill-plan.md`
- Workflow template -> `assets/so-workflow/so-template.json`
- Runtime lock -> `assets/so-workflow/so-package-lock.json`
- Skill contract and behavior wording -> [SKILL.md](../../SKILL.md)
