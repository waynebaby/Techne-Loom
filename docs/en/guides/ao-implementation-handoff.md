# AO Implementation Handoff

[中文](../../zh-cn/guides/ao-implementation-handoff.md)

This guide exists so AO can continue on another machine even when the AO runtime itself is not part of the current commit.

## Current Ground Truth

- The committed AO runtime is not implemented yet.
- The authoritative public design contract is [AgentOrchestrator Guide](../reference/products/ao-guide.md).
- The target runtime path is the official `ModelContextProtocol` C# SDK over `MCP/stdio`.
- The design must preserve a structured sampling-planner route instead of hiding planner requests in prose.

## What Must Stay True

- AO and SO are separate products. AO must not be framed as a parent runtime over SO.
- AO is exploratory and boundary-oriented. SO stays deterministic and step-execution-oriented.
- AO callers resume with structured data, not freeform narrative summaries.
- AO control outputs remain machine-first: workflow path, event log path, status, boundary reason, next frontier, and optional sampling request.

## First Implementation Slice To Resume

1. Create the AO host entry point under `src/dotnet/Techne.Loom.AgentOrchestrator`.
2. Wire the official MCP server/session path before adding product-specific orchestration logic.
3. Implement `run` and `resume` surfaces that persist a mutable workflow file plus an append-only event log.
4. Emit structured boundary payloads for clarification, delegation, tool probing, and sampling requests.
5. Keep the workflow and event artifacts durable enough for cross-turn resume.

## Minimum Done Bar For The Next AO Slice

- `dotnet build` succeeds for the AO project.
- AO returns a machine-readable boundary payload instead of placeholder text.
- AO persists both workflow and event-log paths.
- AO can resume from a structured result file.
- AO documents where sampling requests appear in the control payload.

## Recommended Validation

- Add a smoke test that starts AO, forces a boundary, resumes with a result file, and verifies workflow plus event-log continuity.
- Add one test that asserts sampling/planner requests are emitted as structured data.
- Keep the AO guide and implementation aligned whenever new boundary fields are introduced.
