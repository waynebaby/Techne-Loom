# Beta Runtime Package Reference

[Published beta package index](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)

<!-- package-version-block:start -->
- The current latest published beta runtime version is `0.5.4-beta`.
<!-- package-version-block:end -->

<!-- active-runtime-set:start -->
The unified release closure contains eight self-contained Loom RID runtime packages plus one no-RID `Techne.Loom.Abstractions` contract SDK at the same exact version and channel (9 packages total). This skill routes Agent capability commands through `loom agent`.

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

Detect OS, architecture, and Linux libc and select one RID. First reuse a fully validated immutable generation from the shared current-user OS-temp Loom runtime cache, keyed by exact package ID/version/RID/full `.nupkg` SHA-512. For acquisition, follow the [L0-L6 policy in the plan-execution skill](../SKILL.md#dependency-acquisition-fallback-policy): use managed sources first, and generic CLIs only after L0-L5 are exhausted with explicit per-operation approval. PowerShell web commands, `curl`, `gh`, `wget`, BITS and equivalent download CLIs all share L6; a direct in-process .NET `HttpClient` call from PowerShell is L3; PowerShell web cmdlets remain L6. Never install a runtime/downloader. After a failed request, do not automatically switch transports; blocked or denied sources are stops. For NuGet, compare bytes only with exact registration `catalogEntry.packageHash` (no separate NuGet sidecar). For GitHub fallback, use the exact `.nupkg.sha512` link from the selected package index and compare it with GitHub package bytes; keep authorities source-specific.

<!-- runtime-apphost-invocation:start -->
Before extraction, validate package ID, exact version, RID, nuspec, `runtime.json`, archive paths and sizes, apphost, and English guide assets. Extract only after validation. Run `loom.exe --guide` on Windows or `loom --guide` on Unix as the first runtime operation. After the guide is readable, route this skill's commands through `loom agent`.
<!-- runtime-apphost-invocation:end -->

Use the same extracted apphost for schema/demo, compile, run, and resume. Do not probe for an installed .NET host, use DLL/FDD execution, switch RIDs, or use a floating version. The shared current-user `<OS-temp>/TechneLoom/runtime-cache/` is permitted; no resolver descriptor, fixed bootstrap script, or cache lock is required.

<!-- abstractions-sdk-version:start -->
## Shared Abstractions SDK (no RID)
Install the version-matched SDK independently of runtime RID selection:
`dotnet add package Techne.Loom.Abstractions --version 0.5.4-beta`
<!-- abstractions-sdk-version:end -->
