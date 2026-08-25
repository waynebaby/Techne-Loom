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
- Keep `AGENTS.md` root-only. Do not duplicate it under `/docs`.
- Product guide source files live at `/docs/<lang>/reference/products/ao-guide.md` and `/docs/<lang>/reference/products/so-guide.md`.
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

## Runtime Package Family Rules

- Runtime selection is dual official: self-contained single-file packages are the default channel; legacy framework/library mode is explicit, selected by `runtimeBinding` or an explicit framework bundle directory. There is no implicit fallback between modes after CLI startup.
- Legacy framework/library mode uses one exact-version Product + `Techne.Loom.Common` + `Techne.Loom.Abstractions` bundle with a usable `Microsoft.NETCore.App 9.x` host. Self-contained mode uses one exact-version RID package from the `Techne.Loom.AgentOrchestrator.Runtime.<rid>` or `Techne.Loom.SkillOrchestrator.Runtime.<rid>` family.
- The supported self-contained RIDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64`; do not cross OS, architecture, or Linux libc boundaries.
- Validate SHA-512, nuspec/package identity, manifest, entrypoint, ZIP safety, and size bounds before launch. Isolate user-level cache entries by product, exact version, and RID, protect them with a cross-process lock, validate in a temporary directory, and publish atomically.
- Run and verify a fresh `--guide` from the selected launch descriptor before `compile`, `run`, or `resume`, then preserve that descriptor, exact version, and RID. Errors after CLI startup remain command failures and never trigger fallback.

## Package Version Governance

- Treat package-version-bearing content as belonging to one of four categories only: live docs and indexes, skill-local offline references, checked-in runtime locks, or historical demos and audit examples.
- Live docs and indexes such as root release notes, `packages.released*.md`, `packages.beta*.md`, direct exact-version NuGet URL examples, and package install commands must reflect the current latest published version for their channel and should be refreshed by the CI/CD publish workflows.
- Skill-local offline references under `.agents/skills/*/reference/` are deterministic channel snapshots, not floating latest prose. Within one such snapshot, the version block, install commands, direct exact-version package URLs, guide examples, and `resolved_runtime_version` examples must all use the same channel-specific snapshot version.
- Checked-in runtime locks such as `so-package-lock.json` are authoritative for the owning workflow/runtime surface. Their top-level resolved version, bundle member versions, and any adjacent runtime-binding prose must stay on one exact version consistent with the owning skill contract and channel.
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

## Workflow Terminology Rules

- The repo-wide workflow vocabulary root lives at `/docs/en/architecture/workflow-terminology.md` and `/docs/zh-cn/architecture/workflow-terminology.md`.
- Use that glossary for explanatory prose across AO and SO docs, guides, READMEs, and future schema explanations.
- Prefer **weave out** and **weave back** when explaining outward control transfer and structured continuation.
- Prefer **strand** over **thread** in repo docs to avoid collision with `.NET` threading terminology.
- Use **seam** for conceptual ownership joins, and keep **boundary** for explicit wire/protocol surfaces such as `boundary_reason` and the `type: "boundary"` envelope inside `<so_property>` blocks.
- When explanatory terminology and current wire names differ, mention both on first use and keep implemented field names explicit.
- Do not introduce new workflow metaphors in one product doc without updating the glossary and its bilingual mirror first.

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

- `dotnet so.dll --guide` and `dotnet ao.dll --guide` must install the version-matched English `docs/en` bundle from the published runtime's embedded resource, then emit one JSON object containing the actual absolute `version`, `docs_root`, and `guide_path` values. The guide path is the authoritative entry; callers may inspect the returned docs root only when the guide leaves a question unresolved. The command is English-only and rejects `--lang`, `--section`, and `--export`; CLI documentation, skill contracts, and tests must be updated with every contract change.
- For AO- and SO-routed skills, once the package channel or runtime source is chosen, the next hard gate is proving that the selected Loom runtime is actually runnable and can produce a fresh `--guide` result from that runtime. Do not continue to planning, authoring, validation, compile, run, resume, or downstream input collection until that `--guide` result exists.
- Once a fresh `--guide` result exists, treat that emitted guide as a hard governance handoff back onto the corresponding published AO or SO package runtime for execution authority. Do not let `--guide` become a side path that drifts back to repository builds, hand-assembled runtimes, or non-governed execution after the guide has already established the package/runtime contract.
- Guides should begin with version, build, and compatibility metadata.
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
- Published package-channel runtime restoration must validate a complete three-package bundle at the exact locked version in local cache before network access; missing or invalid cache may download only that exact version and must never float to latest.
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

## Audit Artifact Rules

- Workflow audit outputs are not optional display helpers; treat them as per-step audit records.
- Unless the user explicitly requests an audit destination, use a temporary output root.
- Do not default compile-time artifacts, audit artifacts, or other runtime temporary files under a skill directory or under `assets/so-workflow/`; keep them under a runtime temporary root or a repo-root temporary root unless the user explicitly chooses another destination.
- Audit artifacts, intermediate workflow materializations, and think-out-loud or conversation-referenceable run outputs may be cited during the conversation, but they still default to a runtime temporary root, repo-root temporary root, or an explicit user-chosen execution output root; do not default them into any skill folder.
- Persist audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`.
- Each step directory must include the point-in-time Mermaid Markdown, HTML, and workflow JSON backup.
- Compile and audit flows must never overwrite an existing artifact file in place; fail with a rich error that reports the conflicting path set and tells the caller to choose a different output root or clean the destination.

## Execution Order And Review Cadence

- Before broader implementation, update `AGENTS.md` with the current language, documentation, and execution rules.
- After every major implementation slice, run a reasonable `cto-review-and-commit` review/fix/validate/commit workflow before starting the next slice.
- Treat that cadence as a hard default gate, not a soft suggestion: do not let work continue across multiple major slices and then review later in one large batch unless the user explicitly overrides it.
- Keep each review-and-commit slice small enough to be reviewed with evidence. As a default planning rule, a slice should usually stay at or below 50 changed files; if the pending scope is approaching that size, stop and run `cto-review-and-commit` before adding more.
- Even when a slice is smaller than 50 files, still run `cto-review-and-commit` immediately when the slice changes protocol contracts, schemas, package seams, or runtime control behavior.
- Major slices include work such as root AGENTS rules, flagship README landing pages, docs skeletons, package scaffolding, protocol/schema changes, and code implementation.
- Do not continue into the next major slice with unreviewed or uncommitted work unless the user explicitly overrides that cadence.
