# Workspace Agent Rules

> `AGENTS.md` is the automation-facing source of repository execution rules.

<!-- cto-skills-manager-managed:begin -->
## Shared Python Environment

This workspace uses the shared virtual environment pointer from `.venv.path`.

- Managed by `cto-skills-manager`.
- Windows: resolve `.venv.path` with PowerShell before invoking Python-based tooling.
- If a Python runtime is available but the `.venv.path` target does not exist yet, initialize that virtual environment first and then use the new environment.
- Linux: resolve `.venv.path` with bash before invoking Python-based tooling.

## Run Output Naming

- When a skill creates a per-run output root, keep the skill-owned parent directory and name the run root `exec-<YYYYMMDD_HHMMSS>-<skill-slug>-result/`.
- Keep the timestamp immediately after `exec-` so runs remain sortable even when adjacent steps switch skills.
<!-- cto-skills-manager-managed:end -->

## Copilot Tool Restrictions

- GitHub Copilot must not use the `apply_patch` tool in this repository. Use the VS Code editor or another repository-approved file-editing mechanism instead.

## Repository Direction

- Techne Loom is a .NET-first multi-ecosystem mono-repo with parallel package families across .NET, Node.js, and Python.
- `AgentOrchestrator` and `SkillOrchestrator` are separate products in different niches. They do not call each other and must not be framed as a parent/child runtime pair.
- The user-facing product name for AO narrative, landing-page copy, and guide positioning is `Loom Agent Execution Orchestrator`.
- That user-facing name does not rename implementation identity. Keep `Techne.Loom.AgentOrchestrator`, `dotnet ao.dll`, `/loom-plan-execution`, source paths, and type names unchanged unless a task explicitly calls for a code/package rename.
- Shared abstractions may align at a low level, but packaging, release identity, and product-facing contracts stay independent.

## Workflow Identity And Business Scope

- A workflow template's business steps must match its declared target intent. SO self-bootstrap and target-skill enhancement workflows may contain guide, asset review, aggregate, repair, and validation steps because those are their business purpose; target-skill business workflows must not inherit those enhancement steps.
- Governed workflow instances must declare taskType and workflowKind. Use skill_enhancement with so_self_bootstrap for /loom-skill-enhancement self-bootstrap, skill_enhancement with target_skill_enhancement for an outer target-skill enhancement run, and a target-specific taskType with target_skill_business for a target skill's domain workflow.
- Compile and load validation must reject incompatible taskType/workflowKind declarations before execution. A target business task such as requirement_generation or model_generation must never execute an enhancement workflow merely because both are using the SO runtime.
- caseId and runId identify one business execution and must remain on the same external workflow copy through compile, run, resume, audit, and completion evidence. They are execution identity, not a replacement for the target workflow's business outputs.


## Current Implementation Contract

- Plan, replan, and execution are disk-backed and sessionless. MCP transport connections, host processes, and in-memory objects must not be required to recover business state.
- AO is an independent workflow executor with a first-class `Plan` step. SO remains an independent executor. They may share a framework-neutral execution core, but product identity, CLI, package, and release boundaries remain separate.
- Each product owns one canonical `WorkflowInstance` as the mutable business state. Events, audit records, logs, result envelopes, and large artifacts are companion records and must not become a second mutable execution truth.
- Planning Review is an implementation-planning edit loop. It may revise workflow drafts, templates, and default bundles before implementation, but it is not a runtime node, gate, MCP tool, or persisted execution state.
- Agent-facing workflow access is fragment-first: expose summaries, bounded JSON Pointer fragments, bounded events, and artifact manifests by default. Full workflow reads require an explicit purpose and configured size limits.
- MCP is local stdio only for this scope. Existing local workflow command kinds, including Python and HTTP command execution where explicitly authored, are not MCP Web transport and must not be removed solely because MCP transport is stdio.
- For `/loom-skill-enhancement` self-bootstrap and every Loom-governanced target-skill route, the first governed external step after exact published SO runtime preflight must start the selected runtime's `mcp stdio` server and use it for a bounded workflow-fragment check. Complete the MCP initialize handshake, call the product-scoped fragment tool, and persist `mcp_startup_evidence` on the same external workflow copy before `--guide`, planning, authoring, validation, compile, run, resume, or downstream input collection. MCP failure is failed preflight; do not silently continue through direct CLI or local orchestration. This is a governed workflow step, not a request to configure the current editor's `mcp.json`.

## Packaging And Layout

- Organize source by language root from the start: `/src/dotnet`, `/src/nodejs`, and `/src/python`.
- Every project unit is a publishable package.
- In `.NET`, each `.csproj` maps to one NuGet package or tool package.
- In Node.js, each package directory with its own `package.json` maps to one npm package.
- In Python, each package/build target with its own `pyproject.toml` maps to one PyPI distribution or wheel.
- Keep package families parallel by role: `abstractions`, `common`, `agent-orchestrator`, and `skill-orchestrator`.

