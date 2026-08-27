# Node To File Map

All checked-in document paths in this map are relative to the target skill root `demos/loom-enhanced-research/3. Born-governanced/loom-enhanced-research`. Absolute paths, `..` traversal, repository-global `docs/` paths, and another skill root are invalid map targets. Runtime-owned outputs use explicit placeholders and are not checked-in document ownership.

| Node | File or Artifact |
| --- | --- |
| `transition.capture_runtime_preflight` | runtime-owned published bundle preflight evidence plus `assets/so-workflow/so-package-lock.json` lock context before downstream work |
| `transition.capture_published_runtime_guide` | `assets/so-workflow/reference/so/runtime-contracts.md` as target-local context; the fresh guide `guide_path` remains authoritative |
| `transition.capture_runtime_exception_workaround` | `assets/so-workflow/reference/so/runtime-governance.md`, `assets/so-workflow/so-package-lock.json`, and runtime-owned workaround evidence |
| `transition.collect_intake_contract` | `SKILL.md` and `contract.json` |
| `transition.initialize_run_artifacts` | runtime-created run root outside the skill folder |
| `transition.execute_research_round` | `assets/loom-enhanced-research-research-round.agent.md` and runtime-owned round outputs |
| `transition.build_material_inventory` | runtime-owned material inventory |
| `transition.collect_material_review` | runtime-owned user review payload |
| `transition.update_research_seed` | runtime-owned continuation payload |
| `transition.generate_report_draft` | `assets/loom-enhanced-research-report-draft.agent.md` and runtime-owned report draft |
| `transition.collect_draft_review` | runtime-owned draft review payload |
| `transition.publish_final_report` | runtime-owned final report and completion manifest |
| `assets/so-workflow/skill-plan.md` | checked-in planning source |
| `assets/so-workflow/so-template.json` | checked-in Loom-governanced workflow authority source |
| `assets/so-workflow/so-package-lock.json` | checked-in exact-version runtime lock |
| `assets/so-workflow/reference/document-copy-manifest.json` | source/version/hash/provenance record for target-local SO copies |
| `SKILL.md` | checked-in governance wording and target-skill execution contract |
| `contract.json` | public input and output contract for the skill surface |