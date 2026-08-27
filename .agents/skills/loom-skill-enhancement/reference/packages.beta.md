# Local Offline Package Index (Beta)

This file is the offline package authority for `/loom-skill-enhancement` when the caller selects the beta channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or prerelease policy. Use the rules and versions in this file.

## Beta Channel Rule

- Beta channel means prerelease packages from the development line.
- For deterministic package-channel execution, restore one exact prerelease version for the full SO runtime bundle.
- For this offline snapshot, the current latest beta version is `0.3.253-beta`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Version Shape Rule

- Beta versions follow `major.minor.<distance>-beta`.
- Once the beta channel is selected, do not silently downgrade to released packages.
- Use one exact beta version across the whole SO runtime bundle.

## Full Runtime Bundle Rule

Runtime selection uses two official channels. Self-contained is the default channel and selects one exact-RID single-file package for the detected RID; .NET CLI mode is explicit, selected by `runtimeBinding` or an explicit framework bundle directory, and stages a complete .NET runtime bundle:

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

- Good .NET CLI path: restore the three IL packages above at `0.3.253-beta`, validate the host/CLI preflight, then use one unified runtime directory.
- Good self-contained path: restore exactly one `Techne.Loom.SkillOrchestrator.Runtime.<rid>` package at `0.3.253-beta`, validate its hash and manifest, then use its direct executable.
- Bad: mix package versions, use a different RID, or retry a CLI error that occurred after the CLI already started.
- A valid exact-version cache entry may be reused offline. If no valid cache exists and acquisition fails, block with evidence rather than using repository output.

## Acquisition Commands

Framework-dependent IL acquisition at this `beta` snapshot uses:

```powershell
dotnet add package Techne.Loom.SkillOrchestrator --version 0.3.253-beta
dotnet add package Techne.Loom.Common --version 0.3.253-beta
dotnet add package Techne.Loom.Abstractions --version 0.3.253-beta
```

Self-contained fallback acquisition uses one exact package after RID detection:

```text
Techne.Loom.SkillOrchestrator.Runtime.<rid> @ 0.3.253-beta
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

Both channels are official; there is no implicit fallback from one mode to the other after CLI startup. Self-contained is the default channel, while .NET CLI mode must be explicitly selected through `runtimeBinding` or an explicit framework bundle directory. Arguments, templates, expressions, governance, and business errors after CLI startup remain command failures.

## Launch Mode

The default package-channel launch is the exact-RID published self-contained executable package: run `.\so.exe` on Windows or `./so` on Unix. The framework-dependent launch shown below is only for explicit .NET CLI mode.

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

- `resolved_runtime_version: 0.3.253-beta`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

After every `dotnet so.dll` CLI call, when audit artifacts exist, also include:

- `mermaid_file`
- `html_file`
- `analysis_file` when present

If the call did not emit a fresh Mermaid render, repeat the latest known `mermaid_file`, `html_file`, and `analysis_file` as direct clickable Markdown file links, state that the render is unchanged, and add a concise workflow-location summary. Never expose only a bare Mermaid path. If the chat agent provides a Mermaid card-display tool, pass the existing Mermaid file path directly to it instead; do not read or return the file contents again solely to display the card.

## Maintenance Rule

This file is intentionally self-contained for runtime use.

- Do not tell the runtime flow to consult repository package indexes.
- Do not require browsing NuGet pages to understand beta-channel behavior.
- Refresh this file in a maintenance pass when the beta latest version changes.
