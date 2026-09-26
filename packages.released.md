# Released Runtime Package Index

[中文](packages.released.zh-CN.md) | [Beta](packages.beta.md)

This index covers the published stable runtime packages. AO and SO publish only self-contained product+RID packages; source projects remain available in the repository but are not NuGet runtime packages.

## Active Release Set

The active stable closure is exactly sixteen packages: eight AO RIDs and eight SO RIDs. Select one row using the host OS, architecture, and Linux libc.

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
- The current latest published stable package version is `0.3.326`.
- Stable publishing resolves the next numeric version after the highest published stable or beta package; `main` emits the numeric version without a prerelease suffix.
<!-- package-version-block:end -->



Use the exact version above. Do not request `latest`, a range, or another RID.

## Install Commands

<!-- package-dotnet-block:start -->
| Product | RID | Runtime package | NuGet acquisition | GitHub fallback |
| --- | --- | --- | --- | --- |
| AO | `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.win-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.win-x64.0.3.326.nupkg) |
| AO | `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.win-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.win-arm64.0.3.326.nupkg) |
| AO | `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-x64.0.3.326.nupkg) |
| AO | `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-arm64.0.3.326.nupkg) |
| AO | `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64.0.3.326.nupkg) |
| AO | `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64.0.3.326.nupkg) |
| AO | `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.osx-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-x64.0.3.326.nupkg) |
| AO | `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `dotnet add package Techne.Loom.AgentOrchestrator.Runtime.osx-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.Runtime.osx-arm64.0.3.326.nupkg) |
| SO | `win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.win-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.win-x64.0.3.326.nupkg) |
| SO | `win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.win-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.win-arm64.0.3.326.nupkg) |
| SO | `linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-x64.0.3.326.nupkg) |
| SO | `linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-arm64.0.3.326.nupkg) |
| SO | `linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64.0.3.326.nupkg) |
| SO | `linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64.0.3.326.nupkg) |
| SO | `osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.osx-x64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-x64.0.3.326.nupkg) |
| SO | `osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` | `dotnet add package Techne.Loom.SkillOrchestrator.Runtime.osx-arm64 --version 0.3.326` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.Runtime.osx-arm64.0.3.326.nupkg) |
<!-- package-dotnet-block:end -->



## Acquire And Verify

The host agent detects one supported RID, then acquires only that product's exact package. Prefer a valid entry in the standard NuGet global-packages cache. Otherwise verify downloaded NuGet bytes against the exact registration `catalogEntry.packageHash`. GitHub fallback is permitted only for the same exact package/version and a valid `.sha512` sidecar.

NuGet exact package URL:

```text
https://www.nuget.org/api/v2/package/<PackageId>/0.3.326
```

GitHub exact package and checksum use the same filename under the stable release:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.0.3.321.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.0.3.321.nupkg.sha512
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
