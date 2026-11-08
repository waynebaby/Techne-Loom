# Platform Detection Steps

[中文](../../../zh-cn/reference/runtime/platform-detection.md)

This contract applies to the unified Loom runtime and the skills that acquire it. Each self-contained runtime package contains the same `loom` apphost for exactly one supported RID.

## Version And Package Scope

Use the exact version from the owning skill's checked-in lock or version block. Direct callers choose a channel from the package indexes. Never resolve `latest`, use a version range, or substitute a neighboring version.

The coordinated NuGet release set contains eight `Techne.Loom.Runtime.<rid>` packages, one for each supported RID, plus one no-RID `Techne.Loom.Abstractions` contract SDK at the same exact version and channel (nine packages total). AO and SO remain independent implementation modules behind `loom agent` and `loom skill`; neither has a separate runtime package family.

## Detect One RID

The host agent detects OS, architecture, and Linux libc, then selects exactly one supported RID:

```text
win-x64
win-arm64
linux-x64
linux-arm64
linux-musl-x64
linux-musl-arm64
osx-x64
osx-arm64
```

Do not guess when detection is ambiguous. Do not cross OS, architecture, or libc boundaries. Fail with a concrete diagnostic if no supported RID matches.

## Acquire The Exact Package

Acquire only `Techne.Loom.Runtime.<rid>` for the selected RID. First reuse a validated generation in the shared current-user OS-temp cache at `<OS-temp>/TechneLoom/runtime-cache/`. A generation is identified by the exact package ID, version, RID, and full verified `.nupkg` SHA-512; recompute the hash and verify package identity before reuse.

On a cache miss, check the standard NuGet global-packages cache for a validated exact package before downloading. Stage and validate new or repaired generations, then publish atomically; never overwrite a different valid or in-use generation.

A downloaded NuGet package must match the Base64 `packageHash` from the exact registration catalog entry. A same-version GitHub Release asset is allowed only as a fallback and only when its matching `.nupkg.sha512` sidecar verifies. Use the exact filename; never float the version. Do not add a fixed bootstrap script, resolver, descriptor file, cache lock, or alternate runtime mode.

### NuGet SHA-512 Troubleshooting

For the exact package ID and version, fetch `https://api.nuget.org/v3/registration5-semver1/<lowercase-package-id>/<version>.json`, follow the selected item's `catalogEntry` link, and read its `packageHash`. Hash the raw `.nupkg` bytes, not extracted files.

```powershell
$expectedHex = [BitConverter]::ToString([Convert]::FromBase64String($packageHash)).Replace('-', '')
$actualHex = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash
if ($actualHex -ine $expectedHex) { throw 'The package does not match the NuGet registration SHA-512.' }
```

On mismatch or unavailable registration metadata, do not extract or publish the package. Preserve the expected and actual hashes and failure evidence. Use only a same-version GitHub asset with a matching `.sha512` sidecar as fallback; otherwise stop.

## Verify Before Extraction

Treat `.nupkg` as ZIP content. Before extraction, verify:

- package ID, exact version, RID, SHA-512, and root nuspec identity
- `tools/<rid>/runtime.json` product `loom`, version, RID, `loom` apphost, docs root, and guide path
- the expected `loom.exe` (Windows) or `loom` (Unix-like) apphost and complete English guide tree
- archive size, entry sizes, duplicate paths, path traversal, and unexpected files

Reject any mismatch or unsafe archive. Extract the verified package to an external per-run directory; set executable permission on Unix when required. `runtime.json` records package identity and is not a launch descriptor.

## Guide First

Directly run `loom.exe --guide` on Windows or `loom --guide` on Unix after extraction. This is the first runtime operation; no installed .NET host is required.

Accept only a successful JSON result with the exact expected version, absolute `docs_root` and `guide_path`, a guide path contained by the docs root, and readable files. Failed stderr is not guide evidence. If acquisition, verification, extraction, startup, or guide validation fails, stop and retain failed evidence.

After the guide gate, use the same extracted apphost for `loom agent` and `loom skill` schema/demo generation, compile, run, and resume. Standalone questions use only top-level `loom ask`. A command failure after apphost startup is a command failure, not a package fallback. Do not switch RID, version, workflow copy, or package source after dispatch.
