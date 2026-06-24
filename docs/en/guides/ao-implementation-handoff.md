# Loom Agent Execution Orchestrator Implementation Handoff

[中文](../../zh-cn/guides/ao-implementation-handoff.md) | [Root](../README.md)

This guide exists so another machine can continue Loom Agent Execution Orchestrator implementation and contract hardening from the current committed runtime.

## Current Ground Truth

- The committed Loom Agent Execution Orchestrator runtime is implemented in `.NET`.
- The authoritative public design contract is [Loom Agent Execution Orchestrator Guide](../reference/products/ao-guide.md).
- The target runtime path is the documented CLI/package contract.
- The design must preserve explicit weave-out request data instead of hiding external comparison or planning asks in prose.

## What Must Stay True

- Loom Agent Execution Orchestrator and SO are separate products. Loom Agent Execution Orchestrator must not be framed as a parent runtime over SO.
- Loom Agent Execution Orchestrator is exploratory and seam-oriented in explanatory docs, while explicit boundary payloads remain protocol surfaces. SO stays deterministic and step-execution-oriented.
- Loom Agent Execution Orchestrator callers resume with structured data, not freeform narrative summaries.
- Loom Agent Execution Orchestrator control outputs remain machine-first: `session_id`, `workflow_file`, `event_log_file`, `status`, `boundary_reason`, `next_frontier`, and optional `weave_out_request` data.

## Next Hardening Slice

1. Keep the Loom Agent Execution Orchestrator resume contract strict so stale or mismatched weave-back envelopes are rejected.
2. Keep the documented `run` / `resume` CLI envelopes aligned with the Loom Agent Execution Orchestrator weave-out and weave-back control contract.
3. Keep Loom Agent Execution Orchestrator terminology aligned with the repo-wide weave out / weave back glossary.
4. Keep workflow and event artifacts durable enough for cross-turn resume.

## Minimum Done Bar For The Next Loom Agent Execution Orchestrator Slice

- `dotnet build` succeeds for the Loom Agent Execution Orchestrator project.
- Loom Agent Execution Orchestrator returns machine-readable boundary payloads and result payloads.
- Loom Agent Execution Orchestrator persists both workflow and event-log paths.
- Loom Agent Execution Orchestrator can resume from a structured result file and reject stale weave-back envelopes.
- Loom Agent Execution Orchestrator documentation states where weave-out request data appears in the control payload.

## Recommended Validation

- Add a smoke test that starts Loom Agent Execution Orchestrator, forces a boundary, resumes with a result file, and verifies workflow plus event-log continuity.
- Add one test that asserts weave-out request data is emitted as structured data.
- Keep the Loom Agent Execution Orchestrator guide and implementation aligned whenever new boundary fields are introduced.
