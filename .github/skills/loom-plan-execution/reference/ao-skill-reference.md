# AO Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-plan-execution/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-plan-execution` needs to create or revise workflow JSON:

- [../assets/agents/loom-plan-execution-workflow-designer.agent.md](../assets/agents/loom-plan-execution-workflow-designer.agent.md)

Pass relative links to the plan file, guide file, workflow file, audit artifacts, and any blocked payload evidence so the subagent runs with explicit local context instead of relying on repository-global discovery.

The subagent must generate node-level granularity where each node owns one visible responsibility and where every AO weave-out path has a detailed blocked-action hint.

## Runtime Acquisition

- In package-channel mode, restore the AO runtime bundle together at one resolved version:
  - `Techne.Loom.AgentOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- Build one unified runtime directory and execute AO commands from that directory only.
- Do not execute from partial single-package extraction roots.
- On Windows PowerShell 5.1, do not use `Expand-Archive` directly on `.nupkg`. Treat the package as ZIP content and extract it through ZIP-aware APIs or an equivalent ZIP-based flow.
- If you probe package URLs through `Invoke-WebRequest` or `Invoke-RestMethod` on Windows PowerShell 5.1, add `-UseBasicParsing` to avoid legacy security prompts that stall automation.

## Startup Contract Preflight

Before AO command execution in package-channel mode, verify:

- `ao.dll`
- `ao.deps.json`
- `ao.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.
- If extraction fails or any startup-contract file is missing, stop immediately. Do not emit `runtime_preflight_result: passed`.

## Launch Mode

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --depsfile <ao.deps.json> --runtimeconfig <ao.runtimeconfig.json> <ao.dll> ...`

## Runtime Flow Details

- After channel and runtime-source selection, the next hard gate is proving that the selected AO runtime for that source is runnable and can emit a fresh `dotnet ao.dll --guide [--lang <language>]` result from that runtime.
- Do not proceed to planning, authoring, validation, `compile`, `prompt-plan`, `prompt-replan`, `run`, `resume`, or downstream input collection before that guide result exists.
- Once that guide result exists, official governed execution must return to the corresponding published AO package runtime surface that the guide describes. Reading `--guide` does not allow official execution to keep drifting on repository builds, hand-assembled runtimes, or other non-governed paths.
- Failed stderr output from `dotnet ao.dll --guide` or `dotnet exec ... ao.dll --guide` is not a guide artifact. Save exported guide files only after the guide command succeeds and the startup-contract files are present.
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
