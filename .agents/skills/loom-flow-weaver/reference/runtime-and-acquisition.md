# Runtime and Dependency Acquisition Contract

This contract applies whenever this skill or a declared local agent acquires a runtime, dependency, or external artifact. It does not grant network access or override host, OS, proxy, Zscaler, EDR, or other security controls.

## Ordered acquisition policy

Use the first available integrity-checked capability, without installing a runtime, downloader, or package client solely for acquisition:

- **L0 - Local artifact/cache:** reuse only after confirming exact identity, version/RID where applicable, and required integrity.
- **L1 - Agent/host fetch:** use configured host fetch for the exact artifact without a downloader subprocess; if unavailable, skip; a denied source remains blocked.
- **L2 - Existing NuGet client:** use it only for non-Loom exact NuGet packages, if `nuget.exe` is already installed; exact Loom runtime packages use L3 in-process HTTP.
- **L3 - Existing runtime API:** use an already-installed in-process HTTP API such as PowerShell-hosted .NET `HttpClient`, Python `urllib`, or Node `fetch`; do not invoke a downloader subprocess.
- **L4 - Known GitHub MCP asset:** use only a preconfigured, approved repository, exact release tag and asset, and its matching checksum/sidecar.
- **L5 - Other approved Host/MCP source:** use only an already available, configured, approved source for the exact artifact and required integrity evidence.
- **L6 - Generic network CLI:** only after L0-L5 are unavailable/exhausted, and only with explicit per-operation human approval naming the tool, source/host, exact artifact/version, destination, and purpose. This includes `Invoke-WebRequest`, `Invoke-RestMethod`, `curl`, `gh`, `wget`, BITS, and equivalent download CLIs.

A missing capability may be skipped. After a failed request, do not automatically change transports to repeat the same source. A blocked or denied source stays blocked through every transport. If a nonblocked source may be retried, follow the exact-NuGet exception below. Never install a runtime or downloader solely to acquire an artifact.

## Exact NuGet Acquisition Completion Rule

This rule applies to `/loom-ask-user`, `/loom-plan-execution`, `/loom-skill-enhancement`, and every skill created or updated under Loom Skill Enhancement governance.

1. Reuse an existing local generation only after exact package ID, version, RID, runtime manifest, apphost, guide, archive, and full `.nupkg` SHA-512 verification.
2. Otherwise attempt the exact package on `api.nuget.org` through an in-process runtime HTTP API. A direct PowerShell `.NET HttpClient` call is L3 and must use automatic redirects disabled. Do not use an opaque fetch or shell web command for this attempt.
3. Read the exact version registration `catalogEntry.packageHash` and verify the downloaded `.nupkg` bytes against that SHA-512. Validate nuspec identity, RID, runtime manifest, archive size and safe paths, apphost, and guide files.
4. Any NuGet request, registration/catalog lookup, redirect, hash, extraction, identity, or package-validation failure, including HTTP 404 or a redirect to `nuget.azure.cn`, ends the NuGet attempt. Do not follow the redirect or probe other mirrors.
5. When L0-L2 do not yield a verified package, run a short single-purpose program outside source and skill directories that uses an already installed runtime HTTP API. Re-run the same program for a nonblocked retry; do not switch to a shell fetch while an in-process API exists.
6. Before a GitHub fallback, ask once for explicit human approval naming the exact asset and selected package-index `.nupkg.sha512` authority. Without approval, decline, or unavailable approval, keep acquisition resumably pending and do not download the asset.
7. If approved, verify GitHub bytes only with the GitHub sidecar and allow redirects only to `github.com`, `release-assets.githubusercontent.com`, or `objects.githubusercontent.com`. Never reuse a NuGet hash or probe another mirror.
8. Do not complete, substitute, skip, or record terminal failure until the exact package is verified. A policy block, denied approval, unpublished exact version, or repeated integrity mismatch remains `acquisition_pending`; preserve evidence and the next approved acquisition action.

The Loom Flow Weaver seed has no runtime lock and does not authorize choosing or selecting a runtime package. Do not infer a package version or RID.

## Runtime Documentation References

This rule is conditional. Apply it only when this skill or a declared local agent needs AO/SO technical documentation.

Use the selected exact apphost successful `--guide` JSON. Verify the runtime version, absolute `docs_root` and absolute `guide_path`, containment, and readability. Resolve English technical pages only under that `docs_root`.

For workflow/task contracts, validate same-version `contract_docs_root`, `contract_index_path`, and `builder_template_path`. Follow index links only within `contract_docs_root`; pass resolved paths through `referencePackManifest` or bounded parent context. Do not construct skill-relative `docs/...` paths or copy runtime package documentation into this skill.

Runner and external-child-adapter contracts are hard verification inputs, not defaults for inferred runtime behavior. If authoritative guide/contract evidence is absent, emit a structured blocker with evidence, required inputs, decision scope, and next action.
