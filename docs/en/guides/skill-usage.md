# Using Techne Loom Skills

[中文](../../zh-cn/guides/skill-usage.md) | [Root](../README.md)

This guide is the operator-facing entry for using Techne Loom skills in practice.

If you want package contracts or runtime wire details, read the product guides and the skills reference after this page. This page answers a narrower question first: which skill should you use, what should you give it, and what counts as the official run surface.

## Choose The Right Entry

| Situation | Use this | Read first | Official run surface |
| --- | --- | --- | --- |
| The route is still unclear and you need exploratory orchestration | `/loom-plan-execution` | `packages.released.md` or `packages.beta.md`, then `dotnet ao.dll --guide` for Loom Agent Execution Orchestrator | `dotnet ao.dll run` and `dotnet ao.dll resume` |
| You want to create or upgrade a deterministic skill | `/loom-skill-enhancement` | `packages.released.md` or `packages.beta.md`, then `dotnet so.dll --guide` for Loom Skill Orchestrator | after enhancement, official target-skill runs are `dotnet so.dll run` and `dotnet so.dll resume`; `compile` is validation only |
| You already have a Loom-governanced target skill and want to use it day to day | the target skill itself | the target `SKILL.md` plus `assets/so-workflow/so-package-lock.json` | `dotnet so.dll run` and `dotnet so.dll resume` against a runtime workflow copy |

## Shared Setup Rules

1. Run [Platform Detection Steps](../reference/runtime/platform-detection.md) before runtime acquisition. A governed skill uses its owning locked exact version, CI/CD-managed version block, or checked-in runtime lock as the only version authority; direct callers choose released or beta from the package index.
2. Use the dual-mode runtime contract. Self-contained is the default: resolve the detected RID and restore one exact matching runtime package, then use its direct executable launch descriptor. `.NET CLI mode` is explicit through `runtimeBinding` or an explicit bundle directory; when selected, require a usable `Microsoft.NETCore.App 9.x` host and restore the exact .NET runtime bundle (a NuGet restore set that includes Roslyn). A `.NET CLI` host failure fails closed and does not switch modes.
3. Self-contained packages need no preinstalled .NET runtime, but they still require the target OS and ABI. Unsupported RIDs fail fast; no cross-architecture or neighboring-version fallback is allowed.
4. Both modes must run a fresh `--guide`, verify the emitted JSON version and readable `guide_path`, and reuse the same launch descriptor, exact runtime version, and RID for `compile`, `run`, and `resume`.
5. Keep compile artifacts, audit artifacts, runtime workflow copies, session folders, and event sidecars outside checked-in skill directories unless the user explicitly chooses another output root. Valid exact-version cache entries may be reused offline; missing valid cache plus unavailable network is a blocking result.
6. Use NuGet.org exact V3 package URLs first. Use the same-version official GitHub release asset only after the exact NuGet package cannot be acquired, and apply the same hash, manifest, ZIP-safety, and entry-point checks.
## `/loom-plan-execution`

Use `/loom-plan-execution` when the outer agent still needs to explore, clarify, compare frontiers, or delegate focused work before the route is stable.

### Inputs For Loom Agent Execution Orchestrator Skill

- a rich plan with at least 10 non-empty lines, or a detailed plan file path
- localized skill prose and package-index links may use `en` or `zh-cn` where those surfaces exist; the runtime `--guide` command itself is English-only and returns the English bundle path JSON
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

Use `/loom-skill-enhancement` when you want to create a deterministic skill, upgrade an existing skill into a Loom-governanced skill, or push a skill already enhanced by Loom Skill Orchestrator into exclusive Loom Skill Orchestrator governance mode.

### Inputs For Loom Skill Orchestrator Enhancement

- target skill path or target repository path
- deterministic goal or upgrade request
- requested target-skill changes to create or modify in this enhancement pass
- runtime version authority: reuse the checked-in `assets/so-workflow/so-package-lock.json` plus the current skill package version block, and derive `released` versus `beta` from that bound version when needed
- localized skill prose and package-index links may use `en` or `zh-cn` where those surfaces exist; the runtime `--guide` command itself is English-only and returns the English bundle path JSON
- optional JSON context file
- optional audit output root

### What It Produces

- `<execution-output-root>/plan/skill-plan.md` (runtime-owned per-run plan reference, not a stable target-skill asset)
- a checked-in workflow template under `<target-skill-root>/assets/so-workflow/`
- `<target-skill-root>/assets/so-workflow/so-package-lock.json`
- an updated target `SKILL.md` that explicitly references the lock file, the Loom Skill Orchestrator governance model, and the requirement that the default governed success path continues onto public `dotnet so.dll run` / `resume` until final `Done`

### Loom Skill Orchestrator Enhancement Demo

`{agentskillfolder}/...` below is an agent-neutral placeholder for an external target-skill root. Replace it with the real skill folder used by your agent or host. Use `.agents/skills/...` only when you are explicitly referring to this repository's built-in skills or built-in manifest catalog.

