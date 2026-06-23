# Local Offline Package Index (Beta)

This file is the offline package authority for `/loom-skill-enhancement` when the caller selects the beta channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or prerelease policy. Use the rules and versions in this file.

## Beta Channel Rule

- Beta channel means prerelease packages from the development line.
- For deterministic package-channel execution, restore one exact prerelease version for the full SO runtime bundle.
- For this offline snapshot, the current latest beta version is `0.2.114-beta`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Version Shape Rule

- Beta versions follow `major.minor.<distance>-beta`.
- Once the beta channel is selected, do not silently downgrade to released packages.
- Use one exact beta version across the whole SO runtime bundle.

## Full Runtime Bundle Rule

Never restore only the runtime package.

The SO runtime bundle is always:

- `Techne.Loom.SkillOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All three packages must resolve to the same beta version.

## Deterministic Restore Rule

For official skill execution, prefer exact version restore over floating prerelease resolution after the channel is chosen.

- Good: restore all three packages at `0.2.114-beta`.
- Bad: restore one package at `0.2.112-beta` and another at a different prerelease.
- Bad: restore only `Techne.Loom.SkillOrchestrator`.
- Bad: switch to stable packages after the beta channel has been chosen.

## Acquisition Commands

Use these commands when a local runtime bundle needs to be restored from packages:

```powershell
dotnet add package Techne.Loom.Abstractions --version 0.2.114-beta
dotnet add package Techne.Loom.Common --version 0.2.114-beta
dotnet add package Techne.Loom.SkillOrchestrator --version 0.2.114-beta
```

If the runtime is restored by package extraction rather than project reference, keep the same exact version rule for all three packages.

## Unified Runtime Directory Rule

After package restore or extraction:

- build one unified runtime directory outside the skill folder
- place `so.dll`, `so.deps.json`, `so.runtimeconfig.json`, and dependency assemblies in that one directory
- run SO commands from that unified directory only
- do not execute from partial extraction roots or mixed-version directories
- on Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`
- when PowerShell 5.1 uses `Invoke-WebRequest` or `Invoke-RestMethod` for package probes, add `-UseBasicParsing`

## Startup Preflight

Before using the beta runtime bundle, verify:

- `so.dll` exists
- `so.deps.json` exists
- `so.runtimeconfig.json` exists
- dependent assemblies from `Techne.Loom.Common` and `Techne.Loom.Abstractions` are present in the same runtime directory
- if extraction fails or any startup-contract file is missing, stop immediately and do not record `runtime_preflight_result: passed`

## Launch Mode

Prefer explicit launch mode for deterministic runtime binding:

```powershell
dotnet exec --depsfile .\so.deps.json --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

The same launch form applies to `compile`, `run`, `resume`, `status`, `inspect-workflow`, and `inspect-events`.

Do not export a guide file from failed command stderr. Save guide artifacts only after the guide command succeeds against a runtime that passed startup preflight.

## Official Runtime Surface

Preparation and inspection commands:

- `dotnet so.dll --guide`
- `dotnet so.dll compile`
- `dotnet so.dll status`
- `dotnet so.dll inspect-workflow`
- `dotnet so.dll inspect-events`

Official skill run commands:

- `dotnet so.dll run`
- `dotnet so.dll resume`

## Required Think-Out-Loud Fields

When the skill reports package-channel runtime preparation, include:

- `resolved_runtime_version: 0.2.114-beta`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

When audit artifacts exist, also include:

- `mermaid_file`
- `html_file`
- `analysis_file` when present

## Maintenance Rule

This file is intentionally self-contained for runtime use.

- Do not tell the runtime flow to consult repository package indexes.
- Do not require browsing NuGet pages to understand beta-channel behavior.
- Refresh this file in a maintenance pass when the beta latest version changes.
