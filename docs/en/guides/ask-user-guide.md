# XO Ask and AskUser Consumers

[简体中文](../../zh-cn/guides/ask-user-guide.md) | [Guides](README.md) | [Design](../architecture/ask-user-design.md)

<!-- guide-version:start -->
Version: 0.5.1-beta
Build: published package 0.5.1-beta
<!-- guide-version:end -->
























## Three Layers

`XO Ask` is shorthand for the shared ask capability exposed by the unified Loom runtime binary. It is not a third product or package family.

| Layer | Responsibility | Workflow required? |
| --- | --- | --- |
| XO Ask in the unified Loom binary | Shared question contract, browser session, validation, local drafts, and receipt | No |
| `/loom-ask-user` skill | Agent-facing peer consumer: returns session answers and a receipt, or warned unattended answers without a receipt | No |
| `AskUser` workflow node | Workflow-facing peer consumer: declares questions, maps the receipt into workflow context, and lets the owning runtime resume | Yes, only for this adapter |

The skill and node are independent consumers of XO Ask. Neither calls or depends on the other. A standalone ask has its own ask-session identity and local state; it does not create or require a `WorkflowInstance`. Workflow-specific `contextPath`, `requiredInputs`, SO `validation.declaredUserOwnedFields`, projection, and resume belong only to the node adapter. For workflow-owned questions, resume with the returned ask ID and `--wait-for-ask` (or use MCP `wait_for_ask: true`); the runtime waits for submission without holding the workflow lock, then validates and resumes.

## Current Release Status

Standalone `ask start --contract-file <path>`, `ask result --ask-id <id>`, and `ask wait --ask-id <id>` support is version-specific. Confirm all three in the exact published Loom apphost's fresh help. `ask result` reads current state immediately; `ask wait` keeps the invocation active until submission, checks every minute after its default 5-minute initial wait, and reports expiry. Cancelling the wait does not cancel the ask session. The optional workflow node does not establish standalone support.

## Standalone Route

Use `/loom-ask-user` by default for agent-facing structured clarification. Keep the optional `AskUser` workflow node for workflow-owned answers that must be projected into that same workflow instance.

1. Put the caller's independent questions into one ordered, typed form contract. Preserve prompt, context, intent, required state, options, freeform behavior, and relevant constraints.
2. Resolve the exact Loom runtime release version from the skill version block and detect the host RID. Follow the acquisition order below. The unified runtime provides the same top-level ask entry for every host route.
3. Verify package identity, version, RID, SHA-512, manifest, archive paths, apphost, and a fresh guide. Confirm the selected apphost actually exposes its standalone ask entry.
4. Write the complete contract to a path-only file, then run `loom ask start --contract-file <path>`. Present only the returned host-approved browser route; never infer a public URL or create a tunnel.
5. After starting the browser handoff, call `loom ask wait --ask-id <id>` to receive the submitted typed answers and receipt. Use `loom ask result` for a nonblocking status check; do not resume a workflow from the skill.

## Autopilot and Unattended Runs

When the host explicitly identifies an autopilot or unattended run, apply this branch before starting the interactive route or acquiring a runtime. Do not invoke a blocking user-input tool, open a browser, or wait for a person. If any question requires genuine human approval, do not submit an agent-generated approval through HTTP, a result-file, or an offline submission, and do not consume the active session; return warned suggestions and leave the session and any workflow waiting.

- If the same ask session is already running, the runtime returned its endpoint, and the session does not require genuine human approval, submit context-based, type-valid answers through that endpoint's supported HTTP session/submission flow. Use only the returned endpoint, do not start another worker, and keep pairing/session credentials private. The runtime will produce a real receipt, but the answers are still agent-generated and not user-confirmed.
- If there is no usable active endpoint, skip starting the interactive ask and return the best-supported typed answers directly. Mark unknowns only when the contract permits them; otherwise leave them unresolved. No submitted session means no runtime receipt or server validation. Do not claim user confirmation, and clearly warn that the user was unavailable and the answers were generated.
- When writable, preserve the user-absent explanation in caller-provided additional-context, or in the existing question `context` before a session starts. Never mutate an active contract, add wire fields, or disguise this explanation as an answer.
- For a structured workflow `AskUser` node, do not fabricate a receipt or bypass owning-runtime validation. If HTTP submission is unavailable and no declared noninteractive continuation exists, return the generated answers and warning and report that the workflow remains waiting. If genuine human approval is required, do not submit an agent-generated approval via HTTP, a result-file, or an offline submission, and do not consume the active session; provide warned suggestions and leave it pending. A receipt for an agent-generated answer does not establish human approval; if genuine human approval is required, leave the workflow waiting.

