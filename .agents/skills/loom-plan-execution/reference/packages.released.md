# Local Offline Package Index (Released)

This file is the offline package authority for `/loom-plan-execution` when the caller selects the released channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or version policy. Use the rules and versions in this file.

The coordinated release closure is 16 self-contained AO/SO product+RID runtime packages plus one no-RID `Techne.Loom.Abstractions` contract SDK at the same exact version and channel (17 packages total).

## Released Channel Rule

- Released channel means stable packages only.
- For deterministic package-channel execution, restore one exact stable version for the full AO runtime bundle.
- For this offline snapshot, the current latest released version is `0.4.10`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Full Runtime Bundle Rule

Runtime selection uses an automatic two-way resolver. Before package lookup, a usable .NET 9+ host selects one exact-version DLL/dependency/Roslyn closure; without one, the resolver selects one exact-RID self-contained package. Explicit mode selection is allowed, and each resolution acquires only its locked package closure:

- `Techne.Loom.AgentOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All framework members must use the exact released snapshot version shown above. Do not run from a partial extraction root.

Self-contained packages contain the direct `ao` executable under `tools/<rid>/` and do not require a preinstalled .NET runtime, but they still depend on the target OS and ABI. Legacy mode stages the full three-package closure above when explicitly selected.

The complete AgentOrchestrator runtime family is:

| RID | Runtime package | Entry point |
| --- | --- | --- |
| `win-x64` | `Techne.Loom.AgentOrchestrator.Runtime.win-x64` | `tools/win-x64/ao.exe` |
| `win-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.win-arm64` | `tools/win-arm64/ao.exe` |
| `linux-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-x64` | `tools/linux-x64/ao` |
| `linux-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-arm64` | `tools/linux-arm64/ao` |
| `linux-musl-x64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-x64` | `tools/linux-musl-x64/ao` |
| `linux-musl-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.linux-musl-arm64` | `tools/linux-musl-arm64/ao` |
| `osx-x64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-x64` | `tools/osx-x64/ao` |
| `osx-arm64` | `Techne.Loom.AgentOrchestrator.Runtime.osx-arm64` | `tools/osx-arm64/ao` |

Detect OS, architecture, and Linux libc and select one RID. First reuse a fully validated immutable generation from the shared current-user OS-temp AO/SO cache, keyed by exact package ID/version/RID/full `.nupkg` SHA-512. For acquisition, follow the [L0-L6 policy in the plan-execution skill](../SKILL.md#dependency-acquisition-fallback-policy): use managed sources first, and generic CLIs only after L0-L5 are exhausted with explicit per-operation approval. PowerShell web commands, `curl`, `gh`, `wget`, BITS and equivalent download CLIs all share L6; A direct in-process .NET `HttpClient` call from PowerShell is L3; PowerShell `Invoke-WebRequest`/`Invoke-RestMethod` remain L6. Never install a runtime/downloader. After a failed request, do not automatically switch transport to repeat the same artifact/source; a nonblocked source may be retried only through explicit L6 approval after L0-L5 are exhausted. A blocked or denied source is a stop across every transport. Verify NuGet bytes against exact registration `catalogEntry.packageHash`; use a same-version GitHub Release only as an independently configured and approved source with a valid `.sha512` sidecar.


