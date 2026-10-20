---
name: Loom Skill Governance Rules
description: Planning, enhancement, reviewer, subagent, and governed-skill evidence rules for Loom skill work.
applyTo: ".agents/skills/**,demos/**,docs/**"
---

# Loom Skill Governance Rules

Use these rules when authoring, reviewing, enhancing, or validating Loom skills and their governed workflow assets.

## Selective JSON Reading

- For JSON larger than 16 KiB (16,384 UTF-8 bytes), default to selective reads: use search or regex only to locate the target key/path, then inspect a bounded line window and expand as needed. For minified JSON or unsafe line slices, use JSON Pointer or a structured parser to extract the needed subtree. Read the full JSON only when the user explicitly requests it or whole-document validation requires it; `.agent.md` role contracts remain a separate full-read requirement.

## Workflow Designer Reference And Layered Validation

- Before authoring or revising an AO or SO workflow, consume a bounded reference pack from one selected runtime. It must include a fresh guide result, same-runtime schema and demo, successful same-runtime demo compile, current target contract and requirements, current workflow source, latest compile diagnostics when revising, and applicable `AGENTS.md`.
- Classify by activity purpose, not branch or output file type. For development, each reference records its path, selected runtime version, `authorityRole`, read status, and validation result. SHA-256 fields may be recorded but are optional; missing hashes, re-attestation, or substitute revision/fingerprint gates must not block development. The schema/demo/compile must still come from the same selected apphost; any generation-set label is grouping metadata, not a freshness proof.
- Official package-channel work still requires exact published package identity and integrity checks. Whenever a package is used, validate its SHA-512, manifest, and safe archive before extraction. These package-safety checks are separate from optional hashes on development support references.
- Workflow-designer technical authority is the selected runtime's fresh English guide/schema/demo/successful compile and version-matched English docs. Translated public docs are localization checks only and must not establish or override runtime, CLI, schema, or workflow-control semantics. Keep public docs bilingual; this authority rule applies to technical sources, not the language of user/business payloads or presentation. `authority` covers the selected runtime guide/schema/demo/successful audit and version-matched runtime docs; `current_contract` covers the target contract, requirements, applicable `AGENTS.md`, and current workflow; `diagnostic_evidence` covers compile feedback and probes; `previous_runnable_reference` covers only an older workflow; `supplemental` covers generated models, fixtures, excerpts, and non-authoritative material. Lower-priority evidence never overrides a higher-priority source.
- A previous runnable workflow must never override current schema, requirements, or runtime behavior. Record its version, path, copy time, reusable shapes, differences, and rejected/deprecated items; a source hash is optional in development and never a freshness gate.
- Designer validation remains ordered `runtime`, `JSON`, `graph`, `enum`, `expression`, `projection`, `dataflow`, `gate`, `ownership`, `semantic`. Stop at the first failed or required-unknown layer, repair only that layer, then rerun earlier layers. Compile is a later checkpoint and never proves semantic readiness.
- Each design run keeps runtime-owned `reference-manifest.json`, `static-contract-review.json`, and `semantic-probe-report.json` under `<execution-output-root>/workflow-design/` with the established schema versions. In development, descriptors require readable paths, schema version, runtime version, and verdict; content hashes are optional and do not attest freshness.
- Target business outputs exercised during development remain production-validation objects. Enforce their target-contract types, required evidence, and required artifact hashes against real outputs; any failure blocks acceptance until repaired and freshly validated.
- A governance wrapper exposes only governance responsibilities and a structured handoff to the owning domain orchestrator. It must not duplicate the domain workflow.

## Subagent Authority Rules

- When a skill or skill being enhanced names a subagent markdown file such as `./assets/agents/<agent-name>.agent.md`, that exact file is authoritative.
- Do not require skill-owned or skill-being-enhanced-owned agent files to be mirrored into `.github/agents/` or another discoverable root.
- Every subagent dispatch must state the exact agent name from the declared `.agent.md` frontmatter and include a clickable Markdown link to that exact file. The file, not host registration, is the role authority.
- Before dispatch, compare the exact frontmatter name with the host's registered agent list and call it directly only when it is registered. Treat `agent not found`, an unknown agent, a subagent tool with no `agentName` parameter, or a silent default-agent response as not registered. That is a normal host limitation, not a blocker: resolve the exact file from the current skill/workspace copy first and the matching global installed-skill copy second, then dispatch an available general-purpose subagent that can perform the work as driver. Deliver the contract inline with its full contents, or path-only (no hash) with an instruction to read the whole file first and echo its frontmatter name; pass all required inputs/reference context, large ones by path. Require the driver to follow the file and return its output contract. Do not use a read-only driver for design, editing, or validation. A 408, request-size error, or timeout is a transport failure, not a missing agent; retry once path-only. Stop only when the file is absent/ambiguous or no capable driver exists; driver fallback does not require user approval.
- Resolve a declared skill-owned or skill-being-enhanced-owned file from the current repository first and the corresponding global installed-skill copy second. Do not improvise a near-match role or substitute repository-global prose once a file is named.

