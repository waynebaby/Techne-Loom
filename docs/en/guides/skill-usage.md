# Using Techne Loom Skills

[中文](../../zh-cn/guides/skill-usage.md) | [Root](../README.md)

This guide is the operator-facing entry for using Techne Loom skills in practice.

If you want package contracts or runtime wire details, read the product guides and the skills reference after this page. This page answers a narrower question first: which skill should you use, what should you give it, and what counts as the official run surface.

## Choose The Right Entry

| Situation | Use this | Read first | Official run surface |
| --- | --- | --- | --- |
| The route is still unclear and you need exploratory orchestration | `/loom-plan-execution` | `packages.released.md` or `packages.beta.md`, then `dotnet ao.dll --guide` for Loom Agent Execution Orchestrator | `dotnet ao.dll run` and `dotnet ao.dll resume` |
| You want to create or upgrade a deterministic skill | `/loom-skill-enhancement` | `packages.released.md` or `packages.beta.md`, then `dotnet so.dll --guide` for Loom Skill Orchestrator | after enhancement, official target-skill runs are `dotnet so.dll run` and `dotnet so.dll resume`; `compile` is validation only |
| You already have an SO-enhanced target skill and want to use it day to day | the target skill itself | the target `SKILL.md` plus `assets/so-workflow/so-package-lock.json` | `dotnet so.dll run` and `dotnet so.dll resume` against a runtime workflow copy |

## Shared Setup Rules

1. Choose package channel before you run anything. Stable callers start from [packages.released.md](../../../../packages.released.md). Development callers start from [packages.beta.md](../../../../packages.beta.md).
2. If local runtime download is needed, restore the full runtime bundle, not only the main runtime package. Loom Agent Execution Orchestrator uses `Techne.Loom.AgentOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`. Loom Skill Orchestrator uses `Techne.Loom.SkillOrchestrator`, `Techne.Loom.Common`, and `Techne.Loom.Abstractions`.
3. Keep compile artifacts, audit artifacts, runtime workflow copies, session folders, and event sidecars outside checked-in skill directories unless the user explicitly chooses another output root.
4. Use NuGet.org as the first-class latest package source. Keep GitHub release assets only as a fallback path.

## `/loom-plan-execution`

Use `/loom-plan-execution` when the outer agent still needs to explore, clarify, compare frontiers, or delegate focused work before the route is stable.

### Inputs For Loom Agent Execution Orchestrator Skill

- a rich plan with at least 10 non-empty lines, or a detailed plan file path
- optional language surface: `en` or `zh-cn`
- optional audit output root

### What It Does

- sends the caller to the correct package index first
- treats `dotnet ao.dll --guide` as the authority before execution
- can explicitly call `dotnet ao.dll prompt-plan` to obtain Loom Agent Execution Orchestrator-managed planner prompt blocks before authoring a WorkflowInstance file, and `dotnet ao.dll prompt-replan` to obtain Loom Agent Execution Orchestrator-managed replanner prompt blocks before editing a blocked WorkflowInstance seam
- runs Loom Agent Execution Orchestrator as the only official execution authority for the skill
- returns control-state data such as `session_id`, `workflow_file`, `event_log_file`, and blocked frontier details

### Loom Agent Execution Orchestrator Demo

```text
/loom-plan-execution
Channel: beta
Language: en
Plan:
1. Review the failing CLI behavior.
2. Compare the likely ownership paths.
3. Validate the narrowest fix.
4. Stop on explicit weave-out if human input is required.
...
```

### What Counts As An Official Run

- `dotnet ao.dll run`
- `dotnet ao.dll resume`

`dotnet ao.dll --guide` and `dotnet ao.dll compile` are preparation or validation surfaces, not official skill runs.

## `/loom-skill-enhancement`

Use `/loom-skill-enhancement` when you want to create a deterministic skill, upgrade an existing skill into a Loom Skill Orchestrator-governed skill, or push a skill already enhanced by Loom Skill Orchestrator into SO-exclusive governance.

### Inputs For Loom Skill Orchestrator Enhancement

