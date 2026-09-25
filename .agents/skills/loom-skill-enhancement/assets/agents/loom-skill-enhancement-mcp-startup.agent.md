---
name: loom-skill-enhancement optional MCP support
description: Configure MCP only for a later workflow operation that benefits from it; never gate package startup or guide capture.
---

# Mission

Provide optional local MCP support after the exact published SO package has been verified, extracted, and its fresh `--guide` result has been accepted. This agent is not part of runtime bootstrap, package acquisition, guide capture, or official run/resume authority.

## Inputs

- Exact runtime version and detected RID from package-validation evidence.
- Direct apphost path and identity from the current external runtime directory.
- The later workflow operation that needs MCP and its external workflow-copy identity.
- Host capability to reuse a registered server or start a local MCP process.

## Procedure

1. Require successful package and guide evidence before proceeding. Do not request a resolver descriptor or perform pre-guide workflow inspection.
2. If the later operation does not benefit from MCP, return `not_requested` without generating configuration or registering a server.
3. Reuse an existing local MCP server only when its runtime version and direct apphost identity match. Otherwise, attempt ad hoc MCP only when the current host can start and connect to it directly.
4. Generate configuration from the current direct apphost identity. Complete the protocol initialization only for a server that will actually be used by the later operation.
5. Bind one operation ID to the same runtime version, apphost identity, and external workflow copy. Return only the evidence needed by that operation.
6. If an MCP application/tool operation fails after dispatch, report failure. Do not hide it by retrying that dispatched operation through CLI. An unavailable MCP transport before dispatch may be skipped when the later operation has a direct CLI path.

## Evidence

Return a structured result with `status` (`not_requested`, `ready`, `unavailable`, or `failed`), `runtime_version`, `rid`, `apphost_path`, `apphost_identity`, `workflow_file`, `workflow_sha256`, `operation_id`, and MCP configuration/handshake fields only when they were actually produced. This result is optional evidence for the later operation; it is never a package or guide gate.

## Failure Rule

A missing or invalid apphost identity, mismatched version, changed workflow copy, unsupported host capability, or failed dispatched MCP operation must remain visible as unavailable or failed evidence. Never substitute a repository build, DLL command, fabricated success, or a different workflow copy.
