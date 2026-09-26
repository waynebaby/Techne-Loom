# SkillOrchestrator Guide: Examples

[中文](../../zh-cn/guides/so-guide-reference-examples.md) | [Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.3.325-beta
Build: published package 0.3.325-beta
<!-- guide-version:end -->







## Examples

For a full narrative example of a skill being enhanced under Loom Skill Orchestrator governance run with stage gates, branch fan-out, validation, audit evidence, and Mermaid route diagrams, see [Skill Under Loom Skill Orchestrator Governance Run Example](../examples/so-enhanced-skill-run.md).

```guide-example
name: local-tool-then-block-for-user
flow:
  - ToolCall: ls working directory
  - AskUser: choose target file
result:
  status: blocked
  current_step_kind: AskUser
```

```guide-example
name: model-think-with-memory
flow:
  - MemoryRead: summarize prior review findings
  - ModelThink: propose minimal code edit
result:
  status: blocked
  current_step_kind: ModelThink
  memory_for_next_step: curated summary of prior findings
```

```guide-example
name: wait-for-external-signal
flow:
  - WaitResume: wait for webhook completion
result:
  status: blocked
  current_step_kind: WaitResume
  required_inputs:
    - correlation_id
    - payload
```

```guide-example
name: finished-deterministic-run
flow:
  - ToolCall: generate output
  - ArtifactEmit: write report
result:
  status: completed
  current_node_id: state.done
  context:
    output_path: outputs/report.md
```

```guide-example
name: enhanced-skill being enhanced-runtime-lock-reference
target_skill_markdown: |
  ## Skill Under Loom Skill Orchestrator Governance Runtime Lock

  This skill is under Loom Skill Orchestrator governance.
  Authoritative SO runtime version lock: `assets/so-workflow/so-package-lock.json`.
  Detect one supported host RID and validate one exact product+RID package. Prefer a valid exact package in the standard NuGet cache; otherwise verify its SHA-512 against exact registration metadata. Use the same-version GitHub fallback only when its `.sha512` sidecar verifies.
  Safely extract the package and run its apphost `--guide` as the first runtime operation.
notes:
  - keep the reference checked in with the skill being enhanced
  - treat the lock file as the authority for runtime package version and validation policy
```

```guide-example
name: minimal-so-package-lock
so_package_lock_json: |
  {
    "resolved_version": "1.2.3",
    "runtime_restore": {
      "source": "nuget-registration",
      "fallback_source": "same-version-github-release-asset",
      "cache_policy": "standard-nuget-cache-exact-package-first",
      "reuse_exact_package_when_valid": true,
      "download_exact_locked_package_when_missing_or_invalid": true,
      "never_float_to_latest": true,
      "create_loom_specific_cache": false,
      "required_package_validation": [
        "package_id_matches",
        "exact_version_matches",
        "rid_matches",
        "registration_sha512_or_github_sidecar_matches",
        "nuspec_identity_matches",
        "runtime_manifest_matches",
        "archive_paths_and_sizes_are_safe",
        "apphost_and_english_guide_are_present"
      ]
    },
    "enhancement": {
      "resolved_at_utc": "2026-06-12T00:00:00Z",
      "selected_language": "en"
    },
    "notes": [
      "Detect one supported host RID and acquire only its exact product+RID runtime package.",
      "Reuse an exact package from the standard NuGet cache only after all package checks pass.",
      "Verify package identity, version, RID, SHA-512, manifest, archive safety, apphost, and English guide files before extraction.",
      "Use the same-version GitHub release asset only when its matching .sha512 sidecar verifies.",
      "Run the extracted apphost --guide as the first runtime operation."
    ]
  }
restore_rule:
  - detect one supported host RID and acquire only its exact product+RID package
  - verify package identity, registration SHA-512 or GitHub sidecar, manifest, and archive safety before extraction
  - run the extracted apphost --guide as the first runtime operation
```
