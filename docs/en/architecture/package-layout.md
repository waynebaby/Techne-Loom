# Package Layout

[中文](../../zh-cn/architecture/package-layout.md) | [Root](../README.md)

Techne Loom keeps its implementation modules independent while publishing one unified runtime package family.

## Active .NET Release Set

The current .NET release set contains eight self-contained `Techne.Loom.Runtime.<rid>` packages, one for each supported RID, plus the no-RID `Techne.Loom.Abstractions` contract SDK. All nine packages share the same exact version and release channel. `Techne.Loom.AgentOrchestrator` and `Techne.Loom.SkillOrchestrator` remain separate, buildable implementation modules inside that release boundary; they are not separate runtime package families.

| Published package | Scope |
| --- | --- |
| `Techne.Loom.Runtime.<rid>` | Unified `loom` apphost, Agent and Skill module commands, shared runtime, and RID-specific self-contained dependencies |
| `Techne.Loom.Abstractions` | Public workflow/task contracts and the version-matched SDK |

Supported RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64`.

## Implementation Modules

| Module | Responsibility | Public route |
| --- | --- | --- |
| `Techne.Loom.AgentOrchestrator` | Agent planning, re-planning, workflow execution, and continuation | `loom agent ...` |
| `Techne.Loom.SkillOrchestrator` | Skill-governed workflow authoring, validation, execution, and continuation | `loom skill ...` |

The Agent and Skill modules retain distinct workflow semantics and do not call each other or form a parent/child runtime pair. They share the published `loom` apphost and package release boundary.

## Shared Runtime Capability

| Capability | Responsibility | Public route |
| --- | --- | --- |
| Shared XO Ask | Workflow-independent structured questions | `loom ask ...` |

Shared XO Ask is a shared capability of the `loom` runtime, not a third implementation module or a separate package family.

## Future Ecosystem Packages

`src/nodejs` and `src/python` remain reserved for future ecosystem work. Their package names and invocation forms are plans only; they are not part of the current .NET release set.

## Dependency And Boundary Rules

- `Techne.Loom.Abstractions` remains protocol-neutral.
- `Techne.Loom.Common` holds reusable runtime helpers and is not a separately published apphost family.
- Protocol adapters belong outside the public core when they are product- or transport-specific.
- Package indexes and the exact-version runtime locks are the authority for currently published versions and assets.
