# Mermaid Artifact Delivery

This reference defines how `/loom-plan-execution` reports Mermaid artifacts after the direct AO apphost (`ao.exe` on Windows or `ao` on Unix).

## Source of Truth

- Read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file` paths returned by the current CLI call.
- Do not guess an audit step number, workflow id, or output directory from command text.
- Keep the runtime paths as evidence. A runtime path is not automatically a link that the current chat surface can open.
- `audit_artifacts.mermaid_file`, `audit_artifacts.html_file`, and every returned output file path are normalized filesystem paths. They remain the authoritative direct-open references even when the output is outside the Git worktree or ignored by Git; Git status must never be used as a delivery check.
- Verify each reported output exists and is readable before citing it. When `--workspace-root` is available, use the hash-verified workspace-relative mirror for editor links and retain the real runtime path as technical evidence.
- `must_show_to_user_files` is an audit file list. It does not prove that a chat link is resolvable and does not replace `mermaid_delivery`.

## Workspace Mirror

Pass `--workspace-root <existing-directory>` when the caller needs links that VS Code can open from the workspace. The runtime validates that the directory exists and is outside the skill-owned directory. When the audit step is outside that root, the runtime copies `workflow.mermaid.md` and `workflow.html` into a new ignored `temp/exec-<timestamp>-mermaid-delivery-result/` directory below the workspace root.

The runtime reads both source files, verifies that they are complete and readable, computes SHA-256 values, copies them without overwrite, and verifies both destination hashes. The returned `workspace_relative_mermaid_file` and `workspace_relative_html_file` values are the only paths to use for workspace-relative Markdown links. Normalize separators to `/` in displayed links.

## Host Presentation Flow (Outside the Workflow)

Mermaid files are audit side pieces. They may live under a temporary or Git-ignored output root and must not be promoted into an workflow item for the skill being enhanced, a business output family, or a completion gate. This presentation flow belongs to the current agent/chat host.

Use the actual, verified absolute paths returned by `audit_artifacts.mermaid_delivery`:

1. Try the host's Mermaid card-display capability with the absolute `card_input_file` path. Do not call another LLM, ask another agent to restate the Mermaid, or reread the file solely to render it.
2. If card display is unavailable or fails, create a clickable host notification with two actions. The action targets must be the verified absolute paths, not guessed paths and not workspace-relative paths:
   - `Open workflow Mermaid` -> absolute `mermaid_file` or `workspace_mermaid_file`
   - `Open workflow HTML` -> absolute `html_file` or `workspace_html_file`
3. If the host can render clickable file actions but cannot render Mermaid inline, the notification is the preferred fallback. It should be deduplicated using the step identity and the returned artifact hashes.
4. If the host has neither card display nor clickable file notifications, emit the exact four-link `User Output` block using complete absolute runtime file addresses. Immediately follow each link with a fenced `text` block containing only the identical complete absolute address; add no other text inside the block. Do not claim link clickability unless verified; a separate host/editor open action may use a verified workspace mirror.
5. A verified workspace-relative mirror is only for a separate host/editor open action; never substitute it into the fixed report.
6. For `runtime_path_only`, keep the complete absolute addresses in the report without claiming the host can open them. A verified workspace mirror may be used separately for an editor-open action.
7. For `delivery_failed`, report the failure and next action. Do not create a notification or link for an unverified file.

A host notification is a presentation action, not runtime evidence. It must not change `mermaid_delivery.status`, claim that a card was displayed, or become a workflow node, gate, output family, or completion condition.
## Delivery States

The runtime-produced `mermaid_delivery.status` values are:

- `workspace_mirror`: Mermaid and HTML were generated, copied under the workspace root, and both copies passed hash verification. `link_resolvable` is `true`.
- `runtime_path_only`: Mermaid and HTML were generated and verified, but no workspace mirror was requested. Keep the runtime paths as evidence and do not claim a verified workspace link.
- `delivery_failed`: required files were missing, unreadable, incomplete, or failed mirror verification. Do not emit a guessed link.

Host-only presentation states are not runtime evidence:

- `not_emitted`: the current call returned no `mermaid_delivery` object and produced no new Mermaid artifact. The host derives this continuity state and may reuse only a previously verified workspace-relative link while saying that the render is unchanged.
- `card_displayed`: the host called a Mermaid card-display tool and confirmed the display. The runtime does not claim this state.

`generation_status` is runtime evidence and reports `fresh` or `reused`. When no current `mermaid_delivery` object exists, the host may derive the separate host-only `not_emitted` continuity state. `artifact_generated` means the runtime verified the Mermaid and HTML files. `link_resolvable` means the workspace-relative mirror was verified, not merely that an absolute path exists. `visual_preview_rendered` means a host opened and rendered the HTML preview; writing an HTML file does not set it to `true`. `card_display_available` describes host capability and must remain `false` unless the host reports that capability.

## User Output

After every AO or SO apphost CLI call (`ao.exe`/`ao` or `so.exe`/`so`), begin the think-out-loud update with four verified Markdown link-plus-fence pairs in this exact order: Mermaid, HTML, Analysis, Dataflow. Each artifact title is a Markdown link to its complete verified absolute runtime file address, normalized with `/`. Immediately follow it with a fenced `text` block containing only the identical complete address. Preserve this shape; do not use relative links, shorten paths, or add other text inside the fence.

[Mermaid](C:/path/to/workflow.mermaid.md)
```text
C:/path/to/workflow.mermaid.md
```
[HTML](C:/path/to/workflow.html)
```text
C:/path/to/workflow.html
```
[Analysis](C:/path/to/workflow.analysis.json)
```text
C:/path/to/workflow.analysis.json
```
[Dataflow](C:/path/to/workflow.dataflow.json)
```text
C:/path/to/workflow.dataflow.json
```

Replace examples with only the complete absolute paths verified from the current call or approved continuity set. If a file is unavailable or unverified, follow delivery-failure guidance and never invent a link. A card or notification may supplement the block but cannot replace it.

After the four pairs, print localized headings for execution confidence and estimated overall progress in that order, each with one short reason. For English use `## Execution confidence: x%` and `## Estimated overall progress: x%`; for Chinese use `## 执行信心: x%` and `## 预计整体进度: x%`. Never place these headings before the artifact report or claim completion when required evidence is missing.

All progress, blocked, error, and completion prose must use the current interaction language and plain words. Keep exact identifiers in technical details or evidence only when needed.

## Failure Handling

A delivery exception carries `audit_artifacts.mermaid_delivery` with `status=delivery_failed`. Check `artifact_generated`, `link_resolvable`, and `error` before reporting anything to the user. The writer removes an incomplete audit step and an incomplete workspace mirror. A failed result must therefore contain no user-facing link to a file that was not verified.

If the current call has no `mermaid_delivery` object, derive host-only `not_emitted` and repeat the latest verified Mermaid, HTML, Analysis, and Dataflow link-plus-fence pairs only when all paths were previously verified; state that the earlier render is still valid and include the current workflow location. If no verified artifact set exists, report the missing evidence and next action without links.
