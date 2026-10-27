## Weave-Out Rules

AO weave-out design must be explicit and detailed.

For every branch that can weave out:

- If an existing agent or subagent can already complete the weave-out goal, prefer that subagent route over a generic agent-shaped placeholder node.
- Give the branch a concrete reason and explicit blocked seam.
- Make the blocked hint detailed enough that the caller knows the exact next action.
- When possible, point the hint to concrete local references using relative links.
- If the weave-out depends on a guide rule, cite the local guide file and the exact section title or nearby heading in prose.
- If the weave-out depends on a workflow or plan artifact, name the exact expected file path and the expected payload shape.
- If the weave-out depends on business deliverables, state which deliverable is missing and why AO cannot continue without it.

### Weave-Out Citation Contract

Every AO weave-out response must include a minimal `evidence_references` manifest for the documents that caused or support the next action. Do not dump the full context pack or cite every file that was read. Include only the entry document, the necessary workflow or plan files, and the specific guide evidence that controls the decision.

Each citation must contain verified 1-based inclusive line numbers:

```json
{
	"path": "relative/path/to/file.md",
	"start_line": 1,
	"end_line": 12,
	"role": "why this excerpt is required"
}
```

Use workspace-relative or runtime-output-relative paths, never absolute machine paths. Verify line numbers from the exact file content used for the weave-out; never estimate them. When a guide is involved, cite the successful `guide_path` returned by the JSON result and its output lines. Citing only the guide source location is insufficient. The command does not export a guide file; a weave-out without verified `evidence_references` is incomplete and must not be woven back as successful evidence.

Keep each weave-out response compact: return only the next action or decision, the minimal citation manifest, and the resume payload contract.

## Required AO Weave-Out Families To Consider

When relevant, explicitly model these AO seam families rather than hiding them inside broad nodes:

- clarification-required seams
- tool-probe-required seams
- delegation-required seams
- weave-out-required seams for external comparison, planning, or decision work
- replan-required seams after a failed or stale frontier choice
- completion-claim seams when business evidence is still missing

If a requested workflow could hit one of these families, either model it as a node or explain why it does not apply.

## Output Requirements

When you generate a workflow or workflow revision, ensure it includes:

- explicit node ids and transition ids
- explicit weave-out seam nodes or transitions
- detailed `skill_hint` / blocked-action intent in node descriptions or attached artifacts
- enough node detail that Mermaid and audit analysis show real operational structure
- no silent dependency on external docs beyond the context pack and prompt-provided files

## Output Structure Requirement

Before emitting the final workflow JSON, provide a concise preflight block that lists:

- transition checklist: one line per transition with `id`, guard predicate, success predicate, and output evidence
- gate checklist: one line per gate with pass predicate and required evidence
- ownership checklist: all `AskUser` fields and confirmation that each is user-owned
- weave-out citation checklist: every weave-out has minimal `evidence_references` with verified `path`, `start_line`, `end_line`, and `role`

If any checklist item is missing concrete predicates or evidence paths, revise before finalizing.

## Output Hint Guidance

When producing a workflow template proposal, also provide guidance for these companion outputs when relevant:

- workflow JSON path
- Mermaid review artifact
- HTML review artifact
- workflow analysis artifact
- node-to-file or node-to-artifact map
- blocked seam payload examples
- resume envelope examples

## What To Avoid

- Do not produce a one-node planner.
- Do not hide weave-out decisions in narrative prose only.
- Do not collapse prompt-plan, prompt-replan, run, and resume into one generic execution node.
- Do not assume repo-global docs will be available later.
- Do not leave “agent decides details” as a hidden subflow inside a node.

## Published Runtime Transport Rule

Published AO, SO, and governed skill workflows never require MCP registration for package acquisition or guide capture. AO is CLI-only. A later SO step may use local MCP when useful, bound to the current apphost identity; do not require a resolver descriptor or add a pre-guide inspection gate.
