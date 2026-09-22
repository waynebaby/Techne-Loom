---
name: Loom Runtime And Release Rules
description: Detailed runtime, package, release-set, and guide rules for Loom runtime, package, CI, skill, and release changes.
applyTo: "src/dotnet/**,.agents/skills/**,.github/workflows/**,packages*.md,release-set.json"
---

# Loom Runtime And Release Rules

Use these rules when a change touches runtime acquisition, package layout, release metadata, package publishing, or the published guide surface.

## Runtime Package Family Rules

- Runtime selection belongs to the platform-aware runtime resolver: before any package-cache lookup or network request, automatic mode probes the local host for a usable `dotnet` host with `Microsoft.NETCore.App 9.x` or a higher major version. When available, the resolver selects framework-dependent DLL mode; otherwise it selects the exact-RID published self-contained executable.
- AO skills, SO skills, `so-*` skills, and skills under Loom Skill Orchestrator governance provide only the exact bound runtime version. They must not bind or persist the OS, architecture, libc, RID, package id, executable name, cache directory, or launch path.
- Explicit `dotnet-cli` and self-contained selections are allowed, but the selected mode is immutable for that resolution. A failure must not silently select the other mode.
- .NET CLI mode uses one exact-version .NET runtime bundle with a usable .NET 9 or higher-major host and the embedded Roslyn compiler assemblies required by the C# expression evaluator.
- Self-contained mode uses one exact-version RID package from `Techne.Loom.AgentOrchestrator.Runtime.<rid>` or `Techne.Loom.SkillOrchestrator.Runtime.<rid>`.
- A single resolution acquires only the package closure for its locked mode: DLL/dependency/Roslyn packages or one RID package, never both. Switching modes requires a new resolution identity and explicit continuation.
- Supported self-contained RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64`. Do not cross OS, architecture, or Linux libc boundaries.
- Validate SHA-512, package identity, nuspec metadata, manifest, entrypoint, ZIP safety, and size bounds before launch. Isolate user-level cache entries by product, exact version, and RID; protect them with a cross-process lock; validate in a temporary directory; and publish atomically.
- Published package-channel preflight must verify `so.dll`, `so.deps.json`, `so.runtimeconfig.json`, and their dependency closure before any guide or workflow command. A missing startup-contract file is a failure, never valid runtime evidence.
- Published package restoration validates a complete .NET runtime bundle for the exact locked version in local cache before network access. Missing or invalid cache may download only that exact version and must never fall back to `latest`.
- The resolver must run and verify a fresh `--guide` from its selected launch descriptor before `compile`, `run`, or `resume`, then preserve that descriptor for the execution chain. Skill-owned records retain only the exact version; resolver-produced platform and path facts remain runtime-owned evidence.
- Errors after CLI startup remain command failures and never trigger fallback.

## Runtime Mode Separation

- Automatic mode probes the local host before any package-cache lookup or network request. A usable `Microsoft.NETCore.App 9.x` or higher-major host selects framework-dependent mode; no usable host selects self-contained mode.
- Framework-dependent mode validates and acquires only the exact-version DLL package closure, including dependency packages and Roslyn assemblies. It must not request a RID Runtime EXE package.
- Self-contained mode validates and acquires only one exact-RID package for the selected product and platform. It must not request the DLL, dependency, or Roslyn package closure.
- Runtime evidence must identify the mode decision, exact version, package ids, RID, cache validation, launch descriptor, and failure category. Never report a self-contained RID package as a .NET runtime bundle.

## Package Version Governance

- Package-version-bearing content belongs to one of four categories: live docs and indexes, skill-local offline references, checked-in runtime locks, or historical demos and audit examples.
- Live docs and indexes such as root release notes, `packages.released*.md`, `packages.beta*.md`, exact-version NuGet URLs, and package install commands must reflect the current latest published version for their channel and should be refreshed by CI/CD.
- Skill-local references under `.agents/skills/*/reference/` are deterministic channel snapshots, not floating latest prose. Within one snapshot, version blocks, install commands, exact-version URLs, guide examples, and `resolved_runtime_version` examples must use the same channel-specific version.
- Checked-in runtime locks such as `so-package-lock.json` are authoritative only for the owning skill's exact runtime version. The resolver derives channel, package identity, platform/RID, executable, cache location, and launch path at runtime.
- Historical demos, audit artifacts, and narrative reconstruction material may preserve older versions for reproducibility, but must be clearly scoped as historical and never presented as latest guidance.
- When a current channel version changes, update every version-bearing surface in that category together. Do not introduce ad hoc hardcoded current versions when an existing CI/CD-managed version block, skill version block, or runtime lock owns the value.

## Release-Set Version Closure

- AO and SO skills, their exact-version locks and document-copy manifests, active guide metadata, package indexes, and the 4 core plus 16 RID runtime packages form one channel-specific release set. AO and SO remain independent products; the release set only closes their published version.
- `release-set.json` defines active scope, historical/test exclusions, channel rules, and validation policy. It is scope metadata, not a second version authority.
- During `check-in`, resolve all 20 package ids from the selected CI/CD channel on NuGet. Fail closed on missing or unreadable metadata, unsupported channel versions, or mixed versions. Only after all 20 packages resolve to one compatible exact version may active version surfaces update.
- During `release`, `PACKAGE_VERSION` comes only from the shared version job. A NuGet `latest` result must never replace or invalidate that candidate.
- Active release-set surfaces must resolve to one exact channel version. Reject `latest`, ranges, floating dependencies, mixed AO/SO versions, stale skill locks, stale document-copy manifests, stale guide metadata, and disagreeing package-index commands or URLs. Historical demos and fixtures are allowed only when explicitly classified as historical.
- Active document-copy `source_sha256` values are SHA-256 hashes of canonical UTF-8 source text with CRLF/CR normalized to LF and trailing whitespace removed. Publish refresh scripts and release-set validation must use the same form.
- CI must calculate `PACKAGE_VERSION` once in a shared version job and pass it to runtime packaging, core packaging, validation, and post-push verification. Runtime and publish jobs must not independently run GitVersion.
- Before package push, validate the local 20-package artifact closure against the shared candidate, including filenames, nuspec identity, internal dependency versions, runtime metadata, and guide versions.
- After package push, refresh and validate README, package-index, guide, skill, document-copy manifest, node-map, and package-lock surfaces together with the local package artifacts. Public-feed convergence is a separate probe and never the sole package correctness check.
- Package acquisition and automation restore use exact versions only. `latest` aliases are fallback release assets for human or explicit fallback download paths, never restore authority.

## Guide Surface Rules

- `dotnet so.dll --guide` and `dotnet ao.dll --guide` read the version-matched English `docs/en` tree shipped beside the executable and emit one JSON object containing the actual absolute `version`, `docs_root`, and `guide_path` values. No guide pages are embedded in the executable.
- The guide path is authoritative. Callers may inspect the returned docs root only when the guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`.
- For AO- and SO-routed skills, the selected published runtime must be runnable and produce a fresh `--guide` result before planning, authoring, validation, compile, run, resume, or downstream input collection.
- Once a fresh guide result exists, execution authority returns to the corresponding published AO or SO package runtime. Do not drift back to repository builds or hand-assembled runtimes after the guide establishes the package contract.
- Guides begin with version, build, and compatibility metadata. The fixed hubs at `docs/en/guides/ao-guide.md` and `docs/en/guides/so-guide.md` remain information hubs at or below 200 lines; detailed flow, reference indexes, contracts, behavior, governance, examples, and anti-patterns belong in adjacent guide pages.
- All `ao-guide*.md` and `so-guide*.md` pages are docs assets under `/docs/en/guides` and `/docs/zh-cn/guides`. Do not duplicate or publish guide files under a skill.
