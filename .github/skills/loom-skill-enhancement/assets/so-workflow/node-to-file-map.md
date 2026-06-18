# Node To File Map

| Node | File or Artifact |
| --- | --- |
| `transition.classify_governance` | runtime-owned `governance_state` classification for the current target skill |
| `transition.ask_latest_channel` | branch to the already-enhanced latest-channel question |
| `transition.confirm_channel` | branch to the standard package-channel confirmation path |
| `transition.select_latest_channel` | `package_channel`, `guide_language`, `target_skill_path` with exactly two choices: `released` or `beta` |
| `transition.reacquire_runtime` | `assets/so-workflow/so-package-lock.json` plus runtime-owned resolved runtime metadata |
| `transition.capture_guide` | runtime-owned fresh `dotnet so.dll --guide` surface reference |
| `transition.analyze_scope` | `assets/so-workflow/skill-plan.md` plus resolved guide/package-index references carried into the governed plan gate |
| `transition.draft_template` | `assets/so-workflow/so-template.json` |
| `transition.compile_template` | external compile artifacts: `workflow.mermaid.md`, `workflow.html`, `workflow.json`, `workflow.analysis.json` |
| `transition.request_review` | `approval_decision`, `feedback_notes`, plus compile-time audit artifact links from the AskUser boundary |
| `transition.wait_runtime` | `workflow.current.json`, blocked runtime copy plus strongest-earned blocked-governance artifacts under `gate.bootstrap_blocked_governance` |
| `transition.finalize_lock` | external completion manifest resolved under the OS temp root from `.tmp/loom-skill-enhancement-completion-manifest.md`, governing checked-in `SKILL.md`, `assets/so-workflow/so-package-lock.json`, `assets/so-workflow/governance-notes.md`, `assets/so-workflow/node-to-file-map.md`, and the selected guide/package-index evidence captured for the slice |
