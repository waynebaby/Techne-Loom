# Node To File Map

All checked-in document paths in this map are relative to the target skill root `.agents/skills/loom-skill-enhancement`. Absolute paths, `..` traversal, repository-global `docs/` paths, and another skill root are invalid map targets. Runtime-owned outputs use explicit placeholders and are not checked-in document ownership.
- Bound SO package runtime version: `0.3.288`.

| Node | File or Artifact |
| --- | --- |
| `transition.classify_governance` | runtime-owned `governance_state` seed that marks the current self-bootstrap target as already Loom-governanced before the bound-runtime path decision |
| `transition.enter_reenhancement_context` | branch to the explicit re-enhancement context path for an already-governed target |
| `transition.inspect_existing_skill_markdown` | `SKILL.md` checked-in source snapshot under the target skill root |
| `transition.inspect_existing_package_lock` | `assets/so-workflow/so-package-lock.json` checked-in exact-version lock under the target skill root |
| `transition.inspect_existing_workflow_assets` | `assets/so-workflow/so-template.json`, `assets/so-workflow/governance-notes.md`, `assets/so-workflow/node-to-file-map.md`, `assets/so-workflow/reference/document-copy-manifest.json`, `assets/so-workflow/reference/so/runtime-contracts.md`, and `assets/so-workflow/reference/so/runtime-governance.md` |
| `transition.use_bound_runtime_path` | branch to the standard skill-bound runtime path without a user-facing channel prompt; it still passes through target-local document inspection |
| `transition.reacquire_runtime` | shared entry gate step 1: external runtime-preparation evidence that must weave back `assets/so-workflow/so-package-lock.json` authority plus published-package workflow evidence, runtime preflight result, resolved runtime version, runtime bundle package list, and unified runtime directory evidence |
| `assets/so-workflow/restore-so-runtime.ps1` | exact-version cache-first package acquisition helper; validates the locked runtime bundle before reuse and records cache/download evidence without resolving latest |
| `transition.start_mcp` | shared governance-entry branch: use `assets/agents/loom-skill-enhancement-mcp-startup.agent.md` with the resolver-owned launch descriptor to generate MCP config, try registration/handshake/fragment inspection, and persist `mcp_startup_evidence` before guide capture |
| `transition.require_reenhancement_gap_review` | branch that requires explicit re-enhancement guide-delta review after the fresh guide capture |
| `transition.skip_reenhancement_gap_review` | branch that skips re-enhancement guide-delta review for not-yet-governed targets |
| `transition.compare_skill_markdown_against_latest_guide` | compile-review prerequisite stage: target-local subagent route through `assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md`, plus `SKILL.md` |
| `transition.compare_package_lock_against_latest_guide` | compile-review prerequisite stage: target-local subagent route through `assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md`, plus `assets/so-workflow/so-package-lock.json` |
| `transition.compare_workflow_governance_against_latest_guide` | parallel re-enhancement review batch: target-local subagent route through `assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md`, `assets/so-workflow/governance-notes.md`, and local SO governance reference |
| `transition.build_shared_review_context` | one-time bounded shared context producer: real checked-in snapshots, source manifest, guide/runtime references, context hash, and external workflow-copy identity |
| `transition.aggregate_reenhancement_findings` | `assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md` aggregate of every re-enhancement gap result before strategy judgment |
| `transition.aggregate_plan_findings` | `assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md` aggregate of scope, route/gate, and evidence/node-map analysis before drafting | |
| `transition.judge_reenhancement_template_strategy` | re-enhancement strategy judgment through `assets/agents/loom-skill-enhancement-reenhancement-conflict-judgment.agent.md`, comparing the old template, current requirements, target-local assets, gap reviews, and fresh guide before publishing the selected template-change strategy |
| `transition.analyze_scope` | parallel planning batch: target-local subagent route through `assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md`, consuming the shared context and writing the per-run governed review plan |
| `transition.analyze_route_gate_structure` | parallel planning batch through `assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md`, consuming the shared context |
| `transition.analyze_evidence_node_map` | parallel planning batch through `assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md`, consuming the shared context |
| `transition.aggregate_plan_findings` | complete planning aggregate required before template drafting |
| `transition.review_skill_markdown_before_repair` | parallel pre-repair review batch through the skill-markdown gap reviewer |
| `transition.review_package_lock_before_repair` | parallel pre-repair review batch through the package-lock gap reviewer |
| `transition.review_workflow_governance_before_repair` | parallel pre-repair review batch through the workflow-governance gap reviewer |
| `transition.review_evidence_node_map_before_repair` | parallel pre-repair review batch through the evidence/node-map reviewer |
| `transition.aggregate_review_findings` | `assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md` complete pre-repair findings aggregate |
| `transition.apply_batch_repair` | `assets/agents/loom-skill-enhancement-review-fix-loop.agent.md` one coordinated repair from the complete aggregate |
| `transition.validate_skill_markdown_after_repair` | parallel post-fix validation batch for `SKILL.md` |
| `transition.validate_package_lock_after_repair` | parallel post-fix validation batch for the package lock |
| `transition.validate_workflow_governance_after_repair` | parallel post-fix validation batch for workflow governance assets |
| `transition.validate_evidence_node_map_after_repair` | parallel post-fix validation batch for evidence and node-map assets |
| `transition.aggregate_post_fix_validation` | complete post-fix validation aggregate before serial validation |
| `transition.capture_guide` | shared entry gate step 3: runtime-owned fresh guide result from the same resolver-owned launch descriptor after MCP or CLI governance-entry evidence; local references never replace the returned guide path |
| `transition.run_serial_validation` | final ordered JSON, graph/dataflow, compile, schema/demo, exact-version three-node differential run, batch migration verification, and decision-evidence indexing before official execution; governed by `reference/execution-contract.md` and writes `<execution-output-root>/evidence/` plus `runtime_semantic_probe_evidence`, `batch_migration_evidence`, and `decision_evidence_manifest` |
| `transition.review_weave_out_subagent_fit` | compile-review prerequisite stage: target-local subagent route through `assets/agents/loom-skill-enhancement-weave-out-subagent-fit-review.agent.md` that reviews current weave-outs and records target-skill relative-link updates |
| `transition.draft_template` | compile-review prerequisite stage: target-local workflow-designer route through `assets/agents/loom-skill-enhancement-workflow-designer.agent.md`; consumes the bounded exact-runtime reference pack and schema/demo evidence, then weaves back the checked-in `assets/so-workflow/so-template.json` plus runtime-owned `<execution-output-root>/workflow-design/reference-manifest.json`, `<execution-output-root>/workflow-design/static-contract-review.json`, and `<execution-output-root>/workflow-design/semantic-probe-report.json` descriptors |
| `transition.compile_template` | compile-review prerequisite stage: external compile seam that must weave back runtime-owned workflow.mermaid.md, workflow.html, workflow.json, and workflow.analysis.json artifacts |
| `transition.request_review` | compile-review prerequisite stage: runtime-owned approval and feedback payload plus local map and manifest references from the user review seam |
| `transition.accept_official_runnable` | post-approval branch that enters the explicit review-fix loop before the mandatory official runnable route begins |
| `transition.run_serial_validation` | final serial validation seam that weaves back `serial_validation_evidence`, `review_fix_loop_evidence`, and `commit_report_ready` after both parallel batches and the coordinated repair |
| `assets/agents/loom-skill-enhancement-review-findings-aggregator.agent.md` | local authority for complete batch aggregation without target-skill repair |
| `transition.route_official_runnable_after_review` | post-review-fix branch that enters the official runnable route only after shared entry-gate proof, compile-review artifacts, and explicit review-fix evidence already exist |
| `transition.materialize_runtime_copy` | official runnable route: runtime-owned external workflow.json copy derived from the checked-in template before the public runtime chain starts |
| `transition.wait_runtime` | official runnable route: runtime-owned workflow copy, event log, and strongest-earned blocked evidence when the route blocks; resumed runtime copy identity must remain unchanged |
| `transition.finalize_lock` | official runnable route terminal transition: runtime-owned completion manifest resolved to a unique per-run path under the OS temp root; this route carries the checked-in `SKILL.md`, `assets/so-workflow/so-package-lock.json`, node map, manifest, and target-local source assets as authoritative deliverables |

| `state.shared_review_context`, `state.reenhancement_gap_aggregate`, `state.plan_aggregate`, `state.review_findings_aggregate`, `state.batch_repair`, `state.post_fix_validation`, `state.post_fix_validation_aggregate`, `state.serial_validation` | explicit batch-context, aggregation, coordinated repair, parallel revalidation, and serial validation stages |

| `SKILL.md` progressive-loading routes | `reference/execution-contract.md`, `reference/review-and-evidence-contract.md`, and `reference/plain-language-feedback.md` hold detailed payload, evidence, and wording contracts while `SKILL.md` keeps the mandatory execution route |
| `mermaid_delivery` artifact handoff | `reference/mermaid-artifact-delivery.md` defines verified runtime paths, workspace mirrors, link states, HTML preview, card handling, and fail-closed reporting |

## Document Ownership

The SO reference copies under `assets/so-workflow/reference/so/` are complete guide pages extracted from the exact bound SO package. They are refreshed with the bound SO package version and their source hashes are recorded in `assets/so-workflow/reference/document-copy-manifest.json`. The fresh published-runtime guide path remains the authority for version-specific behavior.