Outside autopilot, use the normal host-approved browser handoff. Newer Loom runtimes include conditional unattended guidance in the node-entry `skill_hint` alongside the authored hint. The agent module route returns endpoints in `ask_user_endpoints`; the skill module route returns them in the separate `ask_user` envelope's `endpoints`. Older locked runtimes may not emit this guidance, so retain it in the authored hint and skill policy.

## Dependency Acquisition Order

Use the first available, integrity-checked capability for the exact locked unified Loom runtime package. Skip a tier only when its capability is unavailable or its source is unavailable; never install a runtime or downloader solely to acquire the package.

| Tier | Capability | Rule |
| --- | --- | --- |
| L0 | Existing local artifact or cache | Check the shared Loom runtime cache, NuGet cache, workspace, configured package cache, and known local paths. Reuse only after exact identity/version/RID and required integrity are verified. |
| L1 | Agent/Host-controlled fetch | Use a controlled fetch for the exact package from its configured primary source; do not start a downloader subprocess. |
| L2 | Existing `nuget.exe` | Reuse only if already installed and executable for the exact NuGet package. Do not install `nuget.exe`, .NET Framework, or a .NET SDK. |
| L3 | Existing runtime's native HTTP API | Use an already-installed runtime's in-process API, such as Node `fetch`, Python `urllib`, or .NET `HttpClient`; do not launch a downloader subprocess or install a runtime. |
| L4 | Known GitHub MCP release asset | Use only a preconfigured, approved repository, exact release tag/asset, and matching `.sha512` sidecar. Skip it if the approved capability is not already exposed; do not register or inspect MCP before the fresh `--guide`. |
| L5 | Other approved Host/MCP source | Use only an already-available, configured source and exact artifact with required integrity evidence. |
| L6 | Generic network CLI | Last resort after L0-L5 are unavailable, with explicit per-operation approval naming the tool, source/host, exact artifact/version, destination, and purpose. |

PowerShell `Invoke-WebRequest`/`Invoke-RestMethod`, `curl`, `gh`, `wget`, BITS tools/cmdlets, and equivalent first- or third-party download CLIs are all L6; being preinstalled or first-party does not exempt them. The only earlier CLI exception is an already-installed `nuget.exe` at L2. A direct in-process .NET `HttpClient` call from PowerShell is L3; PowerShell `Invoke-WebRequest`/`Invoke-RestMethod` remain L6. After any failed request, do not automatically switch transports to repeat the same artifact/source. A nonblocked source may be retried only as an explicit L6 operation after L0-L5 are exhausted and the user approves that exact CLI action. If the source or request is explicitly blocked or denied by network/security policy, stop all attempts to that source through every transport; continue only with an independently configured and approved fallback source, otherwise stop and report the blocker. If the user denies CLI approval, stop and do not switch tools or sources to evade that decision. User approval does not override host security policy and cannot guarantee that an alert will not fire.

