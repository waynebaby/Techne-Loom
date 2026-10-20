# Beta Runtime Package Reference

[Published beta package index](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)

<!-- package-version-block:start -->
- The current latest published beta runtime version is `0.4.3-beta`.
<!-- package-version-block:end -->

The active NuGet release closure is exactly sixteen self-contained runtime packages: eight AO and eight SO product+RID packages. Core libraries are source projects, not active runtime packages.

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
