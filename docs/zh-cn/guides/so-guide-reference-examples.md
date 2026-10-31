# SkillOrchestrator Guide：Examples

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [English](../../en/guides/so-guide-reference-examples.md) | [根目录](../README.md)

<!-- guide-version:start -->
版本：0.4.10
构建：已发布的 0.4.10 包
<!-- guide-version:end -->





























## Examples

如果你想看一份更完整的 受 Loom Skill Orchestrator 治理的 skill being enhanced 运行叙述示例，其中包含 stage gate、branch fan-out、validation、audit evidence 与 Mermaid 路线图，请阅读 [受 Loom Skill Orchestrator 治理的 Skill 运行示例](../examples/so-enhanced-skill-run.md)。

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
  ## Loom Skill Orchestrator Governance Runtime Lock

  本 skill 处于 Loom Skill Orchestrator governance 下。
  权威 SO runtime 版本锁：`assets/so-workflow/so-package-lock.json`。
  检测一个受支持的 host RID，并校验一个精确的 product+RID package。首先复用当前用户 OS-temp 共享缓存 `<OS-temp>/TechneLoom/runtime-cache/` 中通过校验的 generation，其 key 包含精确 package ID、版本、RID 和完整 `.nupkg` SHA-512；若无有效 generation，再检查标准 NuGet cache，完全未命中时才获取精确 package。只有匹配的 `.sha512` sidecar 校验通过时，才使用同版本 GitHub fallback。
  安全解压 package 后，第一条 runtime 操作是直接运行 apphost `--guide`。
notes:
  - 保持这段引用随 skill being enhanced 一起 checked in
  - 把 lock 文件视为 runtime package 版本与校验策略的权威来源
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
      "selected_language": "zh-cn"
    },
    "notes": [
      "检测一个受支持的 host RID，并只获取其精确 product+RID runtime package。",
      "优先创建并使用当前用户 OS-temp 共享缓存 <OS-temp>/TechneLoom/runtime-cache/；generation 按精确 package ID、版本、RID 和完整 .nupkg SHA-512 标识。标准 NuGet cache 仅作为回退，所有候选 package 都必须先通过校验才能复用。",
      "解压前校验 package identity、版本、RID、SHA-512、manifest、archive safety、apphost 和英文 guide 文件。",
      "只有同版本 GitHub release asset 的匹配 .sha512 sidecar 校验通过时才使用它。",
      "安全解压后，第一条 runtime 操作是直接运行 apphost --guide。"
    ]
  }
restore_rule:
  - 检测一个受支持的 host RID，并只获取其精确 product+RID package
  - 解压前校验 package identity、registration SHA-512 或 GitHub sidecar、manifest 与 archive safety
  - 将解压后的 apphost --guide 作为第一条 runtime 操作
```
