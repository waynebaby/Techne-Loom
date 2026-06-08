# Package Layout

[中文](../../zh-cn/architecture/package-layout.md)

Techne Loom reserves language roots from day one and treats every project unit as a publishable package.

## Roots

- `src/dotnet` contains the v1 implementation.
- `src/nodejs` is reserved for future npm workspaces and packages.
- `src/python` is reserved for future PyPI packages and wheels.

## Package Families

Each project unit maps to one publishable package.

| Role | .NET | Node.js | Python |
| --- | --- | --- | --- |
| Shared contracts | `Techne.Loom.Abstractions` | `@techne-loom/abstractions` | `techne-loom-abstractions` |
| Shared runtime helpers | `Techne.Loom.Common` | `@techne-loom/common` | `techne-loom-common` |
| Exploratory orchestration | `Techne.Loom.AgentOrchestrator` | `@techne-loom/agent-orchestrator` | `techne-loom-agent-orchestrator` |
| Deterministic skill execution | `Techne.Loom.SkillOrchestrator` | `@techne-loom/skill-orchestrator` | `techne-loom-skill-orchestrator` |

## Dependency Direction

- `Techne.Loom.Abstractions` has no product-specific runtime dependency.
- `Techne.Loom.Common` depends on `Techne.Loom.Abstractions`.
- `Techne.Loom.SkillOrchestrator` depends on `Techne.Loom.Abstractions` and `Techne.Loom.Common`.
- `Techne.Loom.AgentOrchestrator` should depend on the public core, but must not become a disguised wrapper over SO.

## Current Repository State

| Package | Current state | Notes |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | active `.NET` implementation | workflow/task-tracking model and contracts |
| `Techne.Loom.Common` | active `.NET` implementation | runtime helpers, serialization, stores, expression support |
| `Techne.Loom.SkillOrchestrator` | active `.NET` implementation | public CLI/runtime slice exists |
| `Techne.Loom.AgentOrchestrator` | scaffold + docs | implementation still pending |

## Planning Rule For Future Splits

- If `Common` starts accumulating too much hosting or runtime-control logic, split a dedicated runtime/hosting package early instead of turning `Common` into a second catch-all.

## Boundary Rules

- AO and SO are separate product lines.
- `Abstractions` stays protocol-neutral.
- `Common` holds reusable runtime helpers, not cloud integrations.
- Protocol adapters belong outside the public core when they are product- or transport-specific.
