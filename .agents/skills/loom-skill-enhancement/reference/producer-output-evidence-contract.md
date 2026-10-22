# Producer Output Evidence Discovery

## Purpose

This reference guides the driver or agent performing an AO or SO workflow design when a changed context field depends on an external producer. It prevents unnecessary user questions while keeping type evidence independent from the workflow catalog.

This is a discovery and decision contract, not a universal schema for business outputs. The driver for the target skill decides which evidence is authoritative and sufficient for the specific producer, field, runtime, and workflow route. Do not assume that reviewer, Git, AskUser, WaitResume, gate, or other producers share one business payload schema.

## Authority Boundaries

Keep these sources distinct:

- The selected AO or SO apphost's fresh English guide, generated workflow schema/demo, and matching compile establish runtime and workflow-envelope behavior.
- The target skill's `SKILL.md`, target-local agent contracts, scripts, command definitions, and business contract establish target-specific behavior.
- The owning `context_fields` catalog establishes workflow context paths, JSON types, nullability, container types, and ownership. It describes the expected workflow context; it is not independent proof of a producer's output.
- An authoritative producer schema or exact producer contract establishes declared output shape for the producer/version it covers.
- An actual producer result establishes the shape observed in that execution. A sample alone does not establish every allowed variant or nullability rule.
- The workflow definition establishes how a producer payload is projected through `requiredInputs`, `resumeOutputKey`, `outputPath`, and `outputBindings`.

The runtime schema/demo describes the workflow envelope. It does not define custom business values emitted by a target-skill reviewer, agent, command, or tool. Do not copy complete runtime guide pages into a skill to stand in for target business evidence.

## Required Discovery Before Asking

The target-skill driver/agent owns the evidence-sufficiency decision. Before asking the user for a schema, result, or sample, it must:

1. Read the target skill's `SKILL.md` first and identify the requested change and exact target root.
2. Read the relevant target-owned business contract and owning `context_fields` catalog. Treat the catalog as the expected context contract, not as producer evidence.
3. Trace each affected field through the exact workflow route: producer, source payload path, projection, destination context path, downstream consumer, and any gate that reads it.
4. Inspect the exact target-local agent contract, tool or command definition, script, parser, schema, fixture, example, and reference that can define or exercise the producer. Follow paths from the target skill rather than assuming a similarly named file is relevant.
5. Inspect current run, resume, audit, and probe outputs when they are available in the supplied execution context. Use the exact paths and versions; do not rely on remembered or unrelated runs.
6. Consult the selected runtime guide/schema/demo only for generic runtime and workflow-envelope semantics. Do not expect `so --guide`, `ao --guide`, or schema/demo export to produce a target skill's custom business payload.
7. Decide whether the available evidence answers the actual type question for every changed field, including nullability and container item/value types. Record the inspected sources and the limits of each source.

The driver may accept a target-local contract, a representative result, a safe focused probe, or a combination. It must not ask for both a schema and a sample when one authoritative source is enough for the affected field. It must not request unrelated producer outputs.

## Evidence Selection

Choose the least burdensome evidence that can establish the changed field's contract:

- **Authoritative producer schema/contract:** Prefer a versioned schema or the exact target-local agent/tool contract when it is actually used by the producer and fully covers the relevant output shape. Record the target-relative path and the exact property path or bounded source location.
- **Actual producer result:** Use a result when output is dynamic or no authoritative schema exists. Select the smallest representative result that preserves the relevant value types, field presence/omission, explicit `null`, nesting, and container shape. Record its target-relative fixture path or its real per-run evidence path.
- **Focused probe:** Run the smallest safe probe when the question concerns actual producer or projection behavior and the producer can be invoked with available inputs. Use the selected apphost when runtime behavior is involved, and bind the result to the exact runtime/version and workflow path.
- **Combined evidence:** Combine sources only when one source cannot establish a required property, such as optionality, nullability, multiple variants, or the projection from source payload to context.

A probe result proves only what that probe exercises. Compile success and a catalog read/echo do not prove the business output shape of a dynamic producer.

## Producer-Specific Judgment

### Reviewer or subagent

