# AO Implementation Handoff

[中文](../../zh-cn/guides/ao-implementation-handoff.md)

This guide exists so another machine can continue AO implementation and contract hardening from the current committed runtime.

## Current Ground Truth

- The committed AO runtime is implemented in `.NET`.
- The authoritative public design contract is [AgentOrchestrator Guide](../reference/products/ao-guide.md).
- The target runtime path is the documented CLI/package contract.
- The design must preserve explicit weave-out request data instead of hiding external comparison or planning asks in prose.

## What Must Stay True

- AO and SO are separate products. AO must not be framed as a parent runtime over SO.
- AO is exploratory and seam-oriented in explanatory docs, while explicit boundary payloads remain protocol surfaces. SO stays deterministic and step-execution-oriented.
- AO callers resume with structured data, not freeform narrative summaries.
- AO control outputs remain machine-first: `session_id`, `workflow_file`, `event_log_file`, `status`, `boundary_reason`, `next_frontier`, and optional `weave_out_request` data.

## Next Hardening Slice

1. Keep the AO resume contract strict so stale or mismatched weave-back envelopes are rejected.
2. Keep the documented `run` / `resume` CLI envelopes aligned with AO's weave-out and weave-back control contract.
3. Keep AO terminology aligned with the repo-wide weave out / weave back glossary.
4. Keep workflow and event artifacts durable enough for cross-turn resume.

## Minimum Done Bar For The Next AO Slice

- `dotnet build` succeeds for the AO project.
- AO returns machine-readable boundary payloads and result payloads.
- AO persists both workflow and event-log paths.
- AO can resume from a structured result file and reject stale weave-back envelopes.
- AO documents where weave-out request data appears in the control payload.

## Recommended Validation

- Add a smoke test that starts AO, forces a boundary, resumes with a result file, and verifies workflow plus event-log continuity.
- Add one test that asserts weave-out request data is emitted as structured data.
- Keep the AO guide and implementation aligned whenever new boundary fields are introduced.
