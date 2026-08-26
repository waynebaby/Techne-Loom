# Platform Detection Steps

[中文](../../../zh-cn/reference/runtime/platform-detection.md)

This page defines the shared runtime-selection contract for Loom Agent Execution Orchestrator (AO) and Loom Skill Orchestrator (SO). It applies to direct CLI use and to skills that restore a Loom runtime package.

> **Snapshot status:** this repository snapshot may describe runtime packages before they are published. CI/CD fills the actual runtime package versions, release assets, and SHA-512 values at publish time. Do not invent a runtime version or hash from this page.

## 1. Version Authority And Scope

Direct or manual acquisition starts from the released or beta package index. A governed skill run uses the owning skill's locked exact runtime version, CI/CD-managed version block, or checked-in runtime lock as its only version authority. Do not query `latest`, use a compatibility range, or drift to a neighboring version.

Ownership boundary: the owning AO/SO/target skill supplies and records only the exact runtime version. The platform-aware resolver derives the channel, detects the OS/architecture/libc, selects the RID and package, validates the entrypoint, and returns the cache and launch paths. Those resolver results may appear in runtime-owned evidence, but must not be copied into skill-owned SKILL.md files or version locks.

Runtime selection uses two official channels. Self-contained is the default channel and launches `ao` or `so` directly (`ao.exe`/`so.exe` on Windows) from the exact-RID single-file package. `.NET CLI mode` is explicit, selected by `runtimeBinding` or an explicit bundle directory; it launches the complete IL closure with an available `Microsoft.NETCore.App 9.x` host:

```text
dotnet exec --runtimeconfig <bundle>/ao.runtimeconfig.json <bundle>/ao.dll <args>
dotnet exec --runtimeconfig <bundle>/so.runtimeconfig.json <bundle>/so.dll <args>
```
`--depsfile` and `--runtimeconfig` are required for `.NET CLI mode`. The matching `ao.deps.json` or `so.deps.json` must be present beside the IL entrypoint and must describe the complete exact-version dependency closure; pass `--depsfile <bundle>/<entry>.deps.json` before `--runtimeconfig`. There is no implicit fallback between modes after CLI startup.

Both modes expose the same CLI arguments, workflow state, guide output, audit artifacts, and governance semantics. Self-contained is the default channel; `.NET CLI mode` must be explicitly selected through `runtimeBinding` or an explicit bundle directory. The resolver must return the actual launch descriptor instead of making callers reconstruct a command.

## 2. Probe The .NET Host

First check that the `dotnet` command resolves. Then inspect `dotnet --list-runtimes` and accept a `Microsoft.NETCore.App` entry whose major version is `9`. Do not substitute the SDK version or `dotnet --version` for this check. The installed .NET runtime patch version and the Loom package version are separate fields and must not be conflated.

## 3. Run Startup Preflight And Classify Failures

Self-contained preparation is the default path: resolve one exact RID package, validate its package and manifest, and run its direct entrypoint. For an explicitly selected `.NET CLI` path, restore the owning product's exact-version IL bundle: Product plus `Techne.Loom.Common` plus `Techne.Loom.Abstractions`, including the required `.deps.json` closure. Run a lightweight, side-effect-free host/CLI startup preflight using the same explicit runtime binding that will launch the command, then run a fresh `--guide` invocation.

A host-startup failure in explicit `.NET CLI mode` is a `HostStartup` failure and stops that resolution; it does not select self-contained implicitly. Once the CLI has started, argument, template, expression, governance, or business errors are real command failures. Return them unchanged and do not hide them by retrying with another host.

## 4. Map The Platform To A RID

Support only Windows, Linux, and macOS on x64 or arm64. On Linux, distinguish glibc from musl before selecting the RID. The supported set is:

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

Do not guess a RID, cross architectures, or use a different OS/ABI package as a fallback. An unsupported platform or ABI must fail fast with the detected values and the supported set.

## 5. Acquire One Exact Runtime Package

For AO, resolve `Techne.Loom.AgentOrchestrator.Runtime.<rid>`. For SO, resolve `Techne.Loom.SkillOrchestrator.Runtime.<rid>`. The self-contained package contains the executable for one RID; it does not require a preinstalled .NET runtime, but it still requires the target OS and ABI.

