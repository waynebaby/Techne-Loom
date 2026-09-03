# Node To File Map

All checked-in paths are relative to the target skill root `demos/loom-enhanced-research/4. Released-0.3.282/loom-enhanced-research` and use POSIX separators. Runtime outputs are external evidence, not checked-in source ownership.

| Node | File or artifact |
| --- | --- |
| `transition.capture_runtime_preflight` | `assets/so-workflow/so-package-lock.json` plus exact runtime-owned preflight evidence |
| `transition.start_mcp` | `assets/so-workflow/reference/runtime-semantic-migration.md` plus runtime-owned `mcp_startup_evidence` |
| `transition.capture_guide` | target-local SO references and the fresh runtime-owned guide path |
| `transition.collect_intake_contract` | `SKILL.md` and `contract.json` |
| `transition.execute_research_round` | `assets/loom-enhanced-research-research-round.agent.md` and runtime-owned round evidence |
| `transition.build_material_inventory` | runtime-owned material inventory |
| `transition.collect_material_review` | runtime-owned user-owned review payload |
| `transition.generate_report_draft` | `assets/loom-enhanced-research-report-draft.agent.md` and runtime-owned draft |
| `transition.collect_draft_review` | runtime-owned user-owned draft review payload |
| `transition.publish_final_report` | runtime-owned final report and completion manifest |
| `assets/so-workflow/so-template.json` | canonical released 0.3.282 workflow authority |
| `assets/so-workflow/scripts/*.js` | dry-run migration, producer audit, and idempotence tools |
| `assets/so-workflow/reference/runtime-semantic-migration.md` | 0.3.282 emitter and gov4 reference |
| `assets/so-workflow/reference/migration-script-playbook.md` | migration input/output, rollback, and idempotence contract |
| `assets/so-workflow/reference/document-copy-manifest.json` | source/version/hash provenance for target-local SO copies |
