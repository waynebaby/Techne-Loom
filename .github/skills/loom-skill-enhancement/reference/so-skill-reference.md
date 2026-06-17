# Loom Skill Orchestrator Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-skill-enhancement/SKILL.md`.

## Enhancement Scope

- Enhancement business outcome is target-skill creation or modification.
- Runtime-only verification cannot be reported as final enhancement completion.
- Every enhancement pass must run a fresh `dotnet so.dll --guide [--lang <language>]` from the current selected package runtime before editing or validating target-skill deliverables.
- When the target project does not already have its own dependencies installed, install only the minimum dependency set required for the requested target-skill changes and current guide-aligned validation work.

## Runtime Acquisition

- In package-channel mode, restore the Loom Skill Orchestrator runtime bundle together at one resolved version:
  - `Techne.Loom.SkillOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- Build one unified runtime directory and execute Loom Skill Orchestrator commands from that directory only.
- Do not execute from partial single-package extraction roots.

## Re-Enhancement Upgrade Gate

When the target skill is already enhanced by Loom Skill Orchestrator (`SO-enhanced`):

- ask one user question with exactly two choices: latest released or latest beta
- do not silently reuse the old lock channel or old locked version as the upgrade decision
- reacquire the latest Loom Skill Orchestrator package from the user-confirmed channel
- run `dotnet so.dll --guide [--lang <language>]` from that selected package before any new enhancement edits
- strongly recommend a subagent review that compares the current target skill and Loom Skill Orchestrator workflow assets against that latest guide result before editing

## Workflow Template Governance Baseline

- Workflow templates must model explicit governed steps, guards, seams, and reviewable outputs.
- SO-governed target-skill templates should declare root `templateKind: so-governed-target-skill` plus root `validation.gates`, `validation.routes`, `validation.declaredUserOwnedFields`, and `validation.reservedRuntimeOwnedFields`.
- Terminal governed routes must name the business-output gates that must be satisfied before `done`.
- Blocked governed routes must name the strongest-earned business-output gates that must be satisfied before a runtime-owned wait boundary.
- `AskUser` may request only user-owned inputs or decisions. Runtime-owned facts, runtime provenance, and system-generated artifact paths belong to runtime-owned seams such as `WaitResume`.
- Never author or keep any node whose purpose says or implies `run a multistep plan`.
- Split open-ended work into explicit deterministic steps instead of hiding it behind a generic planner node.
- Review workflow templates for any node whose instruction embeds a multistep plan or a broad prompt to an agent, then decompose that node into smaller governed nodes when possible.

## Governed Validation Enforcement

- `dotnet so.dll compile` and workflow-load paths reject SO-governed target-skill templates that omit the root validation contract.
- `dotnet so.dll compile` and workflow-load paths reject `AskUser` seams that request reserved runtime-owned fields such as `workflow_file`, `event_log_file`, audit artifact paths, or other system-generated provenance.
- `dotnet so.dll compile` and workflow-load paths reject terminal paths that can reach `done` without satisfying the route's declared business-output gates.
- `dotnet so.dll compile` and workflow-load paths reject blocked routes that pause without declaring and publishing the strongest-earned blocked business outputs.

## Startup Contract Preflight

Before Loom Skill Orchestrator command execution in package-channel mode, verify:

- `so.dll`
- `so.deps.json`
- `so.runtimeconfig.json`
- dependency closure readiness in the same runtime directory.

## Launch Mode

- Prefer explicit launch mode in package-channel execution:
  - `dotnet exec --depsfile <so.deps.json> --runtimeconfig <so.runtimeconfig.json> <so.dll> ...`

## Governance and Official Run Surface

In SO-exclusive governance mode:

- Loom Skill Orchestrator is the only official execution authority.
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
- For SO-governed target-skill templates, completion also requires the governed validation contract, route-aware business-output gates, and seam ownership declarations to be present and compile-clean.
