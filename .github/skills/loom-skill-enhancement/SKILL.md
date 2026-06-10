---
name: loom-skill-enhancement
description: Guide-first deterministic skill enhancement skill that routes through Techne Loom package docs and SO package binaries.
---

# /loom-skill-enhancement

Guide-first deterministic skill enhancement skill.

## Mission

This skill helps create or upgrade an existing skill so deterministic nodes can run on-rail through the SO package flow. It depends on package guides and package binaries instead of hiding behavior behind a private skill-local template.

## Read This First

Choose package channel first:

- Released (main, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta (development, English canonical): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>
- Released (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.zh-CN.md>
- Beta (zh-CN mirror): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.zh-CN.md>

Then read the package guide:

- Released guide (English): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md>
- Beta guide (English): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md>
- Released guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/main/docs/zh-cn/reference/products/so-guide.md>
- Beta guide (zh-CN): <https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/so-guide.md>

## Input Contract

- target skill path or repository path
- deterministic skill goal or upgrade request
- optional language surface (`en` or `zh-cn`). If omitted, the current public guide surface defaults to `en`, so callers should pass `zh-cn` explicitly when they need Chinese guide links and should pass `--lang <language>` when invoking the guide command
- optional JSON context file
- optional audit output root

## Default Assumptions

Unless the user overrides them, apply these defaults during SO-based skill enhancement:

- use the absolute URL of the released or beta package index page that matches the chosen language surface as the source of truth for acquiring the SO package; if execution needs a local runtime, install or unpack the runtime from the selected package channel into an external temporary directory instead of the target repo
- require SO skills and any target product that adopts Loom-bin-based skills to preserve released and beta package index absolute URLs in their own skill or product-facing docs, using localized mirrors when the product exposes localized package index pages
- let the AI agent execute `dotnet so.dll planner` / `run` / `resume` directly in the terminal
- keep SO-owned files under `<target-skill-root>/assets/so-workflow/`
- write the planner description file to `<target-skill-root>/assets/so-workflow/skill-plan.md`
- derive that description file at fine granularity from the current `SKILL.md` decision tree when it exists, or from `goal` plus supporting references when creating a greenfield skill, then let the maintainer review it
- when `references/` Markdown sources exist, concatenate them with clear section headers into a temporary `merged-context.md` working note, then convert the needed context into a temporary JSON context file for `--context-file`
- store the deterministic workflow template as its own JSON file, and place run artifacts under an `audit/` child directory
- force workflow-template correctness ahead of every other optimization: the generated workflow JSON template must be complete and detailed, must align with the selected channel guide, and must pass the current public SO workflow-file load/status gate before it can become the execution authority for the enhanced target skill
- keep `SKILL.md` compressed to about 80-100 lines, preserving high-level steps, guardrail headings, SO guidance, and the `## Workflow Contract` section title
- when released-channel docs do not actually ship the same SO enhancement asset shape, mark that surface as Beta Only instead of implying parity
- when SO weaves out, use the structured blocked payload such as `current_step_kind` to classify the wait category, and consume `skill_hint` literally as the next external action instruction: ask the user for mandatory human-input seams, treat waits on email, files, messages, or downstream script results as valid external wait states that either return the expected next input shape or pause until the external result arrives, and continue automatically only when the structured payload plus literal `skill_hint` point to a non-human continuation
- treat these as skill-layer adaptation defaults rather than generic SO runtime guarantees; if the selected channel guide does not expose an equivalent surface, mark that behavior as Beta Only

## Runtime Flow

1. Confirm package channel from the package index.
2. Run `dotnet so.dll --guide [--lang <language>]`.
3. Create or refresh `<target-skill-root>/assets/so-workflow/skill-plan.md` from the target `SKILL.md` when it exists, or from `goal` plus supporting references when creating a new skill.
4. When `references/` Markdown files exist, concatenate them with clear section headers into a temporary `merged-context.md` working note, then convert the needed context into a temporary JSON context file.
5. Run `dotnet so.dll planner --description-file <path> --workflow-file <path> [--context-file <path>]`.
6. Validate that the generated deterministic workflow JSON template is complete and detailed against the selected channel guide, then confirm that it passes the current public SO workflow-file load/status gate. The current public CLI does not expose a standalone `compile` command; a successful `dotnet so.dll status --workflow-file <path>` only confirms load/status compatibility, so it is a required gate but not the only completeness judgment.
7. Store the validated workflow JSON as the target skill template, treat that workflow template as the execution authority for the enhanced target skill only after the guide-alignment review plus workflow-file load/status gate succeed, and write run artifacts under `<target-skill-root>/assets/so-workflow/audit/`.
8. Run `dotnet so.dll run` / `resume` against template copies. When variance appears, update the workflow JSON through planner flow.
9. Use the structured blocked payload such as `current_step_kind` to classify whether a weave-out is waiting for mandatory user input, waiting for external asynchronous results, or explicitly allowing non-human continuation, and then consume `skill_hint` literally as the next action instruction.

## Required Outputs

- chosen package index link
- package index link set for released/beta, including localized mirrors when they exist
- guide link
- planner-generated deterministic workflow template path, after guide-alignment review plus the SO workflow-file load/status gate succeed; that validated template becomes the execution authority for the enhanced target skill
- runtime `workflow_file` / `event_log_file`
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
