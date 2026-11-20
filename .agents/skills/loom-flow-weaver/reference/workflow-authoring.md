# Workflow Authoring Contract

## Supported surface

V1 covers three modes: `from-blank`, `workflow-to-csharp`, and `modify-existing`.

- `from-blank` creates a new workflow through `Build(WorkflowScriptInput input)`.
- `workflow-to-csharp` converts approved AO/SO workflow JSON into reviewable C# workflow-asset fragments. A verified published runner contract is required before fragment execution or freeze.
- `modify-existing` edits an external base workflow and/or related C# assets while preserving baseline and diff evidence.

Workflow JSON remains the canonical persisted state. C# workflow scripts use Build/Edit/Verify entry points on the unified apphost. Current source evidence shows only workflow Build/Edit/Verify execution in `WorkflowScriptHost`; it does not establish an arbitrary C# fragment runner.

## Select the owning apphost route

Choose the module from the workflow owner and its current contract: AO work uses `loom agent`; SO work uses `loom skill`. These modules are independent and are not interchangeable. Bind one selected unified `loom` apphost/version and use it for the required guide, supported script operations, verifier, and compile. Use the exact versioned guide and contract references when runtime behavior is not established by the current task.

Official package-channel work requires the exact locked package identity, host RID, integrity, safe extraction, and fresh guide evidence. A compatible source-built apphost is development-only evidence and must not be written into an official lock. This seed has no runtime lock; do not guess a version, use `latest`, or present a development result as official.

## Skill-owned workflow identity

For a workflow owned by a skill, set `ownerSkillSlug` to the exact owning skill directory slug. The checked-in template and first execution copy must use `<ownerSkillSlug>--<purpose>.json`; reject generic or ownerless names such as `so-template.json`. Designers and updates to legacy skills must enforce this rule and migrate filenames, path references, and materializers together. Generic workflows that are not skill-owned are outside this naming rule.

Create one prefixed execution copy and keep its `caseId`, `runId`, and persisted runtime state together. `resume` must reuse that exact copy without renaming, recopying, or creating a replacement.

## Building a new workflow

Write a disk-backed C# builder that implements `public static WorkflowInstance Build(WorkflowScriptInput input)`. Pass the script and input by file path. The apphost owns JSON parsing and candidate serialization.

## Revising a workflow

Write a disk-backed C# editor that implements `public static WorkflowInstance Edit(WorkflowInstance workflow, WorkflowScriptInput input)`. Keep the base JSON external and write a separate candidate. `--mode export` may create an eligible external Edit starter; review and compile it on the exact selected apphost.

## Verifying a candidate

Use `Verify` only if the selected apphost documents `--verify-script`. The AO workflow host contract is `Verify(WorkflowInstance actual, WorkflowInstance reference, WorkflowModelReference model)` with `--reference-workflow-file` and `--verification-output-file`. Follow the exact guide contract; do not infer a different signature or report.

## Workflow-to-csharp runner gate

Before fragment execution or freeze, obtain an authoritative published runner contract naming the executable or apphost entry, C# input/output types, test inputs, timeout/failure semantics, output/audit fields, and same-copy identity. In this seed, missing runner evidence is a structured blocker.

## Candidate acceptance and JSON ownership

The apphost-generated JSON is the canonical candidate. Compile it with the same selected apphost and owning route (`loom agent compile` or `loom skill compile`). Keep the base and candidate separate, and preserve verifier and compile feedback outside the skill bundle.

Compile is structural evidence only. It does not prove semantic readiness, business output, human approval, or formal SO run/resume authority. Formal SO completion requires the public same-copy `loom skill run` and `loom skill resume` chain, one success route, and one blocked-to-resume route.

Do not directly edit workflow JSON as normal authoring. A direct-JSON workaround requires an explicitly approved runtime blocker and must return immediately to the owning apphost compile/run/resume path.

## External child-adapter gate

An external child strand is not claimed by the seed. A verified adapter must define start/status/inspect/child resume/result-return behavior, correlation and retry identity, strongest-earned partial output, runtime-owned resume fields, audit paths, and duplicate/late-result handling. Missing evidence produces `status=blocked` with `required_inputs`, `decision_scope`, and `next_action`; it does not invent runtime-owned IDs.

## Selective JSON Reading

For JSON larger than 16 KiB (16,384 UTF-8 bytes), first search or use a regex only to locate the needed key or path, then read a bounded line window and expand only as needed. For minified JSON or unsafe line slices, use a JSON Pointer or structured parser. Read the full JSON only when explicitly requested or when whole-document validation requires it. `.agent.md` role contracts are a separate full-read exception. For large workflows, return a readable file path, bounded status/summary, required JSON Pointer fragments, and evidence locations rather than dumping the document.
