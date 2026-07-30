#!/usr/bin/env python3
"""Offline unit tests for the Bulwark runtime-package wrapper."""

from __future__ import annotations

import hashlib
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).with_name("package_bulwark_runtime.py")
SPEC = importlib.util.spec_from_file_location("package_bulwark_runtime", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Could not load package_bulwark_runtime.py")
PACKAGER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGER)


class RuntimePackagerWrapperTests(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = PACKAGER.repository_root()
        (self.repo / "var").mkdir(exist_ok=True)

    def test_sha256_argument_requires_exact_lowercase_hex(self) -> None:
        valid = "a" * 64
        self.assertEqual(PACKAGER.sha256_argument(valid), valid)
        with self.assertRaises(Exception):
            PACKAGER.sha256_argument("abc")
        with self.assertRaises(Exception):
            PACKAGER.sha256_argument("g" * 64)

    def test_paths_are_confined_to_repository_and_scratch_roots(self) -> None:
        with self.assertRaisesRegex(ValueError, "repository-relative"):
            PACKAGER.resolve_repo_path(
                Path("/tmp/source.blend"),
                repo_root=self.repo,
                option="--input-blend",
                strict=False,
            )
        with self.assertRaisesRegex(ValueError, "inside the repository"):
            PACKAGER.resolve_repo_path(
                Path("../source.blend"),
                repo_root=self.repo,
                option="--input-blend",
                strict=False,
            )
        with self.assertRaisesRegex(ValueError, "sandbox/ or var"):
            PACKAGER.resolve_output(
                Path("art/not-a-scratch-output"),
                repo_root=self.repo,
            )

    def test_source_hash_mismatch_fails_before_blender_or_output_creation(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="bulwark-runtime-hash-test-",
            dir=self.repo / "var",
        ) as temporary:
            root = Path(temporary)
            source = root / "source.blend"
            source.write_bytes(b"not-a-real-blend")
            output = root / "fresh-output"
            with mock.patch.object(PACKAGER, "resolve_blender") as resolver:
                with self.assertRaisesRegex(
                    ValueError,
                    "Source hash mismatch before Blender launch",
                ):
                    PACKAGER.main(
                        [
                            "--input-blend",
                            source.relative_to(self.repo).as_posix(),
                            "--expected-source-sha256",
                            "0" * 64,
                            "--output-dir",
                            output.relative_to(self.repo).as_posix(),
                        ]
                    )
            resolver.assert_not_called()
            self.assertFalse(output.exists())

    def test_existing_output_fails_before_blender_invocation(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="bulwark-runtime-overwrite-test-",
            dir=self.repo / "var",
        ) as temporary:
            root = Path(temporary)
            source = root / "source.blend"
            source.write_bytes(b"not-a-real-blend")
            output = root / "existing-output"
            output.mkdir()
            source_sha = hashlib.sha256(source.read_bytes()).hexdigest()
            with mock.patch.object(PACKAGER, "resolve_blender") as resolver:
                with self.assertRaisesRegex(ValueError, "refusing overwrite"):
                    PACKAGER.main(
                        [
                            "--input-blend",
                            source.relative_to(self.repo).as_posix(),
                            "--expected-source-sha256",
                            source_sha,
                            "--output-dir",
                            output.relative_to(self.repo).as_posix(),
                        ]
                    )
            resolver.assert_not_called()
            self.assertTrue(output.is_dir())

    def test_blender_command_is_exact_and_has_no_side_effect(self) -> None:
        blender = Path("/opt/pinned/Blender")
        source = Path("sandbox/source/master.blend")
        output = Path("var/class-models/runtime-proof")
        source_sha = "1" * 64
        command = PACKAGER.blender_command(
            blender=blender,
            source_relative=source,
            source_sha256=source_sha,
            output_relative=output,
        )
        self.assertEqual(
            command,
            [
                "/opt/pinned/Blender",
                "--background",
                "--factory-startup",
                "--python",
                "scripts/class-models/package_bulwark_runtime_blender.py",
                "--",
                "--input-blend",
                "sandbox/source/master.blend",
                "--expected-source-sha256",
                source_sha,
                "--output-dir",
                "var/class-models/runtime-proof",
            ],
        )


if __name__ == "__main__":
    unittest.main()
