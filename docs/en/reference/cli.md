# CLI Reference

[中文](../../zh-cn/reference/cli.md) | [Root](../README.md)

AO and SO ship as self-contained product+RID packages. Run the extracted `ao`/`so` apphost directly on Unix and `ao.exe`/`so.exe` on Windows. Package selection and integrity checks are documented in [Platform Detection Steps](runtime/platform-detection.md); there is no runtime-resolution command or DLL launch mode.

## Loom Agent Plan-Execution Orchestrator

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage and validation-output notes |
| `--guide` | none | none | Return JSON containing the exact version, docs root, and guide path |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write workflow schema, demo JSON, and generated C# contract files |
| `--workflow-script` | `--mode`, `--script-file`, `--input-file`, `--output-file` | `--base-workflow-file`, `--verify-script`, `--reference-workflow-file`, `--verification-output-file`, `--audit-output`, `--workspace-root` | Execute a disk-backed Build or Edit script and verify its output |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an AO workflow and emit compile feedback and audit views |
| `prompt-plan` | `--objective-file` | `--context-file` | Emit AO planner prompt text for WorkflowInstance authoring |
| `prompt-replan` | `--workflow-file`, `--tbr-id` | `--objective-file` | Emit replanner prompt text for an existing workflow |
| `run` | `--workflow-file` | `--context-file`, `--operation-id`, `--audit-output`, `--workspace-root` | Run the workflow until it blocks or completes |
| `resume` | `--workflow-file`, `--result-file` | `--operation-id`, `--audit-output`, `--workspace-root` | Resume from a structured result envelope |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`, `--json-pointer`, size bounds | Return summary metadata or one bounded workflow fragment |
| `inspect-contract-fragment` | `--workflow-file` or `--contract-file` | `--json-pointer`, size bounds | Read a bounded contract fragment for diagnostics |

## SkillOrchestrator

| Command | Required args | Optional args | Purpose |
| --- | --- | --- | --- |
| `--help` | none | none | Print usage and validation-output notes |
| `--guide` | none | none | Return JSON containing the exact version, docs root, and guide path |
| `mcp stdio` | none | none | Start the local newline-delimited JSON-RPC MCP server |
| `mcp generate-config` | `--output-file` | `--format vscode|claude`, `--server-name`, `--force` | Generate optional MCP configuration from the running apphost identity |
| `--patch` | `--patch-content-file`, `--patch-target`, `--from-line`, `--to-line` | none | Replace an inclusive line range from an external patch-content file |
| `--schema-demo-output` | `<directory>` | none | Write workflow schema, demo JSON, and generated C# contract files |
| `--workflow-script` | `--mode`, `--script-file`, `--input-file`, `--output-file` | verification and audit options | Execute a disk-backed workflow Build or Edit script |
| `compile` | `--workflow-file` | `--audit-output`, `--workspace-root` | Validate an SO workflow and emit compile feedback and audit views |
| `run` | `--workflow-file` | `--context-file`, `--operation-id`, `--audit-output`, `--workspace-root` | Run the workflow until it blocks or completes |
| `resume` | `--workflow-file`, `--result-file` | `--operation-id`, `--audit-output`, `--workspace-root` | Resume from a structured result envelope |
| `copy-audit-step` | `--source-step`, `--workflow-id`, `--sequence`, `--action`, `--audit-output`, `--reason`, `--verified-by` | none | Copy verified audit artifacts with reuse provenance; does not advance workflow state |
| `status` | `--workflow-file` | none | Emit the current workflow status |
| `inspect-workflow` | `--workflow-file` | none | Print the current workflow JSON |
| `inspect-workflow-fragment` | `--workflow-file` | `--operation-id`, `--json-pointer`, size bounds | Return a bounded summary or fragment |
| `inspect-contract-fragment` | `--workflow-file` or `--contract-file` | `--json-pointer`, size bounds | Read a bounded contract fragment for diagnostics |
| `inspect-events` | `--workflow-file` | none | Print the event sidecar |
| `ls` | optional path | none | Run the built-in sample workflow |

## File Inputs

Every `*-file` option is a path to an existing file, not inline content. Create and close the complete input set before invoking a command. This applies to scripts, JSON, workflows, objectives, contexts, and resume results. The CLI validates inputs before reading or writing; output paths are destinations owned by the CLI.

## Guide Output

Run `ao.exe --guide`/`so.exe --guide` on Windows or `ao --guide`/`so --guide` on Unix. Each apphost emits one JSON object:

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
.\ao.exe --schema-demo-output outputs\schema-demo
.\ao.exe compile --workflow-file workflow.json --audit-output outputs\audit
.\ao.exe run --workflow-file workflow.json --context-file context.json --operation-id run-001
.\ao.exe resume --workflow-file workflow.json --result-file resume.json --operation-id resume-001

.\so.exe --guide
.\so.exe --schema-demo-output outputs\schema-demo
.\so.exe compile --workflow-file so-template.json --audit-output outputs\audit
.\so.exe mcp generate-config --output-file outputs\mcp.json --format vscode
```

On Unix, use `./ao` or `./so` for the same commands. For a full command listing, run the apphost with `--help`.

## Audit Output

Compile feedback uses `workflow.compile-feedback.v1`. Successful compile, run, and resume steps write audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`. Compile does not overwrite existing artifacts and failed compilation does not create placeholder renders. With `--workspace-root`, verified Mermaid and HTML copies are mirrored for editor links.