**Exact NuGet acquisition completion rule.** For `/loom-ask-user`, `/loom-plan-execution`, `/loom-skill-enhancement`, and every skill created or updated by `/loom-skill-enhancement`, downloading a missing exact-version NuGet package is allowed and required. A local miss never ends the run, and it never permits a different version, RID, channel, local/test build, or agent-native substitute. When L0-L2 do not yield the verified package, the agent writes a short single-purpose download program file outside source and skill folders and runs it on an already-installed runtime's in-process HTTP API: .NET `HttpClient` hosted by PowerShell or `dotnet`, Python `urllib.request`, or Node `fetch`. On Windows, PowerShell-hosted `HttpClient` is always available. While such a runtime exists, the agent never calls `curl`, `wget`, `gh`, BITS, `Invoke-WebRequest`, or `Invoke-RestMethod`, not even as a retry; it retries by re-running the same program and uses an approved L6 CLI only when no installed runtime offers an in-process HTTP API. After a nonblocked failure, the agent also tries every independently configured same-version fallback source. The run never completes, skips, or terminates without the verified exact package. A policy block or denial, a denied L6 approval, an exact version not yet published on any approved source, or repeated integrity mismatch is never bypassed: the agent stops only that source or tool, keeps the run in a resumable acquisition-pending pause, asks the user for an approved source, credentials, a policy change, or a wait for publication, and then resumes acquiring the same exact version. For exact NuGet packages, every generic "stop" outcome above means this pause, and the pause is never reported as complete.

Verify every NuGet package against exact registration `catalogEntry.packageHash` (SHA-512), then validate package identity, version, RID, nuspec, `runtime.json`, archive size and safe paths, apphost, and English guide files before extraction. A GitHub package requires an independently configured, approved same-version release asset and matching `.sha512` sidecar.

The diagram below explains the L0-L6 acquisition policy; it is not an executable workflow.

```mermaid
flowchart TD
    START["🧭 Need exact runtime artifact"] --> L0["🔎 L0: Check local artifact/cache"]
    L0 --> HIT{"❓ Exact identity and integrity verified?"}
    HIT -- "Yes" --> USE["✅ Reuse verified package"]
    HIT -- "No" --> MANAGED["⚙️ Continue with the next untried independent L1-L5 source (L3: run a download program on an installed runtime)"]
    MANAGED --> RESULT{"❓ Exact package obtained?"}
    RESULT -- "Yes" --> VERIFY["🔎 Verify package identity, SHA-512, and archive"]
    RESULT -- "No" --> BLOCKED{"❓ Was a source blocked or denied?"}
    BLOCKED -- "Yes" --> INDEPENDENT{"❓ Different approved source available?"}
    INDEPENDENT -- "No" --> PAUSE["⏸️ Pause resumably, not complete; ask the user for an approved way forward; do not retry the blocked source"]
    INDEPENDENT -- "Yes" --> MANAGED
    BLOCKED -- "No" --> NEXT{"❓ Another independent managed source remains?"}
    NEXT -- "Yes" --> MANAGED
    NEXT -- "No" --> RUNTIME{"❓ Installed runtime with an in-process HTTP API?"}
    RUNTIME -- "Yes" --> RERUN["🔁 Re-run the L3 download program; never a CLI"]
    RERUN --> RERUN_RESULT{"❓ Exact package obtained?"}
    RERUN_RESULT -- "Yes" --> VERIFY
    RERUN_RESULT -- "No" --> PAUSE
    RUNTIME -- "No" --> CONSENT{"❓ Explicit approval for this exact L6 CLI action?"}
    CONSENT -- "No" --> PAUSE
    CONSENT -- "Yes" --> CLI["⚙️ Run the approved CLI once"]
    CLI --> CLI_RESULT{"❓ Did that operation succeed?"}
    CLI_RESULT -- "No" --> PAUSE
    CLI_RESULT -- "Yes" --> VERIFY
    PAUSE -- "User provides an approved option" --> MANAGED
    VERIFY --> VERIFIED{"❓ Integrity and archive checks passed?"}
    VERIFIED -- "Yes" --> EXTRACT["📦 Extract safely"]
    VERIFIED -- "No" --> DISCARD["🗑️ Discard the bytes and download fresh"]
    DISCARD --> NEXT
    EXTRACT --> USE
    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef runtime fill:#dbeafe,stroke:#1d4ed8,color:#172554;
    classDef decision fill:#fee2e2,stroke:#b91c1c,color:#450a0a;
    classDef paused fill:#fecaca,stroke:#dc2626,color:#7f1d1d;
    classDef evidence fill:#f3e8ff,stroke:#7e22ce,color:#3b0764;
    classDef complete fill:#dcfce7,stroke:#15803d,color:#052e16;
    class START,L0,VERIFY intake;
    class MANAGED,CLI runtime;
    class RERUN,DISCARD runtime;
    class HIT,RESULT,BLOCKED,INDEPENDENT,NEXT,RUNTIME,RERUN_RESULT,CONSENT,CLI_RESULT,VERIFIED decision;
    class PAUSE paused;
    class EXTRACT evidence;
    class USE complete;
```

