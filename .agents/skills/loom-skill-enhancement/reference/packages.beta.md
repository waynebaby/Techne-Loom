# Local Offline Package Index (Beta)

This file is the offline package authority for `/loom-skill-enhancement` when the caller selects the beta channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or prerelease policy. Use the rules and versions in this file.

## Beta Channel Rule

- Beta channel means prerelease packages from the development line.
- For deterministic package-channel execution, restore one exact prerelease version for the full SO runtime bundle.
- For this offline snapshot, the current latest beta version is `0.3.231-beta`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Version Shape Rule

- Beta versions follow `major.minor.<distance>-beta`.
- Once the beta channel is selected, do not silently downgrade to released packages.
- Use one exact beta version across the whole SO runtime bundle.

## Full Runtime Bundle Rule

Runtime selection is host-first. If the candidate `Microsoft.NETCore.App 9.x` host passes the CLI startup preflight, use the framework-dependent IL bundle:

- `Techne.Loom.SkillOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All framework members must use the exact beta snapshot version shown above. Do not run from a partial extraction root.

If the .NET 9 host is missing or cannot start the CLI, use one self-contained single-file package for the detected RID. It contains the direct `so` executable and does not require a preinstalled .NET runtime, but it still depends on the target OS and ABI.

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

- Good framework path: restore the three IL packages above at `0.3.231-beta`, validate the host/CLI preflight, then use one unified runtime directory.
- Good self-contained path: restore exactly one `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package at `0.3.231-beta`, validate its hash and manifest, then use its direct executable.
- Bad: mix package versions, use a different RID, or retry a CLI error that occurred after the CLI already started.
- A valid exact-version cache entry may be reused offline. If no valid cache exists and acquisition fails, block with evidence rather than using repository output.

## Acquisition Commands

Framework-dependent IL acquisition at this `beta` snapshot uses:

```powershell
dotnet add package Techne.Loom.SkillOrchestrator --version 0.3.231-beta
dotnet add package Techne.Loom.Common --version 0.3.231-beta
dotnet add package Techne.Loom.Abstractions --version 0.3.231-beta
```

Self-contained fallback acquisition uses one exact package after RID detection:

```text
Techne.Loom.SkillOrchestrator.Runtime.<rid> @ 0.3.231-beta
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

## Unified Runtime Directory Rule

- Framework mode uses one external unified directory containing `so.dll`, `so.runtimeconfig.json`, and the exact-version dependency closure. If `so.deps.json` is present, keep it beside the bundle and use it for explicit dependency binding when the host requires it.
- Self-contained mode uses one external cache directory containing the validated `so` executable for exactly one product, version, and RID.
- Do not probe or execute from partial, mixed-version, or cross-RID directories.
- In Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the package. Add `-UseBasicParsing` to legacy HTTP probes.
- Protect the cache entry with a cross-process lock, validate in a temporary directory, and publish atomically. Set the executable bit on Unix.

## Startup Preflight

Before accepting a launch descriptor, verify the exact package identity, version, RID, allowed manifest, entrypoint, SHA-512, ZIP traversal safety, and size bounds. Framework mode must also verify the complete three-package dependency closure. A missing startup contract or failed host/CLI start is a failed preflight, never success evidence.

Only a missing/unusable host or host-startup failure selects the self-contained package. Arguments, templates, expressions, governance, and business errors after CLI startup remain command failures.

## Launch Mode

Framework mode:

```powershell
dotnet exec --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

If `.\so.deps.json` is present and explicit dependency binding is required, add `--depsfile .\so.deps.json` before `--runtimeconfig`.

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

- `resolved_runtime_version: 0.3.231-beta`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

After every `dotnet so.dll` CLI call, when audit artifacts exist, also include:

- `mermaid_file`
- `html_file`
- `analysis_file` when present

If the call did not emit a fresh Mermaid render, repeat the latest known `mermaid_file`, `html_file`, and `analysis_file` and state that the render is unchanged, then add a concise workflow-location summary.

## Maintenance Rule

This file is intentionally self-contained for runtime use.

- Do not tell the runtime flow to consult repository package indexes.
- Do not require browsing NuGet pages to understand beta-channel behavior.
- Refresh this file in a maintenance pass when the beta latest version changes.
