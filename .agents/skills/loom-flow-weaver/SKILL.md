---
name: loom-flow-weaver
description: Author, revise, and verify Loom workflows and C# workflow assets through the selected unified `loom` apphost; three task modes and hard runner/adapter verification gates.
---

# /loom-flow-weaver

## V1 purpose and limits
This bounded seed covers three task modes: `from-blank`, `workflow-to-csharp`, and `modify-existing`.

- `from-blank` authors a new workflow with a disk-backed C# `Build(WorkflowScriptInput input)` script.
- `workflow-to-csharp` converts approved AO/SO workflow JSON into reviewable C# workflow-asset fragments and an owning workflow instance only after the runner gate passes.
- `modify-existing` preserves an external base workflow and uses `Edit(WorkflowInstance workflow, WorkflowScriptInput input)` for a separate candidate.
- Revisions may use `--mode export` to create an external `Edit` starter from an eligible draft.
- `Verify` is used only when the selected apphost exposes the documented verifier contract.
- Scripts, JSON inputs, candidates, verifier outputs, and audit evidence remain outside this skill directory.
- The unified apphost owns parsing, serialization, and canonical candidate JSON. It is not evidence of runner or adapter authority by itself.
- Workflow-to-csharp remains an explicitly gated capability: a runner contract and external child-adapter contract must be found and verified before implementation claims.
- Designers and legacy-skill updates must enforce the skill-owned file identity `<ownerSkillSlug>--<purpose>.json`: set `ownerSkillSlug` to the exact skill slug for the template and first execution copy, and migrate names and references together.
- Resume must reuse the same persisted prefixed copy and `caseId`/`runId` lineage; never rename, recopy, or recreate the execution instance to satisfy naming policy.

## Stage-specific references

Keep this entrypoint at or below 200 lines. Read the directly linked contract before each stage:

- Before workflow planning, Build/Edit/Verify authoring, or compile: [Workflow Authoring](./reference/workflow-authoring.md).
- Before multi-turn collaboration, fan-out, input/output ownership, or diff review: [Collaboration and Ownership](./reference/collaboration-and-ownership.md).
- Before runtime/dependency acquisition or AO/SO technical documentation lookup: [Runtime and Acquisition](./reference/runtime-and-acquisition.md).

## V1 procedure

1. Confirm the task mode, caller, owner, owning module, source/baseline, requested outputs, ownership, and acceptance checks. The confirmed caller order is `/loom-skill-enhancement` before AO Planner; keep their contracts separate.
2. Classify as `from-blank`, `workflow-to-csharp`, or `modify-existing`. Record unresolved decisions rather than guessing.
3. Bind one exact unified Loom apphost and fresh guide. Use AO or SO only as the owning module contract requires.
4. Author disk-backed C# Build/Edit scripts and external inputs. Use `--mode export` only to obtain an eligible external Edit starter.
5. Run a verifier only with the exact apphost contract. For workflow-to-csharp, stop at the runner gate if the authoritative C# fragment-run contract or test/audit fields are missing.
6. Compile the apphost-generated candidate with the same apphost and owning route. Compile is structural evidence only.
7. Report changed files, baseline/diff, verification/compile evidence, unresolved decisions, blocked gates, and next action.

## Collaboration and concurrency

For multi-turn collaborative planning, design, repair, or validation, prefer an available interactive subsession over a read-only subagent. Never assign edits to a read-only role. Fan out only dependency-free branches that the host can dispatch concurrently, have isolated outputs, and fit available capacity; otherwise serialize without changing the selected strategy semantics. Follow [Collaboration and Ownership](./reference/collaboration-and-ownership.md).

## Runtime Documentation References

When this skill or a declared local agent needs AO/SO runtime documentation, follow the conditional [Runtime Documentation References contract](./reference/runtime-and-acquisition.md#runtime-documentation-references). Do not invent documentation paths or copy runtime docs into this skill.

## Selective JSON Reading

For JSON over 16 KiB, locate only the needed key/path and inspect bounded windows; use a JSON Pointer or structured parser for minified or unsafe slices. Read the full document only by explicit request or for required whole-document validation. See [Workflow Authoring](./reference/workflow-authoring.md#selective-json-reading).

## Autopilot and human approval

When the host identifies autopilot or an unattended run, do not call a blocking question tool, open a browser, or wait. If the active AO `ask_user_endpoints` or SO `ask_user.endpoints` supplies an endpoint, use only its supported session/submission flow for context-supported, type-valid answers and only when genuine human approval is not required. Never guess an endpoint, start another worker, expose credentials, mutate an active contract, or fabricate a receipt or approval. If no safe endpoint is available, provide best-supported suggestions marked agent-generated and not user-confirmed; leave required values without verified evidence unresolved. Keep an absence explanation only in caller-provided additional context or an existing question `context`; do not invent wire fields. Outside autopilot, use the normal approved interaction. `/loom-ask-user` and an optional workflow `AskUser` node are separate consumers.

## Governance and formal SO status

This seed has no governing workflow template, exact runtime lock, authoritative Roslyn fragment runner contract, verified external child-adapter contract, or same-copy run/resume evidence.

- A successful workflow script compile does not prove formal SO authority.
- Formal SO use requires a template and lock, fresh exact guide evidence, and direct same-copy `loom skill run` and `loom skill resume` evidence through one success route and one blocked-to-resume route.
- The external child strand requires a separately verified adapter contract. Until it exists, emit a structured blocker rather than inventing child status, resume descriptors, session IDs, or audit paths.
