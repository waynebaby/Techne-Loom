# loom-enhanced-research Node To File Map

All checked-in document paths in this map are relative to the target skill root `demos/loom-enhanced-research/2.1 Enhance from Ungovernaced/loom-enhanced-research`. Absolute paths, `..` traversal, repository-global `docs/` paths, and another skill root are invalid map targets. Runtime-owned outputs use explicit placeholders and are not checked-in document ownership.

## Runtime proof

- `state.start` -> `assets/so-workflow/so-template.json`
- `state.runtime_path_decision` -> `assets/so-workflow/so-package-lock.json`
- `state.runtime_guide` -> `assets/so-workflow/reference/so/runtime-contracts.md` as target-local context; the fresh runtime `guide_path` is authoritative
- `state.runtime_exception` -> `assets/so-workflow/reference/so/runtime-governance.md` and `assets/so-workflow/so-package-lock.json`

## Intake and setup

- `state.intake` -> `SKILL.md` and `contract.json`
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
- `state.done` -> `assets/so-workflow/reference/document-copy-manifest.json`, `assets/so-workflow/node-to-file-map.md`, and other governed source assets

## Governed source assets

- Workflow plan -> `assets/so-workflow/skill-plan.md`
- Workflow template -> `assets/so-workflow/so-template.json`
- Runtime lock -> `assets/so-workflow/so-package-lock.json`
- Target-local SO contract copy -> `assets/so-workflow/reference/so/runtime-contracts.md`
- Target-local SO governance copy -> `assets/so-workflow/reference/so/runtime-governance.md`
- Copy provenance -> `assets/so-workflow/reference/document-copy-manifest.json`
- Skill contract and behavior wording -> `SKILL.md`