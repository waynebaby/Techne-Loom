# Workflow JSON Contract

[中文](../../zh-cn/architecture/json-contract.md)

The canonical JSON contract is the portability layer between ecosystems and callers.

Repo-wide loom terms such as **weave out** and **weave back** are defined in [Workflow Terminology](workflow-terminology.md). This page keeps the JSON contract names explicit.

## Contract Goals

- Encode workflow structure without host-specific dependencies.
- Preserve explicit step kinds, state, history, artifacts, and waits.
- Support deterministic SO execution and AO control-state exchange.

## Minimum Direction

- A workflow instance contains identifiers, nodes, current position, context, history, and status.
- Step or transition kinds stay explicit in serialized form.
- Blocking returns are machine-first payloads with required input contracts. In repo terminology, these are weave-out surfaces.
- AO resume input and SO external step results use structured envelopes instead of prose-only callbacks. In repo terminology, those envelopes are weave-back surfaces.

## Current Public Contract Layers

### Workflow file

- The persisted workflow file currently uses camelCase property names.
- Polymorphic task nodes use the `$kind` discriminator.
- Nested `context`, command parameters, and side-loaded object values are expected to survive round-trips as dictionaries/lists rather than raw `JsonElement` payloads.

### SO control payloads

- The `<so_property>` JSON envelope currently uses camelCase outer fields: `type`, `timestampUtc`, `payload`.
- The public payload inside that envelope uses snake_case for stable caller-facing fields such as `workflow_file`, `event_log_file`, `current_node_id`, `required_inputs`, and `memory_for_next_step`.

### SO resume envelope

- `so resume --result-file` currently expects a JSON object with `transition_id`, optional `correlation_key`, and `payload`.
- That JSON object is the current SO weave-back sidecar.

## Current Runtime Guarantees

- Nested JSON objects from workflow files, context files, and result sidecars are normalized into runtime dictionaries/lists before evaluation.
- Unsupported model semantics fail explicitly rather than silently degrade.
- Workflow files and CLI sidecars are related, but they are not one undifferentiated JSON surface.

## Current Scope Separation

- The current public SO runtime supports one fully materialized workflow file contract.
- It does not yet expose a separate standalone schema artifact for every CLI sidecar surface.
- AO control payloads remain a documented design target until the public AO runtime lands.

Schema and concrete examples are documented under the reference section and will be implemented alongside the public packages.
