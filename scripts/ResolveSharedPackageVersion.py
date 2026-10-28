#!/usr/bin/env python3
"""Resolve the next monotonic package version from the published NuGet high-water mark."""

from __future__ import annotations

import argparse
import json
import re
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

VERSION_PATTERN = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-beta)?$")
EXPECTED_RETIRED_PACKAGE_IDS = (
    "Techne.Loom.Common",
    "Techne.Loom.AgentOrchestrator",
    "Techne.Loom.SkillOrchestrator",
)
EXPECTED_LIBRARY_PACKAGE = {
    "package_id": "Techne.Loom.Abstractions",
    "project": "src/dotnet/Techne.Loom.Abstractions/Techne.Loom.Abstractions.csproj",
    "target_framework": "net9.0",
    "runtime_identifier": None,
}
EXPECTED_PRODUCTS = ("ao", "so")
EXPECTED_RIDS = (
    "win-x64",
    "win-arm64",
    "linux-x64",
    "linux-arm64",
    "linux-musl-x64",
    "linux-musl-arm64",
    "osx-x64",
    "osx-arm64",
)


@dataclass(frozen=True, order=True)
class NumericVersion:
    major: int
    minor: int
    patch: int

    @classmethod
    def parse(cls, value: str) -> "NumericVersion | None":
        match = VERSION_PATTERN.fullmatch(value.strip())
        if match is None:
            return None
        return cls(*(int(part) for part in match.groups()))

    def as_text(self) -> str:
        return f"{self.major}.{self.minor}.{self.patch}"

    def next_patch(self) -> "NumericVersion":
        return NumericVersion(self.major, self.minor, self.patch + 1)


def select_next_version(
    published_versions: Iterable[str],
    channel: str,
    version_floor: str = "",
) -> str:
    if channel not in {"beta", "released"}:
        raise ValueError(f"Unsupported channel: {channel}")

    parsed_versions = [
        (value.strip(), NumericVersion.parse(value))
        for value in published_versions
    ]
    candidates = [parsed for _, parsed in parsed_versions if parsed is not None]
    if not candidates:
        raise ValueError("No valid published stable or beta package version was found.")

    stable_versions = [
        parsed
        for value, parsed in parsed_versions
        if parsed is not None and not value.endswith("-beta")
    ]
    beta_versions = [
        parsed
        for value, parsed in parsed_versions
        if parsed is not None and value.endswith("-beta")
    ]
    highest_stable = max(stable_versions, default=None)
    highest_beta = max(beta_versions, default=None)
    if channel == "released" and highest_beta is not None and (
        highest_stable is None or highest_beta > highest_stable
    ):
        resolved = highest_beta
    else:
        resolved = max(candidates).next_patch()

    if version_floor:
        parsed_floor = NumericVersion.parse(version_floor)
        if parsed_floor is None or version_floor.strip().endswith("-beta"):
            raise ValueError(f"Release version floor must be a stable numeric version: {version_floor}")
        resolved = max(resolved, parsed_floor)

    version_text = resolved.as_text()
    return f"{version_text}-beta" if channel == "beta" else version_text


def version_floor_from_release_set(manifest_path: Path) -> str:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    release = manifest.get("version_authority", {}).get("release", {})
    version_floor = release.get("version_floor")
    if not isinstance(version_floor, str):
        raise ValueError("Release set must declare version_authority.release.version_floor.")

    parsed_floor = NumericVersion.parse(version_floor)
    if parsed_floor is None or version_floor.strip().endswith("-beta"):
        raise ValueError(f"Release version floor must be a stable numeric version: {version_floor}")
    return parsed_floor.as_text()


def package_ids_from_release_set(manifest_path: Path) -> list[str]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    packages = manifest.get("packages", {})
    runtime = packages.get("runtime", {})
    products = runtime.get("products")
    rids = runtime.get("rids")
    libraries = packages.get("libraries")
    retired = manifest.get("retired_package_high_water", {})
    retired_package_ids = retired.get("package_ids")
    if tuple(retired_package_ids or ()) != EXPECTED_RETIRED_PACKAGE_IDS:
        raise ValueError("Retired package high-water input does not match the published core package family.")
    if tuple(products or ()) != EXPECTED_PRODUCTS:
        raise ValueError("Release set runtime products do not match the public package family.")
    if tuple(rids or ()) != EXPECTED_RIDS:
        raise ValueError("Release set runtime RIDs do not match the public package family.")
    if libraries != [EXPECTED_LIBRARY_PACKAGE]:
        raise ValueError("Release set libraries must contain exactly the no-RID Techne.Loom.Abstractions package.")

    package_ids: list[str] = []
    for product in EXPECTED_PRODUCTS:
        product_name = "AgentOrchestrator" if product == "ao" else "SkillOrchestrator"
        package_ids.extend(f"Techne.Loom.{product_name}.Runtime.{rid}" for rid in EXPECTED_RIDS)
    package_ids.append(EXPECTED_LIBRARY_PACKAGE["package_id"])
    return package_ids


def retired_package_high_water_versions_from_release_set(manifest_path: Path) -> list[str]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    retired = manifest.get("retired_package_high_water", {})
    released = retired.get("released")
    beta = retired.get("beta")
    if NumericVersion.parse(released or "") is None or "-" in released:
        raise ValueError("Retired stable package high-water version is invalid.")
    if NumericVersion.parse(beta or "") is None or not beta.endswith("-beta"):
        raise ValueError("Retired beta package high-water version is invalid.")
    return [released, beta]


def fetch_published_versions(package_id: str) -> list[str]:
    normalized_id = package_id.lower()
    url = f"https://api.nuget.org/v3-flatcontainer/{normalized_id}/index.json"
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "Techne-Loom-shared-version-resolver"},
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        payload = json.load(response)
    versions = payload.get("versions")
    if not isinstance(versions, list) or not all(isinstance(value, str) for value in versions):
        raise ValueError(f"NuGet index for {package_id} has no valid versions list.")
    return versions


def collect_published_versions(
    package_ids: Iterable[str],
    fetcher: Callable[[str], list[str]] | None = None,
) -> list[str]:
    fetch = fetcher or fetch_published_versions
    published_versions: list[str] = []
    for package_id in package_ids:
        versions = fetch(package_id)
        valid_versions = [version for version in versions if NumericVersion.parse(version) is not None]
        if not valid_versions:
            raise RuntimeError(
                f"NuGet index for {package_id} has no valid stable or beta version; refusing to guess a version."
            )
        published_versions.extend(valid_versions)
    return published_versions


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--release-set-file", type=Path, required=True)
    parser.add_argument("--channel", choices=("beta", "released"), required=True)
    args = parser.parse_args()

    package_ids = package_ids_from_release_set(args.release_set_file)
    published_versions = collect_published_versions(package_ids)
    retired_high_water_versions = retired_package_high_water_versions_from_release_set(args.release_set_file)
    version_floor = version_floor_from_release_set(args.release_set_file)
    print(select_next_version([*published_versions, *retired_high_water_versions], args.channel, version_floor))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
