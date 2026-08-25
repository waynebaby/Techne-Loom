# Local Offline Package Index (Released)

This file is the offline package authority for `/loom-skill-enhancement` when the caller selects the released channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or version policy. Use the rules and versions in this file.

## Released Channel Rule

- Released channel means stable packages only.
- For deterministic package-channel execution, restore one exact stable version for the full SO runtime bundle.
- For this offline snapshot, the current latest released version is `0.2.229`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Full Runtime Bundle Rule

Runtime selection uses two official channels. Self-contained is the default channel and selects one exact-RID single-file package for the detected RID; legacy framework/library mode is explicit, selected by `runtimeBinding` or an explicit framework bundle directory, and stages the complete three-package closure:

- `Techne.Loom.SkillOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All framework members must use the exact released snapshot version shown above. Do not run from a partial extraction root.

Self-contained packages contain the direct `so` executable under `tools/<rid>/` and do not require a preinstalled .NET runtime, but they still depend on the target OS and ABI. Legacy mode stages the full three-package closure above when explicitly selected.

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

- Good framework path: restore the three IL packages above at `0.2.229`, validate the host/CLI preflight, then use one unified runtime directory.
- Good self-contained path: restore exactly one `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package at `0.2.229`, validate its hash and manifest, then use its direct executable.
- Bad: mix package versions, use a different RID, or retry a CLI error that occurred after the CLI already started.
- A valid exact-version cache entry may be reused offline. If no valid cache exists and acquisition fails, block with evidence rather than using repository output.

## Acquisition Commands

Framework-dependent IL acquisition at this `released` snapshot uses:

```powershell
dotnet add package Techne.Loom.SkillOrchestrator --version 0.2.229
dotnet add package Techne.Loom.Common --version 0.2.229
dotnet add package Techne.Loom.Abstractions --version 0.2.229
```

Self-contained fallback acquisition uses one exact package after RID detection:

```text
Techne.Loom.SkillOrchestrator.Runtime.<rid> @ 0.2.229
```

For either mode, when the exact package id and version are known, use the exact NuGet.org V3 flat-container URLs instead of waiting for page or registration indexing:

```text
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg.sha512
```

Only after exact NuGet acquisition fails may the official GitHub `released` release assets be tried:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.<exact-version>.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.latest.nupkg
```

The `<PackageId>.latest.nupkg` alias is a manual fallback address only; automated lock/cache restore uses the exact versioned URL and never requests `latest`.

## Unified Runtime Directory Rule

- Framework mode uses one external unified directory containing `so.dll`, `so.deps.json`, `so.runtimeconfig.json`, and the exact-version dependency closure. The `.deps.json` file is mandatory and is used for explicit dependency binding.
- Self-contained mode uses one external cache directory containing the validated `so` executable for exactly one product, version, and RID.
- Do not probe or execute from partial, mixed-version, or cross-RID directories.
- In Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the package. Add `-UseBasicParsing` to legacy HTTP probes.
- Protect the cache entry with a cross-process lock, validate in a temporary directory, and publish atomically. Set the executable bit on Unix.

## Startup Preflight

Before accepting a launch descriptor, verify the exact package identity, version, RID, allowed manifest, entrypoint, SHA-512, ZIP traversal safety, and size bounds. Framework mode must also verify the complete three-package dependency closure. A missing startup contract or failed host/CLI start is a failed preflight, never success evidence.

Both channels are official; there is no implicit fallback from one mode to the other after CLI startup. Self-contained is the default channel, while legacy mode must be explicitly selected through `runtimeBinding` or an explicit framework bundle directory. Arguments, templates, expressions, governance, and business errors after CLI startup remain command failures.

## Launch Mode

Framework mode:

```powershell
dotnet exec --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

The complete legacy bundle must include `.\so.deps.json` and `.\so.runtimeconfig.json`; pass `--depsfile .\so.deps.json` before `--runtimeconfig` for the explicit legacy launch.

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

- `resolved_runtime_version: 0.2.229`
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
- Do not require browsing NuGet pages to understand released-channel behavior.
- Refresh this file in a maintenance pass when the released latest version changes.
