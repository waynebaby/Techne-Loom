#!/usr/bin/env python3

from pathlib import Path
import importlib.util
import json
import sys
import tempfile
import unittest


MODULE_PATH = Path(__file__).parents[1] / "ResolveSharedPackageVersion.py"
SPEC = importlib.util.spec_from_file_location("resolve_shared_package_version", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class ResolveSharedPackageVersionTests(unittest.TestCase):
    def test_development_moves_after_stable_high_water_mark(self) -> None:
        self.assertEqual(
            "0.3.309-beta",
            MODULE.select_next_version(["0.3.300-beta", "0.3.308"], "beta"),
        )

    def test_main_moves_after_beta_high_water_mark(self) -> None:
        self.assertEqual(
            "0.3.310",
            MODULE.select_next_version(["0.3.308", "0.3.309-beta"], "released"),
        )

    def test_non_release_prerelease_versions_do_not_raise_high_water_mark(self) -> None:
        self.assertEqual(
            "0.3.310-beta",
            MODULE.select_next_version(["0.3.309-rc.1", "0.3.309-beta"], "beta"),
        )

    def test_empty_published_versions_fail_closed(self) -> None:
        with self.assertRaises(ValueError):
            MODULE.select_next_version([], "beta")

    def test_invalid_published_versions_fail_closed(self) -> None:
        with self.assertRaises(ValueError):
            MODULE.select_next_version(["not-a-version", "0.3.invalid"], "released")

    def test_package_index_without_valid_release_fails_closed(self) -> None:
        def fetch(package_id: str) -> list[str]:
            return ["0.3.309-rc.1"] if package_id == "bad" else ["0.3.308"]

        with self.assertRaises(RuntimeError):
            MODULE.collect_published_versions(["good", "bad"], fetch)

    def test_release_set_expands_to_complete_package_closure(self) -> None:
        manifest = {
            "packages": {
                "core": list(MODULE.EXPECTED_CORE_PACKAGE_IDS),
                "runtime": {
                    "products": list(MODULE.EXPECTED_PRODUCTS),
                    "rids": list(MODULE.EXPECTED_RIDS),
                },
            }
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            package_ids = MODULE.package_ids_from_release_set(path)

        self.assertEqual(20, len(package_ids))
        self.assertIn("Techne.Loom.AgentOrchestrator.Runtime.win-x64", package_ids)
        self.assertIn("Techne.Loom.SkillOrchestrator.Runtime.linux-x64", package_ids)

    def test_release_set_rejects_unknown_core_package(self) -> None:
        manifest = {
            "packages": {
                "core": [
                    "Techne.Loom.Abstractions",
                    "Techne.Loom.Common",
                    "Techne.Loom.AgentOrchestrator",
                    "Unknown.Package",
                ],
                "runtime": {"products": list(MODULE.EXPECTED_PRODUCTS), "rids": list(MODULE.EXPECTED_RIDS)},
            }
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(ValueError):
                MODULE.package_ids_from_release_set(path)


if __name__ == "__main__":
    unittest.main()
