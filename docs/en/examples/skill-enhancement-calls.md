# Loom Skill Enhancement Call Examples

[中文](../../zh-cn/examples/skill-enhancement-calls.md) | [Root](../README.md)

These examples show how to call `/loom-skill-enhancement` in three common routes while keeping Loom Skill Orchestrator (`dotnet so.dll`) governance explicit.

> [!NOTE]
> Workflow templates produced by these routes must use explicit governed steps, guards, seams, and reviewable outputs. They must never contain a node purpose or node intention that says or implies `run a multistep plan`. Review them for any node instruction that embeds a multistep plan or a broad prompt to an agent, then break that intent into smaller governed nodes when possible.

## Read With

- [Skill Usage Guide](../guides/skill-usage.md)
- [Skills Reference](../reference/skills.md)
- [SkillOrchestrator Guide](../reference/products/so-guide.md)

## 1. Enhance A Skill That Already Exists

Use this route when the target skill already exists but is not yet governed by Loom Skill Orchestrator.

```text
/loom-skill-enhancement
Channel: released
Language: en
Target: .github/skills/existing-skill
Goal: upgrade this existing skill into a Loom-governanced skill with a checked-in workflow template, locked runtime bundle, and explicit governance wording
Requested target skill changes:
- refresh SKILL.md for Loom Skill Orchestrator governance
- create assets/so-workflow/skill-plan.md
- create a checked-in workflow template under assets/so-workflow/
- create assets/so-workflow/so-package-lock.json
```

Expected route:

- read the selected package index first
- run a fresh `dotnet so.dll --guide [--lang <language>]` from the current selected package runtime
- if the target project does not already have its own dependencies installed, install only the minimum dependency set needed for the requested target-skill changes and current guide-aligned validation path
- derive or refresh `skill-plan.md`
- author a deterministic workflow template with no hidden multistep-plan node intent
- review the template for any node instruction that bundles multiple steps or a broad agent prompt, then split it into smaller nodes when possible
- compile before any execution-authority claim

## 2. Create A Skill With A Skill Plan

Use this route when the skill does not exist yet and the primary outcome should begin from a plan-mode markdown artifact.

```text
/loom-skill-enhancement
Channel: beta
Language: en
Target: .github/skills/new-skill
Goal: create a new deterministic skill from a skill plan and keep the first plan-mode outcome as markdown
Requested target skill changes:
- create SKILL.md
- create assets/so-workflow/skill-plan.md as the first plan-mode outcome markdown file
- create a checked-in workflow template under assets/so-workflow/
- create assets/so-workflow/so-package-lock.json
```

Expected route:

- treat `assets/so-workflow/skill-plan.md` as the first authored outcome
- let the workflow template refine that plan into explicit governed steps
- avoid any template node that hides open-ended execution under a generic planner intention
- review the draft template for bundled multistep instructions and break them into smaller governed nodes

## 3. Re-Enhance A Skill Already Enhanced By Loom Skill Orchestrator

Use this route when the target skill is already enhanced by Loom Skill Orchestrator and needs another enhancement pass.

```text
/loom-skill-enhancement
Channel: ask the required re-enhancement gate
Language: en
Target: .github/skills/already-enhanced-skill
Goal: re-enhance this skill with the latest Loom Skill Orchestrator guide and tighten governance wording
Requested target skill changes:
- refresh SKILL.md governance wording
- refresh assets/so-workflow/skill-plan.md if the guide requires it
- refresh the checked-in workflow template if the guide requires it
- keep assets/so-workflow/so-package-lock.json aligned to the current skill-bound runtime version
```

Required decision and route:

- do not ask the user to choose released versus beta during a normal re-enhancement pass
- reacquire the exact package version already bound to the current skill build and checked-in package lock
- run a fresh `dotnet so.dll --guide [--lang <language>]` from that bound package runtime before editing
- if the target project does not already have its own dependencies installed, install only the minimum dependency set needed for the requested target-skill changes and current guide-aligned validation path
- strongly recommend a subagent review of the current skill and workflow assets against the latest guide result
- keep the refreshed workflow template free of any node intent that says or implies `run a multistep plan`
- review the refreshed template for any node instruction that bundles a multistep plan or broad agent prompt, then break that node into smaller governed nodes when possible

## What These Calls Must Never Do

- never treat direct CLI or direct MCP execution as a peer official run surface
- never silently reuse an old package lock to choose the re-enhancement version
- never let a workflow template hide open-ended execution behind a node that says or implies `run a multistep plan`

## Continue Reading

- Return to [Examples](README.md)
- Read [Skill Usage Guide](../guides/skill-usage.md)
- Read [Skills Reference](../reference/skills.md)
