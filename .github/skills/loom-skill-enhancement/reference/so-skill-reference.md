# Loom Skill Orchestrator Skill Local Reference (Offline)

This document holds the detailed rule set referenced by `/loom-skill-enhancement/SKILL.md`.

## Workflow Designer Subagent

Use this exact local workflow-design subagent whenever `/loom-skill-enhancement` needs to create or revise workflow JSON:

- [../assets/agents/loom-skill-enhancement-workflow-designer.agent.md](../assets/agents/loom-skill-enhancement-workflow-designer.agent.md)

Pass relative links to the target `SKILL.md`, workflow template, package lock, guide file, package-index file, audit artifacts, and any blocked seam evidence so the subagent can run independently from repository-global docs.

The subagent must generate node-level granularity where each node owns one visible responsibility and every SO weave-out path has a detailed blocked-action hint, including file/path context when relevant.

If the enhancement flow introduces a target-skill local `.agent.md` file for a reusable weave-out, that file must also be linked by relative path from the target `SKILL.md` and from the workflow template JSON weave-out hints or equivalent `skill_hint` guidance.

Current reusable local weave-out subagents owned by `/loom-skill-enhancement` are:

- [../assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md](../assets/agents/loom-skill-enhancement-skill-markdown-gap-review.agent.md)
- [../assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md](../assets/agents/loom-skill-enhancement-package-lock-gap-review.agent.md)
- [../assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md](../assets/agents/loom-skill-enhancement-workflow-governance-gap-review.agent.md)
- [../assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md](../assets/agents/loom-skill-enhancement-scope-input-output-analysis.agent.md)
- [../assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md](../assets/agents/loom-skill-enhancement-route-gate-analysis.agent.md)
- [../assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md](../assets/agents/loom-skill-enhancement-evidence-node-map-analysis.agent.md)

When one of these subagents already matches the weave-out goal, prefer it over creating a new generic review node.

## Enhancement Scope

- Enhancement business outcome is target-skill creation or modification.
- Runtime-only verification cannot be reported as final enhancement completion.
- Every enhancement pass must first prove that the selected published Loom Skill Orchestrator runtime is runnable and can emit a fresh `dotnet so.dll --guide [--lang <language>]` result from that runtime before editing, validating, compiling, running, resuming, or collecting downstream inputs for target-skill deliverables.
- When the target project does not already have its own dependencies installed, install only the minimum dependency set required for the requested target-skill changes and current guide-aligned validation work.

## Runtime Acquisition

- In package-channel mode, restore the Loom Skill Orchestrator runtime bundle together at one resolved version:
  - `Techne.Loom.SkillOrchestrator`
  - `Techne.Loom.Common`
  - `Techne.Loom.Abstractions`
- For `/loom-skill-enhancement` itself and any SO-enhanced target skill, official workflow operations must use the published SO package artifacts restored from the selected channel. Do not treat repository source builds, local debug outputs, or hand-assembled runtime folders as the normal workflow-operation path.
- Build one unified runtime directory and execute Loom Skill Orchestrator commands from that directory only.
- Do not execute from partial single-package extraction roots.
- Every new official SO run must begin from a freshly copied runtime workflow file outside the skill folder. Resume in that same execution chain must continue against the same persisted runtime copy. Do not reuse the checked-in template itself as the mutable execution file.

## Re-Enhancement Upgrade Gate

When the target skill is already enhanced by Loom Skill Orchestrator (`SO-enhanced`):

- ask one user question with exactly two choices: latest released or latest beta
- do not silently reuse the old lock channel or old locked version as the upgrade decision
- reacquire the latest Loom Skill Orchestrator package from the user-confirmed channel
- prove the selected published Loom Skill Orchestrator runtime is runnable and run `dotnet so.dll --guide [--lang <language>]` from that selected package before any new enhancement edits or downstream steps
- strongly recommend a subagent review that compares the current target skill and Loom Skill Orchestrator workflow assets against that latest guide result before editing

## Workflow Template Governance Baseline

- Before editing target-skill deliverables, first prove the selected published Loom Skill Orchestrator runtime is runnable and capture a fresh guide result from that runtime, then run a plan-first pass when the platform supports it.
- The plan-first pass must analyze inputs, outputs, state nodes, transition groups, guards, branches, loops, user seams, runtime seams, validation gates, and expected output evidence.
- The workflow template JSON is the authority. Mermaid, HTML, localized prose, and review plans are presentation surfaces and must be regenerated or kept aligned after template feedback.
- For `/loom-skill-enhancement` and any SO-enhanced target skill, ordinary workflow governance must remain on the `dotnet so.dll --guide`, `compile`, `run`, and `resume` path. Do not treat checked-in workflow JSON as a freeform direct-edit surface.
- For `/loom-skill-enhancement` and any SO-enhanced target skill, every new official SO run must recopy the execution workflow from checked-in source assets into an external runtime file before execution begins, while resume must continue against the same persisted runtime file produced by that run chain.
- Direct edits to the running external workflow `.json` copy are allowed only when the current SO path is fully blocked, the user explicitly approves a narrow workaround, the change is the smallest one that unblocks the next `dotnet so.dll` step, and the operator immediately returns to `dotnet so.dll compile`, `run`, or `resume`.
- User feedback during planning must update the workflow template or its source planning inputs. Do not accept a Mermaid-only change as a real workflow change.
- `dotnet so.dll compile` emits Mermaid Markdown, HTML, workflow JSON backup, and `workflow.analysis.json` under the audit root. The analysis report is evidence for the plan review.
- Mermaid node backgrounds should use stable light color families derived from step kind semantics plus owned-input metadata: AI/model/subagent green, code/tool blue, user-owned optional branch choices yellow, required user input red, generic conditional branches amber/yellow, and gate/governance states white or very light gray.
- Enhancement outputs should include a node-to-file or node-to-artifact map from workflow node ids to the target files, generated artifacts, or audit evidence they govern.
- When the slice keeps checked-in source assets as the authoritative business deliverables, the workflow should separate those checked-in assets from runtime-owned completion artifacts. A runtime-owned completion manifest may reference checked-in source assets, but that does not by itself replace the checked-in deliverable.
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
- Official workflow operations for `/loom-skill-enhancement` and any SO-enhanced target skill must be executed from published SO package artifacts for the chosen channel unless a blocked-state emergency exception was explicitly approved.
- Enhanced target `SKILL.md` files must say that ordinary workflow changes stay on the SO CLI path and that direct workflow JSON edits are blocked-state-only, user-approved emergency workarounds.
- Direct CLI and MCP are primitive/component paths only.

## Think-Out-Loud Required Fields

Report runtime fields once runtime is prepared and on each progress update:

- `resolved_runtime_version`
- `runtime_bundle_packages`
- `unified_runtime_directory`
- `runtime_preflight_result`
- `package_channel_launch_mode`

Report audit fields on each progress update:

- `mermaid_file`
- `html_file`
- `analysis_file` when present

## Delivery Completion Gate

- Completion requires requested target-skill deliverables to exist and governance wording to be aligned.
- Runtime validation artifacts alone cannot serve as sole completion evidence.
- For SO-governed target-skill templates, completion also requires the governed validation contract, route-aware business-output gates, and seam ownership declarations to be present and compile-clean.
- Completion evidence for enhanced skills should cite the final workflow template, compiled Mermaid, workflow analysis report, confirmation-loop result, and node-to-file or node-to-artifact map.
- Completion evidence should also distinguish three categories explicitly when they differ: checked-in source deliverables, runtime-owned temporary artifacts, and runtime-owned completion manifests that reference checked-in source deliverables.
