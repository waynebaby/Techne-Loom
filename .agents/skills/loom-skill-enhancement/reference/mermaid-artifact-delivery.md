# Mermaid Artifact Delivery

This reference defines how `/loom-skill-enhancement` reports Mermaid artifacts after `dotnet so.dll`, self-contained `so.exe`, or another SO runtime entry point.

## Source of Truth

- Read the actual `audit_artifacts.mermaid_file` and `audit_artifacts.html_file` paths returned by the current CLI call.
- Do not guess an audit step number, workflow id, or output directory from command text.
- Keep the runtime paths as evidence. A runtime path is not automatically a link that the current chat surface can open.
- `must_show_to_user_files` is an audit file list. It does not prove that a chat link is resolvable and does not replace `mermaid_delivery`.

## Workspace Mirror

Pass `--workspace-root <existing-directory>` when the caller needs links that VS Code can open from the workspace. The runtime validates that the directory exists and is outside the skill-owned directory. When the audit step is outside that root, the runtime copies `workflow.mermaid.md` and `workflow.html` into a new ignored `temp/exec-<timestamp>-mermaid-delivery-result/` directory below the workspace root.

The runtime reads both source files, verifies that they are complete and readable, computes SHA-256 values, copies them without overwrite, and verifies both destination hashes. The returned `workspace_relative_mermaid_file` and `workspace_relative_html_file` values are the only paths to use for workspace-relative Markdown links. Normalize separators to `/` in displayed links.

## Delivery States

`mermaid_delivery.status` uses these values:

- `workspace_mirror`: Mermaid and HTML were generated, copied under the workspace root, and both copies passed hash verification. `link_resolvable` is `true`.
- `runtime_path_only`: Mermaid and HTML were generated and verified, but no workspace mirror was requested. Keep the runtime paths as evidence and do not claim a verified workspace link.
- `delivery_failed`: required files were missing, unreadable, incomplete, or failed mirror verification. Do not emit a guessed link.
- `not_emitted`: no new Mermaid artifact was produced by the current operation. Reuse only a previously verified link and say that the render is unchanged.
- `card_displayed`: a host actually called a Mermaid card-display tool and confirmed the display. The runtime does not claim this state by itself.

`generation_status` is independent and reports `fresh`, `reused`, or `not_emitted`. `artifact_generated` means the runtime verified the Mermaid and HTML files. `link_resolvable` means the workspace-relative mirror was verified, not merely that an absolute path exists. `visual_preview_rendered` means a host opened and rendered the HTML preview; writing an HTML file does not set it to `true`. `card_display_available` describes host capability and must remain `false` unless the host reports that capability.

## User Output

Put the Mermaid link first, followed by the HTML preview link:

```text
Mermaid: [Open workflow Mermaid](temp/exec-<timestamp>-mermaid-delivery-result/wf-<id>/step-<n>-<action>/workflow.mermaid.md)
Preview: [Open workflow HTML](temp/exec-<timestamp>-mermaid-delivery-result/wf-<id>/step-<n>-<action>/workflow.html)
```

Only use these links when `status=workspace_mirror`, both workspace files exist, and `link_resolvable=true`. When `status=runtime_path_only`, report that the files were generated and keep the returned runtime paths in technical evidence. When `status=delivery_failed`, report the failure and the next concrete action; never turn a planned or guessed path into a link.

If a Mermaid card-display tool is available in the current chat surface, pass `card_input_file` directly to it. Do not ask another agent to return the Mermaid contents and do not reread the file solely to display it. The card is a presentation convenience, not evidence. Still retain the direct workspace link and HTML link when they are available.

## Failure Handling

A delivery exception carries `audit_artifacts.mermaid_delivery` with `status=delivery_failed`. Check `artifact_generated`, `link_resolvable`, and `error` before reporting anything to the user. The writer removes an incomplete audit step and an incomplete workspace mirror. A failed result must therefore contain no user-facing link to a file that was not verified.

If no new artifact exists, repeat the latest verified Mermaid and HTML links first, state that the earlier render is still valid, and include the current workflow location. If there is no earlier verified link, say that no usable Mermaid link is available yet.
