# Local Offline Package Index (Beta)

This file is the offline package authority for `/loom-skill-enhancement` when the caller selects the beta channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or prerelease policy. Use the rules and versions in this file.

## Beta Channel Rule

- Beta channel means prerelease packages from the development line.
- For deterministic package-channel execution, restore one exact prerelease version for the full SO runtime bundle.
- For this offline snapshot, the current latest beta version is `0.3.295-beta`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Version Shape Rule

- Beta versions follow `major.minor.<distance>-beta`.
- Once the beta channel is selected, do not silently downgrade to released packages.
- Use one exact beta version across the whole SO runtime bundle.

## Full Runtime Bundle Rule

Runtime selection uses an automatic two-way resolver. Before package lookup, a usable .NET 9+ host selects one exact-version DLL/dependency/Roslyn closure; without one, the resolver selects one exact-RID self-contained package. Explicit mode selection is allowed, and each resolution acquires only its locked package closure:

- `Techne.Loom.SkillOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All framework members must use the exact beta snapshot version shown above. Do not run from a partial extraction root.

Self-contained packages contain the direct `so` executable under `tools/<rid>/` and do not require a preinstalled .NET runtime, but they still depend on the target OS and ABI. .NET CLI mode stages the .NET runtime bundle listed above when explicitly selected.

The complete SkillOrchestrator runtime family is:

| RID | Runtime package | Entry point |
| --- | --- | --- |
| `win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `tools/win-x64/so.exe` |
| `win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `tools/win-arm64/so.exe` |
| `linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `tools/linux-x64/so` |
| `linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `tools/linux-arm64/so` |
| `linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `tools/linux-musl-x64/so` |
| `linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `tools/linux-musl-arm64/so` |
| `osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `tools/osx-x64/so` |
| `osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `tools/osx-arm64/so` |

## Deterministic Restore Rule

The owning skill's exact runtime version is the only version authority. `latest`, compatibility ranges, neighboring versions, and cross-channel fallback are invalid.

- Good .NET CLI path: restore the three IL packages above at `0.3.295-beta`, validate the host/CLI preflight, then use one unified runtime directory.
- Good self-contained path: restore exactly one `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package at `0.3.295-beta`, validate its hash and manifest, then use its direct executable.
- Bad: mix package versions, use a different RID, or retry a CLI error that occurred after the CLI already started.
- A valid exact-version cache entry may be reused offline. If no valid cache exists and acquisition fails, block with evidence rather than using repository output.

## Acquisition Commands

Framework-dependent IL acquisition at this `beta` snapshot uses:

```powershell
dotnet add package Techne.Loom.SkillOrchestrator --version 0.3.295-beta
dotnet add package Techne.Loom.Common --version 0.3.295-beta
dotnet add package Techne.Loom.Abstractions --version 0.3.295-beta
```

Self-contained fallback acquisition uses one exact package after RID detection:

```text
Techne.Loom.SkillOrchestrator.Runtime.<rid> @ 0.3.295-beta
```

For either mode, when the exact package id and version are known, use the exact NuGet.org V3 flat-container URLs instead of waiting for page or registration indexing:

```text
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg.sha512
```

