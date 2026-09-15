#!/usr/bin/env python3
"""Apply externally described, validated line-range edits to one UTF-8 file."""

from __future__ import annotations

import json
import os
import sys
import uuid
from typing import Any

UTF8_BOM = b"\xef\xbb\xbf"


def parse_arguments(arguments: list[str]) -> dict[str, str]:
    options: dict[str, str] = {}
    index = 0
    while index < len(arguments):
        argument = arguments[index]
        if argument == "--":
            index += 1
            continue
        if not argument.startswith("--") or index + 1 >= len(arguments):
            raise ValueError(f"Expected an option followed by a value, got '{argument}'.")
        name = argument[2:]
        value = arguments[index + 1]
        index += 2
        normalized_name = name.lower()
        if not name or value.startswith("--") or normalized_name in options:
            raise ValueError(f"Invalid or duplicated option '--{name}'.")
        options[normalized_name] = value
    return options


def require_path(options: dict[str, str], name: str) -> str:
    value = options.get(name)
    if not value or not value.strip():
        raise ValueError(f"Missing required option '--{name}'.")
    return os.path.abspath(value)


def has_utf8_bom(data: bytes) -> bool:
    return data.startswith(UTF8_BOM)


def decode_utf8(data: bytes, file_path: str) -> str:
    content = data[3:] if has_utf8_bom(data) else data
    try:
        return content.decode("utf-8", errors="strict")
    except UnicodeDecodeError as error:
        raise ValueError(f"File '{file_path}' is not valid UTF-8: {error}") from error


def read_bytes(file_path: str) -> bytes:
    with open(file_path, "rb") as file:
        return file.read()


def read_utf8_text(file_path: str) -> str:
    return decode_utf8(read_bytes(file_path), file_path)


def normalize(text: str) -> str:
    return text.replace("\r\n", "\n").replace("\r", "\n")


def split_lines(text: str) -> list[str]:
    normalized = normalize(text)
    if not normalized:
        return []
    if normalized.endswith("\n"):
        normalized = normalized[:-1]
    return normalized.split("\n")


def detect_newline(text: str) -> str:
    if "\r\n" in text:
        return "\r\n"
    if "\r" in text:
        return "\r"
    return "\n"


def trailing_newline(text: str) -> str:
    if text.endswith("\r\n"):
        return "\r\n"
    if text.endswith("\n"):
        return "\n"
    if text.endswith("\r"):
        return "\r"
    return ""


def convert_newlines(text: str, newline: str) -> str:
    if newline == "\r\n":
        return text.replace("\n", "\r\n")
    if newline == "\r":
        return text.replace("\n", "\r")
    return text


def boundary_equals(actual: str, expected: str) -> bool:
    return actual.strip().lower() == expected.strip().lower()


def required_positive_integer(edit: Any, property_name: str) -> int:
    value = edit.get(property_name) if isinstance(edit, dict) else None
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise ValueError(f"Edit property '{property_name}' must be a positive integer.")
    return value


def required_path(edit: Any, property_name: str) -> str:
    value = edit.get(property_name) if isinstance(edit, dict) else None
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"Edit property '{property_name}' must be a non-empty path.")
    return os.path.abspath(value)


def read_boundary(file_path: str) -> str:
    content = read_utf8_text(file_path)
    lines = split_lines(content)
    if not lines and not normalize(content):
        return ''
    if len(lines) != 1:
        raise ValueError(f"Boundary file '{file_path}' must contain exactly one logical line.")
    return lines[0]


