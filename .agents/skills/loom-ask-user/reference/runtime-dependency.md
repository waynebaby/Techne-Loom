# AskUser Runtime Dependency

The Loom Web UI is generated and served by an AO or SO self-contained runtime. A browser form cannot be served by this skill folder alone. A conversational-only clarification does not need a Loom runtime; a Web UI for a workflow wait does.

## Select The Owner

- An AO workflow uses `Techne.Loom.AgentOrchestrator.Runtime.<rid>` and the AO apphost.
- An SO workflow uses `Techne.Loom.SkillOrchestrator.Runtime.<rid>` and the SO apphost.
- Use the runtime that owns the existing business workflow. Do not create a separate workflow for this skill or switch products to get a form.
- The existing typed `AskUser` wait is the source of the form. There is no standalone `so ask` or `ao ask` command.

Use the exact published version that contains the required AskUser Web UI capability. This skill has no package lock or independent version block; take the version from the owning workflow/runtime context or its authoritative package reference. Never choose `latest`, a test/local build, or a version from the other release channel. If no exact published version supports the needed form, stop and report the missing dependency instead of presenting a UI the package cannot serve.

## Reuse Or Acquire

1. Detect the host OS, architecture, and Linux libc, then select exactly one supported RID: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, or `osx-arm64`.
2. Reuse the exact package from the standard NuGet global-packages cache when present, after verifying package ID, version, RID, runtime manifest, apphost, safe archive paths, and package hash against exact NuGet registration metadata.
3. Otherwise fetch that exact version from NuGet.org and compare its bytes with the SHA-512 from exact registration metadata. The same-version GitHub Release fallback is allowed only when its `.sha512` sidecar matches.
4. Extract only after verification and invoke the matching self-contained apphost directly. The first runtime operation is a fresh `ao --guide` or `so --guide`; verify its JSON version and readable, contained guide path before compile, run, or resume.

Do not add a resolver, Loom-specific package cache, workflow template, SO governance route, or skill-owned runtime lock. Package paths and extraction locations are transient for the current run.
