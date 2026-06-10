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

- Released (main): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta (development): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

Then read the package guide:

- Released guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/ao-guide.md>
- Beta guide: <https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/ao-guide.md>

## Input Contract

- Preferred input: a rich plan with at least 10 non-empty lines
- Fallback input: a file path to a detailed plan document
- Optional input: explicit audit output root

If the request is too short, redirect the user into plan mode or require a detailed plan file before proceeding.

## Runtime Flow

1. Confirm package channel from the package index.
2. Run `dotnet ao.dll --guide`.
3. Run `dotnet ao.dll planner --plan-file <path> --workflow-file <path> [--context-file <path>]`.
4. Run `dotnet ao.dll run --objective-file <path> --session-dir <path> [--context-file <path>] [--audit-output <path>]`.
5. When blocked, inspect the returned workflow JSON plus `next_frontier` and continue with `dotnet ao.dll resume`.

## Required Outputs

- chosen package index link
- guide link
- planner-generated workflow JSON path
- runtime `workflow_file` / `event_log_file`
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
