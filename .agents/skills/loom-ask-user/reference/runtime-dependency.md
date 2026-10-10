# XO Ask Runtime Dependency

`/loom-ask-user` requires an exact published AO or SO self-contained runtime binary, but it does not require a workflow or workflow-owned wait. In this documentation, `XO Ask` means the shared ask capability exposed by either existing product apphost; XO is not a third product or package family.

## Version and Product Selection

- The skill's refreshed AO/SO version block is the exact shared release-set baseline. The AO and SO runtime packages in that release closure use the same version.
- Detect the host OS, architecture, and Linux libc, then select exactly one supported RID: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, or `osx-arm64`.
- Check the standard NuGet global-packages cache for a valid AO or SO package at that exact version and RID. Reuse the exact cached package; if both are valid, prefer AO.
- If neither exact package is cached, acquire the AO package for that exact version and RID first. Do not float to `latest`, switch versions, or substitute a local/test build. If the preferred package cannot be acquired or verified, report the dependency failure.
- The selected apphost must expose the standalone `ask` entry point before the skill can start an ask. Confirm this from the fresh package's help/guide instead of assuming that an `AskUser` workflow node implies standalone support.

## Verify and Start

1. Before extraction, enforce package-size bounds and verify package ID, exact version, RID, nuspec identity, SHA-512 against exact NuGet registration metadata, runtime manifest, safe archive paths, and apphost entry point.
2. Extract only after all checks pass and invoke the matching self-contained apphost directly.
3. The first runtime operation is a fresh `ao --guide` or `so --guide`. Verify the exact version and readable, contained guide path; then inspect the same apphost's help for the standalone ask command.
4. Write the complete contract to a disk file, then invoke `ao ask start --contract-file <path>` or `so ask start --contract-file <path>`. Open only the returned host-approved loopback or explicitly configured browser route; do not infer it from forwarded headers or expose it through an automatic tunnel.
5. After submission, query `ao ask result --ask-id <id>` or `so ask result --ask-id <id>` by the returned ask ID. Return status, typed answers, and receipt without workflow projection or resume.

## Relationship to Workflow Nodes

The workflow `AskUser` node is a separate consumer of XO Ask. Its adapter supplies workflow-specific identity and answer projection (`contextPath`, `requiredInputs`, SO `validation.declaredUserOwnedFields`) and asks the owning product runtime to resume its canonical workflow copy. None of those fields or operations are required by `/loom-ask-user` standalone sessions.

## Current Release Gap

The published `0.3.334-beta` SO apphost help has no standalone `ask` entry. The AO apphost from the same runtime source line also has no such entry. Therefore that package line cannot currently complete workflow-free skill use even though its workflow-owned AskUser Web UI works. Do not create a workflow wait as an undocumented substitute; report the gap until a published AO/SO package exposes the standalone command.

Do not add a second resolver, a skill-owned package lock, or a Loom-specific package cache. Package paths and extraction locations are transient for the current run.
