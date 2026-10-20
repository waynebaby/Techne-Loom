# XO Ask Runtime Dependency

`/loom-ask-user` requires an exact published AO or SO self-contained runtime binary, but it does not require a workflow or workflow-owned wait. In this documentation, `XO Ask` means the shared ask capability exposed by either existing product apphost; XO is not a third product or package family.

## Version and Product Selection

- The skill's refreshed AO/SO version block is the exact shared release-set baseline. The AO and SO runtime packages in that release closure use the same version.
- Detect the host OS, architecture, and Linux libc, then select exactly one supported RID: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, or `osx-arm64`.
- Resolve the current user's OS temporary directory through a host-native API. Share `<OS-temp>/TechneLoom/runtime-cache/` with AO and SO skills; the exact skill version block and detected RID remain authoritative.
- Reuse only a verified AO or SO generation at that exact version and RID. Its immutable identity is package ID, exact version, RID, and full verified `.nupkg` SHA-512 hex; four hex characters are only an initial directory abbreviation and must extend on collision. Recompute the package hash and recheck identity, nuspec, manifest, archive safety, apphost, and guide files before reuse.
- If neither product has a valid generation, check the standard NuGet global-packages cache, then acquire the exact AO package first. If both AO and SO are valid at the same version and RID, prefer AO. Never float to `latest`, switch versions/RIDs, or substitute a local/test build.
- The selected apphost must expose the standalone `ask` entry point before the skill can start an ask. Confirm this from the fresh package's help/guide instead of assuming that an `AskUser` workflow node implies standalone support.

## Verify and Start

1. Before extraction, enforce package-size bounds and verify package ID, exact version, RID, nuspec identity, SHA-512, runtime manifest, safe archive paths, and apphost entry point. NuGet package bytes must match the exact registration `catalogEntry.packageHash`; a same-version GitHub fallback requires its matching `.sha512` sidecar.
2. Stage and validate any new or repaired generation before atomic publication. Never overwrite a different valid package or mutate files used by a running apphost. Do not add a cache lock, second resolver, fixed checked-in restore script, or launch descriptor.
3. Extract only after all checks pass and invoke the matching self-contained apphost directly.
4. The first runtime operation is a fresh `ao --guide` or `so --guide`. Verify the exact version and readable, contained guide path; then inspect the same apphost's help for the standalone ask command.
5. Write the complete contract to a disk file, then invoke `ao ask start --contract-file <path>` or `so ask start --contract-file <path>`. Open only the returned host-approved loopback or explicitly configured browser route; do not infer it from forwarded headers or expose it through an automatic tunnel.
6. After submission, query `ao ask result --ask-id <id>` or `so ask result --ask-id <id>` by the returned ask ID. Return status, typed answers, and receipt without workflow projection or resume.

## NuGet SHA-512 Troubleshooting

- Confirm the exact package ID and version, fetch `https://api.nuget.org/v3/registration5-semver1/<lowercase-package-id>/<version>.json`, follow the selected registration item's `catalogEntry` link, and read `packageHash` from that exact catalog record. The value is Base64-encoded SHA-512; hash the `.nupkg` bytes, not extracted files.
- In PowerShell, convert the expected value and compare it with the SHA-512 of the exact package file:

```powershell
$expectedHex = [BitConverter]::ToString([Convert]::FromBase64String($packageHash)).Replace('-', '')
$actualHex = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash
if ($actualHex -ine $expectedHex) { throw 'The package does not match the NuGet registration SHA-512.' }
```

- On mismatch or unavailable registration metadata, do not extract or publish the package. Preserve the exact package/version/RID, source URL, expected and actual hashes, and failure reason. Use only the same-version GitHub asset with a matching `.sha512` sidecar as fallback; otherwise stop.

## Relationship to Workflow Nodes

The workflow `AskUser` node is a separate consumer of XO Ask. Its adapter supplies workflow-specific identity and answer projection (`contextPath`, `requiredInputs`, SO `validation.declaredUserOwnedFields`) and asks the owning product runtime to resume its canonical workflow copy. None of those fields or operations are required by `/loom-ask-user` standalone sessions.

## Current Release Gap

The published `0.3.334-beta` SO apphost help has no standalone `ask` entry. The AO apphost from the same runtime source line also has no such entry. Therefore that package line cannot currently complete workflow-free skill use even though its workflow-owned AskUser Web UI works. Do not create a workflow wait as an undocumented substitute; report the gap until a published AO/SO package exposes the standalone command.

Do not add a second resolver, a skill-owned package lock, a per-skill/workspace cache, or cache state outside the shared OS-temp root. Package paths and extraction locations are transient for the current run.
