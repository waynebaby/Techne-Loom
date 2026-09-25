# Beta Runtime Package Reference

[Published beta package index](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)

This offline snapshot is bound to beta `0.3.320-beta`. The active NuGet closure is exactly sixteen self-contained packages: the AO and SO apphosts for eight supported RIDs. Core libraries are source projects, not active runtime packages.

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

Detect exactly one host RID from OS, architecture, and Linux libc. Reuse a valid exact package in the standard NuGet cache or acquire the exact locked package. Validate NuGet bytes against exact registration `catalogEntry.packageHash`; same-version GitHub fallback requires a valid `.sha512` sidecar.

Before extraction, validate package ID, exact version, RID, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide assets. Extract only after all checks pass. Directly run `so.exe --guide` on Windows or `so --guide` on Unix as the first runtime operation. Validate the returned version and readable contained guide paths.

Use the same extracted apphost for schema/demo, compile, run, and resume. No installed .NET host, DLL/FDD mode, runtime resolver, launch descriptor, fixed bootstrap script, or Loom-specific cache is required. Failed integrity, extraction, startup, or guide checks fail closed.
