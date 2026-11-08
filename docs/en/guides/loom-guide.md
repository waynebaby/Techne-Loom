# Techne Loom CLI and Runtime Guide

[中文](../../zh-cn/guides/loom-guide.md) | [Root](../README.md) |

<!-- guide-version:start -->
Version: 0.4.20-beta
Build: published package 0.4.20-beta
<!-- guide-version:end -->

## Guide Output

Run `loom.exe --guide` on Windows or `loom --guide` on Unix. It returns the actual `version`, `docs_root`, and `guide_path` for the version-matched English guide, plus the workflow/task contract reference paths.

## One CLI, Three Entry Points

- `loom agent <command>` runs Loom Agent Plan-Execution Orchestrator commands.
- `loom skill <command>` runs Loom Skill Orchestrator commands.
- `loom ask <start|result|wait>` is the single workflow-independent question entry point. Do not use `agent ask` or `skill ask`.
- `loom --guide` reports the shared runtime guide and package version.

Examples:

```powershell
loom.exe agent compile --workflow-file .\workflow.json
loom.exe skill run --workflow-file .\workflow.json
loom.exe ask start --contract-file .\question-contract.json
```

```bash
loom agent compile --workflow-file ./workflow.json
loom skill run --workflow-file ./workflow.json
loom ask start --contract-file ./question-contract.json
```

File-valued arguments are paths. Prepare the complete inputs on disk before invoking the command.

## Runtime Package

The target breaking-change release family starting at `0.5.0-beta` is planned to contain eight self-contained RID packages named `Techne.Loom.Runtime.<rid>`. Select exactly one package for the host OS, architecture, and Linux libc. Each package will contain the `loom` apphost, the version-matched English docs, and runtime identity metadata. The coordinated release will also include the no-RID `Techne.Loom.Abstractions` SDK; both package types will share one exact version and channel. This version is not yet published; the current `0.4.20-beta` package family still consists of separate AO/SO packages listed in [the beta package index](../../../packages.beta.md).

Verify the exact package identity, version, RID, checksum, manifest, and archive paths before extraction. Invoke the extracted apphost directly; do not launch a DLL or switch to another package or source build.

## Module Guides

- [Loom Agent Plan-Execution Orchestrator](ao-guide.md)
- [Loom Skill Orchestrator](so-guide.md)
- [Workflow-independent Ask](ask-user-guide.md)
- [CLI and Hosts](../architecture/cli-and-hosts.md)
- [Runtime Platform Detection](../reference/runtime/platform-detection.md)
