# Released Runtime Package Reference

[Published stable package index](https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md)

This offline snapshot is bound to stable `0.3.321`. The coordinated release closure is 16 self-contained AO/SO product+RID runtime packages plus one no-RID `Techne.Loom.Abstractions` contract SDK at the same exact version and channel (17 packages total). Older runtime-only snapshots may predate the SDK; use the live package index to confirm its exact published version.

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

Detect OS, architecture, and Linux libc and select one RID. First reuse a fully validated immutable generation from the shared current-user OS-temp AO/SO cache, keyed by exact package ID/version/RID/full `.nupkg` SHA-512; on a miss, check the standard NuGet cache or download only the exact locked package. Verify NuGet bytes against exact registration `catalogEntry.packageHash`; the same-version GitHub Release fallback requires a valid `.sha512` sidecar.

Before extraction, verify package ID, version, RID, nuspec, manifest, apphost, docs, archive paths, and size limits. Extract only after validation. Run `ao.exe --guide` or `so.exe --guide` on Windows; run `ao --guide` or `so --guide` on Unix. The fresh guide must be readable before any other runtime operation.

Use the same extracted apphost for schema/demo, compile, run, and resume. Do not probe for an installed .NET host, use DLL/FDD execution, switch RIDs, or use a floating version. The shared current-user `<OS-temp>/TechneLoom/runtime-cache/` is permitted; no resolver descriptor, fixed bootstrap script, or cache lock is required.
