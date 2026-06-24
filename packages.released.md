# Released Package Index

[中文](packages.released.zh-CN.md) | [Beta](packages.beta.md)

Use this page for stable or release-oriented package acquisition. Direct CLI or manual callers can choose the released channel here; governed AO/SO skill runs should instead follow the runtime version already bound by the current CI/CD-managed skill package version block or checked-in runtime lock, then derive `released` versus `beta` from that bound version when needed.

Runtime bundle rule for local execution: never restore only the runtime package. Loom Agent Execution Orchestrator runtime acquisition must download `Techne.Loom.AgentOrchestrator` + `Techne.Loom.Common` + `Techne.Loom.Abstractions`; SO runtime acquisition and target-skill restoration must download `Techne.Loom.SkillOrchestrator` + `Techne.Loom.Common` + `Techne.Loom.Abstractions`, all at the same released version.

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
- If the exact package id and version are already known, do not wait for NuGet.org page/search/registration indexing to catch up before deciding whether the package exists. Probe or download the exact `.nupkg` URL directly instead.
- Direct exact-version package URL shape: `https://www.nuget.org/api/v2/package/<PackageId>/<Version>`

| Package | NuGet.org | Latest stable example |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator` |

Direct package check examples:

```text
https://www.nuget.org/api/v2/package/Techne.Loom.AgentOrchestrator/0.2.138
https://www.nuget.org/api/v2/package/Techne.Loom.SkillOrchestrator/0.2.138
```

## Version Shape

<!-- package-version-block:start -->
- The current latest published stable package version is `0.2.138`.
- Stable publishing on `main` pushes `major.minor.<distance>` versions to NuGet.org for released packages in this repository policy.
<!-- package-version-block:end -->



## .NET

<!-- package-dotnet-block:start -->
| Role | Package | Stable acquisition | GitHub fallback | Example |
| --- | --- | --- | --- | --- |
| Abstractions | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.2.138` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Abstractions.latest.nupkg) | `using Techne.Loom.Abstractions.TaskTracking.Model;` |
| Common | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.2.138` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.Common.latest.nupkg) | `var json = WorkflowJsonSerializer.Serialize(instance);` |
| Plan execution runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.2.138` plus restore `Techne.Loom.Common` and `Techne.Loom.Abstractions` at `0.2.138` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | `dotnet ao.dll --guide` from the AO runtime bundle |
| Skill execution runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.2.138` plus restore `Techne.Loom.Common` and `Techne.Loom.Abstractions` at `0.2.138` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-stable-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | `dotnet so.dll --guide` from the SO runtime bundle |
<!-- package-dotnet-block:end -->



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

- `/loom-plan-execution`: if direct CLI/manual acquisition or the governed runtime version already resolves to `released`, read `packages.released.md`, then the Loom Agent Execution Orchestrator guide at `docs/en/reference/products/ao-guide.md`
- `/loom-skill-enhancement`: if direct CLI/manual acquisition or the governed runtime version already resolves to `released`, read `packages.released.md`, then `docs/en/reference/products/so-guide.md`
