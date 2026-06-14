# SO Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-skill-enhancement/SKILL.md`.

## Enhancement Scope

- Enhancement business outcome is target-skill creation or modification.
- Runtime-only verification cannot be reported as final enhancement completion.

## Runtime Acquisition

- In package-channel mode, restore the SO runtime bundle together at one resolved version:
  - `Techne.Loom.SkillOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- Build one unified runtime directory and execute SO commands from that directory only.
- Do not execute from partial single-package extraction roots.

## Startup Contract Preflight

Before SO command execution in package-channel mode, verify:
- `so.dll`
- `so.deps.json`
- `so.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.

## Launch Mode

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --depsfile <so.deps.json> --runtimeconfig <so.runtimeconfig.json> <so.dll> ...`

## Governance and Official Run Surface

In SO-exclusive governance mode:
- SO is the only official execution authority.
- Official skill runs are only:
  - `dotnet so.dll run`
  - `dotnet so.dll resume`
- Direct CLI and MCP are primitive/component paths only.

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

## Delivery Completion Gate

- Completion requires requested target-skill deliverables to exist and governance wording to be aligned.
- Runtime validation artifacts alone cannot serve as sole completion evidence.
