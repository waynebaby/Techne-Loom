# 包布局

[English](../../en/architecture/package-layout.md)

Techne Loom 从第一天起就预留按语言划分的根目录，并把每个项目单元都视为可发布 package。

## 根目录

- `src/dotnet` 承载 v1 实现。
- `src/nodejs` 预留给未来的 npm workspace 和 package。
- `src/python` 预留给未来的 PyPI package 与 wheel。

## Package 家族

每个项目单元都对应一个可发布 package。

| 角色 | .NET | Node.js | Python |
| --- | --- | --- | --- |
| 共享契约 | `Techne.Loom.Abstractions` | `@techne-loom/abstractions` | `techne-loom-abstractions` |
| 共享运行时辅助 | `Techne.Loom.Common` | `@techne-loom/common` | `techne-loom-common` |
| 探索式编排 | `Techne.Loom.AgentOrchestrator` | `@techne-loom/agent-orchestrator` | `techne-loom-agent-orchestrator` |
| 确定性 skill 执行 | `Techne.Loom.SkillOrchestrator` | `@techne-loom/skill-orchestrator` | `techne-loom-skill-orchestrator` |

## 依赖方向

- `Techne.Loom.Abstractions` 不依赖产品特定 runtime。
- `Techne.Loom.Common` 依赖 `Techne.Loom.Abstractions`。
- `Techne.Loom.SkillOrchestrator` 依赖 `Techne.Loom.Abstractions` 与 `Techne.Loom.Common`。
- `Techne.Loom.AgentOrchestrator` 应依赖公开核心，但不能演变成“换个名字的 SO 外壳”。

## 当前仓库状态

| Package | 当前状态 | 说明 |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | `.NET` 活跃实现 | workflow/task-tracking 模型与契约 |
| `Techne.Loom.Common` | `.NET` 活跃实现 | 运行时辅助、序列化、store、表达式支持 |
| `Techne.Loom.SkillOrchestrator` | `.NET` 活跃实现 | 已有公开 CLI/runtime 切片 |
| `Techne.Loom.AgentOrchestrator` | scaffold + 文档 | 正式实现仍待完成 |

## 未来拆分规则

- 如果 `Common` 开始吸入过多 hosting 或 runtime-control 逻辑，应尽早拆出专门的 runtime/hosting package，而不是把 `Common` 变成第二个杂物间。

## 边界规则

- AO 与 SO 是独立产品线。
- `Abstractions` 保持协议中立。
- `Common` 只承载可复用运行时辅助，不承载云集成。
- 当适配器明显依赖具体产品或传输协议时，应放在公开核心之外。