## Documentation And Language Rules

- Public docs are bilingual by default.
- Keep mirrored trees under `/docs/zh-cn` and `/docs/en`.
- Demo indexes and stage `README.md` or `Readme.md` files under `/demos` are public docs too; keep the English default file beside a same-folder Chinese mirror that uses the `.zh-CN.md` suffix.
- Every paired page must include a reciprocal header link to the counterpart page.
- Skill-local references under `.agents/skills/*/reference/` must be English only so skills remain deterministic and runnable offline without multilingual drift.
- When public docs need an agent-neutral example path for an external target skill root, prefer `{agentskillfolder}/...` instead of repository-specific roots.
- Use `.agents/skills/...` explicitly only when the doc is describing this repository's built-in skill root or built-in manifest catalog.
- Localized narrative for skills belongs in bilingual docs under `/docs/en` and `/docs/zh-cn`, not in multilingual variants under skill-local `reference/` directories.
- Root bilingual files are required for `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, and `SECURITY.md`.
- Root English files keep the default file name. Chinese mirrors use the `.zh-CN.md` suffix.
- Root bilingual files should include reciprocal header links.
- Agent definition files (`*.agent.md`), `AGENTS.md`, and other agent-specific configuration files do not require Chinese mirror files.
- `AGENTS.md` is the only repository agent-rules source and is English-only. Do not create or maintain `AGENTS.zh-CN.md` or another language mirror for repository agent rules.
- Keep `AGENTS.md` root-only. Do not duplicate it under `/docs`.
- Product guide source files live at `/docs/<lang>/guides/ao-guide.md` and `/docs/<lang>/guides/so-guide.md`.
- The SO product guide is a mandatory repository contract for `/loom-skill-enhancement` and every Loom-governanced target skill. Its transition, gate, seam-ownership, output-evidence, and unattended-mode rules must be applied during target-skill authoring, review, compile readiness, and governed execution handoff; this rule does not extend to AO behavior or unrelated workflows.
- For AO-facing user docs, prefer the user-facing name `Loom Agent Execution Orchestrator` in titles, intros, README positioning, and guide navigation, while preserving `ao-guide.md`, `dotnet ao.dll`, and package identifiers as implementation-facing names.
- In docs prose, headings, and callout labels, do not use legacy narrative labels such as `SO Governance`, `SO-enhanced`, or `SO-governed`.
- Prefer `Loom-governanced target skill`, `Loom Skill Orchestrator governance`, `Loom Skill Orchestrator-governanced skill`, or the narrower execution-status wording required by the current slice.
- Preserve implementation-identity literals such as file names, command names, package IDs, schema fields, template kinds, and other checked-in wire values when they intentionally retain `so` naming.
- `dotnet ao.dll --guide` and `dotnet so.dll --guide` must emit version-matched, offline guide surfaces derived from curated docs sources.
- Root package acquisition indexes live at `packages.released.md`, `packages.released.zh-CN.md`, `packages.beta.md`, and `packages.beta.zh-CN.md`, and skills should reference them with absolute GitHub URLs.
- Treat NuGet.org as the first-class latest package source for released and beta package acquisition guidance; GitHub-hosted package assets remain fallback download paths when NuGet.org access is unavailable or when the user explicitly requests asset URLs.
- For AO and SO skills, package download channel and exact runtime version must follow the current skill-local CI/CD-managed package version block or checked-in runtime lock, not an ad hoc user channel choice at download time. Derive `released` versus `beta` from that bound version when needed operationally.
- Those package acquisition indexes must also expose GitHub-hosted latest release fallback links for stable and beta package assets, not only package-manager install commands.
- MCP, CLI, and skill input/output contract docs are first-class deliverables; do not leave them implicit in README prose.
- Workflow definition files are the canonical English information carrier across AO, SO, and Loom-governanced target skills. Keep workflow-owned schema keys, node and transition names/descriptions, workflow phases, expressions, hints, failure guidance, evidence references, and control metadata in English. Keep user/business payload values and localized user-facing output in their source or requested language; localization belongs in the presentation layer and must not change workflow keys or control semantics.

## File-Based CLI Input Rules

- Every file-valued CLI parameter is path-only. The caller must create, finish, and close the complete input set before one command starts; the CLI preflights all required input files before reading, executing, modifying, or writing.
- Workflow builder, editor, and verifier examples are ordinary `.cs` files executed by the built-in Roslyn host. They do not require a caller-created project file or an externally installed C# script runtime.
- Patch content, scripts, JSON, workflows, references, objectives, contexts, instances, and resume results must be prepared as complete disk files. Inline script, JSON, patch, or replacement content is rejected.
- Output files and output directories are destinations owned by the CLI; they are not input-content parameters.

## Runtime Package Family Rules

- Runtime selection belongs to the platform-aware runtime resolver: its default package-channel result is the exact-RID published self-contained executable for the detected platform. AO skills, SO skills, `so-*` skills, and Loom-governanced target skills provide only the exact bound runtime version; they must not bind or persist the OS, architecture, libc, RID, package id, executable name, cache directory, or launch path. `.NET CLI mode` and repository-source debug mode are explicit resolver inputs, and no mode fallback is allowed after CLI startup.
- `.NET CLI mode` uses one exact-version .NET runtime bundle (a NuGet restore set) with a usable `Microsoft.NETCore.App 9.x` host; the bundle must include the embedded Roslyn compiler assemblies used by the C# expression evaluator. Self-contained mode uses one exact-version RID package from the `Techne.Loom.AgentOrchestrator.Runtime.<rid>` or `Techne.Loom.SkillOrchestrator.Runtime.<rid>` family.
- The supported self-contained RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64`; do not cross OS, architecture, or Linux libc boundaries.
- Validate SHA-512, nuspec/package identity, manifest, entrypoint, ZIP safety, and size bounds before launch. Isolate user-level cache entries by product, exact version, and RID, protect them with a cross-process lock, validate in a temporary directory, and publish atomically.
- The resolver must run and verify a fresh `--guide` from its selected launch descriptor before `compile`, `run`, or `resume`, then preserve that descriptor for the execution chain. Skill-owned records retain only the exact version; resolver-produced platform and path facts remain runtime-owned evidence. Errors after CLI startup remain command failures and never trigger fallback.