The package distributes one apphost executable. The apphost may use the .NET single-file self-extraction path for bundled framework/native content at startup; this does not add a second distributed runtime file and is required by the embedded Roslyn expression compiler on the self-contained route.

Use the NuGet.org V3 flat-container exact-version URL, not a `latest` or registration URL. Lowercase the package id and use NuGet's normalized exact version in the URL:

```text
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg
https://api.nuget.org/v3-flatcontainer/<lowercased-package-id>/<normalized-exact-version>/<lowercased-package-id>.<normalized-exact-version>.nupkg.sha512
```

The package entry point is fixed at `tools/<rid>/ao` or `tools/<rid>/so`, with `.exe` on Windows. The resolver must retain the exact package id, version, RID, package URL, and hash URL in its runtime evidence.

## 6. Verify Integrity And Package Shape

Verify the NuGet `.sha512` sidecar by decoding its base64 SHA-512 value and comparing it with the downloaded package bytes. Then verify the package nuspec identity, exact version, RID metadata, and entry point. Accept only the documented package files: metadata, `runtime.json`, one executable at `tools/<rid>/ao[.exe]` or `tools/<rid>/so[.exe]`, and the complete `tools/<rid>/docs/en/**` tree containing the product guide.

Reject hash mismatches, identity or version mismatches, missing or duplicate executables, unexpected runtime payloads, ZIP path traversal, oversized entries, and oversized archives. Integrity failure is fail-closed; a different source must not be used to conceal a failed validation.

## 7. Cache And Extract Atomically

Extract into a user-level shared cache whose root can be overridden by environment configuration. Isolate entries by product, exact version, and RID. Use a cross-process lock, validate the complete package in a temporary directory, and publish the immutable cache entry atomically. Set the executable bit on Unix platforms.

A valid cache entry can be reused offline. Discard and atomically rebuild an entry when its hash, manifest, guide version, or package identity no longer matches. If the network is unavailable and no valid exact-version cache entry exists, block with the cache and acquisition evidence; do not substitute a repository build.

## 8. Fall Back From NuGet.org To GitHub

Try the official GitHub release asset only after the exact NuGet.org package cannot be acquired. The fallback must use the same product, channel, exact version, and RID package. The automated resolver accepts only the exact versioned `.nupkg` asset. The channel's `.latest.nupkg` alias may be listed as a durable manual fallback address, but it must not be used for lock/cache automation; if used manually, resolve and verify its content against the bound exact version:

```text
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-<channel>-latest/<PackageId>.<exact-version>.nupkg
https://github.com/waynebaby/Techne-Loom/releases/download/nuget-<channel>-latest/<PackageId>.latest.nupkg
```

Apply the same SHA-512, nuspec, manifest, ZIP safety, and entry-point checks to the fallback. If both sources fail, block. Never replace a failed package acquisition with a repository-source build or a fabricated preflight success.

## 9. Launch And Preserve Evidence

Return one launch descriptor with these machine-readable fields:

```text
runtime_mode
resolved_runtime_version
rid
package_id
package_ids
package_url
package_hash
cache_root
launch_file
launch_prefix_args
preflight_result
```

For `.NET CLI mode`, `package_ids` names the exact .NET runtime bundle (a NuGet restore set that includes Roslyn), `launch_file` is the IL entry point, and `launch_prefix_args` contains the explicit `dotnet exec` binding. For the self-contained mode, `package_id` names the one RID package, `launch_file` is the cached direct executable, and `launch_prefix_args` is empty unless the host requires a platform-specific prefix.

Both modes must run a fresh `--guide` first. Parse the emitted JSON, verify its `version`, and read its returned `guide_path`. Only then may the caller run `compile`, `run`, or `resume`. Every later AO/SO command must reuse the same launch descriptor, exact runtime version, and RID; it must not switch hosts midway through a workflow.

## Authoritative Rules

- The owning skill's locked exact version is the only runtime version authority. `latest`, compatibility ranges, and neighboring-version drift are invalid.
- Only a host-startup-class failure triggers the self-contained fallback. Errors after the CLI has started are returned as command failures and are never hidden by fallback retries.
- A cache entry is isolated by product, exact version, and RID, protected by a cross-process lock, validated in a temporary directory, and published atomically.
- A valid exact-version cache entry supports offline execution; no network plus no valid cache entry is a blocking result.
- This snapshot may precede runtime package publication. CI/CD supplies the actual package versions, assets, and SHA-512 values at release time.