Read the exact target-local `.agent.md` declared by the target skill or workflow. Its output contract applies only to that agent and version. If no output contract exists, inspect available real results or safely exercise the agent through its declared driver. Do not invent a universal reviewer schema or borrow another agent's contract.

### Git or command-line producer

For `.gitignore` or ignore-candidate analysis, inspect the exact rules, tool contract, and structured candidate result. Do not assume a universal `ignore` output shape.

Inspect the exact command, arguments, version assumptions, stdout/stderr handling, and parser used by the workflow. Distinguish raw process text from the structured value the workflow actually consumes. When safe, run the smallest deterministic command/projection using task inputs before asking the user for a result.

### AskUser

Separate the target skill's question/answer contract from the runtime's generic receipt and workflow adapter. The target skill owns its question IDs, answer types, requiredness, choices, and context mappings. Runtime documentation establishes generic AskUser/receipt behavior only. AskUser may request user-owned inputs or decisions; it must not ask the user to supply runtime-owned provenance, workflow paths, audit paths, or system-generated facts.

A generic receipt schema does not establish the target skill's business answer schema.

### WaitResume or other external continuation

Inspect the exact transition's `requiredInputs`, `resumeOutputKey`, `outputPath`, and `outputBindings`. Distinguish the generic runtime envelope from the target-specific payload inside it. A runtime guide can establish the generic resume contract; the target producer or an actual result must establish custom `payload.result` fields.

### Gate

A gate is normally a consumer predicate, not an output producer. Identify the concrete producer for every field consumed by the gate, the reachable route, the applicable value semantics, and the blocked route if evidence is missing. Do not request a separate gate-output schema for a boolean decision already defined by the workflow predicate. A declaration such as `satisfiesGateIds` or `publishesOutputFamilies` does not prove that a value exists.

## Redaction And Fixture Safety

When a result is safe and useful to retain as a target-skill fixture:

- Preserve JSON types and the distinction between absent, `null`, empty, and non-empty values.
- Preserve relevant nesting, arrays, item types, object/value types, and variants.
- Replace sensitive values with representative placeholders without changing their types.
- State whether the sample is synthetic or redacted and what the transformation may affect.
- Do not check in credentials, tokens, personal data, private repository contents, or unrelated user information.

If redaction removes a fact needed to establish the field type or shape, use an authoritative schema or a focused probe that does not expose that data. Do not claim a sample proves ranges, formats, uniqueness, or constraints that it does not preserve.

## Recording And Verdicts

For every added or changed `context_fields` entry, record the comparison in the existing `contextFieldTypeComparisons` section of the runtime-owned `static-contract-review.json`. Do not introduce a parallel evidence report or store per-run output in the checked-in skill bundle.

Each comparison records:

- Target context path, declared JSON/container type, nullability, and owner.
- Concrete producer and workflow node/transition.
- Evidence kind: authoritative schema/contract, actual result, or focused probe.
- Exact evidence path and property path, JSON Pointer, or bounded source location.
- Source payload path and projection to the target context path.
- Observed type/shape, including relevant nullability and container details.
- Runtime/version when relevant to the producer or projection.
- Redaction or synthetic-data limits.
- Verdict: `passed`, `failed`, or `unknown`, with a concise reason.

Use `unknown` when required evidence is missing or insufficient. Do not infer types from names or paths, silently equate missing with `null`, or treat a declaration as proof of a produced value. A failed or unknown comparison blocks readiness only when the changed design depends on that field. Keep the block scoped to the unresolved field.

## When To Ask The User

Ask the user only after completing the discovery above and determining that the missing fact is required for the requested change and cannot be obtained by a safe focused probe.

The question must name:

- The exact producer and workflow node/transition.
- The specific field or shape that remains unknown and why it matters.
- Which target-skill contracts, assets, fixtures, and available run evidence were inspected.
- Whether an authoritative schema/contract or a minimal sanitized result would resolve the gap.
- Which type or structural properties must remain visible after redaction.
- That the affected candidate remains blocked if neither source is available.

Do not ask a broad question such as “Can you provide the producer output schema?” without naming the producer and unresolved fields. If the target skill or current run already contains sufficient evidence, use it and continue without asking. If no changed field depends on the uncertain output, do not block the design on it.
