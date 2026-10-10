---
name: loom-ask-user
description: "Primary workflow-independent agent-facing consumer of shared XO Ask capability in AO/SO runtime binaries. Turns a caller's questions into one structured ask session and returns typed answers plus a receipt. Does not require a workflow."
---

# /loom-ask-user

<!-- skill-package-version-block:start -->
- Current published AO/SO runtime release version: `0.3.335-beta`.
- This is the exact shared AO/SO release-set version; it is not a feature-availability claim.
<!-- skill-package-version-block:end -->


Use this skill first for agent-facing clarification and structured user input. Its contract is standalone: it accepts questions from the calling agent, starts an ask session through an AO or SO runtime binary, and returns validated answers and a receipt. It does not require or create a `WorkflowInstance`, an `AskUser` node, an active workflow wait, or a workflow context. When multiple questions are already knowable, put them in one ordered form and collect one submission; ask later only for genuinely dependent follow-up.

## XO Ask and Its Consumers

`XO Ask` is shorthand for the shared ask capability exposed by either the existing AgentOrchestrator (AO) or SkillOrchestrator (SO) apphost. It is not a third product, package family, or `xo.exe` binary.

| Surface | Responsibility | Requires a workflow? |
| --- | --- | --- |
| XO Ask in an AO/SO binary | Shared question contract, browser session, validation, local drafts, and receipt | No |
| `/loom-ask-user` skill | Agent-facing peer consumer: shape the caller's questions, start XO Ask, and return answers plus receipt | No |
| `AskUser` workflow node | Workflow-facing peer consumer: declare node questions, map the receipt into workflow-owned context, and let the owning runtime resume | Yes, by definition of this optional adapter |

The skill and the node consume the same infrastructure independently. The skill does not call or depend on the node; the node does not call or depend on the skill. A workflow node may preserve `CommandTransition.UserInput`, `requiredInputs`, and SO ownership validation as its own projection contract. Those fields are not prerequisites for standalone skill use.

## Current Published Capability

As of the published `0.3.334-beta` package set, the verified SO apphost help does not expose a standalone `ask` command; the AO apphost from the same runtime source line also has no such command. This is an implementation gap, not a reason to redefine the skill as workflow-bound. Do not claim that `.334-beta` can complete the standalone route. Do not silently create an `AskUser` workflow node or fall back to an agent-native question surface to mask the missing binary capability; report the exact runtime limitation.

When a published AO/SO apphost exposes the standalone ask entry point, use the route below.

## Route

1. Collect the caller's independent questions in their intended order. Preserve each prompt's intent, context, answer type, options, required state, and constraints; do not require workflow context paths.
2. Resolve the exact version from this skill's published AO/SO release-set version block and detect the host RID.
3. Reuse a verified same-version AO or SO package from the standard NuGet cache when available. If neither product package is cached for that exact version and RID, acquire the exact AO package first. Never resolve a floating `latest` version or use a local/test build as release authority.
4. Validate package ID, exact version, RID, hash, runtime manifest, safe ZIP paths, and apphost. Invoke the matching self-contained apphost directly; capture and validate a fresh `ao --guide` or `so --guide`, then confirm the selected apphost exposes its standalone ask entry point.
5. Write the complete versioned contract to a disk file, then start it with `ao ask start --contract-file <path>` or `so ask start --contract-file <path>`. Present only a host-approved browser route; keep the pairing URL out of ordinary progress text, logs, and audit output.
6. After the user submits, query the same binary with `ao ask result --ask-id <id>` or `so ask result --ask-id <id>`. Return typed answers plus the receipt when submitted. Do not run workflow resume from this skill.

## Answer Semantics

Choose answer types by meaning and use [answer semantics](reference/answer-semantics.md). A default prefills a control but does not satisfy a required answer. The shared server validator is authoritative. Each result is keyed by stable question ID; standalone answers do not need `contextPath` bindings.

## Optional Workflow Node Adapter

When a workflow explicitly uses its `AskUser` node, that node may consume the shared XO Ask service. In that path, the node owns the workflow-specific contract: `contextPath` bindings, `requiredInputs`, and (for SO) `validation.declaredUserOwnedFields`. The owning AO/SO runtime validates the receipt, projects answers into the same workflow copy, and resumes it. This is a sibling consumer path, not a prerequisite or implementation dependency of `/loom-ask-user`.

The skill bundle lives at `.agents/skills/loom-ask-user/`, separate from the NuGet runtime `.nupkg`; the shared ask implementation is delivered by the AO/SO runtime release set, not by a skill-specific binary. See [runtime dependency](reference/runtime-dependency.md) and the [AskUser guide](../../../docs/en/guides/ask-user-guide.md).

## Safety

- Pairing URLs are secrets. Pass them only to the approved browser capability; never repeat them in normal progress messages, logs, or audit summaries.
- Use only host-approved routes. Never infer a public URL from request headers or create a tunnel.
- Do not fetch user-provided remote URLs as attachments. Use the supported local-byte upload path.
- Keep attachments within configured size/type limits and synthetic for automated probes.
