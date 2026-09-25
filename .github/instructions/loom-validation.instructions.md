---
name: Loom Validation And Tooling Rules
description: File editing, CLI input, schema checks, audit artifacts, cross-platform testing, and review cadence for Loom changes.
applyTo: ".github/**,scripts/**,src/**,tests/**,Techne.Loom.sln,.agents/skills/**"
---

# Loom Validation And Tooling Rules

Use these rules when editing repository files, running CLI validation, changing source/tests, updating workflows, or preparing a reviewable implementation slice.

## Copilot Tool Restrictions

- GitHub Copilot must not use `apply_patch` in this repository. Use a checked-in range editor or another repository-approved file-editing mechanism.
- Prefer external-file-driven scripts for deterministic edits and validation. Keep expected boundary text, replacement content, and validation inputs in separate files; do not embed multiline edit content in a command or script invocation.
- Use the checked-in range editor matching the available runtime: `scripts/ApplyLineRangeEdit.csx` with `dotnet script`, `.js` with `node`, `.py` with `python`, `.ps1` with Windows PowerShell/PowerShell 7, or `.sh` with Bash.
- Every entry accepts `--target-file` and `--edits-file`; the external JSON manifest uses 1-based inclusive ranges, external expected boundary files, and an external replacement file. Paths resolve from the working directory.
- For one target with multiple changes, pass one manifest with non-overlapping ranges. Each entry reads once, validates all boundaries, applies ranges from highest line number to lowest, writes through a same-directory temporary file, and validates the complete result before and after replacement.
- Range editors preserve strict UTF-8, BOM, newline style, and trailing-newline state. Boundary comparison only normalizes leading/trailing whitespace and case. Failed pre-write validation leaves the target unchanged.
- Do not replace this contract with marker/general string replacement, inline multiline content, implicit wrapper nesting, skipped boundary/overlap checks, or skipped post-write validation. If the selected runtime is unavailable, report the environment block.
- Use PowerShell for Windows orchestration, environment inspection, dotnet, and Git; Bash for WSL/Linux-native restore, build, test, and the Bash range editor; Node.js for JavaScript/TypeScript tooling or JSON processing.

## File-Based CLI Input Rules

- Every file-valued CLI parameter is path-only. The caller creates, completes, and closes the full input set before one command starts; the CLI preflights all required input files before reading, executing, modifying, or writing.
- Workflow builder, editor, and verifier examples are ordinary `.cs` files executed by the built-in Roslyn host. They do not require a caller-created project or an externally installed C# script runtime.
- Patch content, scripts, JSON, workflows, references, objectives, contexts, instances, and resume results are complete disk files. Inline script, JSON, patch, or replacement content is rejected.
- Output files and output directories are CLI-owned destinations, not input-content parameters.

## Schema And Compile Consistency

- Before every code check-in, invoke the current AO and SO self-contained RID apphosts directly with `--schema-demo-output <directory>` using separate external output directories.
- Each run creates both `workflow.schema.json` and `workflow.demo.json`; creating only one is invalid evidence.
- Compile each generated demo with its matching runtime through the documented compile path. Compare docs against the generated schema and compile result; runtime-generated files and compile behavior are the source of truth.
- Do not keep a hand-written JSON workflow example as the current compile contract. Obtain examples from runtime export and identify the runtime version.
- Keep generated schema/demo files under an external temporary or execution-output directory unless explicitly requested as deliverables.

## Audit Artifact Rules

- Workflow audit outputs are per-step audit records, not optional display helpers. Unless explicitly requested, use a temporary output root.
- Do not default compile artifacts, audit artifacts, intermediate workflow materializations, or conversation-referenceable outputs under a skill directory or `assets/so-workflow/`; use a runtime temporary root, repo temporary root, or explicit execution output root.
- Every reported output has a normalized path, existence/readability check, and, when possible, a verified workspace-relative mirror. Do not replace a real path with a guessed repository-relative path.
- Persist audit artifacts under `{output}/wf-{wfid}/step-{seq}-{action}/`.
- Successful render-producing steps include point-in-time Mermaid Markdown, HTML, and workflow JSON backup. Compile-failure steps include the readable workflow JSON backup and `workflow.compile-feedback.json`, and must not create placeholder Mermaid/HTML files.
- Compile and audit flows never overwrite an existing artifact file in place. Fail with a rich conflict error and require a different output root or cleaned destination.

## Cross-Platform WSL Test Check

- For validation of both `development` and `main`, including merge handoffs, start Windows and WSL restore, build, and test jobs in parallel as soon as the checkout is ready. Do not wait for one platform's full sequence before starting the other.
- Keep platform outputs isolated. Windows may use the native checkout; WSL uses a separate checkout/worktree or explicit platform-specific `bin/` and `obj/` roots. Windows and WSL never share build/intermediate directories.
- Each platform runs restore, build, and test in its own job. The final validation waits for both, records results separately, and fails closed if either fails or is environment-blocked.
- On Windows, use WSL2 Ubuntu to reproduce Linux-only failures before changing cross-platform code. Run from the Linux-mounted repository path and never treat mounted Windows `bin/` or `obj/` output as a Linux test run.
- Before the first native Linux build, remove only rebuildable `bin/` and `obj/` directories under `src/` and `tests/` in the WSL checkout. Do not remove source files or checked-in assets.
- Set `NUGET_PACKAGES` explicitly when reusing a readable Windows package cache from WSL. `--ignore-failed-sources` is allowed only for local offline/network-limited probes; CI restore must still fail when a required source/package is unavailable.
- Tests that exercise intentionally long-running tasks must not set a test timeout or add timeout-based cancellation merely to shorten the run. Let the task complete naturally, and report genuine test failures separately from runner or environment interruption.
- The final `dotnet test` result is meaningful only after WSL-native restore and build succeed and produce native Linux Release test assemblies. Record WSL SDK/runtime/RID, restore warnings, and test summary separately. If WSL cannot restore because of network/cache prerequisites, report an environment block rather than a code pass/fail.

## Execution Order And Review Cadence

- Before broader implementation, update `AGENTS.md` with current language, documentation, and execution rules.
- After every major implementation slice, run a reasonable review/fix/validate/commit workflow before starting the next slice.
- Do not carry multiple major slices into one late review unless the user explicitly overrides this cadence.
- Keep a review-and-commit slice usually at or below 50 changed files. Stop and review before adding more when the pending scope approaches that size.
- Run review immediately even for smaller slices when they change protocol contracts, schemas, package seams, or runtime control behavior.
- Major slices include root agent rules, flagship READMEs, docs skeletons, package scaffolding, protocol/schema changes, and code implementation.
- Do not continue into the next major slice with unreviewed or uncommitted work unless explicitly overridden.
