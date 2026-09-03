# SkillOrchestrator Guide：Examples

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-examples.md) | [根目录](../README.md)

版本：0.3.283-beta
构建：已发布的 0.3.283-beta 包

## Examples

如果你想看一份更完整的 Loom 治理 target skill 运行叙述示例，其中包含 stage gate、branch fan-out、validation、audit evidence 与 Mermaid 路线图，请阅读 [Loom 治理 Skill 运行示例](../examples/so-enhanced-skill-run.md)。

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

  本 skill 已切换到 Loom-governanced execution。
  权威 SO runtime 版本锁：`assets/so-workflow/so-package-lock.json`。
  日常 SO runtime bundle 恢复必须先从 NuGet 解析锁定的精确 bundle；如果本地 cache 已经持有该相同版本 bundle，则直接复用，否则重新从 NuGet 下载。
notes:
  - 保持这段引用随 target skill 一起 checked in
  - 把 lock 文件视为日常 SO runtime 恢复的权威来源
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
      "selected_language": "zh-cn"
    },
    "notes": [
      "先从 NuGet 解析精确版本。",
      "除非本地 cache 已经持有完全相同版本，否则重新下载。",
      "只有在 NuGet.org 不可用时才退回 GitHub release asset。"
    ]
  }
restore_rule:
  - 先从 NuGet 解析精确版本
  - 只有本地 cache 已经持有完全相同版本时才复用
  - 否则必须从 NuGet 重新下载该精确版本
```
