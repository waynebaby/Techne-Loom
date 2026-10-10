# XO Ask Answer Semantics

Use this reference when choosing question types or reviewing normalized answers. The question's meaning controls how free text is interpreted. Standalone results are keyed by stable question ID and do not require workflow context paths.

| Question type | Native answer | Free-text behavior |
| --- | --- | --- |
| `text` | Text in the question's existing field | The same text field is the answer; do not add a duplicate fallback field. |
| `singleChoice` | One declared option | `Other` is one mutually exclusive option. Its text replaces a declared option; both cannot be selected. |
| `multipleChoice` | An array of declared options | `Other` counts as one selected option and is subject to `minSelections` and `maxSelections`. |
| `number` | A number | With a number, free text is an explanation. Without a number, nonblank free text is the answer. |
| `boolean` | A boolean | With a boolean, free text is an explanation. Without a boolean, nonblank free text is the answer. |
| `file` | One or more validated attachments | Free text may describe an attachment. Without an attachment, nonblank free text is the answer. |
| `audio` | One validated recording | Free text may describe a recording. Without a recording, nonblank free text is the answer. |

A default prefills a control but does not satisfy a required answer. Required questions cannot be skipped. The shared server validator is authoritative; a browser-side check is for immediate feedback only. Invalid submissions do not mutate ask state or consume an active ask session.

## Direct Result

The `/loom-ask-user` consumer receives the validated typed answer set keyed by question ID and an ask receipt. It returns both to the calling agent. No `WorkflowInstance`, `contextPath`, or resume operation is required for this result.

## Optional Workflow Node Projection

When an `AskUser` workflow node is the consumer, its adapter maps answers to declared `contextPath` values. For this node path only, `requiredInputs` continues to declare context paths and every user-owned SO answer path must also appear in `validation.declaredUserOwnedFields`. The owning AO/SO runtime validates the receipt and performs the existing resume against the same canonical workflow copy. The worker never locks, mutates, or resumes a `WorkflowInstance`.
