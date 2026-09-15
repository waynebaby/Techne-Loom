# Repository Editing Resource

This directory contains five checked-in range editors with one shared contract:

- `ApplyLineRangeEdit.csx` via `dotnet script`
- `ApplyLineRangeEdit.js` via `node`
- `ApplyLineRangeEdit.py` via `python`
- `ApplyLineRangeEdit.ps1` via Windows PowerShell 5.1 or PowerShell 7
- `ApplyLineRangeEdit.sh` via Git Bash, WSL, or Linux Bash

For the `.csx` entry, install the verified script runner with `dotnet tool install --global dotnet-script --version 2.0.1` before using `dotnet script`. The Node.js, Python, PowerShell, and Bash entries use their host runtimes directly. The Bash entry requires Perl 5 with the core `JSON::PP` and `Encode` modules.

## Shared command line

Every entry accepts the same two required options:

- `--target-file`: the file to edit
- `--edits-file`: an external JSON manifest

The optional `--` argument separator is accepted for compatibility with `dotnet script`. Paths are resolved from the process working directory, not from the manifest file directory. Positional arguments, missing values, and duplicate options are rejected.

Examples:

```powershell
# .NET script
 dotnet script scripts/ApplyLineRangeEdit.csx -- `
   --target-file path/to/target.cs `
   --edits-file temp/edits.json

# Node.js
 node scripts/ApplyLineRangeEdit.js --target-file path/to/target.cs --edits-file temp/edits.json

# Python
 python scripts/ApplyLineRangeEdit.py --target-file path/to/target.cs --edits-file temp/edits.json

# Windows PowerShell 5.1
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/ApplyLineRangeEdit.ps1 `
   --target-file path/to/target.cs --edits-file temp/edits.json

# PowerShell 7
 pwsh -NoProfile -File scripts/ApplyLineRangeEdit.ps1 `
   --target-file path/to/target.cs --edits-file temp/edits.json

# Git Bash, WSL, or Linux Bash
 bash scripts/ApplyLineRangeEdit.sh --target-file path/to/target.cs --edits-file temp/edits.json
```

Use the same manifest with every entry. Do not put replacement text directly in the command line.

## Manifest

The manifest must contain a non-empty `edits` array. Each item uses 1-based inclusive line numbers and three external files:

```json
{
  "edits": [
    {
      "start_line": 10,
      "end_line": 24,
      "expected_start_file": "temp/expected-start.txt",
      "expected_end_file": "temp/expected-end.txt",
      "replacement_file": "temp/replacement-a.txt"
    },
    {
      "start_line": 40,
      "end_line": 43,
      "expected_start_file": "temp/expected-start-b.txt",
      "expected_end_file": "temp/expected-end-b.txt",
      "replacement_file": "temp/replacement-b.txt"
    }
  ]
}
```

`start_line` and `end_line` must be positive integers, the range must be inside the target, and ranges must not overlap. Each expected boundary file must contain exactly one logical line. A replacement file may be empty or contain multiple logical lines. The first and last target lines are compared after trimming leading/trailing whitespace and normalizing case; internal lines are replaced as a complete range.

## File safety contract

Each entry reads the target file once before validation. It strictly decodes UTF-8, keeps the target's UTF-8 BOM state, detects and preserves LF, CRLF, or CR newlines, and keeps the target's trailing-newline state. Multiple ranges are applied from highest line number to lowest so all manifest line numbers refer to the original snapshot.

The result is written to a unique temporary file beside the target. The temporary file is fully validated before it replaces the target, and the final target is validated again after replacement. Validation checks all logical lines, the trailing newline, and the BOM. Errors return a non-zero exit code, remove any temporary file, and leave the target unchanged when validation fails before replacement.

Do not substitute marker replacement or direct whole-file string replacement for this contract. The external boundary and replacement files make the edit reviewable and keep multiline content out of commands and script source.

## Contract test

Run the cross-runtime contract test from the repository root:

```powershell
node scripts/tests/ApplyLineRangeEditContract.test.js
```

The test exercises all five entries with LF, CRLF, and CR input, BOM and trailing-newline variants, multiple ranges, empty replacements, empty logical lines, invalid UTF-8, boundary mismatches, overlapping and out-of-range ranges, and argument failures. Use `--only=node`, `--only=python`, `--only=powershell`, `--only=bash`, or `--only=csx` to validate one entry when a runtime is unavailable. A missing runtime is an environment block, not a passing implementation.
