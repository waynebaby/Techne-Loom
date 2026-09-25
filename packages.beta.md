# Beta Runtime Package Index

[中文](packages.beta.zh-CN.md) | [Released](packages.released.md)

This index covers the published beta runtime packages. AO and SO publish only self-contained product+RID packages; source projects remain available in the repository but are not NuGet runtime packages.

## Active Release Set

The active beta closure is exactly sixteen packages: eight AO RIDs and eight SO RIDs. Select one row using the host OS, architecture, and Linux libc.

| RID | AO package | SO package | Apphosts |
| --- | --- | --- | --- |
| `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `ao.exe`, `so.exe` |
| `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `ao.exe`, `so.exe` |
| `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `ao`, `so` |
| `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `ao`, `so` |
| `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `ao`, `so` |
| `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `ao`, `so` |
| `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `ao`, `so` |
| `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `ao`, `so` |

## Version

<!-- package-version-block:start -->
- The current latest published beta runtime version is `0.3.322-beta`.
- Development publishing selects the next numeric patch above the highest active runtime package version and the separate retired-core high-water, then appends `-beta`.
<!-- package-version-block:end -->

Use the exact version above. Do not request `latest`, a range, or another RID.

## Install Commands

<!-- package-dotnet-block:start -->
| Product | RID | Runtime package | NuGet acquisition | GitHub fallback |
| --- | --- | --- | --- | --- |
| AO | `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.win-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.win-x64.0.3.322-beta.nupkg) |
| AO | `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.win-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.win-arm64.0.3.322-beta.nupkg) |
| AO | `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-x64.0.3.322-beta.nupkg) |
| AO | `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-arm64.0.3.322-beta.nupkg) |
| AO | `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64.0.3.322-beta.nupkg) |
| AO | `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64.0.3.322-beta.nupkg) |
| AO | `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.osx-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-x64.0.3.322-beta.nupkg) |
| AO | `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.osx-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-arm64.0.3.322-beta.nupkg) |
| SO | `win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.win-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.win-x64.0.3.322-beta.nupkg) |
| SO | `win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.win-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.win-arm64.0.3.322-beta.nupkg) |
| SO | `linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-x64.0.3.322-beta.nupkg) |
| SO | `linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-arm64.0.3.322-beta.nupkg) |
| SO | `linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64.0.3.322-beta.nupkg) |
| SO | `linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64.0.3.322-beta.nupkg) |
| SO | `osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.osx-x64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-x64.0.3.322-beta.nupkg) |
| SO | `osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.osx-arm64 --version 0.3.322-beta` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-arm64.0.3.322-beta.nupkg) |
<!-- package-dotnet-block:end -->

## Acquire And Verify

The host agent detects one supported RID, then acquires only that product's exact package. Prefer a valid entry in the standard NuGet global-packages cache. Otherwise verify downloaded NuGet bytes against the exact registration `catalogEntry.packageHash`. GitHub fallback is permitted only for the same exact package/version and a valid `.sha512` sidecar.

NuGet exact package URL:

```text
https://www.nuget.org/api/v2/package/<PackageId>/0.3.322-beta
```

GitHub exact package and checksum use the same filename under the beta release:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.0.3.322-beta.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/<PackageId>.0.3.322-beta.nupkg.sha512
```

Before extraction, verify package ID, exact version, RID, SHA-512, nuspec, `tools/<rid>/runtime.json`, archive paths and sizes, apphost, and English guide files. Reject mismatches and unsafe ZIP entries. Extract to an external per-run directory.

## First Command

After extraction, run the apphost directly with `--guide` as the first runtime operation. Verify its version and readable guide paths, then use the same apphost for schema/demo, compile, run, and resume.

```powershell
.\so.exe --guide
.\ao.exe --guide
```

On Unix, invoke `so --guide` or `ao --guide` without `.exe`. No installed .NET host, framework-dependent DLL package, runtime resolver, launch descriptor, or fixed bootstrap script is required.

The four retired core NuGet package IDs are outside the active release set. Their published versions remain only as a separate monotonic version high-water input.

## Other Ecosystems

Node.js and Python are reserved source roots; they have no runnable packages in this index.
