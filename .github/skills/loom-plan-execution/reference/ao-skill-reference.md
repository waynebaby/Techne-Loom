# AO Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-plan-execution/SKILL.md`.

## Runtime Acquisition

- In package-channel mode, restore the AO runtime bundle together at one resolved version:
  - `Techne.Loom.AgentOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- Build one unified runtime directory and execute AO commands from that directory only.
- Do not execute from partial single-package extraction roots.

## Startup Contract Preflight

Before AO command execution in package-channel mode, verify:
- `ao.dll`
- `ao.deps.json`
- `ao.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.

## Launch Mode

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --depsfile <ao.deps.json> --runtimeconfig <ao.runtimeconfig.json> <ao.dll> ...`

## Runtime Flow Details

- Use guide and prompt surfaces for preparation:
  - `dotnet ao.dll --guide`
  - `dotnet ao.dll prompt-plan`
  - `dotnet ao.dll prompt-replan`
  - `dotnet ao.dll compile`
- Official skill runs remain only:
  - `dotnet ao.dll run`
  - `dotnet ao.dll resume`

## Think-Out-Loud Required Fields

Report runtime fields once runtime is prepared and on each progress update:
- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

Report audit fields on each progress update:
- `audit_markdown_file`
- `audit_html_file`

## Business-Outcome-First Gate

- If objective/plan clearly requests business outputs, completion requires business deliverables plus AO completed state.
- Runtime-only or meta-only reporting cannot replace business delivery completion.
