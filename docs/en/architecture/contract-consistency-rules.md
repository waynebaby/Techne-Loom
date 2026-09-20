# Contract Consistency Rules

[中文](../../zh-cn/architecture/contract-consistency-rules.zh-CN.md) | [Root](../README.md)

These rules keep target contracts, workflow bindings, and runtime context consistent without making AO or SO domain interpreters.

## One Authority Per Layer

- `assets/so-workflow/contract.json` is the target business contract.
- The workflow JSON is the execution and gate contract.
- AO/SO runtime is the bounded reader, cache, route, and persistence authority.
- A business step is the domain interpreter.

Do not copy the full contract into workflow JSON or create a second mutable business truth.

## Required Target Contract

Every Loom-governanced enhanced target skill must contain `assets/so-workflow/contract.json`. It must be a JSON object with a non-empty `name` and object-valued `inputs`, `outputs`, and `default_assumptions`. Additional domain fields are allowed.

The enhancement workflow reads this file before planning and authoring, records it as `current_contract` in the bounded reference pack, and records path, parse result, and optional source hash evidence. The initial alignment gate is mandatory. Runtime contract edits after that gate are allowed under B+ and are read by later referenced steps.

## Workflow Binding

The root `contractBinding` points to the target contract. A step's `contractRefs` are JSON Pointers into that file. Compile checks field shape, relative path rules, reserved keys, and pointer syntax; compile does not validate arbitrary domain fields.

## Runtime Consistency

For each referenced step, AO/SO reads one byte snapshot and performs parse, optional hash calculation, and pointer projection from that same snapshot. A path-plus-hash cache may reuse unchanged content. A changed file is read again. Missing or malformed content, invalid pointers, path traversal, or bound overflow fails closed.

## Context Separation

The step receives bounded fragments in `contract_context`. Runtime read metadata belongs to runtime-owned audit/context metadata. The current workflow context is bounded separately. Contract fragments must never silently overwrite ordinary business context keys.

## AO/SO Parity

AO and SO use the same Common provider and limits. Their orchestration responsibilities remain independent:

- SO injects fragments before a referenced deterministic transition.
- AO injects fragments into the current boundary/planning workflow context.
- Neither runtime decides what the contract means for the target domain.

## Verification

A B+ change is incomplete until:

- generated AO and SO schema/demo artifacts agree with the binding fields;
- both demos compile with their matching runtime;
- provider, AO, SO, resume, failure, and path-boundary tests pass;
- English and Chinese pages link to each other;
- the enhancement workflow proves `current_contract` evidence before planning and final Done.
