## Reference Pack Authority Gate (Required)

Every invocation, including a revision after a compile failure, must receive a bounded machine-readable `referencePackManifest` before workflow authoring begins. Passing a directory or a prose inventory is insufficient. The manifest may point to files instead of embedding their contents, but every file must be checked before it is used.

The manifest must contain:

- `schemaVersion` for the reference-manifest format, the runtime (`ao`), the exact `runtimeVersion`, and one `generationSetId` shared by the schema, demo, and successful demo compile audit.
- An `entries` array. Each entry records `path`, `sha256`, `runtimeVersion`, `authorityRole`, `readStatus`, and `validationResult`. Allowed roles are `authority`, `current_contract`, `previous_runnable_reference`, `diagnostic_evidence`, and `supplemental`.
- A generation-set record for the fresh guide JSON and actual returned guide file, `workflow.schema.json`, `workflow.demo.json`, and the same-runtime successful demo compile audit. The record must include each path and hash and must agree with `schemaDemoInput`.
- Current `SKILL.md`, applicable `AGENTS.md`, current requirements or incident notes, the current workflow source, and the latest compile feedback when this is a revision. The current contract and request are business authority and cannot be displaced by an old example.
- When a previous workflow is supplied, a `previousRunnableReferenceDisposition` recording its source version, source path, source hash, copy time, reusable shapes, differences from the current schema and requirements, and rejected or deprecated items. Do not copy it directly into the candidate.

The five reference roles map to the repository contract: `authority` covers exact-runtime guide/schema/demo/demo-audit and version-matched runtime contract/governance/behavior docs; `current_contract` covers the current target contract, requirements, applicable `AGENTS.md`, and current workflow; `diagnostic_evidence` covers compile feedback and prior probes; `previous_runnable_reference` covers only an older runnable workflow; and `supplemental` covers generated C# shape files, small fixtures, and source excerpts. Multiple entries may share a role, but a lower-priority role cannot override a higher-priority source.

The three evidence files use fixed schema versions: `workflow-designer.reference-manifest.v1`, `workflow-designer.static-contract-review.v1`, and `workflow-designer.semantic-probe-report.v1`. Do not substitute a date, a bare numeric version, or a new ad hoc version string.

The designer must reject the dispatch before authoring if a required entry is missing, unreadable, hash-mismatched, from another runtime version, or marked `unknown`. Use the exact runtime guide/schema/demo for fields, discriminator values, enum values, expression shape, and compile rules. Do not load the full runtime source by default, and do not turn an unprobed source-code guess into a workflow fact.

## Selective JSON Reading (Required)

For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective inspection: use search or regex only to locate the target key or path, then read a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract only the needed subtree before authoring. Read the full JSON only when the user explicitly requests it or whole-document validation requires it. This policy applies to JSON data; `.agent.md` role contracts must still be read in full.

## Mandatory Runtime Schema Input Gate (Required)

Every invocation of this designer, including a revision after a compile failure, must receive a fresh schema/demo bundle produced by the exact current AO apphost. The designer must not invent workflow JSON from memory, prose, static examples, or a guide alone. Direct `ao.exe --guide` or `ao --guide` proves the guide surface; the same apphost's schema-demo operation produces the current workflow contract.

The dispatch payload must include a machine-readable `schemaDemoInput` object with all of these fields:

- `runtime`: `ao`
- `runtimeBinding`: `dotnet-ao`
- `runtimeVersion`: the exact runtime version that generated the files
- `schemaFile`: a workspace-relative or runtime-output-relative path to `workflow.schema.json`
- `demoFile`: a workspace-relative or runtime-output-relative path to `workflow.demo.json`
- `demoCompileAudit`: a workspace-relative or runtime-output-relative path to the successful compile audit for the demo
- `schemaSha256` and `demoSha256`: hashes captured after generation

The caller must generate both files in a fresh external output directory, parse both as JSON, and compile the generated demo with the same runtime before dispatching the designer:

```powershell
.\ao.exe --schema-demo-output <external-schema-output>
.\ao.exe compile --workflow-file <external-schema-output>\workflow.demo.json --audit-output <external-schema-audit-root>
```

The schema input gate passes only when `schemaFile` and `demoFile` both exist, both parse as JSON, the schema identifies `techne-loom.workflow-instance`, the demo declares the expected `runtimeBinding`, and the same-runtime compile succeeds with its audit evidence. A path string without the files, a stale or copied schema, only one of the two files, a failed demo compile, or a result from another runtime version is insufficient. The generated files must remain outside skill folders unless explicitly requested as deliverables.

Before authoring or revising any workflow field, read the supplied schema and demo. Use the schema as the source of truth for root fields, required fields, node `$kind` discriminators, expression fields, allowed enum values, and compile rules; use the demo as the current serialized shape example. Local guides and target files explain intent but cannot override the supplied runtime schema. If a requested field, discriminator, enum, expression shape, or compile rule is absent or conflicts with the schema, stop and report a schema-contract mismatch instead of guessing or adding an invented field.

## Priority 1 Document Mapping Gate (Required)

Before designing or revising workflow nodes, consume the current node-to-file map. Map the skill being enhanced's `SKILL.md` first as Priority 1: record its exact root-relative path, owning node(s), requested changes, and validation evidence. It must be the first data row and precede supporting references, contracts, and workflow assets. Do not draft workflow nodes until the map covers every requested document.

## Context Field Type Evidence Gate (Required)

Before adding or changing any field in the owning contract's `context_fields`:

- Compare its declared JSON type, nullability, and container item/value types with an independent actual producer output from the selected exact AO apphost or an authoritative producer output schema. The schema/demo bundle describes the workflow envelope, not custom business field values. Do not infer types from path names or use the contract being edited as the type source.
- Run the same product apphost's `AO inspect-contract-fragment --contract-file <owning-contract> --json-pointer /context_fields` to confirm it reads the exact catalog. This echo is a catalog parse/shape check only; it does not independently prove a business output type.
- For every changed field, preserve the producer output/schema path, runtime and version, declared type, observed type/container shape, and verdict in `contextFieldTypeComparisons` inside the existing `static-contract-review.json`.
- Before comparing evidence or asking the user, follow [Producer Output Evidence Discovery](../../reference/producer-output-evidence-contract.md). The target-skill driver must inspect the target `SKILL.md`, exact target-local agent/tool/command contracts, scripts, schemas, fixtures, and available run/probe outputs, then trace the producer-to-context projection. The driver decides whether existing evidence is sufficient. `context_fields` describes expected context; runtime schema/demo describes the workflow envelope only.
- Run the smallest same-apphost producer-output or projection/compile probe before broad tests when safe and needed. Missing evidence, type mismatches, or unresolved nullability/container shape block a passing verdict. unknown type evidence blocks readiness. Record unresolved required evidence as `unknown`; ask the user for a specific producer schema or sanitized result only after the discovery above and only when the needed fact cannot be safely probed. If no changed field depends on it, do not block readiness.

