# SkillOrchestrator Guide: Examples

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [Root](../README.md)

Version: 0.3.288
Build: published package 0.3.288

## Examples

For a full narrative example of a Loom-governanced target-skill run with stage gates, branch fan-out, validation, audit evidence, and Mermaid route diagrams, see [Loom-Governanced Skill Run Example](../examples/so-enhanced-skill-run.md).

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
name: enhanced-target-skill-runtime-lock-reference
target_skill_markdown: |
  ## Loom-Governanced Runtime Lock

  This skill is enhanced by Loom SO.
  Authoritative SO runtime version lock: `assets/so-workflow/so-package-lock.json`.
  Routine SO runtime bundle restoration must resolve the exact locked bundle from NuGet first; if the local cache already holds that same version bundle, reuse it, otherwise download it again from NuGet.
notes:
  - keep the reference checked in with the target skill
  - treat the lock file as the authority for day-to-day SO runtime restoration
```

```guide-example
name: minimal-so-package-lock
so_package_lock_json: |
  {
    "package_id": "Techne.Loom.SkillOrchestrator",
    "channel": "released",
    "resolved_version": "1.2.3",
    "runtime_restore": {
      "source": "nuget",
      "cache_policy": "exact-version-first",
      "reuse_exact_local_bundle_when_valid": true,
      "download_exact_locked_version_when_missing_or_invalid": true,
      "never_float_to_latest": true,
      "required_bundle_validation": ["package_id_matches", "exact_version_matches", "nuspec_identity_matches", "complete_dotnet_cli_runtime_bundle"],
      "fallback_source": "github-release-asset"
    },
    "enhancement": {
      "resolved_at_utc": "2026-06-12T00:00:00Z",
      "selected_language": "en"
    },
    "notes": [
      "Resolve the exact version from NuGet first.",
      "Validate and reuse a complete local exact-version bundle before downloading.",
      "Download only the exact locked version when any bundle member is missing or invalid; never resolve latest.",
      "Use GitHub release assets only when NuGet.org is unavailable."
    ]
  }
restore_rule:
  - resolve the exact version from NuGet first
  - reuse local cache only when it already holds that exact version
  - otherwise download the exact version again from NuGet
```
