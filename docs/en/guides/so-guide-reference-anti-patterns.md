# SkillOrchestrator Guide: Anti-Patterns

[Hub](so-guide.md) | [Flow](so-guide-flow.md) | [Index](so-guide-reference.md) | [中文](../../zh-cn/guides/so-guide-reference-anti-patterns.md) | [Root](../README.md)

Version: draft
Build: repository source

## Anti-Patterns

- Letting callers infer the next action from prose alone.
- Hiding memory in prompts instead of workflow context.
- Running shorthand commands without compiling them into a persisted workflow.
- Mixing wrapped command output and SO boundary payloads into one undifferentiated plain-text stream.
- Letting a governed skill ask users to choose package/channel when the runtime version is already bound by the CI/CD-managed skill package version block or checked-in runtime lock.
