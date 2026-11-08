# Beta Runtime Package Reference

[Published beta package index](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)

<!-- package-version-block:start -->
- The current latest published beta runtime version is `0.5.2-beta`.
<!-- package-version-block:end -->

<!-- active-runtime-set:start -->
The unified release closure contains eight self-contained Loom RID runtime packages plus one no-RID `Techne.Loom.Abstractions` contract SDK at the same exact version and channel (9 packages total). This skill routes Skill capability commands through `loom skill`.

| RID | Loom runtime package |
| --- | --- |
| `win-x64` | `Techne.Loom.Runtime.win-x64` |
| `win-arm64` | `Techne.Loom.Runtime.win-arm64` |
| `linux-x64` | `Techne.Loom.Runtime.linux-x64` |
| `linux-arm64` | `Techne.Loom.Runtime.linux-arm64` |
| `linux-musl-x64` | `Techne.Loom.Runtime.linux-musl-x64` |
| `linux-musl-arm64` | `Techne.Loom.Runtime.linux-musl-arm64` |
| `osx-x64` | `Techne.Loom.Runtime.osx-x64` |
| `osx-arm64` | `Techne.Loom.Runtime.osx-arm64` |
<!-- active-runtime-set:end -->

Detect exactly one host RID from OS, architecture, and Linux libc. First reuse a fully validated immutable generation from the shared current-user OS-temp AO/SO cache, keyed by exact package ID/version/RID/full `.nupkg` SHA-512; on a miss, check the standard NuGet cache or acquire only the exact locked package. For NuGet, validate bytes only against exact registration `catalogEntry.packageHash`, without a separate NuGet sidecar. For GitHub fallback, follow the exact `.nupkg.sha512` link in the selected package index and verify it against GitHub package bytes; keep authorities source-specific.

<!-- runtime-apphost-invocation:start -->
Before extraction, validate package ID, exact version, RID, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide assets. Extract only after validation. Run `loom.exe --guide` on Windows or `loom --guide` on Unix as the first runtime operation. After the guide is readable, route this skill's commands through `loom skill`.
<!-- runtime-apphost-invocation:end -->

Use the same extracted apphost for schema/demo, compile, run, and resume. No installed .NET host, DLL/FDD mode, runtime resolver, launch descriptor, or fixed bootstrap script is required. The shared current-user `<OS-temp>/TechneLoom/runtime-cache/` is the allowed generation cache; do not add a cache lock. Failed integrity, extraction, startup, or guide checks fail closed.

<!-- abstractions-sdk-version:start -->
## Shared Abstractions SDK (no RID)
Install the version-matched SDK independently of runtime RID selection:
`dotnet add package Techne.Loom.Abstractions --version 0.5.2-beta`
<!-- abstractions-sdk-version:end -->














