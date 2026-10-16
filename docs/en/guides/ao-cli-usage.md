# AO CLI Usage

[中文](../../zh-cn/guides/ao-cli-usage.md) | [Root](../README.md)

This page is the single source of truth for the Loom Agent Plan-Execution Orchestrator (`ao`) CLI: commands, file inputs, AskUser, output, and audit behavior. SO has its own [SO CLI Usage](so-cli-usage.md). AO and SO are independent products.

AO ships as a self-contained product+RID package. Run the extracted `ao` apphost directly on Unix and `ao.exe` on Windows. Package selection and integrity checks are documented in [Platform Detection Steps](../reference/runtime/platform-detection.md); there is no runtime-resolution command or DLL launch mode.

## Commands

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage and validation-output notes |
| `--guide` | none | none | Return JSON containing the exact version, docs root, and guide path |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `ask start` | `--contract-file` | none | Start a standalone structured question session without a workflow |
| `ask result` | `--ask-id` | none | Read standalone ask status, typed answers, and receipt |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write workflow schema, demo JSON, and generated C# contract files |
| `--workflow-script` | `--mode`, `--script-file`, `--input-file`, `--output-file` | `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--verification-output-file`, `--audit-output`, `--workspace-root` | Execute a disk-backed Build or Edit script and verify its output |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an AO workflow and emit compile feedback and audit views |
| `prompt-plan` | `--objective-file` | `--context-file` | Emit AO planner prompt text for WorkflowInstance authoring |
| `prompt-replan` | `--workflow-file`, `--tbr-id` | `--objective-file` | Emit replanner prompt text for an existing workflow |
| `run` | `--workflow-file` | `--context-file`, `--operation-id`, `--audit-output`, `--workspace-root` | Run the workflow until it blocks or completes |
| `resume` | `--workflow-file` and exactly one of `--result-file`, `--ask-id`, or `--offline-submission-file` | `--operation-id` (with `--result-file`) | Resume from a result envelope, persisted AskUser receipt, or versioned offline submission |
| `status` | `--workflow-file` | none | Emit the current workflow status |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`, `--json-pointer`, size bounds | Return summary metadata or one bounded workflow fragment |
| `inspect-contract-fragment` | `--workflow-file` or `--contract-file` | `--json-pointer`, size bounds | Read a bounded contract fragment for diagnostics |

For bounded fragment reads, the size options are `--max-bytes`, `--max-array-items`, `--max-object-properties`, and `--max-depth`. Without `--json-pointer`, workflow inspection returns summary metadata; with it, the command returns the selected bounded fragment.

## File Inputs

Every `*-file` option is a path to an existing file, not inline content. Create and close the complete input set before invoking a command. This applies to scripts, JSON, workflows, objectives, contexts, and resume results. The CLI validates inputs before reading or writing; output paths are destinations owned by the CLI.

## File Editing

For file editing, `--patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat `--patch` as a command-line fallback when normal patch application fails. The target file must already exist; `--from-line` and `--to-line` are 1-based inclusive; when `--to-line` exceeds EOF it is clamped to the last line; and an empty patch-content file deletes the requested line range.

## Standalone Ask User

Shared XO Ask in the existing AO/SO binaries supports two peer consumers: the workflow-independent `/loom-ask-user` skill and the optional workflow `AskUser` node. XO is shorthand for AO or SO, not a third product. Standalone ask does not require or mutate a `WorkflowInstance`, a wait state, context fields, or workflow resume.

The current source implements `ao ask start --contract-file <path>` and `ao ask result --ask-id <id>`. Prepare the complete question contract on disk before starting. The selected runtime serves the browser form; read the result with the returned ask ID. Do not treat this command listing as proof that an older published package supports standalone ask: verify the exact installed apphost's `--help` first.

For workflow-owned questions, the owning product validates the receipt, maps answers, and resumes its workflow as described below. The shared ask worker never locks, mutates, or resumes a workflow.

## Structured AskUser Resume

For a workflow-file resume, provide exactly one input source:

- `--result-file` keeps the existing result-envelope route. Raw structured `AskUser` answers must use a validated receipt or offline submission.
- `--ask-id` applies a validated receipt already stored by the ask worker.
- `--offline-submission-file` imports the versioned JSON downloaded from the offline form.

Receipt modes use the operation ID carried by the receipt or submission and reject `--operation-id`. Offline submissions use this shape; `answers` is keyed by stable question ID:

```json
{
  "schemaVersion": 1,
  "askId": "<ask-id>",
  "expectedGeneration": 4,
  "operationId": "<operation-id>",
  "answers": {
    "question-id": {
      "value": "approved",
      "skipped": false,
      "attachmentIds": []
    }
  }
}
```

The offline HTML copy includes metadata for existing attachments; the downloaded JSON keeps their IDs but contains neither metadata nor bytes. Offline copies cannot download existing attachments or upload new ones.

```powershell
.\ao.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\ao.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

## Guide Output

Run `ao.exe --guide` on Windows or `ao --guide` on Unix. Each apphost emits one JSON object:

```json
{
  "version": "<package-version>",
  "docs_root": "<absolute-docs-root>",
  "guide_path": "<absolute-guide-path>"
}
```

The command accepts no extra arguments. Read the returned `guide_path`; inspect `docs_root` only when the guide leaves a question unresolved. Failed stderr is not guide evidence.

## Direct Apphost Examples

```powershell
.\ao.exe --guide
.\ao.exe ask start --contract-file questions.json
.\ao.exe ask result --ask-id <ask-id>
.\ao.exe --schema-demo-output outputs\schema-demo
.\ao.exe compile --workflow-file workflow.json --audit-output outputs\audit
.\ao.exe run --workflow-file workflow.json --context-file context.json --operation-id run-001
.\ao.exe resume --workflow-file workflow.json --result-file resume.json --operation-id resume-001
.\ao.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\ao.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

On Unix, use `./ao` for the same commands. Run the apphost with `--help` to check the exact installed version's command surface.

## Output Shape And Execution State

AO control state is emitted as `<ao_property>{json}</ao_property>` using snake_case field names. In repo terminology, `ao.exe run` / `ao run` may weave out and `ao.exe resume` / `ao resume` is the weave-back entry point. The canonical `--workflow-file` run, resume, status, and prompt-replan paths are sessionless: the workflow JSON and its `.events.jsonl` sidecar carry durable state, while the MCP/CLI process is disposable. Plan result envelopes must include a non-empty `result_id`; duplicate Plan results are no-ops. The legacy session form remains available for compatibility. When AO workflow JSON is needed, the calling agent authors it first and uses `ao.exe compile` / `ao compile` as the validation step. Compile fails instead of overwriting existing audit artifact files. Legacy AO snapshots may be compiled for display only when their `context` is empty; snapshots with context values must be migrated to a `WorkflowInstance` with `contractBinding` to the owning `contract.json`.

`ao resume --result-file` reads a JSON envelope with `transition_id`, optional `correlation_key`, and `payload`. Plan results require a non-empty `result_id`; each fan-out result also requires the active entry's persisted `wait_id`. On external execution failure, send non-empty `execution_error` without a non-empty result payload.

## Audit Output

Compile feedback uses `workflow.compile-feedback.v1`. Successful compile, run, and resume steps write audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`. Compile does not overwrite existing artifacts and failed compilation does not create placeholder renders. With `--workspace-root`, verified Mermaid and HTML copies are mirrored for editor links.

## Related Documentation

- [AO Guide](ao-guide.md)
- [Ask User Guide](ask-user-guide.md)
- [SO CLI Usage](so-cli-usage.md)
