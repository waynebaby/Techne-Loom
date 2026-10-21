# Plain-Language Feedback Contract

Read this file when writing progress, blocked, error, completion, or user-question text for SO or an skill being enhanced.

## Plain-Language Feedback For Every Language

All user-facing progress, blocked, error, and completion updates from SO and every skill being enhanced it creates or updates must be understandable to a high-school reader with no workflow background, in the language requested by the user. English is not automatically plain language, and the rule applies equally to every supported language.

Every skill being enhanced must also require each post-execution update to include localized headings for execution confidence and estimated overall progress in that order, each with one short reason. Confidence estimates the likelihood that the requested work will be completed successfully; estimated overall progress measures approximate completion of the whole request. Base both on the latest verified evidence. No fixed formula or threshold is prescribed, and these percentages never replace required deliverables, evidence gates, or the final workflow completion state. Whenever a skill being enhanced runs or reports an AO/SO workflow execution, require the fixed Mermaid, HTML, Analysis, and Dataflow report in that order. Each title must link to a complete local file URL the active host has verified it can open: Windows `file://C:/...`, Linux/macOS `file:///...`. Encode every space as `%20` and repeat the identical encoded URL in the following text fence. If a file URL cannot be opened, create and verify a mirror and use its encoded file URL. Never include literal spaces in the URL, omit the scheme, truncate the URL, substitute a code-fenced path for a clickable link, or reuse an unverified stale address. If no URL opens, follow delivery-failure guidance without artifact links. Require the target `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints to preserve this exact format and address rule.

Use short sentences, familiar words, and direct verbs. Say four things in order: what happened, whether the user's work or data is still safe or what result remains valid, why it happened, and exactly what will happen next.

Do not make the reader translate status values, step kinds, node IDs, gate names, handoff terms, runtime details, or audit jargon. Explain a necessary technical word in ordinary language before showing its exact name. Keep commands, paths, IDs, and payload fields in a separate `Technical details` line only when they help the user act or verify the result. Never use workflow-only labels such as `FPx`, `xxx_preflight_xxx`, node IDs, gate IDs, or internal field names as the user-facing explanation; keep exact identifiers in technical details or evidence only. When this skill creates or updates a skill being enhanced, copy the same rule, a compact term-conversion table, and at least one before/after example into the target `SKILL.md`, user-facing subagent prompts, failure guidance, and workflow hints.

### Per-Execution Progress Log

For `/loom-skill-enhancement` and every skill it creates or updates, write each complete post-execution Markdown progress report to a separate entry file, including localized confidence/progress headings, the verified artifact report, an ISO 8601 UTC timestamp, and a local timestamp with numeric UTC offset and OS timezone ID. Resolve the reporting skill's root to an absolute path and invoke its own helper from `assets/scripts/AppendProgressReport.ps1` or `assets/scripts/AppendProgressReport.sh`. The helper reads only the entry file, append-writes `<execution-output-root>/progress-report.md`, and prints its full file URL; use that URL verbatim if reporting the log address. Never read, open, or inspect existing target content, and never use manual append or read-modify-write. If the helper fails, preserve prior entries, report the failure, and pause later business-step progression without changing workflow execution state.

## Plain-Language Term Examples

The left column is for machine records. The right column shows the meaning to express to a user. These English sentences are examples, not fixed output: translate the meaning into the user's requested language. English is not a substitute for simple language in another locale.

| Internal wording | Say this in the user's language |
| --- | --- |
| `Done` | "The requested work is complete." |
| `WaitResume` | "I need your information or confirmation before I can continue." |
| `SubagentCall` | "A specialist is checking this part." |
| `gate` | "A required check has not passed yet." |
| `transition` | "The next step is to ..." |
| `seam` or `boundary` | "The work is waiting at a handoff point." |
| `frontier` | "There are a few possible next actions." |
| `runtime` | "the program that is running the task" |
| `render unchanged` | "The earlier diagram is still valid; no new diagram was needed." |

### Example: output folder already has a record

Internal note: `step-0008-compiled` already exists; the render is unchanged.

User-facing update: "The task itself is fine. The output folder already has the earlier record, so this run did not overwrite it. The earlier diagram and report are still valid. I will use a new output folder and continue the same saved run."

### Example: the review found unresolved problems

Internal note: the review returned four findings, but the clean-review check was claimed before classification and repair.

User-facing update: "The review found four problems. The process stopped too early because it treated the review as finished instead of checking whether the problems were fixed. I will sort and fix the four problems, then check again."

### Example: waiting for a decision

Internal note: the task is waiting at a user-input step.

User-facing update: "I need one decision from you before I can continue: [state the decision in one short sentence]."

Do not copy the internal note into the user-facing update. Keep exact commands, paths, IDs, and evidence fields in a separate `Technical details` section when they are needed for action or verification.
