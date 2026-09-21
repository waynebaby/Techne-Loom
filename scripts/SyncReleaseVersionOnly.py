#!/usr/bin/env python3
"""Copy only safe version-only line changes from a release refresh worktree."""

from __future__ import annotations

import argparse
import re
import subprocess
from pathlib import Path

VERSION_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?![0-9A-Za-z])"
)
PROTECTED_CONTENT_PATTERN = re.compile(
    r"(?:"
    r"https?://|"
    r"ftp://|"
    r"www\.|"
    r"!?\[[^\]]+\]\([^)]+\)|"
    r"\b(?:url|uri|href|link|integrity|"
    r"(?:[a-z0-9-]+[_-])?(?:sha(?:1|224|256|384|512)?|hash|checksum|digest))"
    r"[\"']?\s*[:=]"
    r")",
    re.IGNORECASE,
)


def normalize_versions(line: str) -> str:
    return VERSION_PATTERN.sub("<VERSION>", line.rstrip("\r\n"))


def is_version_only_candidate(line: str) -> bool:
    content = line.rstrip("\r\n")
    return bool(VERSION_PATTERN.search(content)) and not PROTECTED_CONTENT_PATTERN.search(content)


def changed_paths(source_root: Path) -> list[str]:
    result = subprocess.run(
        ["git", "-C", str(source_root), "diff", "--name-only"],
        check=True,
        capture_output=True,
        text=True,
    )
    return sorted({line for line in result.stdout.splitlines() if line})


def read_lines(path: Path) -> list[str]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return handle.read().splitlines(keepends=True)


def sync_file(source_path: Path, target_path: Path) -> int:
    if not source_path.is_file() or not target_path.is_file():
        return 0

    source_lines = read_lines(source_path)
    target_lines = read_lines(target_path)
    replacements: dict[int, str] = {}

    if len(source_lines) == len(target_lines):
        for index, (source_line, target_line) in enumerate(zip(source_lines, target_lines)):
            if (
                source_line != target_line
                and is_version_only_candidate(source_line)
                and is_version_only_candidate(target_line)
                and normalize_versions(source_line) == normalize_versions(target_line)
            ):
                newline = target_line[len(target_line.rstrip("\r\n")) :]
                replacements[index] = source_line.rstrip("\r\n") + newline
    else:
        target_normalized: dict[str, list[int]] = {}
        for index, target_line in enumerate(target_lines):
            if is_version_only_candidate(target_line):
                target_normalized.setdefault(normalize_versions(target_line), []).append(index)
        for source_line in source_lines:
            if not is_version_only_candidate(source_line):
                continue
            candidates = target_normalized.get(normalize_versions(source_line), [])
            if len(candidates) == 1:
                index = candidates[0]
                target_line = target_lines[index]
                if source_line != target_line:
                    newline = target_line[len(target_line.rstrip("\r\n")) :]
                    replacements[index] = source_line.rstrip("\r\n") + newline

    if not replacements:
        return 0

    for index, replacement in replacements.items():
        target_lines[index] = replacement
    with target_path.open("w", encoding="utf-8", newline="") as handle:
        handle.write("".join(target_lines))
    return len(replacements)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--target-root", type=Path, required=True)
    args = parser.parse_args()

    total = sum(
        sync_file(args.source_root / relative_path, args.target_root / relative_path)
        for relative_path in changed_paths(args.source_root)
    )
    print(f"Synchronized {total} version-only lines from refresh workspace.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
