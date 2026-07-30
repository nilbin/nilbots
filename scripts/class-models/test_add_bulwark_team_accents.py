#!/usr/bin/env python3
"""Offline unit tests for the Bulwark team-accent process wrapper."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("add_bulwark_team_accents.py")
SPEC = importlib.util.spec_from_file_location("add_bulwark_team_accents", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Could not load add_bulwark_team_accents.py")
ACCENTS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ACCENTS)


class TeamAccentWrapperTests(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = ACCENTS.repository_root()

    def test_sha256_argument_requires_exact_lowercase_hex(self) -> None:
        valid = "a" * 64
        self.assertEqual(ACCENTS.sha256_argument(valid), valid)
        with self.assertRaises(Exception):
            ACCENTS.sha256_argument("abc")
        with self.assertRaises(Exception):
            ACCENTS.sha256_argument("g" * 64)

    def test_public_paths_reject_absolute_and_repository_escape(self) -> None:
        with self.assertRaisesRegex(ValueError, "repository-relative"):
            ACCENTS.resolve_repo_path(
                Path("/tmp/source.blend"),
                repo_root=self.repo,
                option="--input-blend",
                strict=False,
            )
        with self.assertRaisesRegex(ValueError, "inside the repository"):
            ACCENTS.resolve_repo_path(
                Path("../source.blend"),
                repo_root=self.repo,
                option="--input-blend",
                strict=False,
            )

    def test_output_is_limited_to_new_sandbox_or_var_directory(self) -> None:
        with self.assertRaisesRegex(ValueError, "sandbox/ or var"):
            ACCENTS.resolve_output(
                Path("art/not-a-scratch-output"),
                repo_root=self.repo,
            )
        _resolved, relative = ACCENTS.resolve_output(
            Path("var/class-models/test-output-that-does-not-exist"),
            repo_root=self.repo,
        )
        self.assertEqual(
            relative,
            Path("var/class-models/test-output-that-does-not-exist"),
        )

    def test_report_artifact_verification_is_hash_pinned_and_relative(self) -> None:
        var_dir = self.repo / "var"
        var_dir.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="bulwark-accent-wrapper-test-",
            dir=var_dir,
        ) as temporary:
            output = Path(temporary)
            artifact = output / "model.glb"
            artifact.write_bytes(b"test-glb")
            relative_output = output.relative_to(self.repo)
            source_relative = Path("sandbox/source.blend")
            source_sha = "1" * 64
            report = {
                "source": {
                    "path": source_relative.as_posix(),
                    "sha256": source_sha,
                },
                "outputs": {
                    "glb": {
                        "path": artifact.relative_to(self.repo).as_posix(),
                        "sha256": hashlib.sha256(b"test-glb").hexdigest(),
                    }
                },
            }
            ACCENTS.verify_artifacts(
                report,
                repo_root=self.repo,
                expected_source_relative=source_relative,
                expected_source_sha256=source_sha,
                expected_output_relative=relative_output,
            )
            report["outputs"]["glb"]["sha256"] = "0" * 64
            with self.assertRaisesRegex(ValueError, "hash drifted"):
                ACCENTS.verify_artifacts(
                    report,
                    repo_root=self.repo,
                    expected_source_relative=source_relative,
                    expected_source_sha256=source_sha,
                    expected_output_relative=relative_output,
                )

    def test_report_schema_and_pass_are_required(self) -> None:
        var_dir = self.repo / "var"
        var_dir.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="bulwark-accent-report-test-",
            dir=var_dir,
        ) as temporary:
            path = Path(temporary) / "report.json"
            path.write_text(
                json.dumps(
                    {
                        "schema": ACCENTS.EXPECTED_REPORT_SCHEMA,
                        "passed": True,
                    }
                ),
                encoding="utf-8",
            )
            self.assertTrue(ACCENTS.load_report(path)["passed"])
            path.write_text(
                json.dumps(
                    {
                        "schema": ACCENTS.EXPECTED_REPORT_SCHEMA,
                        "passed": False,
                    }
                ),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "did not pass"):
                ACCENTS.load_report(path)


if __name__ == "__main__":
    unittest.main()
