---
name: loom-ask-user
description: "Primary workflow-independent agent-facing consumer of shared XO Ask capability in AO/SO runtime binaries. Returns typed answers and a receipt for submitted sessions, or clearly warned agent-generated answers without a receipt when unattended. Does not require a workflow."
---

# /loom-ask-user

<!-- skill-package-version-block:start -->
- Current published AO/SO runtime release version: `0.4.12-beta`.
- This is the exact shared AO/SO release-set version; it is not a feature-availability claim.
<!-- skill-package-version-block:end -->















Use `/loom-ask-user` by default for agent-facing clarification and structured user input. Its contract is standalone: for an interactive or HTTP-submitted ask session, it returns validated answers and a runtime receipt; its unattended direct-answer fallback returns warned agent-generated answers without a receipt. It does not require or create a `WorkflowInstance`, an `AskUser` node, an active workflow wait, or a workflow context. When multiple questions are already knowable, put them in one ordered form and collect one submission; ask later only for genuinely dependent follow-up.

## XO Ask and Its Consumers

`XO Ask` is shorthand for the shared ask capability exposed by either the existing AgentOrchestrator (AO) or SkillOrchestrator (SO) apphost. It is not a third product, package family, or `xo.exe` binary.

| Surface | Responsibility | Requires a workflow? |
| --- | --- | --- |
| XO Ask in an AO/SO binary | Shared question contract, browser session, validation, local drafts, and receipt | No |
| `/loom-ask-user` skill | Agent-facing peer consumer: shape questions and return session answers plus receipt, or warned unattended answers without a receipt | No |
| `AskUser` workflow node | Workflow-facing peer consumer: declare node questions, map the receipt into workflow-owned context, and let the owning runtime resume | Yes, by definition of this optional adapter |

The skill and the node consume the same infrastructure independently. The skill does not call or depend on the node; the node does not call or depend on the skill. A workflow node may preserve `CommandTransition.UserInput`, `requiredInputs`, and SO ownership validation as its own projection contract. Those fields are not prerequisites for standalone skill use.

## Current Published Capability

Standalone `ask` support is version-specific. Before use, confirm the selected exact published AO or SO apphost's fresh help exposes both `ask start --contract-file <path>` and `ask result --ask-id <id>`. If it does not, report the exact runtime limitation; do not silently create an `AskUser` workflow node or fall back to an agent-native question surface to mask the missing binary capability.

## Route

1. Collect the caller's independent questions in their intended order. Preserve each prompt's intent, context, answer type, options, required state, and constraints; do not require workflow context paths.
2. Resolve the exact version from this skill's published AO/SO release-set version block and detect the host RID.
3. Reuse a fully validated immutable AO/SO generation from the shared current-user OS-temp cache only when exact package ID/version/RID and full `.nupkg` SHA-512 match. If neither product is valid, check the standard NuGet cache and follow the L0-L6 acquisition order in [runtime dependency](reference/runtime-dependency.md), preferring the exact AO package when both choices are equivalent. Never install a runtime or downloader or resolve a floating `latest`. After any failed request, do not automatically switch transports to repeat the same artifact/source. A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted and the user approves that exact CLI action. If the source or request is explicitly blocked or denied by network/security policy, stop all attempts to that source through every transport; continue only with an independently configured and approved fallback source, otherwise stop and report the blocker. If the user denies CLI approval, stop and do not switch tools or sources to evade that decision. Stage and validate any new or repaired generation before atomic publication; never overwrite a different valid or in-use generation.
4. Validate package ID, exact version, RID, hash, runtime manifest, safe ZIP paths, and apphost. Invoke the matching self-contained apphost directly; capture and validate a fresh `ao --guide` or `so --guide`, then confirm the selected apphost exposes its standalone ask entry point.
5. Write the complete versioned contract to a disk file, then start it with `ao ask start --contract-file <path>` or `so ask start --contract-file <path>`. Present only a host-approved browser route; keep the pairing URL out of ordinary progress text, logs, and audit output.
6. After the user submits, query the same binary with `ao ask result --ask-id <id>` or `so ask result --ask-id <id>`. Return typed answers plus the receipt when submitted. Do not run workflow resume from this skill.

## Autopilot / Unattended Runs

When the host explicitly identifies the current run as autopilot or unattended, apply this branch before the numbered interactive route, runtime acquisition, or a blocking input tool. Do not open a browser or wait for a person. If any question requires genuine human approval, do not submit an agent-generated approval via HTTP or consume the active session; return warned suggestions and leave the session and any workflow waiting.