```text
/loom-skill-enhancement
Bound runtime version: <current skill package version>
Language: en
Target: {agentskillfolder}/my-target-skill
Goal: upgrade this skill into a Loom-governanced skill under exclusive Loom Skill Orchestrator governance, with a checked-in workflow template and a locked runtime bundle
Requested target skill changes:
- refresh SKILL.md governance wording
- write or refresh the per-run plan under <execution-output-root>/plan/skill-plan.md
- create or refresh the checked-in workflow template
- create or rewrite assets/so-workflow/so-package-lock.json
```

Three concrete call patterns are documented in [Loom Skill Enhancement Call Examples](../examples/skill-enhancement-calls.md).

Workflow-template governance baseline:

- workflow templates must use explicit governed steps, guards, seams, and reviewable outputs
- workflow templates must never contain a node purpose or node intention that says or implies `run a multistep plan`
- workflow template review must look for any node instruction that embeds a multistep plan or a broad prompt to an agent, then break that intent into smaller governed nodes when possible

## Governed SO Entry

For every Loom Skill Orchestrator-governanced target-skill verification, including `/loom-skill-enhancement` self-bootstrap, the local MCP server is the first external interface after exact published runtime preflight.

1. Start the selected published runtime with `dotnet so.dll mcp stdio` or its validated self-contained equivalent.
2. Complete `initialize` and the `notifications/initialized` notification.
3. Call `so_inspect_workflow_fragment` against the same external workflow copy and preserve the bounded result.
4. Only after `mcp_startup_evidence` is complete may the workflow capture `--guide` and continue to planning, authoring, validation, compile, run, or resume.

This is a governed workflow step, not a request to configure the current editor's `mcp.json`. If MCP cannot start or the fragment call fails, stop the saved workflow at failed preflight; direct CLI or local orchestration cannot bypass it. MCP calls support verification but do not replace the official `dotnet so.dll run` / `dotnet so.dll resume` chain.

### What Counts As An Official Run After Enhancement

- the enhancement pass may use `dotnet so.dll compile` as a validation step before governance is finalized
- when the enhancement pass executes the target-skill workflow, the official target-skill run surface is `dotnet so.dll run` and `dotnet so.dll resume`
- once the target skill is under exclusive Loom Skill Orchestrator governance, only `dotnet so.dll run` and `dotnet so.dll resume` count as official target-skill runs
- if a creation or re-enhancement slice stops after guide refresh, checked-in asset updates, and compile validation, the correct status is an in-progress or blocked enhancement slice rather than governed completion

Direct CLI snippets, MCP calls, or prose explanations do not become official runs by themselves.

## Using A Loom-governanced Target Skill

Once a target skill has switched into the Loom Skill Orchestrator governance type, treat it as a Loom-governanced target skill rather than a generic prompt-only skill.

### Day-To-Day Run Order

1. Read the target `SKILL.md`.
2. Read `assets/so-workflow/so-package-lock.json` and restore the exact locked Loom Skill Orchestrator runtime bundle from NuGet.
3. Keep the checked-in workflow template clean. Clone it to a runtime workflow copy outside the skill folder.
4. Start `dotnet so.dll mcp stdio`, complete the handshake, and call `so_inspect_workflow_fragment` against that same runtime copy. Keep `mcp_startup_evidence`; do not use the current editor `mcp.json` as proof.
5. Run `dotnet so.dll run --workflow-file <runtime-copy-path>`.
6. If Loom Skill Orchestrator blocks, follow `skill_hint`, preserve `memory_for_next_step`, and resume with `dotnet so.dll resume --workflow-file <runtime-copy-path> --result-file <path>`.

### Minimal Demo

```text
Read SKILL.md -> read assets/so-workflow/so-package-lock.json -> restore exact locked Loom Skill Orchestrator runtime bundle -> clone checked-in template -> start and use dotnet so.dll mcp stdio -> capture guide -> run dotnet so.dll run -> follow blocked seam -> dotnet so.dll resume
```

### What Not To Do

- do not silently float to a newer Loom Skill Orchestrator package version inside the same channel
- do not restore only `Techne.Loom.SkillOrchestrator`
- do not point `run` or `resume` back at the checked-in source template
- do not treat direct CLI or direct MCP execution as a peer official run surface once the target skill is under exclusive Loom Skill Orchestrator governance

For a target skill that is already Loom-governanced, the stable status wording should be that the target skill is a Loom-governanced target skill and that its official execution surface is the public `dotnet so.dll run` and `dotnet so.dll resume` path against a runtime workflow copy. Treat compile-only or compile-validated states as intermediate enhancement milestones, not as normal governed completion wording.

When a repository also keeps demo timelines or recorded-slice narratives, treat those pages as historical records rather than as the authority for the current completion contract. The authority stays with the target skill's checked-in `SKILL.md`, `contract.json`, and `assets/so-workflow/` surfaces.

## Deeper References

- [Agent Integration](agent-integration.md)
- [Skill Integration](skill-integration.md)
- [Loom Agent Execution Orchestrator Guide](ao-guide.md)
- [SkillOrchestrator Guide](so-guide.md)
- [Skills Input/Output Reference](../reference/skills.md)
- [Loom Skill Enhancement Call Examples](../examples/skill-enhancement-calls.md)
- [Loom-Governanced Skill Run Example](../examples/so-enhanced-skill-run.md)
