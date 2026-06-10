# Beta Package Index

[中文](packages.beta.zh-CN.md) | [Released](packages.released.md)

Use this page for development or beta acquisition. Skills and agents should direct users here when they want the development branch behavior or unreleased package shape.

## Repository References

- Main branch: `https://github.com/waynebaby/Techne-Loom/tree/main`
- Development branch: `https://github.com/waynebaby/Techne-Loom/tree/development`

## GitHub Release Fallback

Use these links when you need the latest beta package assets without rebuilding locally. The beta fallback release is a GitHub prerelease and keeps both exact prerelease `.nupkg` assets and durable `*.latest.nupkg` aliases.

- Latest beta fallback release page: <https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-beta-latest>

## .NET

| Role | Package / source | Beta acquisition | GitHub fallback | Example |
| --- | --- | --- | --- | --- |
| Abstractions | `Techne.Loom.Abstractions` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet pack .\\src\\dotnet\\Techne.Loom.Abstractions` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.latest.nupkg) | consume latest prerelease `.nupkg` |
| Common | `Techne.Loom.Common` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet pack .\\src\\dotnet\\Techne.Loom.Common` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Common.latest.nupkg) | consume latest prerelease `.nupkg` |
| Plan execution runtime | `Techne.Loom.AgentOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet build .\\src\\dotnet\\Techne.Loom.AgentOrchestrator` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | `dotnet ao.dll --guide` from development build |
| Skill execution runtime | `Techne.Loom.SkillOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet build .\\src\\dotnet\\Techne.Loom.SkillOrchestrator` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | `dotnet so.dll --guide` from development build |

## Node.js

| Role | Package | Beta acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `@techne-loom/abstractions` | _Not implemented yet_ | _TBD_ |
| Common | `@techne-loom/common` | _Not implemented yet_ | _TBD_ |
| Plan execution runtime | `@techne-loom/agent-orchestrator` | _Not implemented yet_ | _TBD_ |
| Skill execution runtime | `@techne-loom/skill-orchestrator` | _Not implemented yet_ | _TBD_ |

## Python

| Role | Package | Beta acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `techne-loom-abstractions` | _Not implemented yet_ | _TBD_ |
| Common | `techne-loom-common` | _Not implemented yet_ | _TBD_ |
| Plan execution runtime | `techne-loom-agent-orchestrator` | _Not implemented yet_ | _TBD_ |
| Skill execution runtime | `techne-loom-skill-orchestrator` | _Not implemented yet_ | _TBD_ |

## Required Reading Before Running Skills

- `/loom-plan-execution`: read `packages.beta.md` first when you want development behavior, then run `dotnet ao.dll --guide`
- `/loom-skill-enhancement`: read `packages.beta.md` first when you want development behavior, then run `dotnet so.dll --guide`
