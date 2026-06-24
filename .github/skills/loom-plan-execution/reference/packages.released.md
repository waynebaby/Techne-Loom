# Local Offline Package Index (Released)

This file is the offline package authority for `/loom-plan-execution` when the caller selects the released channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or version policy. Use the rules and versions in this file.

## Released Channel Rule

- Released channel means stable packages only.
- For deterministic package-channel execution, restore one exact stable version for the full AO runtime bundle.
- For this offline snapshot, the current latest released version is `0.2.126`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Full Runtime Bundle Rule

Never restore only the runtime package.

The AO runtime bundle is always:

- `Techne.Loom.AgentOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All three packages must resolve to the same released version.

## Deterministic Restore Rule

For official skill execution, prefer exact version restore over floating resolution after the channel is chosen.

- Good: restore all three packages at `0.2.126`.
- Bad: restore one package at `0.2.77` and another at a different stable version.
- Bad: restore only `Techne.Loom.AgentOrchestrator`.
- Bad: switch to beta packages after the released channel has been chosen.

## Acquisition Commands

Use these commands when a local runtime bundle needs to be restored from packages:

```powershell
dotnet add package Techne.Loom.Abstractions --version 0.2.126
dotnet add package Techne.Loom.Common --version 0.2.126
dotnet add package Techne.Loom.AgentOrchestrator --version 0.2.126
```

If the runtime is restored by package extraction rather than project reference, keep the same exact version rule for all three packages.

When the exact package id and version are already known, do not use NuGet.org page/search/registration indexing freshness as the existence gate. Probe or download the exact `.nupkg` URL directly instead, for example:

```text
https://www.nuget.org/api/v2/package/Techne.Loom.AgentOrchestrator/0.2.77
```

## Unified Runtime Directory Rule

After package restore or extraction:

- build one unified runtime directory outside the skill folder
- place `ao.dll`, `ao.deps.json`, `ao.runtimeconfig.json`, and dependency assemblies in that one directory
- run AO commands from that unified directory only
- do not execute from partial extraction roots or mixed-version directories
- on Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`
- when PowerShell 5.1 uses `Invoke-WebRequest` or `Invoke-RestMethod` for package probes, add `-UseBasicParsing`

## Startup Preflight

Before using the released runtime bundle, verify:

- `ao.dll` exists
- `ao.deps.json` exists
- `ao.runtimeconfig.json` exists
- dependent assemblies from `Techne.Loom.Common` and `Techne.Loom.Abstractions` are present in the same runtime directory
- if extraction fails or any startup-contract file is missing, stop immediately and do not record `runtime_preflight_result: passed`

## Launch Mode

Prefer explicit launch mode for deterministic runtime binding:

```powershell
dotnet exec --depsfile .\ao.deps.json --runtimeconfig .\ao.runtimeconfig.json .\ao.dll --guide
```

The same launch form applies to `compile`, `prompt-plan`, `prompt-replan`, `run`, and `resume`.

Do not export a guide file from failed command stderr. Save guide artifacts only after the guide command succeeds against a runtime that passed startup preflight.

## Official Runtime Surface

Preparation and inspection commands:

- `dotnet ao.dll --guide`
- `dotnet ao.dll compile`
- `dotnet ao.dll prompt-plan`
- `dotnet ao.dll prompt-replan`

Official skill run commands:

- `dotnet ao.dll run`
- `dotnet ao.dll resume`

`--guide`, `compile`, `prompt-plan`, and `prompt-replan` are not official skill run modes.

## Required Think-Out-Loud Fields

When the skill reports package-channel runtime preparation, include:

- `resolved_runtime_version: 0.2.126`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

When audit artifacts exist, also include:

- `audit_markdown_file`
- `audit_html_file`

## Maintenance Rule

This file is intentionally self-contained for runtime use.

- Do not tell the runtime flow to consult repository package indexes.
- Do not require browsing NuGet pages to understand released-channel behavior.
- Refresh this file in a maintenance pass when the released latest version changes.