## Runtime Mode Separation

Resolve the runtime mode before any package-cache lookup or network request. The two package paths are independent and must not be combined.

- `self-contained` mode is the default package-channel path. It validates and acquires only one exact-RID package for the selected product and platform: `Techne.Loom.AgentOrchestrator.Runtime.<rid>` for AO or `Techne.Loom.SkillOrchestrator.Runtime.<rid>` for SO. It launches the validated `ao.exe` or `so.exe` directly. It must not download, validate, extract, or assemble the `.NET CLI mode` .NET runtime bundle.
- `.NET CLI mode` is explicit. Only this mode validates and acquires the same exact-version .NET runtime bundle (a NuGet restore set that includes the embedded Roslyn compiler assemblies used by the C# expression evaluator), checks the `.dll`, `.deps.json`, `.runtimeconfig.json`, Roslyn, and dependency closure, then launches through the shared .NET host.
- Once a mode is selected, a failure stays in that mode and fails closed. Do not fall back from `.NET CLI mode` to self-contained or from self-contained to `.NET CLI mode` after startup or package acquisition begins.
- Runtime evidence must identify `runtime_mode`, exact version, package ids, RID, cache validation, launch descriptor, and failure category. Never report a self-contained RID package as a .NET runtime bundle.

## Package Version Governance

- Treat package-version-bearing content as belonging to one of four categories only: live docs and indexes, skill-local offline references, checked-in runtime locks, or historical demos and audit examples.
- Live docs and indexes such as root release notes, `packages.released*.md`, `packages.beta*.md`, direct exact-version NuGet URL examples, and package install commands must reflect the current latest published version for their channel and should be refreshed by the CI/CD publish workflows.
- Skill-local offline references under `.agents/skills/*/reference/` are deterministic channel snapshots, not floating latest prose. Within one such snapshot, the version block, install commands, direct exact-version package URLs, guide examples, and `resolved_runtime_version` examples must all use the same channel-specific snapshot version.
- Checked-in runtime locks such as `so-package-lock.json` are authoritative only for the owning skill's exact runtime version. The resolver derives channel, package identity, platform/RID, executable, cache location, and launch path at runtime; those facts must not be duplicated in a skill-owned version lock.
- Historical demos, audit artifacts, and narrative reconstruction material may intentionally preserve older package versions for reproducibility. Keep those older versions clearly scoped to demo or audit surfaces, and do not present them as latest-version guidance.
- When a current channel version changes, update every version-bearing surface in that same category together: version blocks, install commands, direct exact-version URLs, workflow refresh regex replacements, and lock-file resolved versions.
- Do not introduce new ad hoc hardcoded "current" package versions in prose when an existing CI/CD-managed version block, skill version block, or checked-in runtime lock already owns that value.

## Mermaid Diagram Rules

- Treat Mermaid diagrams in Markdown as first-class documentation surfaces, not decorative afterthoughts.
- Mermaid diagrams must remain readable for color-blind readers. Color may reinforce meaning, but it must never be the only carrier of meaning.
- When a diagram uses categories, phases, or semantic node classes, apply a second channel in node labels using meaning-aligned emoji, not generic colored-square emoji.
- Choose emoji that match the node meaning closely. Examples: `🧭` intake or navigation, `🔎` research or inspection, `💬` user review or discussion, `📝` drafting, `✅` completion, `⚙️` runtime execution, `📜` contract, `🧾` audit evidence, `❓` decision gate, `🚧` blocked or boundary state, `🔁` continuation or loop.
- When one node class maps to one emoji, all nodes in that class should use the same emoji consistently within that diagram.
- Prefer Markdown legends adjacent to the Mermaid block over embedded legend subgraphs when the embedded legend would distort layout, create large empty boxes, or compete with the main reading path.
- If a Mermaid legend is needed, keep it compact and outside the graph unless the graph layout clearly benefits from an in-diagram legend.
- Keep Mermaid styling semantically stable across related docs: the same concept family should reuse the same emoji and approximately the same color family when practical.
- In Chinese Markdown docs, if a Mermaid node includes an English term or English-first label, append the Chinese equivalent in the same node label on its own line using `<br/>`, with English first and Chinese second, unless the term is intentionally code-like or a literal wire name.
- When Mermaid labels contain bilingual text, HTML line breaks, or punctuation that could confuse parsing, wrap the label text in quotes and keep one language per line instead of a single inline `English / 中文` string.
- Do not force bilingual expansion for literal filenames, CLI tokens, field names, protocol values, or other implementation-identity strings that should stay exact.

## Schema And Compile Consistency Check

- Before every code check-in, run the current AO and SO runtime entry points with `--schema-demo-output <directory>` using separate external output directories.
- Each run must create both `workflow.schema.json` and `workflow.demo.json`. A run that creates only one file is invalid evidence.
- Compile each generated `workflow.demo.json` with the matching AO or SO runtime. The demo must pass the same compile path that the documentation describes.
- Compare the documentation's workflow shape, required fields, node discriminator, expression shape, enum values, and command examples with the generated `workflow.schema.json` and the compile result. The runtime-generated files and compile behavior are the source of truth.
- Do not add or keep a hand-written JSON workflow example as the current compile contract. If a document needs an example, obtain it from the runtime export and identify its runtime version.
- Do not mark a code check-in ready when the docs and generated schema/demo disagree. Record the export commands, runtime version, generated file paths, and compile result in the review evidence.
- Keep generated schema/demo files in an external temporary or execution-output directory unless they are explicitly requested as checked-in deliverables.

### Plain-Language Feedback For Every Language

- This rule applies to every user-facing progress, blocked, error, and completion update from `/loom-plan-execution` (AO), `/loom-skill-enhancement` (SO), and every Loom-governanced target skill.
- Use the language requested by the user. In every language, write for a high-school reader with no knowledge of workflow software. English is not automatically plain language.
- Use short sentences, familiar words, and direct verbs. Explain four things in order: what happened, whether the user's work or data is still safe, why it happened, and what happens next.
- Do not make the reader understand how the software is built. Do not lead with internal status values, step kinds, node IDs, gate names, stage names, handoff terms, runtime terms, or audit jargon. Explain the idea in ordinary language first; define a necessary technical word in the same sentence before using it.
- Keep exact commands, paths, IDs, status values, and evidence fields in a separate `Technical details` section only when they help the user act or verify the result. Machine-readable payloads and audit records may keep exact internal tokens.
- A blocked or failed update must say whether the requested work is wrong or whether a tool, file, or output-folder problem stopped progress. It must say what remains valid and give one concrete next action.
- When SO creates or updates a target skill, copy this language rule into the target skill's user instructions, local subagent prompts, failure guidance, and workflow hints.

### Human-Facing Workflow Language

- This rule applies to every skill and every workflow-facing skill surface, including AO skills, SO skills, `so-*` skills, and Loom-governanced target skills.
- Keep internal workflow identifiers in machine-readable contracts, source code, logs, audit artifacts, and exact implementation documentation when interoperability requires them; do not use those identifiers as the default wording of a user-facing status, explanation, error, or question.
- Translate internal statuses and step kinds into concrete human language. For example: `Done` becomes “the requested work is complete”; `noop` becomes “no action is needed”; `WaitResume` becomes “waiting for your information or confirmation before continuing”; `SubagentCall` becomes “a specialist analysis step is running”; `gate` becomes “a required check” or “approval check”; and `transition` becomes “the next step” or “move to the next stage”.
- Do not directly address the user with internal terms such as `Done`, `noop`, `WaitResume`, `SubagentCall`, `gate`, or `transition` unless the user explicitly asks for exact workflow details or machine-readable evidence.
- User questions must request a concrete human action or decision. Use wording such as “Please choose whether to continue” or “Please provide the remote branch name”; in Chinese-facing interactions, use equivalents such as “请你选择是否继续” or “请提供远程分支”. Do not ask users to supply internal state names, node kinds, gate results, transition data, or runtime-owned artifact details.

- The repo-wide workflow vocabulary and human-friendly status mapping have one bilingual source of truth at `/docs/en/architecture/workflow-terminology.md`; this file is included in the published `docs/en` bundle.
- Use that glossary for explanatory prose across AO and SO docs, guides, READMEs, and future schema explanations.
- Prefer **weave out** and **weave back** when explaining outward control transfer and structured continuation.
- Prefer **strand** over **thread** in repo docs to avoid collision with `.NET` threading terminology.
- Use **seam** for conceptual ownership joins, and keep **boundary** for explicit wire/protocol surfaces such as `boundary_reason` and the `type: "boundary"` envelope inside `<so_property>` blocks.
- When explanatory terminology and current wire names differ, mention both on first use and keep implemented field names explicit.
- Do not introduce new workflow metaphors or human-facing status wording in one product doc without updating the shared bilingual glossary first.

## Subagent Authority Rules

- When a skill or target skill explicitly names a subagent markdown file such as `./assets/agents/<agent-name>.agent.md`, that exact file is the authoritative behavior source for the subagent.
- Do not require that skill-owned or target-skill-owned `.agent.md` files be mirrored into `.github/agents/`, user-profile agent folders, or any other discoverable agent root before they can be used.
- If the runtime can resolve the exact subagent name directly, call that subagent name directly while still treating the declared `.agent.md` file as the behavior contract.
- If the runtime cannot resolve the subagent by exact name, resolve the declared `.agent.md` file path first and pass the resolved file path plus the full file content into the subagent-driving call so the same contract still governs execution.
- When resolving a declared skill-owned or target-skill-owned `.agent.md` path, test the current repository/workspace copy first and the corresponding global installed-skill copy second before failing resolution.
- Do not improvise a near-match role, rewrite the subagent contract ad hoc, or substitute repository-global prose for the declared `.agent.md` file once that file has been named as the route.

## README Positioning

- Treat `README.md` and `README.zh-CN.md` as flagship landing pages, not only technical indexes.
- Use GitHub-supported rich Markdown intentionally: badges, alerts/callouts, comparison tables, Mermaid diagrams, architecture visuals, and strong use-case framing.
- Marketing language can be ambitious, but claims must remain defensible against the current implementation and docs.
- When reframing terminology or ecosystem positioning, use bounded research, including `cto-web-research` when appropriate, before rewriting the landing-page narrative.

## Guide Surface Rules

- `dotnet so.dll --guide` and `dotnet ao.dll --guide` must read the version-matched English `docs/en` tree shipped beside the executable in the published runtime package, then emit one JSON object containing the actual absolute `version`, `docs_root`, and `guide_path` values. No guide pages are embedded in the executable. The guide path is the authoritative entry; callers may inspect the returned docs root only when the guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`; CLI documentation, skill contracts, and tests must be updated with every contract change.
- For AO- and SO-routed skills, once the package channel or runtime source is chosen, the next hard gate is proving that the selected Loom runtime is actually runnable and can produce a fresh `--guide` result from that runtime. Do not continue to planning, authoring, validation, compile, run, resume, or downstream input collection until that `--guide` result exists.
- Once a fresh `--guide` result exists, treat that emitted guide as a hard governance handoff back onto the corresponding published AO or SO package runtime for execution authority. Do not let `--guide` become a side path that drifts back to repository builds, hand-assembled runtimes, or non-governed execution after the guide has already established the package/runtime contract.
- Guides should begin with version, build, and compatibility metadata.
- The fixed AO and SO guide hubs at `docs/en/guides/ao-guide.md` and `docs/en/guides/so-guide.md` are information hubs and must remain at or below 200 lines. Keep each hub focused on version metadata, entry flow, critical contract boundaries, and navigation. Put the operational flow in an adjacent `*-guide-flow.md` file. Keep `*-guide-reference.md` as a concise index and split detailed contracts, behavior, governance, examples, and anti-patterns into adjacent `*-guide-reference-<chapter>.md` files. Keep public bilingual mirrors aligned. Runtime `guide_path` points to the hub under the extracted package `docs/en/guides` tree, while the complete documentation bundle carries the linked flow, index, and chapter pages.
- All `ao-guide*.md` and `so-guide*.md` pages are docs assets under `/docs/en/guides` and `/docs/zh-cn/guides`; do not duplicate or publish guide files under a skill. `ao-skill-reference.md` and `so-skill-reference.md` may document runtime acquisition, but the fresh extracted `--guide` result is authoritative.
- Guides should cover behavior, responsibilities, contracts, templates, examples, and anti-patterns.
- Keep guide content both human-readable and model-ingestible. Use stable fenced blocks such as `guide-contract`, `guide-template`, `guide-checklist`, and `guide-example` when extraction stability matters.
- Guide and reference content should enumerate MCP methods, CLI arguments, planner flows, audit artifact paths, and skill input/output payload shapes explicitly.

## SO Workflow Validation Rules

- For Loom-governanced target-skill templates, `dotnet so.dll compile` and workflow-load paths must enforce more than structural validity; they must also reject missing business-output gates, seam-ownership violations, and done paths that can complete with governance-only evidence.
- `AskUser` seams may request only user-owned inputs or decisions. Runtime-owned facts, runtime provenance, and system-generated artifact paths belong to runtime-owned seams such as `WaitResume` or blocked-resume payloads, not to user prompts.
- Route-aware workflow templates should declare the business-output gates and strongest-earned blocked outputs needed for each governed route so compile/load validation can prove that meaningful business artifacts exist before `done` or before a runtime-owned wait boundary.

## External Result And Evidence Dataflow Rules

- External transitions must use one explicit projection contract: validate payload paths, extract `resumeOutputKey` relative to the payload, write the extracted value to `outputPath`, and apply explicit `outputBindings`; governed templates must not rely on implicit wrapper nesting.
- `satisfiesGateIds` and `publishesOutputFamilies` are declarations, not evidence. Every required output family must have a reachable producer and a concrete `outputPath` or `outputBindings` projection into the current workflow instance context before a gate can pass.
- Governed gates must declare value semantics for required families when empty strings, empty arrays, empty objects, or boolean values have business meaning. Missing and empty evidence must remain distinguishable in validation and runtime diagnostics.
- A persisted workflow instance with `Failed` status can recover to the previous state and resume when the request identifies the most recent failed transition belonging to that state; if failure history, the previous state, or transition ownership evidence is missing, recovery must fail closed. The runtime must preserve failed history and event/audit evidence, restore the instance to `Running`, and retry from that state. A `Succeeded` instance remains terminal for resume and requires a fresh external workflow copy.
- SO CLI commands that read or mutate one persisted workflow file must hold its adjacent cross-process file lock for the complete load, execution, and persistence operation; a contending process must re-read the workflow file after acquiring the lock.
- Published package-channel runtime preflight must verify `so.dll`, `so.deps.json`, and `so.runtimeconfig.json` plus dependency closure before any guide or workflow command; a missing startup-contract file is a failed preflight, never successful runtime evidence.
- Published package-channel runtime restoration must validate a complete .NET runtime bundle (.NET CLI mode) at the exact locked version in local cache before network access; missing or invalid cache may download only that exact version and must never float to latest.
- Enhancement plans and mutable run checklists are per-run evidence under the execution output root. They are not stable target-skill assets; completion manifests may reference them without copying them into a skill bundle.
- Verified audit-step copies must carry `audit-reuse.json` provenance and `artifact_origin: verified-copy`; they are presentation continuity only and cannot replace workflow execution, event-log, gate, guide, or completion evidence.

## Expression Contract Rules

- Legacy expression evaluators and non-C# language values are removed from the repository; only `csharp` is supported and any other language value must fail closed.
- The only currently implemented expression language is `csharp`, evaluated by a Roslyn-based compiler in the .NET runtime. VB and F# are not supported and must not be added as language values, evaluators, or future candidates.
- Workflow templates declare a root `runtimeBinding` (which runtime/CLI executes the workflow) and a root `expressionBinding` (language, language version, contract id/version, `requiredExpressionCapabilities`, and `compileFeedbackContract`). `requiredExpressionCapabilities` is the single canonical capability field name; do not introduce parallel names such as `expressionCapabilities` or `expressionFeatureSet`.
- Guard, succeed, and gate pass expressions use the structured `ExpressionDefinition` shape (`kind`, `source`, `entryPoint`, `resultType`). A plain string is only a compatibility shorthand that requires an explicit C# binding and version; serializers must always write the structured form. Legacy non-C# expression source must fail closed, never be silently reinterpreted as C#.
- Per-node or per-gate expression language overrides are not supported. The root binding is the only canonical binding; do not add local override fields until a mixed-language boundary contract is explicitly approved.
- Expressions are synchronous only: `async`, `await`, and `Task` are rejected. The runtime executes immutable compiled boolean delegates; compile and execute lifecycles are separated internally, and validator, compile, run, and resume must all route through the same compiler/router.
- Expression inputs are trusted checked-in templates that have passed review and compile. The analyzer, reference allowlist, and read-only contract API are constraint boundaries for trusted code, not a malicious-code sandbox. Docs, guides, and diagnostics must not claim stronger isolation.
- `compile` must emit detailed, structured `ExpressionCompileFeedback` for every expression: status, language and version, contract identity, workflow/gate/transition/field location, source span, stable diagnostic code and category (syntax, semantic, contract, security, reference, resource), severity, actionable message, suggested fix, referenced symbols, and compiler identity. Success results must also record the resolved kind, entry point, result type, capabilities, and warnings. Raw compiler text alone is not an acceptable diagnostic.
- Every future supported expression language and runtime must implement the same `detailedCompileFeedbackV1` contract before it can be marked supported. This is a mandatory section of the Rust+CEL architecture docs and of any future Node.js or Python adapter contract; host interpreter exceptions passed through verbatim do not satisfy it.
- Rust+CEL is the fourth runtime route: a future cross-platform Loom runtime core (Rust) with CEL as the canonical expression language. It is not Rust code execution and not a Lua scheme. Its bilingual architecture docs must reuse the canonical `runtimeBinding`, `expressionBinding`, `ExpressionDefinition`, `requiredExpressionCapabilities`, `compileFeedbackContract`, and `ExpressionCompileFeedback` fields; they must not invent parallel schema.
- Node.js and Python remain adapter/ecosystem routes. They do not automatically become expression languages because of their host language, and they must not implement independent evaluators unless a formal language contract with `detailedCompileFeedbackV1` is approved.
- Cross-language or cross-runtime migration is the skill's responsibility: translate the expression source, update binding/version/contract/capabilities, and keep evidence (source, translated source, translating agent/tool, review, and compile feedback). Runtimes never auto-translate expressions.

## Loom Skill Enhancement Governance

- `/loom-skill-enhancement` must plan before it edits a target skill: analyze the target skill inputs, outputs, nodes, guards, branches, loops, user seams, runtime seams, gates, and output evidence before authoring target-skill deliverables.
- Re-enhancement strategy belongs to repository governance and skill reference documents, not to publishable subagent bodies. Apply it equally when the target is `/loom-skill-enhancement`: self-bootstrap uses its own checked-in old template, current contract and concept references, and fresh guide as one input set; it records the strategy for the current run and never recursively launches another enhancement run. Keep this policy in `AGENTS.md`, the skill reference and contract, and the workflow authority; do not duplicate it in `assets/agents/*.agent.md` or generic skill-body text.
- Self-bootstrap-only scope and exceptions must not alter the generic published behavior of a skill or subagent. A published `SKILL.md` or `.agent.md` may describe reusable rules, inputs, outputs, and assets, while self-bootstrap applicability comes from repository policy and the current run context.
- Both `/loom-skill-enhancement` itself and every Loom-governanced target skill are forced onto the Loom Skill Orchestrator-governanced route: no step transition may advance until it has passed a boundary check on the exact external runtime workflow copy, then received explicit approval or structured continuation instruction for that next step. Compile-clean is only a precondition; inferred intent, prose, stale guide results, unapproved draft copies, local orchestration, and direct workflow JSON edits are never valid continuations.
- The workflow template JSON is the authority for review and execution. Mermaid, HTML, and localized plan text are display layers generated from or kept aligned with the template; user feedback must update the workflow template or its source plan inputs, not only the rendered Mermaid.
- For `/loom-skill-enhancement` self-bootstrap and for full-delivery Loom-governanced target-skill enhancement runs, the default governed success path must copy a runtime workflow instance and continue on the public `dotnet so.dll run` / `dotnet so.dll resume` chain until final `Done`; compile-review completion, blocked seams, or compile-ready wording are not normal completion states.
- Do not keep `compile-only` or `compile-ready governance integration` as a supported default or exception completion route for `/loom-skill-enhancement` self-bootstrap or for full-delivery Loom-governanced target-skill enhancement slices unless the user explicitly changes the task contract in that session before implementation starts.
- When a governed route includes business-intake or `AskUser` seams, completion requires weaving back through those seams to final `Done` on the same runtime workflow-copy lineage; reaching a blocked seam is strongest-earned blocked evidence, not completion.
- Workflow visualizations should carry stable node-type semantics. Use light color families consistently: AI/model/subagent work in green, code/tool work in blue, optional user choices in yellow, mandatory mid-run user input in red, and required gate/governance states in white or very light gray.
- Skill-enhancement completion evidence must include the final workflow template, generated Mermaid, node-to-file or node-to-artifact mapping, actual implementation/audit evidence, and the target-skill deliverables changed. Runtime-only validation is not enough.
- Step 1 of the loom-skill-enhancement upgrade is the reusable foundation: plan mode, workflow analysis, template generation, compile-generated Mermaid, confirmation loop, node-to-file mapping, final evidence reporting, and the existing latest-package behavior for normal target skills.
- Step 2 is self-bootstrap: after Step 1 has its own review/fix/validate/commit, `/loom-skill-enhancement` may consume that foundation to become Loom-governanced. The self-bootstrap execution may use the current repository `src` build result and record that local runtime manifest only under the audit root, while the resulting future official skill behavior must still restore the latest package/channel runtime and package-lock semantics.
- Self-bootstrap backups are taken after the Step 1 commit and before Step 2 edits. Back up only loom-skill-enhancement skill-local files to the audit root unless the user explicitly asks for a wider snapshot.

## SO Enhancement Batch Review Method

- In `/loom-skill-enhancement` and every Loom-governanced target-skill enhancement, build one bounded, hashable shared review context after MCP-first runtime proof and fresh guide capture. The context must carry the source manifest, bounded source snapshots, guide/schema/runtime references, its content hash, and the same external workflow-copy identity.
- Independent review or validation responsibilities must consume that shared context by reference and run as one `ConcurrencyStrategy.All` external batch when they do not depend on one another. A batch is complete only after every expected external transition has returned; one result must never authorize the next phase.
- Aggregate all findings from a batch into one explicit findings record before any repair. The repair step receives the complete aggregate and applies one coordinated repair pass across the affected target-skill and workflow deliverables; do not launch one rewrite per finding.
- After repair, run independent post-fix checks as a second `ConcurrencyStrategy.All` external batch. Keep the final parse, graph/dataflow, compile, and ordered runtime checks in one serial validation phase after every post-fix result has arrived.
- Missing, duplicate, or incomplete batch results fail closed and remain on the same persisted workflow copy. These rules govern SO enhancement planning and delivery orchestration only; they do not create a generic AO/SO runtime Review engine or change AO behavior.
## Audit Artifact Rules

- Workflow audit outputs are not optional display helpers; treat them as per-step audit records.
- Unless the user explicitly requests an audit destination, use a temporary output root.
- Do not default compile-time artifacts, audit artifacts, or other runtime temporary files under a skill directory or under `assets/so-workflow/`; keep them under a runtime temporary root or a repo-root temporary root unless the user explicitly chooses another destination.
- Audit artifacts, intermediate workflow materializations, and think-out-loud or conversation-referenceable run outputs may be cited during the conversation, but they still default to a runtime temporary root, repo-root temporary root, or an explicit user-chosen execution output root; do not default them into any skill folder.
- Valid JSON workflow and template files written to disk, including `so-template.json`, generated workflow templates, `workflow.demo.json`, `workflow.schema.json`, runtime workflow copies, and audit workflow backups, must use readable multi-line pretty JSON with indentation. When a caller supplies malformed JSON, preserve the exact malformed source only as failed-input evidence and never present it as a successful workflow artifact. Compact JSON is reserved for JSONL, MCP/CLI wire payloads, and explicitly canonical hash or comparison projections.
- Output targets may be outside the Git worktree or ignored by Git. Git tracking is never a delivery or validity requirement: every reported output must carry its normalized filesystem path, be checked for existence and readability, and, when a workspace root is available, expose a verified workspace-relative mirror that the editor can open directly. Never replace a real output path with a guessed repository-relative path.
- Whenever a Mermaid audit file is emitted or refreshed, user-facing think-out-loud must prefer a Mermaid card display tool when the chat agent provides one: pass the existing file path directly to the tool without reading or returning the file contents again solely for display. If no card tool is available, show a direct clickable Markdown file link, using a workspace-relative link when the artifact is inside the workspace; a bare path alone is insufficient.
- Persist audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`.
- Each successful render-producing step directory must include the point-in-time Mermaid Markdown, HTML, and workflow JSON backup. A compile-failure step must instead include the readable workflow JSON backup and `workflow.compile-feedback.json`, and must not create placeholder Mermaid or HTML files.
- Compile and audit flows must never overwrite an existing artifact file in place; fail with a rich error that reports the conflicting path set and tells the caller to choose a different output root or clean the destination.

## Execution Order And Review Cadence

- Before broader implementation, update `AGENTS.md` with the current language, documentation, and execution rules.
- After every major implementation slice, run a reasonable `cto-review-and-commit` review/fix/validate/commit workflow before starting the next slice.
- Treat that cadence as a hard default gate, not a soft suggestion: do not let work continue across multiple major slices and then review later in one large batch unless the user explicitly overrides it.
- Keep each review-and-commit slice small enough to be reviewed with evidence. As a default planning rule, a slice should usually stay at or below 50 changed files; if the pending scope is approaching that size, stop and run `cto-review-and-commit` before adding more.
- Even when a slice is smaller than 50 files, still run `cto-review-and-commit` immediately when the slice changes protocol contracts, schemas, package seams, or runtime control behavior.
- Major slices include work such as root AGENTS rules, flagship README landing pages, docs skeletons, package scaffolding, protocol/schema changes, and code implementation.
- Do not continue into the next major slice with unreviewed or uncommitted work unless the user explicitly overrides that cadence.
