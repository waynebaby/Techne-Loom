---
name: loom-plan-execution
description: Guide-first plan execution skill that routes through Techne Loom package docs and AO package binaries.
---

# /loom-plan-execution

Guide-first plan execution skill.

## Mission

This skill does not hide package setup behind its own template. It first points the user to the correct package channel and guide surface, then routes execution through the installed Techne Loom package binaries.

## Read This First

Choose package channel first:

- Released (main, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta (development, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

Then read the package guide:

- Released guide (English): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/ao-guide.md>
- Beta guide (English): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/ao-guide.md>
- Released guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/ao-guide.md>
- Beta guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/ao-guide.md>

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Optional input: language surface (`en` or `zh-cn`). If omitted, the current public guide surface defaults to `en`, so callers should pass `zh-cn` explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Default Assumptions

Unless the user overrides them, apply these defaults during AO-based plan execution:

- use the package index absolute URLs as the source of truth for acquiring the AO package
- require AO skills and any target product that adopts Loom-bin-based skills to preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- keep `dotnet ao.dll --guide [--lang <language>]` as the authoritative runtime surface instead of restating private templates in the skill
- treat AO as CLI-only in this project; do not rely on MCP hosts or MCP tools

## DLL Interface Mapping

- `dotnet ao.dll --guide [--lang <language>]`: runtime authority and command surface source of truth
- `dotnet ao.dll planner --plan-file <path> --workflow-file <path> [--context-file <path>]`: derive executable workflow from the plan
- `dotnet ao.dll compile --workflow-file <path> [--audit-output <path>]`: validate workflow materialization when execution flow requires explicit compile
- `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>]`: execute plan objective
- `dotnet ao.dll resume --workflow-file <path> --result-file <path>`: weave back with structured external result

## Runtime Flow

1. Confirm package channel from the package index.
2. Run `dotnet ao.dll --guide [--lang <language>]`.
3. Run `dotnet ao.dll planner --plan-file <path> --workflow-file <path> [--context-file <path>]`.
4. Run `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>]`.
5. When blocked, inspect the returned workflow JSON plus `next_frontier` and continue with `dotnet ao.dll resume`.

## Required Outputs

- chosen package index link
- package index link set for released/beta, including localized mirrors when they exist
- guide link
- DLL interface mapping used by this skill (`--guide`, `planner`, `compile`, `run`, `resume`)
- planner-generated workflow JSON path
- runtime `workflow_file` / `event_log_file`
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
