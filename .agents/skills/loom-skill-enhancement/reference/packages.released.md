# Local Offline Package Index (Released)

This file is the offline package authority for `/loom-skill-enhancement` when the caller selects the released channel.

During skill execution, do not switch to repository docs or web pages to decide package ids, bundle composition, or version policy. Use the rules and versions in this file.

## Released Channel Rule

- Released channel means stable packages only.
- For deterministic package-channel execution, restore one exact stable version for the full SO runtime bundle.
- For this offline snapshot, the current latest released version is `0.2.223`.
- If a future maintenance pass refreshes this file, the refreshed value becomes the new local authority.

## Full Runtime Bundle Rule

Never restore only the runtime package.

The SO runtime bundle is always:

- `Techne.Loom.SkillOrchestrator`
- `Techne.Loom.Common`
- `Techne.Loom.Abstractions`

All three packages must resolve to the same released version.

## Deterministic Restore Rule

For official skill execution, prefer exact version restore over floating resolution after the channel is chosen.

- Good: restore all three packages at `0.2.223`.
- Bad: restore one package at `0.2.77` and another at a different stable version.
- Bad: restore only `Techne.Loom.SkillOrchestrator`.
- Bad: switch to beta packages after the released channel has been chosen.

## Acquisition Commands

Use these commands when a local runtime bundle needs to be restored from packages:

```powershell
dotnet add package Techne.Loom.Abstractions --version 0.2.223
dotnet add package Techne.Loom.Common --version 0.2.223
dotnet add package Techne.Loom.SkillOrchestrator --version 0.2.223
```

If the runtime is restored by package extraction rather than project reference, keep the same exact version rule for all three packages.

When the exact package id and version are already known, do not use NuGet.org page/search/registration indexing freshness as the existence gate. Probe or download the exact `.nupkg` URL directly instead, for example:

```text
https://www.nuget.org/api/v2/package/Techne.Loom.SkillOrchestrator/0.2.223
```

## Unified Runtime Directory Rule

After package restore or extraction:

- build one unified runtime directory outside the skill folder
- place `so.dll`, `so.runtimeconfig.json`, and dependency assemblies in that one directory
- if `so.deps.json` is present, keep it beside the runtime bundle; if it is absent, do not fail preflight on that fact alone before testing the co-located runtime bundle
- run SO commands from that unified directory only
- do not execute from partial extraction roots or mixed-version directories
- on Windows PowerShell 5.1, treat `.nupkg` as ZIP content and do not use `Expand-Archive` directly on the `.nupkg`
- when PowerShell 5.1 uses `Invoke-WebRequest` or `Invoke-RestMethod` for package probes, add `-UseBasicParsing`

## Startup Preflight

Before using the released runtime bundle, verify:

- `so.dll` exists
- `so.runtimeconfig.json` exists
- dependent assemblies from `Techne.Loom.Common` and `Techne.Loom.Abstractions` are present in the same runtime directory
- if `so.deps.json` is present, keep it with the runtime bundle and prefer launch modes that use it explicitly
- if extraction fails, `so.dll` is missing, `so.runtimeconfig.json` is missing, dependency closure is broken, or the co-located runtime bundle cannot actually start, stop immediately and do not record `runtime_preflight_result: passed`

## Launch Mode

Prefer explicit launch mode for deterministic runtime binding:

```powershell
dotnet exec --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

If `so.deps.json` is present and the host requires it for deterministic binding, this explicit form also remains valid:

```powershell
dotnet exec --depsfile .\so.deps.json --runtimeconfig .\so.runtimeconfig.json .\so.dll --guide
```

The same launch form applies to `compile`, `run`, `resume`, `status`, `inspect-workflow`, and `inspect-events`.

After the guide command succeeds against a runtime that passed startup preflight, parse its JSON `version`, `docs_root`, and `guide_path` fields and read the returned `guide_path`. Do not treat failed command stderr as guide evidence.

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

- `resolved_runtime_version: 0.2.223`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

After every `dotnet so.dll` CLI call, when audit artifacts exist, also include:

- `mermaid_file`
- `html_file`
- `analysis_file` when present

If the call did not emit a fresh Mermaid render, repeat the latest known `mermaid_file`, `html_file`, and `analysis_file` and state that the render is unchanged, then add a concise workflow-location summary.

## Maintenance Rule

This file is intentionally self-contained for runtime use.

- Do not tell the runtime flow to consult repository package indexes.
- Do not require browsing NuGet pages to understand released-channel behavior.
- Refresh this file in a maintenance pass when the released latest version changes.
