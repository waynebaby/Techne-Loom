# Beta Package Index

[中文](packages.beta.zh-CN.md) | [Released](packages.released.md)

Use this page for development or beta acquisition. Direct CLI or manual callers can choose the beta channel here when they want development-branch behavior or unreleased package shape; governed AO/SO skill runs should instead follow the runtime version already bound by the current CI/CD-managed skill package version block or checked-in runtime lock, then derive `released` versus `beta` from that bound version when needed.

Runtime selection rule for local execution: both official channels are published. Self-contained is the default channel and uses one exact-RID single-file runtime package for the detected RID; legacy framework/library mode is explicit, selected by `runtimeBinding` or an explicit framework bundle directory, and stages the complete three-package bundle (`Techne.Loom.AgentOrchestrator`/`Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, `Techne.Loom.Abstractions`) with a usable `Microsoft.NETCore.App 9.x` host. There is no implicit fallback between modes after startup. Both modes use the same CLI and governance contract; follow [Platform Detection Steps](docs/en/reference/runtime/platform-detection.md) and keep the returned launch descriptor for every command.

## Repository References

- Main branch: `https://github.com/waynebaby/Techne-Loom/tree/main`
- Development branch: `https://github.com/waynebaby/Techne-Loom/tree/development`

## GitHub Release Fallback

Use these links when you need the latest beta package assets without rebuilding locally. The beta fallback release is a GitHub prerelease and keeps both exact prerelease `.nupkg` assets and durable `*.latest.nupkg` aliases.

- Latest beta fallback release page: <https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-beta-latest>

## NuGet.org Latest Version

Use the NuGet.org package page when you want the latest published prerelease version number before choosing an exact `--version`.

- Open the package page and inspect the version list on NuGet.org to confirm the newest published prerelease.
- If you want the newest prerelease without pinning a number, use `dotnet add package <PackageId> --prerelease`.
- If you need an exact prerelease version, copy it from NuGet.org and use `dotnet add package <PackageId> --version <latest-beta-version>`.
- If the exact package id and version are already known, do not wait for NuGet.org page/search/registration indexing to catch up before deciding whether the package exists. Probe or download the exact `.nupkg` URL directly instead.
- Direct exact-version package URL shape: `https://www.nuget.org/api/v2/package/<PackageId>/<Version>`

| Package | NuGet.org | Latest beta example |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions --prerelease` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common --prerelease` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator --prerelease` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator --prerelease` |

Direct package check examples:

```text
https://www.nuget.org/api/v2/package/Techne.Loom.AgentOrchestrator/0.3.239-beta
https://www.nuget.org/api/v2/package/Techne.Loom.SkillOrchestrator/0.3.239-beta
```

## Version Shape

<!-- package-version-block:start -->
- The current latest published beta package version is `0.3.239-beta`.
- Development publishing on `development` pushes `major.minor.<distance>-beta` versions to NuGet.org, where `<distance>` is the GitVersion commit distance from the current version source.
<!-- package-version-block:end -->




















## .NET

<!-- package-dotnet-block:start -->
| Role | Package / source | Beta acquisition | GitHub fallback | Example |
| --- | --- | --- | --- | --- |
| Abstractions | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.3.239-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.latest.nupkg) | consume exact latest prerelease |
| Common | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.3.239-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Common.latest.nupkg) | consume exact latest prerelease |
| Plan execution runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.3.239-beta` plus restore `Techne.Loom.Common` and `Techne.Loom.Abstractions` at `0.3.239-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | use exact latest prerelease AO runtime bundle |
| Skill execution runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.3.239-beta` plus restore `Techne.Loom.Common` and `Techne.Loom.Abstractions` at `0.3.239-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | use exact latest prerelease SO runtime bundle |
<!-- package-dotnet-block:end -->


## Runtime Package Family

The self-contained runtime family is not a fourth governance product. It is an alternate host for the same AO or SO CLI. The beta channel has published the 16-package Runtime Package Family at `0.3.234-beta`; stable may still be pending until a stable release exists. Do not invent a version or hash that NuGet does not show.

