## Obtaining A Current Workflow Example

This page intentionally does not include a hand-written workflow JSON example. Get the current accepted shape from the exact self-contained runtime package for the detected host RID.

Run the extracted apphost directly from its package directory:

```powershell
.\so.exe --help
.\so.exe compile --workflow-file <external-workflow.json> --audit-output <external-audit-root>
```

On Unix, invoke `./so` with the same arguments. `compile` validates an existing workflow file; it does not create a workflow from nothing. Read the generated `workflow.json`, `workflow.compile-feedback.json`, `workflow.mermaid.md`, `workflow.html`, `workflow.analysis.json`, and `workflow.dataflow.json` under the returned audit step directory. The feedback JSON is the structured source for compile counts and diagnostics. The backup is the serialized shape accepted by the exact runtime that performed the check. Failed compile does not create placeholder Mermaid or HTML renders.

The Mermaid Markdown keeps the complete graph in its fenced block, then appends a phase-grouped business summary using each state's `workflowPhase`, `name`, `id`, and `description`. Pale nodes and legend entries set an explicit dark text color.

The successful compile HTML is an audit report, not only a graph preview. It records product/runtime and workflow provenance, compile counts and diagnostics, control-flow and ownership analysis, gates, artifact mappings, and transition-level dataflow evidence from the same compile. It does not infer runtime execution results.

For a saved runtime workflow, use the same apphost with `inspect-workflow --workflow-file <external-workflow.json>`. Do not use the JSON returned by `--guide` as a workflow example; it contains guide paths, not a workflow file.

### Exporting Schema And Demo Together

Generate a current schema contract and compile-ready demo from one runtime invocation:

```powershell
.\so.exe --schema-demo-output <external-output-directory>
.\so.exe compile --workflow-file <external-output-directory>\workflow.demo.json --audit-output <external-audit-root>
```

On Unix, invoke `./so` with the same arguments. The export writes `workflow.schema.json`, `workflow.demo.json`, `workflow.model.cs`, `workflow.demo.cs`, and `workflow.demo.verify.cs`. The generated C# files use the built-in Roslyn host; no separate project or script runtime is required. Keep generated files outside skill folders unless explicitly requested. Use the schema and successful same-version demo compile as the source of truth when updating this document.

The public model may be broader than the current SO runtime. Unsupported multi-transition strategies fail explicitly instead of silently degrading. `so.exe compile`, `so compile`, run, and resume write `workflow.analysis.json` under the audit step directory; it summarizes requested inputs, output families, branches, loops, seams, gates, and control-flow risk.

`copy-audit-step` copies only explicitly verified unchanged audit artifacts and records hashes in `audit-reuse.json`; it never advances workflow state or creates official run evidence. `compile` rejects workflows whose state nodes omit a non-empty `workflowPhase`, and it does not overwrite existing artifact files.
