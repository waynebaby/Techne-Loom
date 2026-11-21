# Collaboration and Ownership Contract

## Multi-turn collaboration

For collaborative planning, workflow design, repair, and validation that need follow-up turns, prefer an available interactive subsession over a read-only subagent. A read-only role may inspect or review, but it cannot edit; never route implementation or repair to it.

Support all three modes without changing their authority: `from-blank`, `workflow-to-csharp`, and `modify-existing`. Apply the confirmed caller order: `/loom-skill-enhancement` integration before AO Planner integration.

Before any role is engaged, verify its declared capability and complete role contract. Do not infer edit authority from a filename or role label.

## Fan-out gate

Fan out branches only when all of the following are true:

1. Branches have no dependency on one another's results.
2. The host can actually dispatch them concurrently.
3. Each branch has isolated state and a distinct output path.
4. The available resource/concurrency capacity can run the batch.

If any condition fails, execute the affected branches sequentially. Preserve the selected strategy semantics rather than replacing the strategy to make serialization convenient. `firstPass`, `whenAnyResponse`, and `whenAnyPass` route to the selected transition target; aggregation strategies require an explicit join target; no-pass and execution-error routes remain distinct. When an early winner cancels other work, request cancellation; if physical cancellation is unavailable, persist logical cancellation and reject late results.

## External child-adapter gate

An independent child strand for workflow-to-csharp or modify-existing may be started only by a separately verified external adapter contract. The contract must cover start, status/inspect, child resume, result return, correlation and retry identity, strongest-earned partial output, runtime-owned resume fields, audit references, and duplicate/late-result handling.

No such adapter contract is established by this seed. Until it is present, preserve the strongest valid partial artifact and emit a structured blocker; do not invent a child session, resume descriptor, runtime-owned path, or audit link.

## Input and output ownership

- Treat the caller's requirements, existing workflow JSON, reference files, and user-authored inputs as read-only unless their owner explicitly authorizes a change.
- Create builder/editor/verifier source, complete input files, candidate JSON, and audit evidence at separate external paths. Do not overwrite an existing file or place generated run output in the checked-in skill directory.
- For skill-owned workflow assets, enforce the `<ownerSkillSlug>--<purpose>.json` template/first-copy rule in [Workflow Authoring](./workflow-authoring.md); resume must reuse the same persisted copy and case/run lineage.
- Let the selected apphost own workflow JSON parsing, serialization, canonical candidate output, and its own verification/compile reports. Do not turn a declared output path into input content.
- Before editing, record the exact baseline branch/HEAD, intended files, owners, and allowed write paths. Recheck that baseline and ownership at each write boundary.
- Preserve all unrelated and concurrent work. Review diffs only within the authorized target scope; never reset, restore, or overwrite work to make the tree appear clean.
- In this repository, the local skill path is `.agents/skills/loom-flow-weaver/`. Do not write to an alternate skill location or modify a caller, package lock, workflow template, runtime source, public docs, or plan unless separately authorized.
- Do not commit or push without explicit authorization.

## Unresolved decisions and blocked states

Do not guess a business decision, role authority, package version, runtime contract, output owner, approval, adapter behavior, or runner behavior. Record each unresolved item with evidence, impact, required input/approval, and next action.

A structured blocker includes `status=blocked`, `evidence`, `strongest_earned_output`, `unresolved_user_decisions`, `required_inputs`, `decision_scope`, `resume_descriptor`, and `next_action`. Keep `resume_descriptor` runtime-owned and leave it null or unresolved unless verified.

A baseline mismatch, missing exact runtime contract, unavailable approved dependency source, missing runner/adapter contract, failed verifier/compile, or missing human approval is a blocker, not success. Preserve original inputs and diagnostics; do not retry an unchanged action.

When delegated, send parent-owned decisions and exact evidence to the parent and wait for resolution before dependent edits. Report candidate creation, verification, compile, source writeback, and required run/resume completion as separate milestones.
