# Beta Package Index

[中文](packages.beta.zh-CN.md) | [Released](packages.released.md)

Use this page for development or beta acquisition. Skills and agents should direct users here when they want the development branch behavior or unreleased package shape.

## Repository References

- Main branch: `https://github.com/waynebaby/Techne-Loom/tree/main`
- Development branch: `https://github.com/waynebaby/Techne-Loom/tree/development`

## GitHub Release Fallback

Use these links when you need the latest beta package assets without rebuilding locally. The beta fallback release is a GitHub prerelease and keeps both exact prerelease `.nupkg` assets and durable `*.latest.nupkg` aliases.

- Latest beta fallback release page: <https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-beta-latest>

## NuGet.org Latest Version

Use the NuGet.org package page when you want the latest published prerelease version number before choosing an exact `--version`.

- Open the package page and inspect the version list on NuGet.org to confirm the newest published prerelease.
- If you want the newest prerelease without pinning a number, use `dotnet add package <PackageId> --prerelease`.
- If you need an exact prerelease version, copy it from NuGet.org and use `dotnet add package <PackageId> --version <latest-beta-version>`.

| Package | NuGet.org | Latest beta example |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions --prerelease` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common --prerelease` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator --prerelease` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator --prerelease` |

## Version Shape

- The repository packable projects currently inherit `VersionPrefix=0.1.0` from `src/dotnet/Directory.Build.props`.
- Development publishing on `development` pushes `major.minor.<distance>-beta` versions to NuGet.org, where `<distance>` is the GitVersion commit distance from the current version source, so the exact latest prerelease number should be read from the package page before pinning it.

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
