# XO Ask Runtime Dependency

`/loom-ask-user` requires an exact published AO or SO self-contained runtime binary, but it does not require a workflow or workflow-owned wait. In this documentation, `XO Ask` means the shared ask capability exposed by either existing product apphost; XO is not a third product or package family.

## Version and Product Selection

- The skill's refreshed AO/SO version block is the exact shared release-set baseline. The AO and SO runtime packages in that release closure use the same version.
- Detect the host OS, architecture, and Linux libc, then select exactly one supported RID: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, or `osx-arm64`.
- Resolve the current user's OS temporary directory through a host-native API. Share `<OS-temp>/TechneLoom/runtime-cache/` with AO and SO skills; the exact skill version block and detected RID remain authoritative.
- Reuse only a verified AO or SO generation at that exact version and RID. Its immutable identity is package ID, exact version, RID, and full verified `.nupkg` SHA-512 hex; four hex characters are only an initial directory abbreviation and must extend on collision. Recheck package hash, identity, nuspec, manifest, archive safety, apphost, and guide files before reuse.
- If neither product has a valid generation, check the standard NuGet global-packages cache as L0. If both products are valid at the same version and RID, prefer AO; otherwise acquire the exact AO package first through the acquisition order below. Never change versions or RIDs, resolve a floating `latest`, or substitute a local/test build as release authority.
- The selected apphost must expose the standalone `ask` entry point before the skill can start an ask. Confirm this from the fresh package's help/guide instead of assuming that an `AskUser` workflow node implies standalone support.

## Dependency Acquisition Fallback Policy

Use the first available, integrity-checked capability for the exact locked runtime package. Never install a runtime, downloader, or package client solely to acquire it.

| Tier | Capability | Constraints |
|---|---|---|
| L0 | Existing local artifact or cache | Check the shared runtime cache, standard NuGet cache, workspace, configured package cache, and known local paths. Reuse only after exact ID/version/RID and required integrity are verified. |
| L1 | Agent/Host-controlled fetch | Use a controlled fetch for the exact package from its configured primary source. Do not start a downloader subprocess. |
| L2 | Existing `nuget.exe` | Use only if already installed and executable, and only for the exact NuGet package. Do not install `nuget.exe`, .NET Framework, or a .NET SDK. |
| L3 | Existing runtime's native HTTP API | Use an already-installed runtime's in-process HTTP API, such as Node `fetch`, Python `urllib`, or .NET `HttpClient`. Do not invoke a downloader through a subprocess or install a runtime. |
| L4 | Known GitHub MCP release asset | Use only a preconfigured, approved repository, exact release tag and asset, and matching `.sha512` sidecar. Skip this tier if the approved capability is not already exposed; do not register or inspect MCP before the fresh `--guide`. |
| L5 | Other approved Host/MCP source | Use only an already-available, configured source and exact artifact with required integrity evidence. Do not add a required MCP startup gate. |
| L6 | Generic network CLI | Last resort after L0-L5 are unavailable. Require explicit per-operation approval naming the tool, source/host, exact artifact/version, destination, and purpose. |

PowerShell `Invoke-WebRequest`/`Invoke-RestMethod`, `curl`, `gh`, `wget`, BITS tools/cmdlets, and equivalent first- or third-party network CLIs are all L6. Being first-party or preinstalled does not exempt a CLI. The only earlier CLI exception is an already-installed `nuget.exe` at L2. A direct in-process runtime API is L3; a PowerShell web cmdlet is L6. A direct in-process .NET `HttpClient` call from PowerShell is L3.

After any failed request, do not automatically switch transports to repeat the same artifact/source. A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted and the user approves that exact CLI action. If the source or request is explicitly blocked or denied by network/security policy, stop all attempts to that source through every transport; continue only with an independently configured and approved fallback source, otherwise stop and report the blocker. If the user denies CLI approval, stop and do not switch tools or sources to evade that decision. A user L6 approval does not override host, proxy, Zscaler, EDR, or other security controls and cannot guarantee that an alert will not fire.

NuGet package integrity remains mandatory at every tier: verify exact ID/version/RID and compare the `.nupkg` bytes with `catalogEntry.packageHash` from the exact registration record (SHA-512). Verify nuspec, `runtime.json`, archive size and safe paths, apphost, and English guide files before extraction. A GitHub package is acceptable only as an independently configured and approved same-version L4 source with a matching `.sha512` sidecar.

## Verify and Start

1. Before extraction, enforce package-size bounds and verify package ID, exact version, RID, nuspec identity, SHA-512, runtime manifest, safe archive paths, and apphost entry point. NuGet package bytes must match the exact registration `catalogEntry.packageHash`.
2. Stage and validate any new or repaired generation before atomic publication. Never overwrite a different valid package or mutate files used by a running apphost. Do not add a cache lock, second resolver, fixed checked-in restore script, or launch descriptor.
3. Extract only after all checks pass and invoke the matching self-contained apphost directly.
4. The first runtime operation is a fresh `ao --guide` or `so --guide`. Verify the exact version and readable, contained guide path; then inspect the same apphost's help for the standalone ask command.
5. Write the complete contract to a disk file, then invoke `ao ask start --contract-file <path>` or `so ask start --contract-file <path>`. Open only the returned host-approved loopback or explicitly configured browser route; do not infer it from forwarded headers or expose it through an automatic tunnel.
6. After submission, query `ao ask result --ask-id <id>` or `so ask result --ask-id <id>` by the returned ask ID. Return status, typed answers, and receipt without workflow projection or resume.

## NuGet SHA-512 Troubleshooting

- Confirm the exact package ID and version, then obtain `https://api.nuget.org/v3/registration5-semver1/<lowercase-package-id>/<version>.json` through an available L1-L3 capability or a specifically approved L6 operation. Follow the selected registration item's `catalogEntry` link and read `packageHash` from that exact catalog record. The value is Base64-encoded SHA-512; hash the `.nupkg` bytes, not extracted files.
- The following local calculation verifies a file already acquired; it does not authorize or perform a download:

```powershell
$expectedHex = [BitConverter]::ToString([Convert]::FromBase64String($packageHash)).Replace('-', '')
$actualHex = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash
if ($actualHex -ine $expectedHex) { throw 'The package does not match the NuGet registration SHA-512.' }
```

- On mismatch or unavailable metadata, do not extract or publish the package. Preserve the exact package/version/RID, source URL, expected and actual hashes, and failure reason. Use a GitHub Release only if it is an independently configured and approved same-version L4 source with a matching `.sha512` sidecar. If the NuGet source was blocked or denied, never retry that source through another transport; otherwise stop when no independent approved source exists.

## Relationship to Workflow Nodes

The workflow `AskUser` node is a separate consumer of XO Ask. Its adapter supplies workflow-specific identity and answer projection (`contextPath`, `requiredInputs`, SO `validation.declaredUserOwnedFields`) and asks the owning product runtime to resume its canonical workflow copy. None of those fields or operations are required by `/loom-ask-user` standalone sessions.

## Standalone Entry Point Check

Standalone `ask` support is version-specific. Confirm the selected exact published AO or SO apphost's fresh help exposes both `ask start` and `ask result`. If it does not, report the exact runtime limitation; do not create a workflow wait as an undocumented substitute.

Do not add a second resolver, a skill-owned package lock, a per-skill/workspace cache, or cache state outside the shared OS-temp root. Package paths and extraction locations are transient for the current run.
