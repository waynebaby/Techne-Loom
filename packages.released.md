# Released Package Index

[中文](packages.released.zh-CN.md) | [Beta](packages.beta.md)

Use this page for stable or release-oriented package acquisition. Skills and agents should make users choose the package channel first, then proceed with setup and execution.

## Repository References

- Main branch: `https://github.com/waynebaby/Techne-Loom/tree/main`
- Development branch: `https://github.com/waynebaby/Techne-Loom/tree/development`

## GitHub Release Fallback

Use these links when NuGet feed access is unavailable. The stable fallback release keeps both the exact versioned `.nupkg` assets and durable `*.latest.nupkg` aliases.

- Latest stable fallback release page: <https://github.com/waynebaby/Techne-Loom/releases/tag/nuget-stable-latest>

## NuGet.org Latest Version

Check the NuGet.org package page to find the latest published stable version number before deciding whether to pin an exact `--version`.

- The package page always shows the current latest stable version at the top.
- If you do not need to pin an exact version, `dotnet add package <PackageId>` resolves the latest stable package from NuGet.org.
- If you do need to pin an exact version, copy the latest stable version from NuGet.org and use `dotnet add package <PackageId> --version <latest-stable-version>`.

| Package | NuGet.org | Latest stable example |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator` |

## Version Shape

- The repository packable projects currently inherit `VersionPrefix=0.1.0` from `src/dotnet/Directory.Build.props`.
- Stable publishing on `main` pushes the SemVer `major.minor.patch` release number to NuGet.org, so the exact stable number should be read from the package page before pinning it.

## .NET

| Role | Package | Stable acquisition | GitHub fallback | Example |
| --- | --- | --- | --- | --- |
| Abstractions | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.1.0` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Abstractions.latest.nupkg) | `using Techne.Loom.Abstractions.TaskTracking.Model;` |
| Common | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.1.0` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Common.latest.nupkg) | `var json = WorkflowJsonSerializer.Serialize(instance);` |
| Plan execution runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.1.0` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | `dotnet ao.dll --guide` |
| Skill execution runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.1.0` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | `dotnet so.dll --guide` |

## Node.js

| Role | Package | Stable acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `@techne-loom/abstractions` | _Not implemented yet_ | _TBD_ |
| Common | `@techne-loom/common` | _Not implemented yet_ | _TBD_ |
| Plan execution runtime | `@techne-loom/agent-orchestrator` | _Not implemented yet_ | _TBD_ |
| Skill execution runtime | `@techne-loom/skill-orchestrator` | _Not implemented yet_ | _TBD_ |

## Python

| Role | Package | Stable acquisition | Example |
| --- | --- | --- | --- |
| Abstractions | `techne-loom-abstractions` | _Not implemented yet_ | _TBD_ |
| Common | `techne-loom-common` | _Not implemented yet_ | _TBD_ |
| Plan execution runtime | `techne-loom-agent-orchestrator` | _Not implemented yet_ | _TBD_ |
| Skill execution runtime | `techne-loom-skill-orchestrator` | _Not implemented yet_ | _TBD_ |

## Required Reading Before Running Skills

- `/loom-plan-execution`: read `packages.released.md` or `packages.beta.md`, then `docs/en/reference/products/ao-guide.md`
- `/loom-skill-enhancement`: read `packages.released.md` or `packages.beta.md`, then `docs/en/reference/products/so-guide.md`
