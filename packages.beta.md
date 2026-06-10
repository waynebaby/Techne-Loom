# Beta Package Index

[中文](packages.beta.zh-CN.md) | [Released](packages.released.md)

Use this page for development or beta acquisition. Skills and agents should direct users here when they want the development branch behavior or unreleased package shape.

## Repository References

- Main branch: `https://github.com/waynebaby/Techne-Loom/tree/main`
- Development branch: `https://github.com/waynebaby/Techne-Loom/tree/development`

## .NET

| Role | Package / source | Beta acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `Techne.Loom.Abstractions` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet pack .\\src\\dotnet\\Techne.Loom.Abstractions` | consume local prerelease `.nupkg` |
| Common | `Techne.Loom.Common` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet pack .\\src\\dotnet\\Techne.Loom.Common` | consume local prerelease `.nupkg` |
| Plan execution runtime | `Techne.Loom.AgentOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet build .\\src\\dotnet\\Techne.Loom.AgentOrchestrator` | `dotnet ao.dll --guide` from development build |
| Skill execution runtime | `Techne.Loom.SkillOrchestrator` | `git clone --branch development https://github.com/waynebaby/Techne-Loom.git` then `dotnet build .\\src\\dotnet\\Techne.Loom.SkillOrchestrator` | `dotnet so.dll --guide` from development build |

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