- target skill path or target repository path
- deterministic goal or upgrade request
- requested target-skill changes to create or modify in this enhancement pass
- runtime version authority: reuse the checked-in `assets/so-workflow/so-package-lock.json` plus the current skill package version block, and derive `released` versus `beta` from that bound version when needed
- optional language surface: `en` or `zh-cn`
- optional JSON context file
- optional audit output root

### What It Produces

- `<target-skill-root>/assets/so-workflow/skill-plan.md`
- a checked-in workflow template under `<target-skill-root>/assets/so-workflow/`
- `<target-skill-root>/assets/so-workflow/so-package-lock.json`
- an updated target `SKILL.md` that explicitly references the lock file and the Loom Skill Orchestrator governance model

### Loom Skill Orchestrator Enhancement Demo

```text
/loom-skill-enhancement
Bound runtime version: <current skill package version>
Language: en
Target: .github/skills/my-target-skill
Goal: upgrade this skill into an SO-exclusive governed skill with a checked-in workflow template and a locked runtime bundle
Requested target skill changes:
- refresh SKILL.md governance wording
- create or refresh assets/so-workflow/skill-plan.md
- create or refresh the checked-in workflow template
- create or rewrite assets/so-workflow/so-package-lock.json
```

Three concrete call patterns are documented in [Loom Skill Enhancement Call Examples](../examples/skill-enhancement-calls.md).

Workflow-template governance baseline:

- workflow templates must use explicit governed steps, guards, seams, and reviewable outputs
- workflow templates must never contain a node purpose or node intention that says or implies `run a multistep plan`
- workflow template review must look for any node instruction that embeds a multistep plan or a broad prompt to an agent, then break that intent into smaller governed nodes when possible

### What Counts As An Official Run After Enhancement

- the enhancement pass may use `dotnet so.dll compile` as a validation step before governance is finalized
- when the enhancement pass executes the target-skill workflow, the official target-skill run surface is `dotnet so.dll run` and `dotnet so.dll resume`
- once the target skill is SO-exclusive governed, only `dotnet so.dll run` and `dotnet so.dll resume` count as official target-skill runs

Direct CLI snippets, MCP calls, or prose explanations do not become official runs by themselves.

## Using An SO-Enhanced Target Skill

An SO-enhanced target skill is no longer used like a generic prompt-only skill.

### Day-To-Day Run Order

1. Read the target `SKILL.md`.
2. Read `assets/so-workflow/so-package-lock.json` and restore the exact locked Loom Skill Orchestrator runtime bundle from NuGet.
3. Keep the checked-in workflow template clean. Clone it to a runtime workflow copy outside the skill folder.
4. Run `dotnet so.dll run --workflow-file <runtime-copy-path>`.
5. If Loom Skill Orchestrator blocks, follow `skill_hint`, preserve `memory_for_next_step`, and resume with `dotnet so.dll resume --workflow-file <runtime-copy-path> --result-file <path>`.

### Minimal Demo

```text
Read SKILL.md -> read assets/so-workflow/so-package-lock.json -> restore exact locked Loom Skill Orchestrator runtime bundle -> clone checked-in template -> run dotnet so.dll run -> follow blocked seam -> dotnet so.dll resume
```

### What Not To Do

- do not silently float to a newer Loom Skill Orchestrator package version inside the same channel
- do not restore only `Techne.Loom.SkillOrchestrator`
- do not point `run` or `resume` back at the checked-in source template
- do not treat direct CLI or direct MCP execution as a peer official run surface once the target skill is SO-exclusive governed

## Deeper References

- [Agent Integration](agent-integration.md)
- [Skill Integration](skill-integration.md)
- [Loom Agent Execution Orchestrator Guide](../reference/products/ao-guide.md)
- [SkillOrchestrator Guide](../reference/products/so-guide.md)
- [Skills Input/Output Reference](../reference/skills.md)
- [Loom Skill Enhancement Call Examples](../examples/skill-enhancement-calls.md)
- [SO-Enhanced Skill Run Example](../examples/so-enhanced-skill-run.md)
