# Loom Agent CLI Usage

[中文](../../zh-cn/guides/ao-cli-usage.md) | [Root](../README.md)

This page documents the `loom agent` route for Loom Agent Plan-Execution Orchestrator: commands, file inputs, output, and audit behavior. Loom Skill Orchestrator commands are documented in [Loom Skill CLI Usage](so-cli-usage.md). Agent and Skill remain independent modules under one public CLI and runtime package family.

The unified Loom runtime package contains one `loom` apphost per supported RID. Run `loom agent` for this module on Windows or Unix. Package selection and integrity checks are documented in [Platform Detection Steps](../reference/runtime/platform-detection.md); there is no runtime-resolution command or DLL launch mode.

## Commands

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage and validation-output notes |
| `loom --guide` (top-level) | none | none | Return the exact version, guide, and workflow contract documentation paths |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `loom ask start` (top-level) | `--contract-file` | none | Start a standalone structured question session without a workflow |
| `loom ask result` (top-level) | `--ask-id` | none | Read standalone ask status, typed answers, and receipt |
| `loom ask wait` (top-level) | `--ask-id` | `--wait-timeout-seconds` | Wait for a submitted standalone answer; the initial wait defaults to 5 minutes, then checks every minute |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write workflow schema, demo JSON, and generated C# contract files |
| `--workflow-script` | `--mode`, `--input-file`, `--output-file` | `--script-file` (required for build/edit), `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--verification-output-file`, `--audit-output`, `--workspace-root` | Export a draft workflow to C# builder source, or run a disk-backed Build/Edit script |
| `skeleton-check` | `--workflow-file`, `--skeleton-file` | none | Check a standalone phase skeleton against its workflow |
| `phase-compile` | `--workflow-file`, `--skeleton-file`, `--fragment-file`, `--phase-id`, `--output-file` | none | Statically validate one isolated phase |
| `impact` | `--previous-workflow-file`, `--previous-skeleton-file`, `--candidate-workflow-file`, `--candidate-skeleton-file`, `--output-file` | `--previous-result-file`, `--candidate-result-file`, `--changed-phase-id` (repeatable) | Compare structured changes and select phases to revalidate |
| `merge` | `--workflow-file`, `--skeleton-file`, `--result-file` (repeatable), `--output-file` | none | Write a new candidate from passing phase results |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an AO workflow, emit compile feedback and audit views, and return a workflow JSON artifact path instead of inline JSON |
| `prompt-plan` | `--objective-file` | `--context-file` | Emit AO planner prompt text for WorkflowInstance authoring |
| `prompt-replan` | `--workflow-file`, `--tbr-id` | `--objective-file` | Emit replanner prompt text for an existing workflow |
| `run` | `--workflow-file` | `--context-file`, `--operation-id`, `--audit-output`, `--workspace-root` | Run the workflow until it blocks or completes |
| `resume` | `--workflow-file` and exactly one of `--result-file`, `--ask-id`, or `--offline-submission-file` | `--wait-for-ask`, `--wait-timeout-seconds` (with `--ask-id`); `--operation-id` (with `--result-file`) | Optionally wait for AskUser submission before the owning runtime validates the receipt and resumes |
| `status` | `--workflow-file` | none | Emit the current workflow status |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`, `--json-pointer`, size bounds | Return summary metadata or one bounded workflow fragment |
| `inspect-contract-fragment` | `--workflow-file` or `--contract-file` | `--json-pointer`, size bounds | Read a bounded contract fragment for diagnostics |

For bounded fragment reads, the size options are `--max-bytes`, `--max-array-items`, `--max-object-properties`, and `--max-depth`. Without `--json-pointer`, workflow inspection returns summary metadata; with it, the command returns the selected bounded fragment. The default `--max-bytes` limit is `262,144` bytes (256 KiB) for both `inspect-workflow-fragment` and `inspect-contract-fragment`; raise it for one call with `--max-bytes 524288` (512 KiB).

Successful `compile` prints a compact JSON reference with the absolute `workflow_json_file` path, `workflow_json_bytes`, and `compile_audit_path`. The complete workflow remains in the readable audit `workflow.json` backup and is not printed inline.

## File Inputs

Every `*-file` option is a path to an existing file, not inline content. Create and close the complete input set before invoking a command. This applies to scripts, JSON, workflows, objectives, contexts, and resume results. The CLI validates inputs before reading or writing; output paths are destinations owned by the CLI.

## Phase-Sliced Workflow Design

Count authored states only. Keep workflows with fewer than 40 authored `StateNode`s on the whole-workflow route. At 40 or more, create and freeze a typed `loom.workflow-skeleton.v1` before authoring isolated phases; 25 authored states per phase is a soft cap. Use the same selected `loom` apphost through the `loom agent` route for every command in this module workflow.

`skeleton-check` verifies the skeleton against its workflow. `phase-compile` checks one fragment's graph, expressions, projections, and declared interfaces only; it does not run scripts, HTTP, AskUser, or subagents. Aggregate interface-change requests centrally, update the skeleton, then run `impact`. Impact compares structured definitions and dependencies, not hashes. Revalidate affected phases and proven dependents; unknown ownership or dependencies require full revalidation.

`merge` requires exactly one passing result for each expected phase and writes a new candidate. Then run `loom agent compile` against that exact candidate. Do not modify or migrate an active `WorkflowInstance`. Limit repairs to three attempts per phase and two post-merge full-compile rounds; at either cap, preserve evidence and request an explicit decision.

The designer returns `phase_design_evidence` linked to `phaseDesignEvidence` in the existing `static-contract-review.json`. Keep detailed phase evidence in that record; do not add a fourth report. Keep skeletons, fragments, results, candidates, and audit files outside the checked-in skill bundle. See the shared [Workflow Model](../architecture/workflow-model.md) and [Skill CLI Usage](so-cli-usage.md).

```powershell
.\loom.exe agent skeleton-check --workflow-file candidate.json --skeleton-file workflow-skeleton.json
.\loom.exe agent phase-compile --workflow-file candidate.json --skeleton-file workflow-skeleton.json --fragment-file phase-01.json --phase-id p01 --output-file phase-01.result.json
.\loom.exe agent impact --previous-workflow-file previous.json --previous-skeleton-file previous-skeleton.json --candidate-workflow-file candidate.json --candidate-skeleton-file workflow-skeleton.json --output-file impact.json
.\loom.exe agent merge --workflow-file candidate.json --skeleton-file workflow-skeleton.json --result-file phase-01.result.json --result-file phase-02.result.json --output-file merged-candidate.json
.\loom.exe agent compile --workflow-file merged-candidate.json --audit-output audit\merged-candidate
```

On Unix, run the same module commands with `./loom agent`.

## Workflow Script Modes

`--mode export` reads an eligible draft or unstarted workflow JSON from `--input-file` and writes an `Edit(WorkflowInstance, WorkflowScriptInput)` C# starter to `--output-file`; it does not use `--script-file`. `--mode build` runs a disk-backed script with `Build(WorkflowScriptInput)` to create a workflow. `--mode edit` runs a disk-backed script with `Edit(WorkflowInstance, WorkflowScriptInput)` and reads the external base JSON from `--base-workflow-file`. Build and edit require `--script-file` and the workflow-script input file; edit also requires `--base-workflow-file`. Prepare every input on disk before invoking the apphost.

The package's `workflow-instance-builder.csx.md` is Markdown reference text, not an executable file. New workflows use `Build`; revisions use `Edit`. Keep scripts and candidate JSON outside the skill bundle. Scripts must not embed complete workflow JSON or perform file I/O: the apphost reads/deserializes the external base JSON and writes the canonical candidate. Use the same exact apphost/version to author and compile. See the [complete bilingual authoring route](skill-usage.md).
## File Editing

For file editing, `--patch` is the direct line-range patch path when GitHub Copilot conditions make the command interface the preferred route. On other platforms or tools, treat `--patch` as a command-line fallback when normal patch application fails. The target file must already exist; `--from-line` and `--to-line` are 1-based inclusive; when `--to-line` exceeds EOF it is clamped to the last line; and an empty patch-content file deletes the requested line range.

## Standalone Ask User

Shared XO Ask in the existing AO/SO binaries supports two peer consumers: the workflow-independent `/loom-ask-user` skill and the optional workflow `AskUser` node. XO is shorthand for AO or SO, not a third product. Standalone ask does not require or mutate a `WorkflowInstance`, a wait state, context fields, or workflow resume.

The current source implements `loom ask start`, `loom ask result`, and `loom ask wait`. Prepare the complete question contract on disk before starting. `ask result` reads current state immediately; `ask wait` returns the submitted typed answers and receipt, checking every minute after its default 5-minute initial wait. Verify the exact installed apphost's `--help` before assuming an older published package supports these commands.

For workflow-owned questions, the owning product validates the receipt, maps answers, and resumes its workflow. Use `resume --ask-id <id> --wait-for-ask` (or MCP `wait_for_ask: true`) to let the runtime wait for submission before validating and resuming; it does not hold the workflow file lock while waiting. The shared ask worker never locks, mutates, or resumes a workflow.

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
.\loom.exe agent resume --workflow-file workflow.json --ask-id <ask-id>
.\loom.exe agent resume --workflow-file workflow.json --offline-submission-file submission.json
```

