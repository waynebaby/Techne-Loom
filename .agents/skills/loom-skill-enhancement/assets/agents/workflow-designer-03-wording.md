## Workflow File Language

Workflow definition files are the canonical English information carrier across AO, SO, and skills being enhanced under Loom Skill Orchestrator governance. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.

For every state, pair a checkpoint code with a concise business-readable `name` (for example, `CK1 - Concept direction review`), use `description` to explain its business purpose and outcome, and write `workflowPhase` with a stable stage identifier plus a readable phase name. Never rely on the code alone to explain a checkpoint.

On successful compile, preserve the complete Mermaid graph fence and append the phase-grouped business summary table to `workflow.mermaid.md`. Treat `workflow.html` as the audit report for that same compile: it may summarize only the recorded compile feedback, workflow analysis, and dataflow evidence. Keep workflow JSON and its JSON audit sidecars as the machine-readable evidence source.


- Use the published-runtime, package-channel, and launch rules from the linked local skill reference and the successful guide; do not create a second runtime authority in the workflow design.

## Plain-Language Wording For Every Language

- Write every user-facing `skillHint`, `failureGuidance.summary`, `failureGuidance.nextAction`, progress message, and completion explanation in the user's requested language. Include at least one ordinary-language example when a hint explains a block or error.

- Make the first explanation understandable to a high-school reader with no workflow background. English is not automatically plain language. Use short sentences, familiar words, and direct verbs.

- State four things in order: what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and exactly what happens next.

- Do not lead with status values, step kinds, node IDs, gate names, handoff terms, runtime details, or audit jargon. Explain a necessary technical word in ordinary language before showing its exact name. Keep exact commands, paths, IDs, and evidence fields in a separate technical-details line only when they are needed for action or verification.



## User-Facing Text Review Gate

Before returning a workflow or workflow revision, inspect every string that a person may see. This includes:

- `skillHint` / `skill_hint`, `human_or_agent_hint`, and blocked-action hints
- `AskUser` question text, title, prompt, options, and required-input labels
- `failureGuidance.summary` and `failureGuidance.nextAction`
- node descriptions, transition descriptions, progress messages, status explanations, and completion explanations

Apply this checklist to each user-facing string:

1. Write it in the user's requested language. English is not automatically plain language.
2. Use short sentences, familiar words, and direct verbs for a high-school reader with no workflow background.
3. Explain what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and what happens next, in that order when the text describes a block or error.
4. Replace internal status values, step kinds, node IDs, gate names, handoff terms, runtime details, and audit jargon with the meaning a person needs. Explain a technical term before showing its exact name.
5. State who must act and what they must provide or decide. An `AskUser` question may request only a user-owned input or decision; never ask the user for runtime-owned paths, internal statuses, gate results, or evidence fields.
6. Put exact commands, paths, IDs, and payload keys in a separate `Technical details` line only when they help the user act or verify the result.

Reject the workflow text and rewrite it when a hint only dumps a field name or command, a question asks for an internal result, a block has no reason or next action, or the first sentence starts with an internal token, node ID, path, or audit label. Add a concise `plain_language_review` checklist to the design notes or dispatch result showing that every listed field was checked.

### Before And After Examples

Bad `skillHint`: `WaitResume at gate.review; return payload for transition.review.`

Good `skillHint`: `I need the review result before I can continue. Your saved work is unchanged. Please provide the review result. Technical details: resume using the recorded transition and payload fields.`

Bad `AskUser` question: `Provide the gate result and runtime evidence for transition.review.`

Good `AskUser` question: `Please choose one: keep the current review plan, or change it. Tell me which option you want.`

Bad failure message: `compile failed because step-0008-compiled exists; render unchanged.`

Good failure message: `The task itself is fine. The output folder already has the earlier record, so this run did not overwrite it. The earlier diagram and report are still valid. I will use a new output folder and continue the same saved run.`

Do not copy the English examples into another language. Translate their meaning into the user's language while keeping the same simple order.

## AskUser Caller Hint Contract (Required)

For every `AskUser` boundary, put browser-routing and page-opening instructions in the caller-facing hint (`skillHint`, `human_or_agent_hint`, or the current runtime-supported blocked-action hint). The hint is for the initiating agent, not the person answering the form. Do not put these agent actions only in question `prompt` or `helpText`, a node description, or documentation; do not invent a schema field.

The hint must instruct the initiating agent to:

- Read `ask_user_endpoints` from the same structured result and use only endpoint URLs returned by the runtime. The current descriptor supplies `url` as the loopback endpoint. Use a reverse route only when the host explicitly returns it as an approved route candidate; do not assume that candidate support exists or create a candidate in workflow logic.
- Select a route using the embedded browser's network context, not shell or worker reachability. Use loopback only when the embedded browser can reach the worker host. Otherwise use only a host-provided, approved HTTPS reverse-proxy candidate. Never rewrite a hostname, infer a public address from untrusted forwarded headers, or create a tunnel.
- Open the selected URL itself through the initiating agent's embedded-browser capability. Never instruct the CLI, runtime, worker, or OS URL handler to launch a desktop browser.
- Treat the full endpoint URL, including any one-time pairing fragment, as a secret. Pass it directly to the embedded-browser capability; keep it out of logs, progress text, audit artifacts, and other ordinary output.
- If no returned, approved route is reachable, leave the ask pending and explain the limitation. If embedded browsing is unavailable but an approved route is reachable, provide an explicit user-action handoff using that approved route.

When bilingual AskUser design navigation is useful, use the English technical page resolved under the validated runtime `docs_root` and supplied in `referencePackManifest`; use a Chinese counterpart only when supplied for localization. Never construct docs paths relative to the installed skill.

