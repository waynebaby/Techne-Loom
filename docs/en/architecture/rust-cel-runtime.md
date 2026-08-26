# Rust+CEL Runtime Route

[中文](../../zh-cn/architecture/rust-cel-runtime.md) | [Architecture index](README.md)

## Status

Rust+CEL is a future fourth Loom runtime route. It is not implemented by the current .NET runtime, and it is not Rust source-code execution inside workflow expressions. The current supported route remains C# compiled by Roslyn.

## Purpose

The future route is a cross-platform Loom Runtime Core implemented in Rust with CEL as its canonical expression language. It is intended to provide a small, portable execution core for environments that cannot host the .NET runtime directly while preserving the same workflow wire contract.

## Canonical Contract Reuse

The route must reuse the existing root fields and must not invent a parallel schema:

- `runtimeBinding` identifies the runtime and CLI that owns workflow execution.
- `expressionBinding` identifies `language`, `languageVersion`, `contractId`, `contractVersion`, `requiredExpressionCapabilities`, and `compileFeedbackContract`.
- `ExpressionDefinition` carries `kind`, `source`, `entryPoint`, and `resultType`.
- `ExpressionCompileFeedback` follows `detailedCompileFeedbackV1` for success and failure.

The Rust runtime must reject unsupported bindings, asynchronous execution, unsupported expression forms, and missing contract capabilities. It must report stable diagnostic codes, categories, source spans, actionable messages, suggested fixes, referenced symbols, compiler identity, resolved form, result type, capabilities, and warnings. A host interpreter exception copied verbatim is not sufficient feedback.

## CEL Compilation Layers

1. Validate the JSON binding and the root runtime ownership.
2. Validate the expression definition shape and result type.
3. Parse CEL syntax and map source spans to the original expression.
4. Resolve symbols against the read-only workflow contract API.
5. Enforce capability and resource limits for the trusted-template execution model.
6. Emit `ExpressionCompileFeedback` with the same contract identity and diagnostic categories used by the .NET route.

Cross-language migration remains skill-owned. A skill translating C# to CEL must preserve the original source, translated source, translator/tool identity, review evidence, and compile feedback. The runtime never auto-translates source.

## Platform And Distribution Matrix

| Target | Runtime artifact | Distribution | Verification |
| --- | --- | --- | --- |
| Windows x64 | `loom-runtime.exe` | GitHub Releases installer and checksum | CI compile, unit tests, smoke workflow |
| Linux x64 | `loom-runtime` | GitHub Releases archive and checksum | CI compile, unit tests, smoke workflow |
| macOS arm64 | `loom-runtime` | GitHub Releases archive and checksum | CI compile, unit tests, smoke workflow |
| macOS x64 | `loom-runtime` | GitHub Releases archive and checksum | CI compile, unit tests, smoke workflow |

Installers and archives must publish SHA-256 checksums. Release automation must keep the runtime version, CLI contract version, and direct package guide tree aligned.

## Six Milestones

1. **Documentation first**: freeze terminology, canonical fields, trust model, feedback contract, and platform targets in bilingual architecture docs.
2. **Prototype validation**: prove CEL parsing, read-only context access, source spans, capability checks, and representative workflow predicates.
3. **Contract freeze**: publish compatibility tests for binding, definitions, feedback, errors, and cross-runtime fixtures.
4. **Runtime implementation**: implement the Rust core, resource limits, workflow loading, compile cache, and execution lifecycle.
5. **CLI release**: ship platform artifacts, checksums, startup contract, `--guide`, compile, run, resume, and audit surfaces.
6. **.NET adapter integration**: integrate the adapter behind the same router without changing the canonical wire schema or weakening C# behavior.

Until all milestones and the shared feedback contract are complete, Rust+CEL must be documented as future-only and must not be marked as a supported expression language.
