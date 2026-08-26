# Loom Agent Execution Orchestrator Guide: Anti-Patterns

[Hub](ao-guide.md) | [Flow](ao-guide-flow.md) | [Index](ao-guide-reference.md) | [中文](../../zh-cn/guides/ao-guide-reference-anti-patterns.md) | [Root](../README.md)

Version: draft
Build: repository source

## Anti-Patterns

- Treating AO as a general-purpose chat wrapper.
- Returning prose that omits workflow, node, or artifact state.
- Using AO to execute deterministic step-by-step skill logic that belongs in SO.
- Replacing the documented CLI/package control path with a private wrapper without a clear reason.
- Letting AO imply a weave-out request informally instead of emitting an explicit structured boundary for it.
- Letting a Loom-governanced skill ask users to choose package/channel when the runtime version is already bound by the CI/CD-managed skill package version block or checked-in runtime lock.
