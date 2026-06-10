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

- Released (main): <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- Beta (development): <https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md>

Then read the package guide:

- Released guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md>
- Beta guide: <https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md>

## Input Contract

- target skill path or repository path
- deterministic skill goal or upgrade request
- optional context file
- optional audit output root

## Runtime Flow

1. Confirm package channel from the package index.
2. Run `dotnet so.dll --guide`.
3. Run `dotnet so.dll planner --description-file <path> --workflow-file <path> [--context-file <path>]`.
4. Store the generated deterministic workflow JSON as the target skill template.
5. Run `dotnet so.dll run` / `resume` against template copies. When variance appears, update the workflow JSON through planner flow.

## Required Outputs

- chosen package index link
- guide link
- planner-generated deterministic workflow template path
- runtime `workflow_file` / `event_log_file`
- audit artifact links for Mermaid Markdown, HTML, and workflow JSON backups