- If this same ask session is already running, its endpoint was returned by the runtime, and it does not require genuine human approval, use only that endpoint and the runtime's supported HTTP session/submission flow. Read the active contract, generate answers that satisfy its types and constraints from the available request/workflow context, and submit them to that session. Do not start a second worker or guess a URL or credential. Keep pairing URLs and session tokens private.
- If no active endpoint is available or HTTP submission is not possible, do not start an interactive ask. Return the best-supported typed answers directly from the available context. Mark unresolved information as unknown or use a safe value only when the contract allows it. Without a submitted session there is no runtime receipt or server validation; do not fabricate either or claim that a person confirmed the answers.
- Clearly warn that the user is unavailable and the answers were generated by the agent, not confirmed by the user. When writable, preserve this explanation in caller-provided additional-context, or in the existing question `context` before a session starts; never mutate an active contract, add wire fields, or disguise the explanation as an answer.
- For a workflow `AskUser` node, use the HTTP submission path when available and genuine human approval is not required so the owning runtime creates a real validated receipt. Never fabricate a receipt or bypass the runtime's validation. If no supported noninteractive continuation exists, return the generated answers with the warning and report that the workflow remains waiting. A receipt for an agent-generated answer does not establish human approval; if genuine human approval is required, leave the workflow waiting.

Outside autopilot, continue with the normal approved browser handoff.

## AO/SO Test Execution Isolation (Required)

When testing AskUser through an AO or SO runtime:

- For apphost integration or end-to-end tests, invoke the exact runtime selected for the test directly by its normalized absolute apphost path. Record the runtime version, executable path, and working directory. Published-runtime claims require the exact published package; current-source runs are development-only evidence. Check the selected apphost's fresh help before assuming standalone `ask` support.
- Give each test lane a unique external contract-file path and storage root for AskUser state, attachments, workflow copies, event/log records, and audit outputs. Use only documented or test-supported storage-root controls and verify the resolved paths. If storage cannot be isolated, serialize the affected tests rather than sharing mutable state.
- Give every simultaneously active worker/test lane its own OS-assigned ephemeral loopback port. Use only the endpoint returned by the runtime and assert that active parallel lanes have different ports. Never hard-code or reuse one fixed port, reconstruct the endpoint, or invent a port-selection CLI flag.
- For automated readiness polling, check immediately when supported and start with the smallest valid interval. Increase the next wait only after a recorded `pending` or `not-ready` result, by exactly 1 second per retry. Keep each idle wait at or below 5 seconds and preserve runtime operation/session deadlines; do not shorten a human response wait or extend a runtime deadline through polling logic.
- Keep pairing tokens and full route URLs private and pass only the runtime-returned approved endpoint to the browser capability.

## Selective JSON Reading

For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective inspection: use search or regex only to locate the target key or path, then read a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract the needed subtree. Read the full JSON only when the user explicitly requests it or whole-document validation requires it. This rule applies to JSON data; `.agent.md` role contracts remain a separate full-read requirement.

## Answer Semantics

Choose answer types by meaning and use [answer semantics](reference/answer-semantics.md). A default prefills a control but does not satisfy a required answer. The shared server validator is authoritative. Each result is keyed by stable question ID; standalone answers do not need `contextPath` bindings.

## Optional Workflow Node Adapter

When a workflow explicitly uses its `AskUser` node, that node may consume the shared XO Ask service. In that path, the node owns the workflow-specific contract: `contextPath` bindings, `requiredInputs`, and (for SO) `validation.declaredUserOwnedFields`. The owning AO/SO runtime validates the receipt, projects answers into the same workflow copy, and resumes it. This is a sibling consumer path, not a prerequisite or implementation dependency of `/loom-ask-user`.

The skill bundle lives at `.agents/skills/loom-ask-user/`, separate from the NuGet runtime `.nupkg`; the shared ask implementation is delivered by the AO/SO runtime release set, not by a skill-specific binary. See [runtime dependency](reference/runtime-dependency.md) and the [AskUser guide](../../../docs/en/guides/ask-user-guide.md).

## Progress Report Logging

Only when the caller provides an execution-output root and this skill emits a post-execution progress report, write the complete Markdown entry to a separate file with ISO 8601 UTC and local timestamps including numeric UTC offset and OS timezone ID. Resolve this skill's root to an absolute path and invoke its bundled helper under `assets/scripts/`: Windows uses `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <active-skill-root>/assets/scripts/AppendProgressReport.ps1 -TargetFile <execution-output-root>/progress-report.md -EntryFile <entry-file>`; Linux/macOS uses `bash <active-skill-root>/assets/scripts/AppendProgressReport.sh --target-file <execution-output-root>/progress-report.md --entry-file <entry-file>`. The helper reads only the entry file and appends to the target; it prints the target's full file URL, which must be reused verbatim if reporting the log location. Never read, open, or inspect the existing `progress-report.md`, and never use manual append or read-modify-write. If logging fails, report the failure; do not invent an output root or imply that the report was saved.

## Safety

- Pairing URLs are secrets. Pass them only to the approved browser capability; never repeat them in normal progress messages, logs, or audit summaries.
- Use only host-approved routes. Never infer a public URL from request headers or create a tunnel.
- Do not fetch user-provided remote URLs as attachments. Use the supported local-byte upload path.
- Keep attachments within configured size/type limits and synthetic for automated probes.
