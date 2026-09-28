# Mermaid Artifact Delivery

This reference defines how `/loom-skill-enhancement` reports Mermaid artifacts after the direct SO apphost (`so.exe` on Windows or `so` on Unix).

## Source of Truth

- Read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file` paths returned by the current CLI call.
- Do not guess an audit step number, workflow id, or output directory from command text.
- Keep the runtime paths as evidence. A runtime path is not automatically a link that the current chat surface can open.
- `audit_artifacts.mermaid_file`, `audit_artifacts.html_file`, and every returned output file path are normalized filesystem paths. They remain the authoritative direct-open references even when the output is outside the Git worktree or ignored by Git; Git status must never be used as a delivery check.
- Verify each reported output exists and is readable before citing it. The fixed multi-file report uses the complete absolute runtime file address in both the Markdown link target and matching `text` fence. Use `--workspace-root` mirrors only for separate host/editor open actions; never substitute workspace-relative paths into the report.
- `must_show_to_user_files` is an audit file list. It does not prove that a chat link is resolvable and does not replace `mermaid_delivery`.

## Workspace Mirror

Pass `--workspace-root <existing-directory>` when the caller needs links that VS Code can open from the workspace. The runtime validates that the directory exists and is outside the skill-owned directory. When the audit step is outside that root, the runtime copies `workflow.mermaid.md` and `workflow.html` into a new ignored `temp/exec-<timestamp>-mermaid-delivery-result/` directory below the workspace root.

The runtime reads both source files, verifies that they are complete and readable, computes SHA-256 values, copies them without overwrite, and verifies both destination hashes. The returned `workspace_relative_mermaid_file` and `workspace_relative_html_file` values may be used only for separate workspace/editor open actions. They must not replace the complete absolute address in the fixed multi-file report. Normalize separators to `/` in displayed paths.

## Host Presentation Flow (Outside the Workflow)

Mermaid files are audit side pieces. They may live under a temporary or Git-ignored output root and must not be promoted into an workflow item for the skill being enhanced, a business output family, or a completion gate. This presentation flow belongs to the current agent/chat host.

Use the actual, verified absolute paths returned by `audit_artifacts.mermaid_delivery`:

1. Try the host's Mermaid card-display capability with the absolute `card_input_file` path. Do not call another LLM, ask another agent to restate the Mermaid, or reread the file solely to render it.
2. If card display is unavailable or fails, create a clickable host notification with two actions. The action targets must be the verified absolute paths, not guessed paths and not workspace-relative paths:
   - `Open workflow Mermaid` -> verified absolute `mermaid_file`
   - `Open workflow HTML` -> verified absolute `html_file`
3. If the host can render clickable file actions but cannot render Mermaid inline, the notification is the preferred fallback. It should be deduplicated using the step identity and the returned artifact hashes.
4. If the host has neither card display nor clickable file notifications, emit the exact multi-file block from `User Output` using complete absolute runtime file addresses. Keep each title as a Markdown link to the full address and immediately follow it with a `text` fence containing only the identical address. Do not add labels, secondary path lines, shortened paths, workspace-relative addresses, or alternate formatting.
5. A verified workspace-relative mirror may be used only as a separate host/editor open action; never substitute it into or append it to the fixed multi-file report.
6. An absolute Markdown target may not be clickable in every host. Do not claim clickability unless verified; preserve the complete absolute address and exact report structure regardless. A separate host/editor open action may use the verified workspace mirror; notification action targets remain absolute runtime file paths.
7. For `delivery_failed`, report the failure and next action. Do not create a notification or link for an unverified file.

A host notification is a presentation action, not runtime evidence. It must not change `mermaid_delivery.status`, claim that a card was displayed, or become a workflow node, gate, output family, or completion condition.
## Delivery States

The fixed multi-file report always uses the complete verified absolute runtime address in both each Markdown link target and its matching `text` fence. This format does not depend on `link_resolvable`.

- `workspace_mirror`: Mermaid and HTML were generated, copied under the workspace root, and both copies passed hash verification. `link_resolvable=true` permits a separate workspace/editor open action; it does not change the report paths.
- `runtime_path_only`: Mermaid and HTML were generated and verified without a workspace mirror. Use their complete absolute addresses in the report; do not claim a workspace/editor link is available.
- `delivery_failed`: required files were missing, unreadable, incomplete, or failed mirror verification. Do not emit links to unverified files.

Host-only presentation states are not runtime evidence:

- `not_emitted`: the current call returned no `mermaid_delivery` object and produced no new artifact. If a previously verified absolute artifact set exists, repeat those exact absolute link-and-fence pairs and say the render is unchanged. If none exists, report that no verified artifact paths are available and give the next action without links.
- `card_displayed`: the host called a Mermaid card-display tool and confirmed the display. The runtime does not claim this state.

`generation_status` is runtime evidence and reports `fresh` or `reused`. `artifact_generated` means the runtime verified the Mermaid and HTML files. `link_resolvable` means the workspace mirror was verified for a separate editor/open action; it does not select or alter the fixed report address. `visual_preview_rendered` means a host opened and rendered the HTML preview; writing an HTML file does not set it to `true`. `card_display_available` describes host capability and must remain `false` unless the host reports that capability.

## User Output

After every AO or SO apphost CLI call (`ao.exe`/`ao` or `so.exe`/`so`), begin the update with exactly four verified Markdown links in this order: Mermaid, HTML, Analysis, Dataflow. Put each artifact-title link on its own line; each target must be the complete verified absolute runtime file address, using `/` separators. Do not use code blocks, repeat addresses on separate lines, use relative paths, shorten or ellipsize addresses, or add labels/commentary to the link lines. Keep this format unchanged.

[Mermaid](C:/path/to/workflow.mermaid.md)
[HTML](C:/path/to/workflow.html)
[Analysis](C:/path/to/workflow.analysis.json)
[Dataflow](C:/path/to/workflow.dataflow.json)

Use only actual paths verified in the current call or approved continuity set. If a file is missing or unverified, follow delivery-failure guidance and do not invent a link. Cards or notifications may supplement the link list but cannot replace it.

After the four links, print localized execution-confidence and estimated-overall-progress headings in that order, each with one brief reason. Never put those headings before the links. Use verified evidence only and do not claim completion when required evidence is missing.

For English interaction, use `## Execution confidence: x%` and `## Estimated overall progress: x%`. For Chinese interaction, use `## 执行信心: x%` and `## 预计整体进度: x%`.

All progress, blocked, error, and completion prose must use the current interaction language and explain what happened, whether the work or data is safe, why it happened, and what happens next. Keep exact identifiers in technical details or evidence only when needed.

## Failure Handling

A delivery exception carries `audit_artifacts.mermaid_delivery` with `status=delivery_failed`. Check `artifact_generated`, `link_resolvable`, and `error` before reporting anything to the user. The writer removes an incomplete audit step and an incomplete workspace mirror. A failed result must contain no user-facing link to a file that was not verified.

If the host derives `not_emitted` because the current operation returned no `mermaid_delivery` object, repeat the latest verified absolute Mermaid, HTML, Analysis, and Dataflow link-and-fence pairs only when all paths were previously verified, and state that the render is unchanged. If no verified set exists, report the missing evidence and next action without links. For `runtime_path_only`, use complete absolute addresses in the fixed report; a verified workspace mirror may be used separately for a host/editor open action. For `delivery_failed`, report the failure and one concrete next action without links, notifications, or reuse of an earlier artifact.
