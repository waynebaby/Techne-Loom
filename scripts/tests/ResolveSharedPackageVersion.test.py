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

    def test_main_promotes_newer_beta_candidate(self) -> None:
        self.assertEqual(
            "0.3.309",
            MODULE.select_next_version(["0.3.308", "0.3.309-beta"], "released"),
        )

    def test_main_advances_when_beta_candidate_is_already_stable(self) -> None:
        self.assertEqual(
            "0.3.310",
            MODULE.select_next_version(["0.3.308", "0.3.309-beta", "0.3.309"], "released"),
        )

    def test_version_floor_selects_040_beta_and_stable(self) -> None:
        published = ["0.3.337", "0.3.337-beta"]
        self.assertEqual("0.4.0-beta", MODULE.select_next_version(published, "beta", "0.4.0"))
        self.assertEqual("0.4.0", MODULE.select_next_version(published, "released", "0.4.0"))

    def test_release_promotes_040_beta_candidate(self) -> None:
        self.assertEqual(
            "0.4.0",
            MODULE.select_next_version(["0.3.337", "0.4.0-beta"], "released", "0.4.0"),
        )

    def test_next_beta_advances_after_stable_040(self) -> None:
        self.assertEqual(
            "0.4.1-beta",
            MODULE.select_next_version(["0.4.0-beta", "0.4.0"], "beta", "0.4.0"),
        )

    def test_main_promotes_next_beta_after_stable_040(self) -> None:
        self.assertEqual(
            "0.4.1",
            MODULE.select_next_version(["0.4.0", "0.4.1-beta"], "released", "0.4.0"),
        )

    def test_version_floor_does_not_regress_newer_high_water(self) -> None:
        self.assertEqual(
            "0.4.3-beta",
            MODULE.select_next_version(["0.4.2-beta"], "beta", "0.4.0"),
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

    def test_release_set_expands_to_sixteen_active_runtime_packages(self) -> None:
        manifest = {
            "packages": {
                "runtime": {
                    "products": list(MODULE.EXPECTED_PRODUCTS),
                    "rids": list(MODULE.EXPECTED_RIDS),
                },
            },
            "retired_package_high_water": {
                "package_ids": list(MODULE.EXPECTED_RETIRED_PACKAGE_IDS),
                "released": "0.3.321",
                "beta": "0.3.320-beta",
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            package_ids = MODULE.package_ids_from_release_set(path)
            high_water_versions = MODULE.retired_package_high_water_versions_from_release_set(path)

        self.assertEqual(16, len(package_ids))
        self.assertIn("Techne.Loom.AgentOrchestrator.Runtime.win-x64", package_ids)
        self.assertIn("Techne.Loom.SkillOrchestrator.Runtime.linux-x64", package_ids)
        self.assertNotIn("Techne.Loom.Common", package_ids)
        self.assertEqual(["0.3.321", "0.3.320-beta"], high_water_versions)

    def test_retired_stable_high_water_advances_beta_after_runtime_versions(self) -> None:
        manifest = {
            "retired_package_high_water": {
                "package_ids": list(MODULE.EXPECTED_RETIRED_PACKAGE_IDS),
                "released": "0.3.321",
                "beta": "0.3.320-beta",
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            high_water_versions = MODULE.retired_package_high_water_versions_from_release_set(path)

        self.assertEqual("0.3.322-beta", MODULE.select_next_version(["0.3.320-beta", *high_water_versions], "beta"))

    def test_release_set_rejects_unknown_retired_high_water_package(self) -> None:
        manifest = {
            "packages": {
                "runtime": {"products": list(MODULE.EXPECTED_PRODUCTS), "rids": list(MODULE.EXPECTED_RIDS)},
            },
            "retired_package_high_water": {
                "package_ids": ["Unknown.Package"],
                "released": "0.3.321",
                "beta": "0.3.320-beta",
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(ValueError):
                MODULE.package_ids_from_release_set(path)


    def test_version_floor_is_read_from_release_set(self) -> None:
        manifest = {"version_authority": {"release": {"version_floor": "0.4.0"}}}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            self.assertEqual("0.4.0", MODULE.version_floor_from_release_set(path))

    def test_release_set_rejects_prerelease_version_floor(self) -> None:
        manifest = {"version_authority": {"release": {"version_floor": "0.4.0-beta"}}}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "release-set.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(ValueError):
                MODULE.version_floor_from_release_set(path)

if __name__ == "__main__":
    unittest.main()