## Guide Output

Run `loom.exe --guide` on Windows or `loom --guide` on Unix. Each apphost emits one JSON object:

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
.\loom.exe --guide
.\loom.exe ask start --contract-file questions.json
.\loom.exe ask result --ask-id <ask-id>
.\loom.exe ask wait --ask-id <ask-id>
.\loom.exe agent --schema-demo-output outputs\schema-demo
.\loom.exe agent compile --workflow-file workflow.json --audit-output outputs\audit
.\loom.exe agent run --workflow-file workflow.json --context-file context.json --operation-id run-001
.\loom.exe agent resume --workflow-file workflow.json --result-file resume.json --operation-id resume-001
.\loom.exe agent resume --workflow-file workflow.json --ask-id <ask-id>
.\loom.exe agent resume --workflow-file workflow.json --offline-submission-file submission.json
```

On Unix, use `./loom --guide` for the shared guide and prefix AO commands with `./loom agent`. Run `./loom agent --help` to check the exact installed version's module command surface.

## Output Shape And Execution State

AO control state is emitted as `<ao_property>{json}</ao_property>` using snake_case field names. In repo terminology, `loom.exe agent run` / `loom agent run` may weave out and `loom.exe agent resume` / `loom agent resume` is the weave-back entry point. The canonical `--workflow-file` run, resume, status, and prompt-replan paths are sessionless: the workflow JSON and its `.events.jsonl` sidecar carry durable state, while the MCP/CLI process is disposable. Plan result envelopes must include a non-empty `result_id`; duplicate Plan results are no-ops. The legacy session form remains available for compatibility. When AO workflow JSON is needed, the calling agent authors it first and uses `loom.exe agent compile` / `loom agent compile` as the validation step. Compile fails instead of overwriting existing audit artifact files. Legacy AO snapshots may be compiled for display only when their `context` is empty; snapshots with context values must be migrated to a `WorkflowInstance` with `contractBinding` to the owning `contract.json`.

`loom agent resume --result-file` reads a JSON envelope with `transition_id`, optional `correlation_key`, and `payload`. Plan results require a non-empty `result_id`; each fan-out result also requires the active entry's persisted `wait_id`. On external execution failure, send non-empty `execution_error` without a non-empty result payload.

## Audit Output

Compile feedback uses `workflow.compile-feedback.v1`. Successful compile, run, and resume steps write audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`. Compile does not overwrite existing artifacts and failed compilation does not create placeholder renders. With `--workspace-root`, verified Mermaid and HTML, plus Analysis and Dataflow when generated, are mirrored for editor links. Delivery metadata reports `workspace_mirror` or `runtime_path_only` with `mirror_error`; native runtime paths remain available when a mirror fails.

## Related Documentation

- [AO Guide](ao-guide.md)
- [Ask User Guide](ask-user-guide.md)
- [SO CLI Usage](so-cli-usage.md)