| RID | AO runtime package | SO runtime package | Fixed entrypoints |
| --- | --- | --- | --- |
| `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `tools/win-x64/ao.exe` / `tools/win-x64/so.exe` |
| `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `tools/win-arm64/ao.exe` / `tools/win-arm64/so.exe` |
| `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `tools/linux-x64/ao` / `tools/linux-x64/so` |
| `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `tools/linux-arm64/ao` / `tools/linux-arm64/so` |
| `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `tools/linux-musl-x64/ao` / `tools/linux-musl-x64/so` |
| `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `tools/linux-musl-arm64/ao` / `tools/linux-musl-arm64/so` |
| `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `tools/osx-x64/ao` / `tools/osx-x64/so` |
| `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `tools/osx-arm64/ao` / `tools/osx-arm64/so` |

The complete matrix is AO x 8 plus SO x 8, for 16 runtime PackageIds:

- AO: `Techne.Loom.AgentOrchestrator.Runtime.<rid>` for each RID in the table.
- SO: `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for each RID in the table.

Beta GitHub fallback aliases for the runtime family:

- AO: [win-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.win-x64.latest.nupkg), [win-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.win-arm64.latest.nupkg), [linux-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-x64.latest.nupkg), [linux-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-arm64.latest.nupkg), [linux-musl-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64.latest.nupkg), [linux-musl-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64.latest.nupkg), [osx-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-x64.latest.nupkg), [osx-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-arm64.latest.nupkg).
- SO: [win-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.win-x64.latest.nupkg), [win-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.win-arm64.latest.nupkg), [linux-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-x64.latest.nupkg), [linux-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-arm64.latest.nupkg), [linux-musl-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64.latest.nupkg), [linux-musl-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64.latest.nupkg), [osx-x64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-x64.latest.nupkg), [osx-arm64](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-arm64.latest.nupkg).

## Expected Stable Release Addresses

The development documentation predeclares the stable addresses that become active after the same package batch is published from `main`:

- Stable fallback release page: <https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-stable-latest>
- Exact-version asset: `https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.<exact-version>.nupkg`
- Durable latest alias: `https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.latest.nupkg`

Use the NuGet.org V3 flat-container URL for the bound exact version. Lowercase the package id and normalize the exact version:

```text
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg.sha512
```

For manual package-page acquisition, the NuGet.org V2 exact-version URL remains:

```text
https://www.nuget.org/api/v2/package/<PackageId>/<exact-version>
```

The official GitHub fallback for the `beta` channel uses the same exact product, version, and RID package and must pass the same validation:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.<exact-version>.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.latest.nupkg
```

Follow [Platform Detection Steps](docs/en/reference/runtime/platform-detection.md) for host preflight, RID selection, SHA-512 verification, ZIP safety, cache isolation, and launch descriptor continuity. Stable/beta publication must atomically add these 16 packages to the existing 4-package release matrix, for 20 packages total.

## Node.js

| Role | Package | Beta acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `@techne-loom/abstractions` | _Not implemented yet_ | _TBD_ |
| Common | `@techne-loom/common` | _Not implemented yet_ | _TBD_ |
| Plan execution runtime | `@techne-loom/agent-orchestrator` | _Not implemented yet_ | _TBD_ |
| Skill execution runtime | `@techne-loom/skill-orchestrator` | _Not implemented yet_ | _TBD_ |

## Python

| Role | Package | Beta acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `techne-loom-abstractions` | _Not implemented yet_ | _TBD_ |
| Common | `techne-loom-common` | _Not implemented yet_ | _TBD_ |
| Plan execution runtime | `techne-loom-agent-orchestrator` | _Not implemented yet_ | _TBD_ |
| Skill execution runtime | `techne-loom-skill-orchestrator` | _Not implemented yet_ | _TBD_ |

## Required Reading Before Running Skills

- `/loom-plan-execution`: if direct CLI/manual acquisition or the governed runtime version already resolves to `beta`, read `packages.beta.md` first, then run `dotnet ao.dll --guide` from the Loom Agent Execution Orchestrator runtime bundle
- `/loom-skill-enhancement`: if direct CLI/manual acquisition or the governed runtime version already resolves to `beta`, read `packages.beta.md` first, then run `dotnet so.dll --guide`