def read_edits(manifest_path: str) -> list[dict[str, Any]]:
    try:
        manifest = json.loads(read_utf8_text(manifest_path))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"Unable to read edits manifest '{manifest_path}': {error}") from error
    if not isinstance(manifest, dict) or not isinstance(manifest.get("edits"), list) or not manifest["edits"]:
        raise ValueError("The edits manifest must contain a non-empty 'edits' array.")

    edits: list[dict[str, Any]] = []
    for edit in manifest["edits"]:
        start_line = required_positive_integer(edit, "start_line")
        end_line = required_positive_integer(edit, "end_line")
        if end_line < start_line:
            raise ValueError(f"Invalid edit range {start_line}-{end_line}.")
        expected_start_path = required_path(edit, "expected_start_file")
        expected_end_path = required_path(edit, "expected_end_file")
        replacement_path = required_path(edit, "replacement_file")
        edits.append(
            {
                "start_line": start_line,
                "end_line": end_line,
                "expected_start": read_boundary(expected_start_path),
                "expected_end": read_boundary(expected_end_path),
                "replacement_lines": split_lines(read_utf8_text(replacement_path)),
            }
        )
    return edits


def validate_ranges(edits: list[dict[str, Any]], original_lines: list[str], target_path: str) -> None:
    ordered = sorted(edits, key=lambda edit: edit["start_line"])
    for index, edit in enumerate(ordered):
        start_line = edit["start_line"]
        end_line = edit["end_line"]
        if start_line > len(original_lines) or end_line > len(original_lines):
            raise ValueError(
                f"Edit range {start_line}-{end_line} exceeds '{target_path}' ({len(original_lines)} lines)."
            )
        if not boundary_equals(original_lines[start_line - 1], edit["expected_start"]):
            raise ValueError(f"Edit range {start_line}-{end_line} has mismatched boundary content.")
        if not boundary_equals(original_lines[end_line - 1], edit["expected_end"]):
            raise ValueError(f"Edit range {start_line}-{end_line} has mismatched boundary content.")
        if index > 0 and ordered[index - 1]["end_line"] >= start_line:
            previous = ordered[index - 1]
            raise ValueError(
                f"Edit ranges {previous['start_line']}-{previous['end_line']} and {start_line}-{end_line} overlap."
            )


def validate_result(file_path: str, expected_lines: list[str], expected_trailing: str, expected_bom: bool) -> None:
    data = read_bytes(file_path)
    text = decode_utf8(data, file_path)
    if (
        split_lines(text) != expected_lines
        or trailing_newline(text) != expected_trailing
        or has_utf8_bom(data) != expected_bom
    ):
        raise ValueError(f"Post-write validation failed for '{file_path}'.")


def write_utf8_file(file_path: str, text: str, with_bom: bool) -> None:
    data = text.encode("utf-8", errors="strict")
    if with_bom:
        data = UTF8_BOM + data
    descriptor = os.open(file_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o666)
    try:
        with os.fdopen(descriptor, "wb") as file:
            file.write(data)
    except Exception:
        try:
            os.close(descriptor)
        except OSError:
            pass
        raise


def main() -> None:
    options = parse_arguments(sys.argv[1:])
    target_path = require_path(options, "target-file")
    edits_manifest_path = require_path(options, "edits-file")

    original_bytes = read_bytes(target_path)
    original = decode_utf8(original_bytes, target_path)
    newline = detect_newline(original)
    original_trailing = trailing_newline(original)
    original_lines = split_lines(original)
    edits = read_edits(edits_manifest_path)
    validate_ranges(edits, original_lines, target_path)

    expected_lines = list(original_lines)
    for edit in sorted(edits, key=lambda item: item["start_line"], reverse=True):
        start_index = edit["start_line"] - 1
        remove_count = edit["end_line"] - edit["start_line"] + 1
        expected_lines[start_index : start_index + remove_count] = edit["replacement_lines"]

    updated = convert_newlines("\n".join(expected_lines), newline) + original_trailing
    temporary_path = f"{target_path}.{uuid.uuid4().hex}.range-edit.tmp"
    try:
        write_utf8_file(temporary_path, updated, has_utf8_bom(original_bytes))
        validate_result(temporary_path, expected_lines, original_trailing, has_utf8_bom(original_bytes))
        os.replace(temporary_path, target_path)
        validate_result(target_path, expected_lines, original_trailing, has_utf8_bom(original_bytes))
        print(f"Applied {len(edits)} line-range edits to '{target_path}' from one original read.")
    finally:
        if os.path.exists(temporary_path):
            os.unlink(temporary_path)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
