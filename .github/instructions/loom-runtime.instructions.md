---
name: Loom Runtime And Release Rules
description: Detailed runtime, package, release-set, and guide rules for Loom runtime, package, CI, skill, and release changes.
applyTo: "src/dotnet/**,.agents/skills/**,.github/workflows/**,packages*.md,release-set.json"
---

# Loom Runtime And Release Rules

Use these rules when a change touches runtime acquisition, package layout, release metadata, package publishing, or the published guide surface.

## Runtime Package Family Rules

- The active .NET runtime NuGet release set contains only self-contained product+RID packages: eight `Techne.Loom.AgentOrchestrator.Runtime.<rid>` packages and eight `Techne.Loom.SkillOrchestrator.Runtime.<rid>` packages. Source projects and ProjectReferences remain available for builds and tests but are not part of this runtime package release set.
- AO/SO skills and governed workflows bind an exact published version. The host agent determines the local OS, architecture, and Linux libc, then selects exactly one supported RID. Checked-in skill state does not persist a host, RID, package cache path, executable path, or launch descriptor.
- The sole runtime mode is the exact-RID self-contained apphost. Do not probe for `dotnet`, assemble framework-dependent DLL/dependency/Roslyn closures, or fall back between runtime modes.
- Supported RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64`. Never cross OS, architecture, or Linux libc boundaries.
- On first use, the host agent may use an already available shell or script engine to acquire and extract the exact package. Do not add a fixed checked-in acquisition script or a second resolver. If the host lacks a required acquisition, verification, extraction, or execution capability, stop with a concrete diagnostic.
- Prefer a valid exact package already present in the standard NuGet global-packages cache; otherwise request the exact locked version from NuGet.org, comparing bytes to `catalogEntry.packageHash` in exact registration metadata. The exact-version GitHub Release fallback requires its matching `.sha512` sidecar. Do not use floating aliases or resolve `latest`.
- Before extraction or launch, enforce archive-size bounds; verify package ID, exact version, nuspec identity, RID, SHA-512, manifest, apphost entrypoint, and safe ZIP paths. Extract only after all checks pass. Do not add a Loom-specific package cache, cache lock, or atomic cache-publish subsystem. The package `runtime.json` is identity metadata, not a launch descriptor.
- Invoke the extracted apphost directly. Preserve only exact package identity/version/RID, hash result, extraction path for the current run, and guide evidence as runtime-owned facts.
- A command failure after the apphost starts is a command failure; never disguise it as a package fallback.

## Runtime Mode Separation

- There is one runtime mode: one exact-version package for the current product and detected RID. The host agent validates the package and starts its platform apphost directly; no resolver-owned launch descriptor is created or required.
- A fresh `so[.exe] --guide` or `ao[.exe] --guide` call is the first runtime operation after extraction. Accept only JSON with the exact expected version, absolute `docs_root` and `guide_path`, a guide path contained by the docs root, and readable files.
- After guide validation, use the same extracted apphost for schema/demo generation, compile, run, and resume. A failed guide or validation gate stops the route.
- Runtime evidence identifies exact version, package ID, RID, package hash, extraction result, guide JSON, and failure category. It does not include a runtime mode or launch descriptor.

## Package Version Governance

- Package-version-bearing content belongs to one of four categories: live docs and indexes, skill-local offline references, checked-in runtime locks, or historical demos and audit examples.
- Live docs and indexes such as root release notes, `packages.released*.md`, `packages.beta*.md`, exact-version NuGet URLs, and package acquisition commands must reflect the current latest published version for their channel and should be refreshed by CI/CD.
- Skill-local references under `.agents/skills/*/reference/` are deterministic channel snapshots, not floating latest prose. Within one snapshot, version blocks, acquisition commands, exact-version URLs, guide examples, and `resolved_runtime_version` examples must use the same channel-specific version.
- Checked-in runtime locks such as `so-package-lock.json` are authoritative for the owning skill's exact runtime version. The host agent derives RID and executable name from the current host; runtime package paths are transient acquisition evidence, not skill-owned configuration.
- Historical demos, audit artifacts, and narrative reconstruction material may preserve older versions for reproducibility, but must be clearly scoped as historical and never presented as latest guidance.
- When a current channel version changes, update every version-bearing surface in that category together. Do not introduce ad hoc hardcoded current versions when an existing CI/CD-managed version block, skill version block, or runtime lock owns the value.

## Release-Set Version Closure

- AO/SO skills, their exact-version locks and document-copy manifests, active guide metadata, package indexes, and the 16 RID runtime packages form one channel-specific release set. AO and SO remain independent products; the release set only closes their published version.
- `release-set.json` defines active scope, historical/test exclusions, channel rules, and validation policy. It is scope metadata, not a second version authority.
- During `check-in`, resolve all 16 active runtime package IDs from the selected CI/CD channel on NuGet and require one compatible exact version. Keep retired core package versions in a separate monotonic high-water input; do not include those packages in the active release closure.
- During `release`, `PACKAGE_VERSION` comes only from the shared version job. A NuGet `latest` result must never replace or invalidate that candidate.
- Active release-set surfaces must resolve to one exact channel version. Reject `latest`, ranges, floating dependencies, mixed AO/SO versions, stale skill locks, stale document-copy manifests, stale guide metadata, and disagreeing package-index commands or URLs. Historical demos and fixtures are allowed only when explicitly classified as historical.
- Active document-copy `source_sha256` values are SHA-256 hashes of canonical UTF-8 source text with CRLF/CR normalized to LF and trailing whitespace removed. Publish refresh scripts and release-set validation must use the same form.
- CI must calculate `PACKAGE_VERSION` once in a shared version job and pass it to all 16 runtime package builds, validation, and post-push verification. Do not pack or push the four non-runtime core packages in this release set. Runtime and publish jobs must not independently run GitVersion.
- Before package push, validate that the local artifact closure contains exactly the 16 expected runtime package IDs and versions, including filenames, nuspec identity, runtime metadata, apphost, hashes, and guide versions; reject extra or missing packages.
- After package push, refresh and validate README, package-index, guide, skill, document-copy manifest, node-map, and package-lock surfaces together with the local package artifacts. Public-feed convergence is a separate probe and never the sole package correctness check.
- Package acquisition and automation restore use exact versions only. The same-version GitHub Release fallback is permitted only after its `.sha512` sidecar validates; floating aliases are never restore authority.

## Guide Surface Rules

- Run `so.exe --guide`/`ao.exe --guide` on Windows or `so --guide`/`ao --guide` on Unix by directly executing the extracted apphost. Each command reads the version-matched English `docs/en` tree shipped beside the apphost and emits one JSON object containing the actual absolute `version`, `docs_root`, and `guide_path` values. No guide pages are embedded in the executable.
- The guide path is authoritative. Callers may inspect the returned docs root only when the guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`.
- For AO- and SO-routed skills, exact package and hash validation, safe extraction, direct apphost startup, and a fresh readable `--guide` result are required before planning, authoring, validation, compile, run, resume, or downstream input collection. No descriptor, MCP registration, or fragment inspection is a prerequisite for package startup or guide retrieval.
- Once a fresh guide result exists, execution authority stays with that exact published AO or SO apphost. Do not drift back to repository builds or hand-assembled runtime bundles after the guide establishes the package contract.
- Guides begin with version, build, and compatibility metadata. The fixed hubs at `docs/en/guides/ao-guide.md` and `docs/en/guides/so-guide.md` remain information hubs at or below 200 lines; detailed flow, reference indexes, contracts, behavior, governance, examples, and anti-patterns belong in adjacent guide pages.
- All `ao-guide*.md` and `so-guide*.md` pages are docs assets under `/docs/en/guides` and `/docs/zh-cn/guides`. Do not duplicate or publish guide files under a skill.
