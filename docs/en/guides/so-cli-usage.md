# SO CLI Usage

[中文](../../zh-cn/guides/so-cli-usage.md) | [Root](../README.md)

This page is the single source of truth for the Loom Skill Orchestrator (`so`) CLI: commands, file inputs, AskUser, output, and audit behavior. AO has its own [AO CLI Usage](ao-cli-usage.md). AO and SO are independent products.

SO ships as a self-contained product+RID package. Run the extracted `so` apphost directly on Unix and `so.exe` on Windows. Package selection and integrity checks are documented in [Platform Detection Steps](../reference/runtime/platform-detection.md); there is no runtime-resolution command or DLL launch mode.

## Commands

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage and validation-output notes |
| `--guide` | none | none | Return JSON containing the exact version, docs root, and guide path |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `mcp generate-config` | `--output-file` | `--format vscode\|claude`, `--server-name`, `--force` | Generate optional MCP configuration from the running apphost identity |
| `ask start` | `--contract-file` | none | Start a standalone structured question session without a workflow |
| `ask result` | `--ask-id` | none | Read standalone ask status, typed answers, and receipt |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write workflow schema, demo JSON, and generated C# contract files |
| `--workflow-script` | `--mode`, `--script-file`, `--input-file`, `--output-file` | `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--verification-output-file`, `--audit-output`, `--workspace-root` | Execute a disk-backed workflow Build or Edit script |
| `skeleton-check` | `--workflow-file`, `--skeleton-file` | none | Check a standalone phase skeleton against its workflow |
| `phase-compile` | `--workflow-file`, `--skeleton-file`, `--fragment-file`, `--phase-id`, `--output-file` | none | Statically validate one isolated phase |
| `impact` | `--previous-workflow-file`, `--previous-skeleton-file`, `--candidate-workflow-file`, `--candidate-skeleton-file`, `--output-file` | `--previous-result-file`, `--candidate-result-file`, `--changed-phase-id` (repeatable) | Compare structured changes and select phases to revalidate |
| `merge` | `--workflow-file`, `--skeleton-file`, `--result-file` (repeatable), `--output-file` | none | Write a new candidate from passing phase results |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an SO workflow and emit compile feedback and audit views |
| `run` | `--workflow-file` | `--context-file`, `--operation-id`, `--audit-output`, `--workspace-root` | Run the workflow until it blocks or completes |
| `resume` | `--workflow-file` and exactly one of `--result-file`, `--ask-id`, or `--offline-submission-file` | `--operation-id` (with `--result-file`), `--audit-output`, `--workspace-root` | Resume from a result envelope, persisted AskUser receipt, or versioned offline submission |
| `copy-audit-step` | `--source-step`, `--workflow-id`, `--sequence`, `--action`, `--audit-output`, `--reason`, `--verified-by` | none | Copy verified audit artifacts with reuse provenance; does not advance workflow state |
| `status` | `--workflow-file` | none | Emit the current workflow status |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`, `--json-pointer`, size bounds | Return a bounded summary or fragment |
| `inspect-contract-fragment` | `--workflow-file` or `--contract-file` | `--json-pointer`, size bounds | Read a bounded contract fragment for diagnostics |
| `inspect-events` | `--workflow-file` | none | Print the event sidecar |
| `ls` | optional path | none | Run the built-in sample workflow |

For bounded fragment reads, the size options are `--max-bytes`, `--max-array-items`, `--max-object-properties`, and `--max-depth`. Without `--json-pointer`, workflow inspection returns summary metadata; with it, the command returns the selected bounded fragment.

## File Inputs

Every `*-file` option is a path to an existing file, not inline content. Create and close the complete input set before invoking a command. This applies to scripts, JSON, workflows, objectives, contexts, and resume results. The CLI validates inputs before reading or writing; output paths are destinations owned by the CLI.

## Phase-Sliced Workflow Design

Count authored states only. Keep workflows with fewer than 40 authored `StateNode`s on the whole-workflow route. At 40 or more, create and freeze a typed `loom.workflow-skeleton.v1` before authoring isolated phases; 25 authored states per phase is a soft cap. Use one selected SO apphost for every command in this route.

`skeleton-check` verifies the skeleton against its workflow. `phase-compile` checks one fragment's graph, expressions, projections, and declared interfaces only; it does not run business commands, HTTP, AskUser, or subagents. Aggregate interface-change requests centrally, update the skeleton, then run `impact`. Impact compares structured definitions and dependencies, not hashes. Revalidate affected phases and proven dependents; unknown ownership or dependencies require full revalidation.

`merge` requires exactly one passing result for each expected phase and writes a new candidate. Then run the complete SO `compile` against that exact candidate. Do not modify or migrate an active `WorkflowInstance`. Limit repairs to three attempts per phase and two post-merge full-compile rounds; at either cap, preserve evidence and request an explicit decision.

The designer returns `phase_design_evidence` linked to `phaseDesignEvidence` in the existing `static-contract-review.json`. Keep detailed phase evidence in that record; do not add a fourth report. Keep skeletons, fragments, results, candidates, and audit files outside the skill bundle. See the shared [Workflow Model](../architecture/workflow-model.md) and the independent [AO CLI Usage](ao-cli-usage.md).

```powershell
.\so.exe skeleton-check --workflow-file candidate.json --skeleton-file workflow-skeleton.json
.\so.exe phase-compile --workflow-file candidate.json --skeleton-file workflow-skeleton.json --fragment-file phase-01.json --phase-id p01 --output-file phase-01.result.json
.\so.exe impact --previous-workflow-file previous.json --previous-skeleton-file previous-skeleton.json --candidate-workflow-file candidate.json --candidate-skeleton-file workflow-skeleton.json --output-file impact.json
.\so.exe merge --workflow-file candidate.json --skeleton-file workflow-skeleton.json --result-file phase-01.result.json --result-file phase-02.result.json --output-file merged-candidate.json
.\so.exe compile --workflow-file merged-candidate.json --audit-output audit\merged-candidate
```

On Unix, run the same commands with `./so`.

## File Editing

For file editing, `--patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat `--patch` as a command-line fallback when normal patch application fails. The target file must already exist; `--from-line` and `--to-line` are 1-based inclusive; when `--to-line` exceeds EOF it is clamped to the last line; and an empty patch-content file deletes the requested line range.

