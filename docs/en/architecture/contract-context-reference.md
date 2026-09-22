# Contract Context Reference

[中文](../../zh-cn/architecture/contract-context-reference.zh-CN.md) | [Root](../README.md)

This page defines the B+ contract context provider shared by AO, SO, and skills being enhanced under Loom Skill Orchestrator governance.

## Meaning

A contract of the skill being enhanced at `assets/so-workflow/contract.json` is a static business dictionary. It is not compiled into the generic workflow schema and it is not interpreted as CDD, research, or another domain by AO or SO.

- `compile` validates workflow structure and contract binding/reference syntax only.
- SO reads contract content before each referenced transition. AO reads and merges the references from the current boundary's candidate transitions before writing its planning context.
- The business step interprets the fragment and decides its business output or next action.
- Runtime state remains separate from the static contract.

## Binding

A workflow may declare a root binding:

```json
{
  "contractBinding": {
    "path": "assets/so-workflow/contract.json",
    "format": "json"
  }
}
```

A transition declares only the fragments it needs:

```json
{
  "contractRefs": [
    "/inputs/request",
    "/outputs/result"
  ]
}
```

The path is resolved under the declared target asset root. Absolute paths and parent traversal are rejected for governed workflows.

## Runtime Read

Before a referenced step runs, the provider:

1. Reads one byte snapshot of the current contract file.
2. Parses that same snapshot.
3. Resolves each JSON Pointer in `contractRefs`.
4. Applies byte, depth, array, and object-property bounds.
5. Returns only the bounded fragments to the step.
6. Records path, read time, optional SHA-256, cache state, returned bytes, and refs in runtime-owned audit/context metadata.

AO uses the same provider at a planning boundary. It merges refs from the current state's candidate transitions so branch selection receives one bounded context; this does not claim that every candidate transition will execute.

Steps without `contractRefs` do not read the contract.

## Manual Changes

Contract files may be edited while a workflow is running. The provider uses a path-plus-hash cache: unchanged content can be reused; changed content is read again before the next referenced step. A valid manual change does not fail the workflow. Missing files, malformed JSON, invalid pointers, path escapes, or exceeded bounds fail closed with a repair instruction.

A resume operation keeps the same workflow lineage but reads the current contract for its next referenced step. It does not replay completed steps.

## Step Context

The business step receives a bounded `contract_context` envelope containing fragments only. Runtime-owned metadata is kept separately. The bounded current workflow context is provided through the runtime's existing context channel and must not be confused with the static contract.

## Diagnostics

Diagnostic inspection may use either a workflow binding or an explicit contract file:

```powershell
dotnet so.dll inspect-contract-fragment --workflow-file <workflow> --json-pointer <pointer>
dotnet so.dll inspect-contract-fragment --contract-file <contract> --json-pointer <pointer>
```

Diagnostic output includes the fragment and read metadata. Diagnostic inspection does not replace official `run`/`resume` execution.

## Enhancement Gate

Every skill being enhanced must keep its own contract at `assets/so-workflow/contract.json`. The enhancement workflow reads it as `current_contract`, checks the minimum surfaces `name`, `inputs`, `outputs`, and `default_assumptions`, records evidence, and aligns workflow refs to the contract. The enhancement skill's own governance contract is never copied into a skill being enhanced.
