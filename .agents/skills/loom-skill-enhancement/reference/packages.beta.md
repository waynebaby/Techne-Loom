# Beta Runtime Package Reference

[Published beta package index](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)

<!-- package-version-block:start -->
- The current latest published beta runtime version is `0.4.11-beta`.
<!-- package-version-block:end -->

The coordinated release closure is 16 self-contained AO/SO runtime packages plus one no-RID `Techne.Loom.Abstractions` contract SDK at the same exact version and channel (17 packages total). Older runtime-only snapshots may predate the SDK; use the live package index to confirm its exact published version.

| RID | AO package | SO package |
| --- | --- | --- |
| `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `Techne.Loom.SkillOrchestrator.Runtime.win-x64` |
| `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.win-arm64` |
| `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-x64` |
| `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-arm64` |
| `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-x64` |
| `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.linux-musl-arm64` |
| `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-x64` |
| `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `Techne.Loom.SkillOrchestrator.Runtime.osx-arm64` |

Detect exactly one host RID from OS, architecture, and Linux libc. First reuse a fully validated immutable generation from the shared current-user OS-temp AO/SO cache, keyed by exact package ID/version/RID/full `.nupkg` SHA-512; on a miss, check the standard NuGet cache or acquire only the exact locked package. Validate downloaded NuGet bytes against exact registration `catalogEntry.packageHash`; same-version GitHub fallback requires a valid `.sha512` sidecar.

Before extraction, validate package ID, exact version, RID, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide assets. Extract only after all checks pass. Directly run `so.exe --guide` on Windows or `so --guide` on Unix as the first runtime operation. Validate the returned version and readable contained guide paths.

Use the same extracted apphost for schema/demo, compile, run, and resume. No installed .NET host, DLL/FDD mode, runtime resolver, launch descriptor, or fixed bootstrap script is required. The shared current-user `<OS-temp>/TechneLoom/runtime-cache/` is the allowed generation cache; do not add a cache lock. Failed integrity, extraction, startup, or guide checks fail closed.

<!-- abstractions-sdk-version:start -->
## Shared Abstractions SDK (no RID)
Install the version-matched SDK independently of AO/SO runtime selection:
`dotnet add package Techne.Loom.Abstractions --version 0.4.11-beta`
<!-- abstractions-sdk-version:end -->




