# Beta Package Index

[中文](packages.beta.zh-CN.md) | [Released](packages.released.md)

Use this page for development or beta acquisition. Direct CLI or manual callers can choose the beta channel here when they want development-branch behavior or unreleased package shape; governed AO/SO skill runs should instead follow the runtime version already bound by the current CI/CD-managed skill package version block or checked-in runtime lock, then derive `released` versus `beta` from that bound version when needed.

Runtime bundle rule for local execution: never restore only the runtime package. Loom Agent Execution Orchestrator runtime acquisition must download `Techne.Loom.AgentOrchestrator` + `Techne.Loom.Common` + `Techne.Loom.Abstractions`; SO runtime acquisition and target-skill restoration must download `Techne.Loom.SkillOrchestrator` + `Techne.Loom.Common` + `Techne.Loom.Abstractions`, all at the same beta version.

Unified runtime directory rule for local execution: after resolving one exact beta version, acquire the full three-package runtime bundle in one pass, then extract all bundle members into one external unified runtime directory before running `ao.dll` or `so.dll`. Do not probe or execute from partial single-package extraction roots.

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
- If the exact package id and version are already known, do not wait for NuGet.org page/search/registration indexing to catch up before deciding whether the package exists. Probe or download the exact `.nupkg` URL directly instead.
- Direct exact-version package URL shape: `https://www.nuget.org/api/v2/package/<PackageId>/<Version>`

| Package | NuGet.org | Latest beta example |
| --- | --- | --- |
| `Techne.Loom.Abstractions` | <https://www.nuget.org/packages/Techne.Loom.Abstractions> | `dotnet add package Techne.Loom.Abstractions --prerelease` |
| `Techne.Loom.Common` | <https://www.nuget.org/packages/Techne.Loom.Common> | `dotnet add package Techne.Loom.Common --prerelease` |
| `Techne.Loom.AgentOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.AgentOrchestrator> | `dotnet add package Techne.Loom.AgentOrchestrator --prerelease` |
| `Techne.Loom.SkillOrchestrator` | <https://www.nuget.org/packages/Techne.Loom.SkillOrchestrator> | `dotnet add package Techne.Loom.SkillOrchestrator --prerelease` |

Direct package check examples:

```text
https://www.nuget.org/api/v2/package/Techne.Loom.AgentOrchestrator/0.2.206-beta
https://www.nuget.org/api/v2/package/Techne.Loom.SkillOrchestrator/0.2.206-beta
```

## Version Shape

<!-- package-version-block:start -->
- The current latest published beta package version is `0.2.206-beta`.
- Development publishing on `development` pushes `major.minor.<distance>-beta` versions to NuGet.org, where `<distance>` is the GitVersion commit distance from the current version source.
<!-- package-version-block:end -->














## .NET

<!-- package-dotnet-block:start -->
| Role | Package / source | Beta acquisition | GitHub fallback | Example |
| --- | --- | --- | --- | --- |
| Abstractions | `Techne.Loom.Abstractions` | `dotnet add package Techne.Loom.Abstractions --version 0.2.206-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Abstractions.latest.nupkg) | consume exact latest prerelease |
| Common | `Techne.Loom.Common` | `dotnet add package Techne.Loom.Common --version 0.2.206-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.Common.latest.nupkg) | consume exact latest prerelease |
| Plan execution runtime | `Techne.Loom.AgentOrchestrator` | `dotnet add package Techne.Loom.AgentOrchestrator --version 0.2.206-beta` plus restore `Techne.Loom.Common` and `Techne.Loom.Abstractions` at `0.2.206-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.AgentOrchestrator.latest.nupkg) | use exact latest prerelease AO runtime bundle |
| Skill execution runtime | `Techne.Loom.SkillOrchestrator` | `dotnet add package Techne.Loom.SkillOrchestrator --version 0.2.206-beta` plus restore `Techne.Loom.Common` and `Techne.Loom.Abstractions` at `0.2.206-beta` | [latest .nupkg](https://github.com/waynebaby/Techne-Loom/releases/download/nuget-beta-latest/Techne.Loom.SkillOrchestrator.latest.nupkg) | use exact latest prerelease SO runtime bundle |
<!-- package-dotnet-block:end -->














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

- `/loom-plan-execution`: if direct CLI/manual acquisition or the governed runtime version already resolves to `beta`, read `packages.beta.md` first, then run `dotnet ao.dll --guide` from the Loom Agent Execution Orchestrator runtime bundle
- `/loom-skill-enhancement`: if direct CLI/manual acquisition or the governed runtime version already resolves to `beta`, read `packages.beta.md` first, then run `dotnet so.dll --guide`