Legend: 🧭 request intake (blue); 🔎 inspection (blue); ⚙️ managed or approved action, 🔁 runtime-program retry, and 🗑️ discard of failed bytes (blue); ❓ decision (red); ⏸️ resumable pause, never completion (red); 📦 safe extraction (violet); ✅ verified use (green). The labels carry meaning independently of color.

## Shared Browser Flow

The branches below are independent consumers of the same binary capability. A caller chooses one branch; the skill does not route through the node.

```mermaid
flowchart TD
    SKILL["🧭 /loom-ask-user skill"] --> XO["⚙️ XO Ask in unified Loom binary"]
    NODE["🧭 AskUser workflow node"] --> XO
    XO --> SESSION["🧾 Independent ask session and local draft store"]
    SESSION --> FORM["💬 Runtime-served browser form"]
    FORM --> CHECK{"❓ Is the typed answer set valid?"}
    CHECK -- "No" --> FORM
    CHECK -- "Yes" --> RECEIPT["🧾 Validated answer set and receipt"]
    RECEIPT --> DIRECT["✅ Skill returns answers and receipt to caller"]
    RECEIPT --> MAP["⚙️ Optional node maps answers to workflow context"]
    MAP --> RESUME["🔁 Owning module route resumes that workflow"]

    classDef intake fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e;
    classDef runtime fill:#dbeafe,stroke:#1d4ed8,color:#172554;
    classDef user fill:#fef3c7,stroke:#b45309,color:#451a03;
    classDef evidence fill:#f3e8ff,stroke:#7e22ce,color:#3b0764;
    classDef decision fill:#fee2e2,stroke:#b91c1c,color:#450a0a;
    classDef complete fill:#dcfce7,stroke:#15803d,color:#052e16;
    class SKILL,NODE intake;
    class XO,MAP,RESUME runtime;
    class FORM user;
    class SESSION,RECEIPT evidence;
    class CHECK decision;
    class DIRECT complete;
```

Legend: 🧭 consumer (blue); ⚙️ binary or workflow-node action (blue); 💬 user interaction (amber); 🧾 ask state/result (violet); ❓ validation decision (red); 🔁 optional workflow continuation (teal); ✅ direct result (green). Labels and symbols carry meaning independently of color.

## Question and Answer Semantics

Use stable question IDs and ordered groups. Supported types are `singleChoice`, `multipleChoice`, `text`, `number`, `boolean`, `file`, and `audio`. A default only prefills a control; it does not satisfy a required answer. Choice questions provide `Other` as a free-text alternative, so a clarification round can preserve choice suggestions while allowing a user-authored answer. See [answer semantics](../../../.agents/skills/loom-ask-user/reference/answer-semantics.md).

Standalone answers are returned by question ID and do not need workflow context paths. When an `AskUser` node is the consumer, its adapter supplies `contextPath` bindings, checks `requiredInputs` and SO ownership declarations, then maps the receipt and invokes the owning runtime's existing resume. That node-specific contract does not constrain the skill consumer.

## Runtime and Safety

AO and SO remain independent implementation modules but publish one exact-version Loom runtime closure. `XO Ask` is only shorthand for the shared capability present in one of those binaries. The skill version block tracks the shared release-set version; it is not a claim that a specific version implements standalone `ask`.

Pairing URLs are secrets. Pass them only through the approved browser handoff; do not repeat them in ordinary progress, logs, or audit summaries. Use only host-approved routes. Do not fetch arbitrary remote attachment URLs.

## Related Pages

- [AskUser design](../architecture/ask-user-design.md)
- [AskUser implementation plan](../architecture/ask-user-implementation-plan.md)
- [Using Techne Loom Skills](skill-usage.md)
- [SkillOrchestrator guide](so-guide.md)
- [Loom Agent Plan-Execution Orchestrator guide](ao-guide.md)