## Standalone Ask User

Shared XO Ask in the existing AO/SO binaries supports two peer consumers: the workflow-independent `/loom-ask-user` skill and the optional workflow `AskUser` node. XO is shorthand for AO or SO, not a third product. Standalone ask does not require or mutate a `WorkflowInstance`, a wait state, context fields, or workflow resume.

The current source implements `so ask start --contract-file <path>` and `so ask result --ask-id <id>`. Prepare the complete question contract on disk before starting. The selected runtime serves the browser form; read the result with the returned ask ID. Do not treat this command listing as proof that an older published package supports standalone ask: verify the exact installed apphost's `--help` first.

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
.\so.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\so.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

## Guide Output

Run `so.exe --guide` on Windows or `so --guide` on Unix. Each apphost emits one JSON object:

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
.\so.exe --guide
.\so.exe ask start --contract-file questions.json
.\so.exe ask result --ask-id <ask-id>
.\so.exe --schema-demo-output outputs\schema-demo
.\so.exe compile --workflow-file so-template.json --audit-output outputs\audit
.\so.exe mcp generate-config --output-file outputs\mcp.json --format vscode
.\so.exe resume --workflow-file workflow.json --ask-id <ask-id>
.\so.exe resume --workflow-file workflow.json --offline-submission-file submission.json
```

On Unix, use `./so` for the same commands. Run the apphost with `--help` to check the exact installed version's command surface.

## Output Shape And Execution State

`so` keeps wrapped execution output and SO-owned control data separate.

- Wrapped external command output is emitted as line-by-line XML-like fragments on stdout:
  `<wrapped_exec>`
  `<commandline>...</commandline>`
  `<exectionstream>`
  `...streamed output lines...`
  `</exectionstream>`
  `</wrapped_exec>`
- SO state, blocking guidance, and final result metadata are emitted as:
  `<so_property>`
  `{json}`
  `</so_property>`

This keeps shell-visible wrapped output streamable while still making SO guidance machine-readable.

The JSON inside `<so_property>` uses snake_case field names. `so.exe resume --result-file` / `so resume --result-file` expects a JSON envelope with `transition_id`, optional `correlation_key`, `result_id` for Plan results, and `payload`. Every fan-out result must also include its active entry's `wait_id` from the persisted workflow. For an external execution failure, add non-empty `execution_error` and do not send a non-empty result payload.

## Audit Output

Compile feedback uses `workflow.compile-feedback.v1`. Successful compile, run, and resume steps write audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`. Compile does not overwrite existing artifacts and failed compilation does not create placeholder renders. With `--workspace-root`, verified Mermaid and HTML, plus Analysis and Dataflow when generated, are mirrored for editor links. Delivery metadata reports `workspace_mirror` or `runtime_path_only` with `mirror_error`; native runtime paths remain available when a mirror fails.

## Related Documentation

- [SO Guide](so-guide.md)
- [Ask User Guide](ask-user-guide.md)
- [AO CLI Usage](ao-cli-usage.md)