Only after exact NuGet acquisition fails may the official GitHub `beta` release assets be tried:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.<exact-version>.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.latest.nupkg
```

The `<PackageId>.latest.nupkg` alias is a manual fallback address only; automated lock/cache restore uses the exact versioned URL and never requests `latest`.

## Unified Runtime Directory Rule

- .NET CLI mode uses one external unified directory containing `so.dll`, `so.deps.json`, `so.runtimeconfig.json`, the exact-version dependency closure, and the embedded Roslyn compiler assemblies. The `.deps.json` file is mandatory and is used for explicit dependency binding.
- Self-contained mode uses one external cache directory containing the validated `so` executable for exactly one product, version, and RID.
- Do not probe or execute from partial, mixed-version, or cross-RID directories.
- In Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the package. Add `-UseBasicParsing` to legacy HTTP probes.
- Protect the cache entry with a cross-process lock, validate in a temporary directory, and publish atomically. Set the executable bit on Unix.

## Startup Preflight

Before accepting a launch descriptor, verify the exact package identity, version, RID, allowed manifest, entrypoint, SHA-512, ZIP traversal safety, and size bounds. .NET CLI mode must also verify the .NET runtime bundle. A missing startup contract or failed host/CLI start is a failed preflight, never success evidence.

Both modes are official. A selected resolution never mixes DLL/dependency/Roslyn packages with a RID Runtime EXE package. A failure stops; a later mode change requires a new resolution identity and explicit continuation. Arguments, templates, expressions, governance, and business errors after CLI startup remain command failures.

## Launch Mode

Automatic package-channel launch selects the exact-version DLL/dependency/Roslyn closure when a usable .NET host exists, otherwise the exact-RID published executable. The resolver-owned descriptor supplies the actual launch command.

.NET CLI mode:

```powershell
dotnet exec --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

The complete .NET runtime bundle must include `.\so.deps.json` and `.\so.runtimeconfig.json`; pass `--depsfile .\so.deps.json` before `--runtimeconfig` for the explicit .NET CLI mode launch.

Self-contained mode:

```powershell
.\so.exe --guide
```

Use the matching Unix executable path without `.exe` on Unix systems. Both modes must emit a fresh guide JSON; verify its `version` and readable `guide_path` before compile, run, or resume. Reuse the same launch descriptor for every later command.

## Official Runtime Surface

Preparation and inspection commands:

- `dotnet so.dll --guide`
- `dotnet so.dll compile`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`

Official skill run commands:

- `dotnet so.dll run`
- `dotnet so.dll resume`

## Required Think-Out-Loud Fields

When the skill reports package-channel runtime preparation, include:

- `resolved_runtime_version: 0.3.295-beta`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

After every SO binary execution (`dotnet so.dll`, `so.exe`, or the platform executable `so`), report:

- `mermaid_file`
- `html_file`
- `analysis_file`
- `dataflow_file`
- `must_show_to_user_files`
- `workflow_location_summary`
- `execution_confidence`
- `estimated_overall_progress`

The think-out-loud update must begin with the current verified Mermaid, HTML, Analysis, and Dataflow artifact pairs in that order. Each Markdown link must be immediately followed by a `text` fence containing the same normalized `/` path. After those four pairs, print localized headings in this order: `## 执行信心: x%`, one short reason, `## 预计整体进度: x%`, and one brief progress sentence. For English interaction, use `## Execution confidence: x%` and `## Estimated overall progress: x%`. Confidence measures the strength of current evidence; estimated overall progress measures approximate completion of the whole request. Use current verified paths or the latest verified continuity paths only; never guess a path. When no `mermaid_delivery` is returned, state that the render is unchanged. When delivery fails or a required artifact is unavailable, report the failure and next action without inventing a link.

All progress, blocked, error, and completion prose must use the current interaction language and plain words. Do not expose workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal status/field names as the user-facing explanation. Keep exact tokens only in a separate technical-details or evidence section.

## Maintenance Rule

This file is intentionally self-contained for runtime use.

- Do not tell the runtime flow to consult repository package indexes.
- Do not require browsing NuGet pages to understand beta-channel behavior.
- Refresh this file in a maintenance pass when the beta latest version changes.

## Mermaid Artifact Continuity

After every SO binary execution, report only verified Mermaid, HTML, Analysis, and Dataflow paths. Use workspace-relative paths for editor links when a verified workspace mirror exists.

- `not_emitted`: the render is unchanged and the latest verified paths may be repeated.
- `runtime_path_only`: keep the verified runtime paths as technical evidence until a workspace mirror is available.
- `delivery_failed`: report the failure and next action without inventing a link.
