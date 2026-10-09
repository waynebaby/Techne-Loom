# AskUser Answer Semantics

Use this reference when selecting question types or reviewing normalized answers. The question's meaning controls how free text is interpreted.

| Question type | Native answer | Free-text behavior |
| --- | --- | --- |
| `text` | Text in the question's existing field | The same text field is the answer; do not add a duplicate fallback field. |
| `singleChoice` | One declared option | `Other` is one mutually exclusive option. Its text replaces a declared option; both cannot be selected. |
| `multipleChoice` | An array of declared options | `Other` counts as one selected option and is subject to `minSelections` and `maxSelections`. |
| `number` | A number | With a number, free text is an explanation. Without a number, nonblank free text is the answer. |
| `boolean` | A boolean | With a boolean, free text is an explanation. Without a boolean, nonblank free text is the answer. |
| `file` | One or more validated attachments | Free text may describe an attachment. Without an attachment, nonblank free text is the answer. |
| `audio` | One validated recording | Free text may describe a recording. Without a recording, nonblank free text is the answer. |

## Resume Projection

When free text is absent, preserve the existing native value or attachment projection. When present:

- A single-choice `Other` answer projects as a string.
- A multiple-choice answer projects as an array, with `Other` appended as one element.
- A number or boolean with a native value projects as `{ value, freeText }`; text-only input projects as a string.
- A file or audio answer with an attachment projects as `{ attachment, freeText }`; text-only input projects as a string.
- A skipped answer cannot carry free text.

The server-side Common validator is authoritative. A browser-side check is for immediate feedback only. An invalid submission must not mutate workflow context/history or consume the active wait.

## Workflow Ownership

`requiredInputs` continues to declare context paths. Every user-owned SO answer path must also be declared in `validation.declaredUserOwnedFields`. A structured ask worker stores drafts, attachments, and a submission receipt only. The owning AO or SO runtime validates that receipt and performs the existing resume operation.
