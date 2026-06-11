# Getting Started

[中文](../../zh-cn/getting-started/README.md)

Start here if you want the shortest path from repository clone to a first runnable workflow.

## Read In Order

- [Install And Run](install.md)
- [First Workflow](first-workflow.md)

The first public release is `.NET` first. Node.js and Python roots are reserved, documented, and schema-aligned, but they are not implemented runtimes in v1.

## What "Runnable" Means Right Now

The current reviewed SO slice supports two useful starting points:

1. `dotnet so.dll ls <path>`
Details:
This is the fastest end-to-end smoke path. It compiles shorthand input into a workflow, runs a wrapped command-line listing, and emits a `<so_property>` result block.

2. `dotnet so.dll run --workflow-file ...`
Details:
This is the general path for authored workflows. Use it when you want to control step kinds, blocked payload surfaces, and resume envelopes explicitly.

Use [First Workflow](first-workflow.md) for both paths.
