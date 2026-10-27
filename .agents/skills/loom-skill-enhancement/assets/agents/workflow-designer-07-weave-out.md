## Required SO Weave-Out Families To Consider

When relevant, explicitly model these SO weave-out families:

- `ModelThink` seams for non-deterministic reasoning that the runtime cannot execute directly
- `SubagentCall` seams for structured delegated analysis or synthesis
- `AskUser` seams for mandatory human decisions
- `WaitResume` seams for runtime-owned blocked waits
- blocked-state recovery seams described by the linked local skill reference, when the approved policy permits a workaround
- review-confirmation loops before a workflow becomes the authority for execution

If a requested workflow could hit one of these families, either model it as explicit nodes or explain why it does not apply.

## Weave-Out Hint Rules

Every weave-out branch must have a detailed hint.

The hint must:

- If an existing agent or subagent can already complete the weave-out goal, prefer that subagent route over a generic agent-shaped placeholder node.
- say exactly why SO cannot continue deterministically
- name the exact next artifact or decision required
- when possible, point to a concrete local file using a relative link
- when possible, name the relevant guide file, nearby section, and expected payload or artifact shape
- distinguish checked-in source deliverables from runtime-owned temporary artifacts
- avoid vague instructions such as “review this” or “handle externally” without structure

### Weave-Out Citation Contract

Every weave-out response must include a minimal citation manifest for the documents that caused or support the external action. Do not dump the whole context pack or cite every file that was read. Include only the entry document and the workflow or contract files required to continue the current boundary.

Each citation must use this shape and verified 1-based inclusive line numbers:

```json
{
	"path": "relative/path/to/file.md",
	"start_line": 1,
	"end_line": 12,
	"role": "why this excerpt is required"
}
```

Citation rules:

- Verify the line numbers from the exact file content used in this weave-out; never estimate them.
- Use workspace-relative or runtime-output-relative paths, not absolute machine paths.
- For guide evidence, cite the actual successful `guide_path` returned by the JSON result and include its guide line numbers, not only the guide source location. The command does not produce an export file; cite the captured runtime guide path that was actually read.
- Keep the manifest limited to the entry file, the necessary workflow JSON or contract files, and the specific guide excerpt that controls the decision.
- Every external boundary payload must carry the manifest under `evidence_references`; a response without verified citations is incomplete and must not be woven back as successful evidence.

For each weave-out hint, include a resume contract snippet with:

- expected `transition_id`
- optional `correlation_key` rule when needed
- required `payload` keys with ownership annotations
- minimum evidence that must exist before resume
- `evidence_references` containing the verified citation manifest above

When a weave-out for skill enhancement would clearly benefit from a dedicated reusable subagent, recommend creating a detailed local agent file for the skill being enhanced named `{target-skill-name}-{task-name}.agent.md` under `{skill-folder}/assets/` and design the workflow so that future runs can call that subagent explicitly.

When such a local agent file for the skill being enhanced is created, require both of these:

- the target `SKILL.md` must include a relative-link reference to that `.agent.md` file
- the workflow template JSON weave-out hints, blocked-action hints, or equivalent `skill_hint` guidance must reference that `.agent.md` file by relative path so the operator knows the intended subagent route

When designing a blocked-state recovery path, use the unattended-mode contract in the linked local skill reference. Model its required evidence fields and return path without restating or changing that policy here.

