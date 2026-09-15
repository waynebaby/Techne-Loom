# Mermaid Artifact Delivery

This reference defines how `/loom-plan-execution` reports Mermaid artifacts after `dotnet ao.dll`, self-contained `ao.exe`, or another AO runtime entry point.

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

Mermaid files are audit side pieces. They may live under a temporary or Git-ignored output root and must not be promoted into an SO-enhanced workflow item, a business output family, or a completion gate. This presentation flow belongs to the current agent/chat host.

Use the actual, verified absolute paths returned by `audit_artifacts.mermaid_delivery`:

1. Try the host's Mermaid card-display capability with the absolute `card_input_file` path. Do not call another LLM, ask another agent to restate the Mermaid, or reread the file solely to render it.
2. If card display is unavailable or fails, create a clickable host notification with two actions. The action targets must be the verified absolute paths, not guessed paths and not workspace-relative paths:
   - `Open workflow Mermaid` -> absolute `mermaid_file` or `workspace_mermaid_file`
   - `Open workflow HTML` -> absolute `html_file` or `workspace_html_file`
3. If the host can render clickable file actions but cannot render Mermaid inline, the notification is the preferred fallback. It should be deduplicated using the step identity and the returned artifact hashes.
4. If the host has neither card display nor clickable file notifications, emit Markdown links using the verified workspace-relative paths when `link_resolvable=true`, and put the exact absolute paths beside them as technical text:

```markdown
Mermaid: [Open workflow Mermaid](temp/exec-<timestamp>-mermaid-delivery-result/wf-<id>/step-<n>-<action>/workflow.mermaid.md)
Preview: [Open workflow HTML](temp/exec-<timestamp>-mermaid-delivery-result/wf-<id>/step-<n>-<action>/workflow.html)
Absolute Mermaid path: `E:\absolute\audit\workflow.mermaid.md`
Absolute HTML path: `E:\absolute\audit\workflow.html`
```

The current chat renderer does not reliably resolve a Windows absolute filesystem path as a Markdown target. Do not show an absolute-path link as if it were clickable. When `runtime_path_only` has no workspace mirror, show the verified absolute paths as code text and request `--workspace-root` for clickable editor links.
5. A verified workspace-relative path may be shown as an additional editor link only when `link_resolvable=true`; it is never the primary path for this Mermaid notification flow.
6. For `runtime_path_only`, the absolute paths remain valid technical evidence and notification targets if the host supports absolute-path opening. If the host requires workspace links, rerun with `--workspace-root`.
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

Use the host presentation flow above. For a clickable notification, pass the verified absolute `mermaid_file`/`workspace_mermaid_file` and `html_file`/`workspace_html_file` paths to the notification actions. Only add workspace-relative Markdown links when `status=workspace_mirror` and `link_resolvable=true`. If neither a card nor a clickable notification is available, emit Markdown links with the verified workspace-relative paths when `link_resolvable=true`, and retain the exact absolute paths as adjacent technical text. For `runtime_path_only`, show the absolute paths as code text and do not claim they are clickable. When `status=runtime_path_only`, retain those absolute paths as technical evidence; when `status=delivery_failed`, report the failure and never turn a guessed path into a link.

## Failure Handling

A delivery exception carries `audit_artifacts.mermaid_delivery` with `status=delivery_failed`. Check `artifact_generated`, `link_resolvable`, and `error` before reporting anything to the user. The writer removes an incomplete audit step and an incomplete workspace mirror. A failed result must therefore contain no user-facing link to a file that was not verified.

If the current call has no mermaid_delivery object, the host may derive host-only not_emitted. Reuse only the latest previously verified workspace-relative Mermaid and HTML links, state that the earlier render is still valid, and include the current workflow location. Keep their exact absolute paths as technical evidence or host action targets, never as Markdown destinations. For runtime_path_only, show absolute paths as code text and do not claim they are clickable. For delivery_failed, report the failure and next action only; do not create a notification or link, and do not reuse a previous link. If there is no earlier verified workspace-relative link, say that no usable Mermaid link is available yet.
