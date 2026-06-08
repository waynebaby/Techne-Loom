# Workflow JSON Contract

[中文](../../zh-cn/architecture/json-contract.md)

The canonical JSON contract is the portability layer between ecosystems and callers.

## Contract Goals

- Encode workflow structure without host-specific dependencies.
- Preserve explicit step kinds, state, history, artifacts, and waits.
- Support deterministic SO execution and AO control-state exchange.

## Minimum Direction

- A workflow instance contains identifiers, nodes, current position, context, history, and status.
- Step or transition kinds stay explicit in serialized form.
- Blocking returns are machine-first payloads with required input contracts.
- AO resume input and SO external step results use structured envelopes instead of prose-only callbacks.

Schema and concrete examples are documented under the reference section and will be implemented alongside the public packages.