## Loom Skill Enhancement Governance

- `/loom-skill-enhancement` must plan before editing the skill being enhanced: analyze inputs, outputs, nodes, guards, branches, loops, user seams, runtime seams, checks, and output evidence.
- Re-enhancement strategy belongs in repository governance and skill references, not publishable subagent bodies. Apply it equally when the target is `/loom-skill-enhancement`: self-bootstrap uses its checked-in old template, current contract/concept references, and fresh guide as one input set; record the strategy for the run and never recursively launch another enhancement run.
- Self-bootstrap-only exceptions must not alter generic published skill or subagent behavior. Applicability comes from repository policy and run context.
- `/loom-skill-enhancement` and every skill under Loom Skill Orchestrator governance use the Loom Skill Orchestrator route. No step advances until it passes a boundary check on the exact external workflow copy and receives explicit approval or structured continuation. Compile-clean is only a precondition; inferred intent, prose, stale guide results, unapproved drafts, local orchestration, and direct workflow JSON edits are not valid continuation.
- The workflow template JSON is authoritative. Mermaid, HTML, and localized plan text are display layers; feedback must update the template or source plan inputs, not only rendered Mermaid.
- Business execution and official governed delivery that require skill outputs must use the public self-contained `so[.exe] run`/`resume` chain through final completion and verify the requested business artifacts. A development-only workflow-design iteration may stop after complete owning-product compile, all required semantic probes, and production validation of any target outputs it exercised; it does not need to run/resume the entire candidate for every phase edit. Compile alone never satisfies a business-output or required-hash gate.
- When a governed route includes business-intake or `AskUser` seams, completion must weave back through them on the same workflow-copy lineage. A blocked seam is blocked evidence, not completion.
- Workflow visualizations use stable node-type semantics: AI/model/subagent work green, code/tool work blue, optional user choices yellow, mandatory mid-run user input red, and required checks white or light gray.
- Enhancement completion evidence includes the final workflow template, generated Mermaid, node-to-file or node-to-artifact mapping, actual implementation/audit evidence, and changed skill-being-enhanced deliverables. Runtime-only validation is insufficient.
- Step 1 of `/loom-skill-enhancement` is the reusable foundation: plan mode, workflow analysis, template generation, compile-generated Mermaid, confirmation loop, node-to-file mapping, and final evidence reporting.
- Step 2 self-bootstrap begins only after Step 1 review/fix/validate/commit. Self-bootstrap may consume the current repository build result only as audit evidence; future official skill behavior still restores the latest package/channel runtime and lock semantics.
- Enhancement plans and mutable run checklists are per-run evidence under the execution output root. They are not stable assets of the skill being enhanced; completion manifests may reference them without copying them into a skill bundle.
- Self-bootstrap backups occur after Step 1 commit and before Step 2 edits. Back up only skill-local files to the audit root unless a wider snapshot is explicitly requested.

## SO Enhancement Batch Review Method

- Build one bounded shared review context after selecting and validating the runtime and capturing its fresh guide. It carries the package or source-build evidence, paths and bounded source snapshots, schema/runtime references, and external workflow-copy identity. A content hash is optional in development and is never a re-attestation or freshness gate. Independent checks consume the same context by reference; inspect workflow fragments only when a later step needs them, not as a runtime-startup gate.
- Independent review or validation responsibilities consume the shared context by reference and run as one `ConcurrencyStrategy.All` external batch when independent. A batch is complete only after every expected transition returns.
- Aggregate all findings into one explicit findings record before repair. The repair step receives the complete aggregate and applies one coordinated repair pass across affected deliverables.
- After repair, run independent post-fix checks as a second `ConcurrencyStrategy.All` batch. Keep final parse, graph/dataflow, compile, and ordered runtime checks in one serial validation phase after all post-fix results arrive.
- Missing, duplicate, or incomplete batch results fail closed and remain on the same persisted workflow copy.
