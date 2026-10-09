---
name: loom-ask-user
description: "Preferred cross-agent route for workflow-owned AskUser input. Use before an agent's built-in question surface for most requirements, decisions, and multi-input intake that can be handled by an AO/SO workflow; batch known questions into one runtime-backed form submission. Uses the owning exact runtime and creates no standalone workflow or package lock."
---

# /loom-ask-user

<!-- skill-package-version-block:start -->
- Current published SO package runtime version: `0.3.332-beta`.
- This baseline follows the published package channel; use only a version verified to include AskUser Web UI support.
<!-- skill-package-version-block:end -->

Use this Agent Skill to present a runtime-backed Web UI for structured Loom AskUser input. The UI is served by the existing AO or SO runtime at an `AskUser` wait; it is not a chat question panel or a standalone static page.

## Use This Skill When

- An agent needs requirements, decisions, constraints, or other user-owned inputs for an AO/SO business workflow. The user does not need to request a browser form.
- Before using an agent-native AskUser/question interface, prefer this route whenever the inputs can belong to an AO/SO workflow, regardless of which agent is active.
- When multiple questions are already knowable, put them in one ordered form and collect one submission. Do not split the same intake across agent-specific prompts; ask later only for genuinely dependent follow-up.
- Use the existing owner's `AskUser` wait, or put the wait in the same business workflow while that workflow is being designed or updated.
- Use typed choices, free text, numbers, booleans, files, or audio so answer semantics remain consistent across agents.

Use an agent-native conversational question surface only for an immediate clarification that does not belong to a workflow, when no suitable business workflow can own an `AskUser` wait, when the user explicitly prefers the native surface, or when the exact published runtime cannot express the needed input. Never create a throwaway workflow only to use this skill.

## Route

1. Identify the owning AO or SO business workflow. Reuse it if it exists; if it is being designed or updated, include the intake in that same workflow. Do not switch products or create a separate ask-only workflow.
2. Confirm that the owning workflow's `AskUser` wait has a typed `CommandTransition.UserInput` form contract. Keep `requiredInputs` as context paths and, for SO, preserve `validation.declaredUserOwnedFields` ownership checks.
3. Resolve the exact published, ask-capable runtime for that owner. Reuse a verified package in the standard NuGet cache or download and verify that exact version. Follow [runtime dependency](reference/runtime-dependency.md).
4. Extract the package safely, invoke its matching self-contained apphost directly, and capture a fresh `ao --guide` or `so --guide` result before compile/run or other runtime operations.
5. Run the same external workflow copy through its owning runtime. At the `AskUser` wait, the runtime serves the browser form; the initiating agent presents only a host-approved route in its embedded browser.
6. After a valid receipt is submitted, let the owning runtime validate it and perform its existing resume on that same workflow copy. Report the projected answers and distinguish a saved draft from a submitted response.

## Runtime Boundary

The Web UI depends on the AO or SO runtime. This skill has no runtime of its own, no workflow template, no SO governance run, and no independent package lock. It selects the existing workflow owner's exact ask-capable package and does not add another NuGet package family.

There is no standalone `so ask` or `ao ask` command. The runtime produces and serves the form through an existing typed `AskUser`/`WaitResume` path. If no published exact package supports the required form, report that dependency as unavailable; never substitute a local/test build, a floating `latest` alias, or an unrelated product version.

The discoverable skill bundle lives at `.agents/skills/loom-ask-user/`, like the other repository skills. It is distributed as a repository skill folder, not inside a NuGet runtime `.nupkg`. The [AskUser guide](../../../docs/en/guides/ask-user-guide.md) describes the Web UI flow.

## Answer Semantics

Select question types by meaning and use [answer semantics](reference/answer-semantics.md). Free text is not a universal second field. A default prefills a control but does not satisfy a required answer. The Common server validator is authoritative for browser submissions.

## Safety

- Pairing URLs are secrets. Pass them only through the approved structured result to the initiating agent/browser; do not repeat them in normal progress messages, logs, or audit summaries.
- Use only host-approved routes. Never infer a public URL from request headers or create a tunnel.
- Do not fetch user-provided remote URLs as attachments. Ask the user or owning agent to provide local bytes through the supported upload path.
- Keep attachments within configured size/type limits and use synthetic files for automated probes.
