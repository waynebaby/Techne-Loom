---
name: Loom Documentation Rules
description: Bilingual documentation, Mermaid, guide writing, plain-language, README, and user-facing workflow terminology rules.
applyTo: "docs/**,README*.md,CONTRIBUTING*.md,CHANGELOG*.md,SECURITY*.md,demos/**,.agents/skills/**"
---

# Loom Documentation Rules

Use these rules when changing public docs, READMEs, demos, skill-local documentation, guides, or user-facing workflow text.

## Documentation And Language Rules

- Public docs are bilingual by default. Keep mirrored trees under `/docs/zh-cn` and `/docs/en`.
- Demo indexes and stage `README.md` or `Readme.md` files under `/demos` are public docs; keep the English default beside a same-folder Chinese mirror using the `.zh-CN.md` suffix.
- Every paired page has a reciprocal header link to its counterpart.
- Skill-local references under `.agents/skills/*/reference/` are English only so skills remain deterministic and runnable offline without multilingual drift.
- For public docs, use `{agentskillfolder}/...` for agent-neutral external target-skill roots. Use `.agents/skills/...` explicitly only for this repository's built-in skill root or manifest catalog.
- Localized skill narrative belongs in bilingual docs under `/docs/en` and `/docs/zh-cn`, not multilingual variants under skill-local `reference/` directories.
- Root bilingual files are required for `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, and `SECURITY.md`; English keeps the default name and Chinese uses `.zh-CN.md`.
- `AGENTS.md` and other agent configuration files are English-only and do not need Chinese mirrors. Keep `AGENTS.md` root-only.
- Product guide sources live at `/docs/<lang>/guides/ao-guide.md` and `/docs/<lang>/guides/so-guide.md`.
- AO-facing docs use `Loom Agent Plan-Execution Orchestrator` in titles, intros, README positioning, and guide navigation while preserving `ao-guide.md`, direct apphost command names such as `ao`/`ao.exe`, and package identifiers. Runnable examples must not use DLL-based launch commands.
- Do not use legacy narrative labels such as `SO Governance`, `SO-enhanced`, or `SO-governed`. Prefer `enhancing skill`, `skill being enhanced`, `skill under Loom Skill Orchestrator governance`, or the narrower status wording required by the current slice.
- Preserve implementation-identity literals such as file names, commands, package ids, schema fields, template kinds, and other checked-in wire values when they intentionally retain `so` naming.
- Root package indexes are `packages.released.md`, `packages.released.zh-CN.md`, `packages.beta.md`, and `packages.beta.zh-CN.md`; skills should reference them with absolute GitHub URLs.
- Workflow definition files are the canonical English carrier for schema keys, node/transition names, phases, expressions, hints, failure guidance, evidence references, and control metadata. Keep user/business payload values and localized output in their source/requested language; localization belongs in presentation and must not change control semantics.

## Mermaid Diagram Rules

- Treat Mermaid diagrams in Markdown as first-class documentation surfaces.
- AO, SO, and every skill under Loom Skill Orchestrator governance must begin each CLI think-out-loud update with verified Mermaid, HTML, Analysis, and Dataflow Markdown link-plus-`text`-fence pairs in that order, followed by localized `##` headings for execution confidence and estimated overall progress, each with one short reason or brief progress sentence. A card or notification may supplement the block but cannot replace it. The artifact title is the link itself: do not emit a preceding `Mermaid:`/`HTML:`/`Analysis:`/`Dataflow:` label, standalone artifact name, or outer `##` heading around these links.
- User-facing progress, blocked, error, and completion messages for those skill surfaces use plain words in the current interaction language. Keep exact identifiers in technical details or evidence only.
- Mermaid diagrams must remain readable for color-blind readers; color must never be the only meaning channel.
- Categorical diagrams use a second channel in node labels with meaning-aligned emoji: `🧭` intake/navigation, `🔎` research/inspection, `💬` review/discussion, `📝` drafting, `✅` completion, `⚙️` runtime execution, `📜` contract, `🧾` audit evidence, `❓` decision, `🚧` blocked/boundary, and `🔁` continuation/loop.
- Reuse emoji consistently within a diagram and keep legends adjacent to Mermaid when an embedded legend would distort layout.
- In Chinese Markdown docs, English-first Mermaid labels append the Chinese equivalent on its own line with `<br/>`, unless the term is code-like or a literal wire name.
- Wrap bilingual labels, HTML line breaks, and ambiguous punctuation in quotes, keeping one language per line. Do not expand literal filenames, CLI tokens, field names, or protocol values.

## Plain-Language Feedback

- User-facing progress, blocked, error, and completion updates for `/loom-plan-execution`, `/loom-skill-enhancement`, and skills under Loom Skill Orchestrator governance use the user's requested language and plain words.
- Explain four things in order: what happened, whether the user's work/data is safe, why it happened, and what happens next.
- Do not lead with workflow internals such as status values, step kinds, node ids, checks, stages, handoffs, runtime terms, or audit jargon. Explain the idea first and define a necessary technical term in the same sentence.
- Keep exact commands, paths, ids, status values, and evidence fields in a separate `Technical details` section only when they help the user act or verify the result. Machine-readable records may keep exact tokens.
- A blocked or failed update must say whether the request is wrong or a tool/file/output-folder problem stopped progress, what remains valid, and one concrete next action.
- When SO creates or updates a skill being enhanced, copy this language rule into its user instructions, local subagent prompts, failure guidance, and workflow hints.

## Human-Facing Workflow Language

- Keep internal workflow identifiers in machine-readable contracts, source code, logs, audit artifacts, and exact implementation docs, not as default user-facing wording.
- Translate internal statuses and step kinds into concrete language: `Done` means the requested work is complete; `noop` means no action is needed; `WaitResume` means waiting for information or confirmation; `SubagentCall` means a specialist analysis is running; `gate` means a required check; `transition` means the next step.
- User questions request a concrete human action or decision. In Chinese, use wording such as “请你选择是否继续” or “请提供远程分支”; do not ask for internal state names, node kinds, check results, transition data, or runtime-owned artifact details.
- Use `/docs/en/architecture/workflow-terminology.md` as the bilingual glossary. Prefer **weave out**, **weave back**, **strand**, **seam** for conceptual ownership joins, and **boundary** only for explicit wire/protocol surfaces.
- When explanatory terminology differs from wire names, mention both on first use and keep implemented field names explicit. Do not introduce a new human-facing workflow metaphor without updating the glossary.

## README Positioning

- Treat `README.md` and `README.zh-CN.md` as flagship landing pages, not only technical indexes.
- Use GitHub-supported rich Markdown intentionally: badges, alerts/callouts, comparison tables, Mermaid diagrams, architecture visuals, and clear use-case framing.
- Marketing claims must remain defensible against the current implementation and docs.
