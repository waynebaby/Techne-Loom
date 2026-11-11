# Released Runtime Package Index
[中文](packages.released.zh-CN.md) | [Beta](packages.beta.md)

This index covers the published stable runtime package set. Each RID-specific `Techne.Loom.Runtime.<rid>` package contains one self-contained `loom` apphost for both Agent and Skill module commands; the no-RID `Techne.Loom.Abstractions` SDK is published at the same version. Source projects remain buildable in the repository but are not runtime packages.

## Active Release Set

<!-- active-runtime-set:start -->
The unified Loom release set contains eight self-contained RID runtime packages plus one same-version, no-RID `Techne.Loom.Abstractions` contract SDK (9 packages total). Select one runtime package using the host OS, architecture, and Linux libc.

| RID | Loom runtime package | Apphost |
| --- | --- | --- |
| `win-x64` | `Techne.Loom.Runtime.win-x64` | `loom.exe` |
| `win-arm64` | `Techne.Loom.Runtime.win-arm64` | `loom.exe` |
| `linux-x64` | `Techne.Loom.Runtime.linux-x64` | `loom` |
| `linux-arm64` | `Techne.Loom.Runtime.linux-arm64` | `loom` |
| `linux-musl-x64` | `Techne.Loom.Runtime.linux-musl-x64` | `loom` |
| `linux-musl-arm64` | `Techne.Loom.Runtime.linux-musl-arm64` | `loom` |
| `osx-x64` | `Techne.Loom.Runtime.osx-x64` | `loom` |
| `osx-arm64` | `Techne.Loom.Runtime.osx-arm64` | `loom` |
<!-- active-runtime-set:end -->

## Version

<!-- package-version-block:start -->
- The latest published `released` package version is `0.5.4` for all eight Loom RID runtime packages and the no-RID Abstractions SDK.
<!-- package-version-block:end -->

- Both channels share one monotonic version line, beginning at `0.5.0-beta`.

Use the exact version above. Do not request `latest`, a range, or another RID.

## Install Commands

<!-- package-dotnet-block:start -->
| Product | RID | Runtime package | NuGet acquisition | GitHub fallback |
| --- | --- | --- | --- | --- |
| Loom Runtime | `win-x64` | `Techne.Loom.Runtime.win-x64` | `dotnet add package Techne.Loom.Runtime.win-x64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.win-x64.0.5.4.nupkg) |
| Loom Runtime | `win-arm64` | `Techne.Loom.Runtime.win-arm64` | `dotnet add package Techne.Loom.Runtime.win-arm64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.win-arm64.0.5.4.nupkg) |
| Loom Runtime | `linux-x64` | `Techne.Loom.Runtime.linux-x64` | `dotnet add package Techne.Loom.Runtime.linux-x64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.linux-x64.0.5.4.nupkg) |
| Loom Runtime | `linux-arm64` | `Techne.Loom.Runtime.linux-arm64` | `dotnet add package Techne.Loom.Runtime.linux-arm64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.linux-arm64.0.5.4.nupkg) |
| Loom Runtime | `linux-musl-x64` | `Techne.Loom.Runtime.linux-musl-x64` | `dotnet add package Techne.Loom.Runtime.linux-musl-x64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.linux-musl-x64.0.5.4.nupkg) |
| Loom Runtime | `linux-musl-arm64` | `Techne.Loom.Runtime.linux-musl-arm64` | `dotnet add package Techne.Loom.Runtime.linux-musl-arm64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.linux-musl-arm64.0.5.4.nupkg) |
| Loom Runtime | `osx-x64` | `Techne.Loom.Runtime.osx-x64` | `dotnet add package Techne.Loom.Runtime.osx-x64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.osx-x64.0.5.4.nupkg) |
| Loom Runtime | `osx-arm64` | `Techne.Loom.Runtime.osx-arm64` | `dotnet add package Techne.Loom.Runtime.osx-arm64 --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Runtime.osx-arm64.0.5.4.nupkg) |
| SDK | `—` | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.5.4` | [exact `.nupkg` + `.sha512`](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Abstractions.0.5.4.nupkg) |
<!-- package-dotnet-block:end -->
















## Acquire And Verify

Detect one supported RID from the host OS, architecture, and Linux libc, then acquire only the exact `Techne.Loom.Runtime.<rid>` package for that RID. The package provides the single `loom` apphost; route Agent commands through `loom agent` and Skill commands through `loom skill`. The Abstractions SDK is a separate no-RID package at the same exact version.

For NuGet packages, compare the package bytes only with `packageHash` from that exact version registration `catalogEntry`; do not require a separate NuGet `.sha512` sidecar. For GitHub fallback, verify the package bytes against the exact-version `.nupkg.sha512` sidecar linked below. Keep the two checksum authorities source-specific.

NuGet exact package URL:

```text
https://www.nuget.org/api/v2/package/<PackageId>/0.5.4
```

GitHub exact package and checksum use the same filename under the stable release:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.0.5.2.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/<PackageId>.0.5.2.nupkg.sha512
```

Before extraction, verify package identity, exact version, RID, nuspec, `tools/<rid>/runtime.json`, archive paths and sizes, apphost, and English guide files. Reject mismatches and unsafe ZIP entries.

## First Command

Run the exact RID `loom` apphost directly. Its `--guide` output identifies the version-matched documentation and is the first runtime operation.

```powershell
.\loom.exe --guide
.\loom.exe agent --help
.\loom.exe skill --help
```

```bash
./loom --guide
./loom agent --help
./loom skill --help
```

Node.js and Python remain reserved source roots; no runnable implementation is committed yet.
