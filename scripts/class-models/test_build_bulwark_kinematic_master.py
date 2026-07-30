#!/usr/bin/env python3
"""Fast host-side tests for the deterministic Bulwark master launcher."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT_DIR = Path(__file__).resolve().parent
LAUNCHER_PATH = SCRIPT_DIR / "build_bulwark_kinematic_master.py"
BLENDER_SCRIPT_PATH = SCRIPT_DIR / "build_bulwark_kinematic_master_blender.py"


def load_launcher():
    spec = importlib.util.spec_from_file_location(
        "build_bulwark_kinematic_master",
        LAUNCHER_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load Bulwark launcher")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class BulwarkLauncherTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.launcher = load_launcher()

    def test_sources_compile_without_importing_bpy(self) -> None:
        for path in (LAUNCHER_PATH, BLENDER_SCRIPT_PATH):
            compile(path.read_text(encoding="utf-8"), str(path), "exec")

    def test_output_must_be_new_and_sandboxed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repo = Path(temporary).resolve()
            output, relative = self.launcher.resolve_output(
                repo,
                Path("sandbox/bulwark-proof"),
            )
            self.assertEqual(relative, Path("sandbox/bulwark-proof"))
            self.assertEqual(output, repo / relative)
            with self.assertRaisesRegex(ValueError, "repository-relative"):
                self.launcher.resolve_output(repo, output)
            with self.assertRaisesRegex(ValueError, "sandbox/ or var/"):
                self.launcher.resolve_output(repo, Path("art/not-scratch"))
            (repo / "sandbox").mkdir()
            output.mkdir()
            with self.assertRaisesRegex(ValueError, "refusing overwrite"):
                self.launcher.resolve_output(
                    repo,
                    Path("sandbox/bulwark-proof"),
                )

    def test_authority_ledger_rejects_hash_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repo = Path(temporary)
            authority = repo / "approved.png"
            authority.write_bytes(b"approved")
            expected = hashlib.sha256(b"approved").hexdigest()
            with mock.patch.object(
                self.launcher,
                "AUTHORITY_FILES",
                {Path("approved.png"): expected},
            ):
                self.assertEqual(
                    self.launcher.verify_authorities(repo),
                    {"approved.png": expected},
                )
                authority.write_bytes(b"drifted")
                with self.assertRaisesRegex(ValueError, "Authority hash drift"):
                    self.launcher.verify_authorities(repo)

    def test_blender_source_pins_semantic_contracts(self) -> None:
        source = BLENDER_SCRIPT_PATH.read_text(encoding="utf-8")
        for clip in (
            "PodPlant_HoverSettle",
            "Shell_Deploy",
            "Tower_Deploy",
            "Mobile_Recoil",
            "Shell_Recoil",
            "Tower_Recoil",
        ):
            self.assertIn(f'"{clip}"', source)
        self.assertIn("VANE_COUNT = 8", source)
        self.assertIn('material["nilbotsRole"] = "team-accent"', source)
        self.assertIn("export_animations=False", source)
        self.assertIn("world = evaluated.matrix_world", source)
        self.assertIn("enable_original_uv:true", source)

    def test_report_validator_requires_pass_and_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            relative = Path("sandbox/example")
            authorities = {"approved.png": "abc"}
            report = {
                "schema": self.launcher.REPORT_SCHEMA,
                "status": "PASS",
                "outputDirectory": relative.as_posix(),
                "authorities": authorities,
            }
            report_path = output / "validation-report.json"
            report_path.write_text(json.dumps(report), encoding="utf-8")
            expected = (
                "aegis-tortoise-kinematic-master.blend",
                "aegis-tortoise-neutral-animated.glb",
                "aegis-tortoise-texture-upload.glb",
                "proofs/mobile-top.png",
                "proofs/mobile-side.png",
                "proofs/mobile-gameplay.png",
                "proofs/team-accent-neutral-gameplay.png",
                "proofs/team-accent-blue-gameplay.png",
                "proofs/team-accent-orange-gameplay.png",
                "proofs/team-accent-coverage-mask.png",
                "proofs/shell-deploy-contact-sheet.png",
                "proofs/tower-deploy-contact-sheet.png",
                "proofs/podplant-contact-sheet.png",
                "proofs/tower-recoil-contact-sheet.png",
                "proofs/shell-deploy-41px-contact-sheet.png",
                "proofs/tower-deploy-41px-contact-sheet.png",
                "proofs/mobile-recoil-41px-contact-sheet.png",
                "proofs/shell-recoil-41px-contact-sheet.png",
                "proofs/tower-recoil-41px-contact-sheet.png",
                "proofs/mobile-recoil-loop.gif",
                "proofs/shell-recoil-loop.gif",
                "proofs/tower-recoil-loop.gif",
                "proofs/shell-deploy-loop.gif",
                "proofs/tower-deploy-loop.gif",
                "proofs/uv-layout.svg",
            )
            for item in expected:
                path = output / item
                path.parent.mkdir(parents=True, exist_ok=True)
                path.touch()
            validated = self.launcher.validate_report(
                report_path,
                output_relative=relative,
                authorities=authorities,
            )
            self.assertEqual(validated["status"], "PASS")
            report["status"] = "FAIL"
            report["failures"] = ["deliberate"]
            report_path.write_text(json.dumps(report), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "failed validation"):
                self.launcher.validate_report(
                    report_path,
                    output_relative=relative,
                    authorities=authorities,
                )


if __name__ == "__main__":
    unittest.main()
