#!/usr/bin/env python3
"""Focused offline tests for the guarded Meshy Smart Topology runner."""

from __future__ import annotations

from contextlib import redirect_stderr, redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).with_name("meshy_smart_topology.py")
SPEC = importlib.util.spec_from_file_location("meshy_smart_topology", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot import {SCRIPT}")
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
REPO_ROOT = SCRIPT.resolve().parents[2]


class MeshySmartTopologyTests(unittest.TestCase):
    def load_real_plan(
        self,
        trial: str = "oblique",
    ) -> tuple[object, Path, dict[str, object]]:
        approved = MODULE.validate_approved_input(trial, None, REPO_ROOT)
        ledger_path, ledger = MODULE.load_experiment_ledger(REPO_ROOT)
        return approved, ledger_path, ledger

    def load_gated_ledger(self) -> tuple[Path, dict[str, object]]:
        _, ledger_path, ledger = self.load_real_plan()
        gated = json.loads(json.dumps(ledger))
        top_spec = MODULE.TRIAL_SPECS["top"]
        gated["calls"] = [
            call
            for call in gated["calls"]
            if call.get("trial") != "top"
            and call.get("input", {}).get("path") != top_spec["path"].as_posix()
        ]
        gated["budget"].update(
            {
                "callsSubmitted": 1,
                "creditsCommitted": 15,
                "creditsConsumed": 15,
            }
        )
        gated["trials"]["top"]["status"] = "approved-input-gated"
        gated["firstGate"]["expansionAuthorized"] = False
        gated["firstGate"]["status"] = "reviewed-b2-gated"
        return ledger_path, gated

    def test_default_mode_is_offline_and_does_not_build_data_uri(self) -> None:
        output = io.StringIO()
        ledger_path, gated = self.load_gated_ledger()
        with (
            patch.object(MODULE, "read_keychain_token") as credential,
            patch.object(MODULE, "build_request_payload") as payload,
            patch.object(MODULE, "MeshyClient") as client,
            patch.object(
                MODULE,
                "load_experiment_ledger",
                return_value=(ledger_path, gated),
            ),
            redirect_stdout(output),
        ):
            self.assertEqual(MODULE.main(["--trial", "top"]), 0)
        credential.assert_not_called()
        payload.assert_not_called()
        client.assert_not_called()
        self.assertIn("OFFLINE DRY RUN", output.getvalue())
        self.assertIn("Trial: top (B2)", output.getvalue())
        self.assertIn("1/3 calls", output.getvalue())
        self.assertIn("Submission gate: BLOCKED", output.getvalue())

    def test_paid_mode_requires_both_explicit_flags_and_exact_ceiling(self) -> None:
        for argv in (
            ["--submit"],
            ["--max-credits", "15"],
            ["--trial", "top", "--submit"],
            ["--trial", "top", "--max-credits", "15"],
        ):
            with self.subTest(argv=argv), redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit):
                    MODULE.parse_args(argv)
        parsed = MODULE.parse_args(
            ["--trial", "top", "--submit", "--max-credits", "15"]
        )
        self.assertTrue(parsed.submit)
        self.assertEqual(parsed.trial, "top")
        self.assertEqual(parsed.max_credits, 15)
        MODULE.enforce_credit_guard(15)
        for value in (None, 0, 14, 16, 45, True):
            with self.subTest(value=value):
                with self.assertRaises(MODULE.RunnerError):
                    MODULE.enforce_credit_guard(value)

    def test_trial_selects_only_its_exact_owner_approved_path_and_hash(self) -> None:
        for trial, spec in MODULE.TRIAL_SPECS.items():
            with self.subTest(trial=trial):
                approved = MODULE.validate_approved_input(trial, None, REPO_ROOT)
                self.assertEqual(approved.trial, trial)
                self.assertEqual(approved.candidate, spec["candidate"])
                self.assertEqual(approved.role, spec["role"])
                self.assertEqual(approved.recorded_path, spec["path"].as_posix())
                self.assertEqual(approved.sha256, spec["sha256"])

        oblique_path = REPO_ROOT / MODULE.TRIAL_SPECS["oblique"]["path"]
        top_path = REPO_ROOT / MODULE.TRIAL_SPECS["top"]["path"]
        with self.assertRaisesRegex(MODULE.RunnerError, "only the owner-approved"):
            MODULE.validate_approved_input("oblique", top_path, REPO_ROOT)
        with self.assertRaisesRegex(MODULE.RunnerError, "only the owner-approved"):
            MODULE.validate_approved_input("top", oblique_path, REPO_ROOT)

    def test_hash_drift_blocks_the_exact_path(self) -> None:
        for trial, spec in MODULE.TRIAL_SPECS.items():
            with self.subTest(trial=trial), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                candidate = root / spec["path"]
                candidate.parent.mkdir(parents=True)
                candidate.write_bytes(
                    MODULE.PNG_SIGNATURE
                    + b"\x00\x00\x00\rIHDR"
                    + struct.pack(
                        ">II",
                        MODULE.APPROVED_INPUT_WIDTH,
                        MODULE.APPROVED_INPUT_HEIGHT,
                    )
                )
                with self.assertRaisesRegex(MODULE.RunnerError, "hash drifted"):
                    MODULE.validate_approved_input(trial, None, root)

    def test_request_is_fixed_t2_and_omits_unsupported_fields(self) -> None:
        payloads = {}
        for trial in MODULE.TRIAL_SPECS:
            approved, _, _ = self.load_real_plan(trial)
            payload = MODULE.build_request_payload(approved)
            payloads[trial] = payload
            self.assertEqual(
                set(payload),
                {
                    "model_type",
                    "ai_model",
                    "target_polycount",
                    "should_texture",
                    "enable_pbr",
                    "texture_resolution",
                    "texture_prompt",
                    "target_formats",
                    "image_url",
                },
            )
            self.assertEqual(payload["model_type"], "smart-topology")
            self.assertEqual(payload["ai_model"], "meshy-t2")
            self.assertEqual(payload["target_polycount"], 15000)
            self.assertTrue(payload["should_texture"])
            self.assertTrue(payload["enable_pbr"])
            self.assertEqual(payload["texture_resolution"], "4k")
            self.assertEqual(payload["target_formats"], ["glb"])
            self.assertTrue(payload["image_url"].startswith("data:image/png;base64,"))
            self.assertNotIn("image_enhancement", payload)
            self.assertNotIn("remove_lighting", payload)
        self.assertNotEqual(
            payloads["oblique"]["image_url"],
            payloads["top"]["image_url"],
        )

        manifest = json.loads(
            (
                REPO_ROOT
                / "art/class-models/provider-inputs/meshy/"
                "aegis-tortoise-turret/manifest.json"
            ).read_text(encoding="utf-8")
        )
        self.assertEqual(
            payloads["top"]["texture_prompt"],
            manifest["providerSubmission"]["texturePrompt"],
        )

    def test_b2_requires_recorded_verdict_and_explicit_expansion(self) -> None:
        _, ledger = self.load_gated_ledger()
        with self.assertRaisesRegex(MODULE.RunnerError, "remains gated"):
            MODULE.enforce_experiment_gate(ledger, "top", 15)
        ledger["firstGate"]["expansionAuthorized"] = True
        verdict = ledger["firstGate"]["reviewVerdict"]
        ledger["firstGate"]["reviewVerdict"] = None
        with self.assertRaisesRegex(MODULE.RunnerError, "reviewVerdict"):
            MODULE.enforce_experiment_gate(ledger, "top", 15)
        ledger["firstGate"]["reviewVerdict"] = verdict
        MODULE.enforce_experiment_gate(ledger, "top", 15)

    def test_gated_b2_submit_stops_before_credential_or_network(self) -> None:
        ledger_path, gated = self.load_gated_ledger()
        with (
            patch.object(
                MODULE,
                "load_experiment_ledger",
                return_value=(ledger_path, gated),
            ),
            patch.object(MODULE, "read_keychain_token") as credential,
            patch.object(MODULE, "MeshyClient") as client,
            self.assertRaisesRegex(MODULE.RunnerError, "remains gated"),
        ):
            MODULE.main(
                ["--trial", "top", "--submit", "--max-credits", "15"]
            )
        credential.assert_not_called()
        client.assert_not_called()

    def test_duplicate_trials_are_rejected(self) -> None:
        _, _, ledger = self.load_real_plan()
        with self.assertRaisesRegex(MODULE.RunnerError, "already has"):
            MODULE.enforce_experiment_gate(ledger, "oblique", 15)

        ledger["firstGate"]["expansionAuthorized"] = True
        top = MODULE.TRIAL_SPECS["top"]
        ledger["calls"].append(
            {
                "callIndex": 2,
                "candidate": "B2",
                "trial": "top",
                "taskId": "top-already-paid",
                "status": "SUCCEEDED",
                "balanceBefore": 725,
                "balanceAfter": 710,
                "expectedCredits": 15,
                "maxCredits": 15,
                "consumedCredits": 15,
                "input": {
                    "path": top["path"].as_posix(),
                    "sha256": top["sha256"],
                },
            }
        )
        ledger["budget"].update(
            {
                "callsSubmitted": 2,
                "creditsCommitted": 30,
                "creditsConsumed": 30,
            }
        )
        with self.assertRaisesRegex(MODULE.RunnerError, "already has"):
            MODULE.enforce_experiment_gate(ledger, "top", 15)

    def test_ledger_rejects_non_numeric_balance_or_credit_values(self) -> None:
        _, _, ledger = self.load_real_plan()
        ledger["calls"][0]["balanceBefore"] = "740"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            ledger_path = root / MODULE.LEDGER_RELATIVE
            ledger_path.parent.mkdir(parents=True)
            ledger_path.write_text(json.dumps(ledger), encoding="utf-8")
            with self.assertRaisesRegex(MODULE.RunnerError, "must be numeric"):
                MODULE.load_experiment_ledger(root)

    def test_sanitizer_removes_credentials_data_uris_and_signed_queries(self) -> None:
        value = {
            "Authorization": "Bearer never-record-this",
            "api_key": "never-record-this",
            "nested": {
                "credential": "never-record-this",
                "message": "bad data:image/png;base64,never-record-this",
            },
            "model_urls": {
                "glb": (
                    "https://assets.meshy.ai/task/model.glb"
                    "?Expires=1&Signature=never-record-this#fragment"
                )
            },
        }
        sanitized = MODULE.sanitize_for_record(value)
        encoded = json.dumps(sanitized)
        self.assertNotIn("Authorization", sanitized)
        self.assertNotIn("api_key", sanitized)
        self.assertNotIn("credential", sanitized["nested"])
        self.assertNotIn("never-record-this", encoded)
        self.assertEqual(
            sanitized["nested"]["message"],
            "<redacted-data-uri>",
        )
        self.assertEqual(
            sanitized["model_urls"]["glb"],
            "https://assets.meshy.ai/task/model.glb",
        )

    def test_balance_parser_is_numeric_only(self) -> None:
        class NumericClient(MODULE.MeshyClient):
            def request_json(self, method, path, payload=None):
                del method, path, payload
                return {"balance": 45}

        self.assertEqual(NumericClient("not-a-real-token").balance(), 45)

        class StringClient(MODULE.MeshyClient):
            def request_json(self, method, path, payload=None):
                del method, path, payload
                return {"balance": "45"}

        with self.assertRaisesRegex(MODULE.RunnerError, "must be numeric"):
            StringClient("not-a-real-token").balance()

    def test_artifact_plan_tracks_previews_and_isolates_raw_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            run = root / "art/run"
            raw = root / "var/raw"
            task = {
                "model_urls": {
                    "glb": (
                        "https://assets.meshy.ai/task/model.glb"
                        "?Signature=one"
                    )
                },
                "thumbnail_url": (
                    "https://assets.meshy.ai/task/preview.png?Signature=one"
                ),
                "thumbnail_urls": {
                    "front": (
                        "https://assets.meshy.ai/task/preview.png?Signature=two"
                    ),
                    "right": (
                        "https://assets.meshy.ai/task/right.png?Signature=one"
                    ),
                },
                "texture_urls": [
                    {
                        "base_color": (
                            "https://assets.meshy.ai/task/base.png?Signature=one"
                        ),
                        "normal": (
                            "https://assets.meshy.ai/task/normal.png?Signature=one"
                        ),
                    }
                ],
            }
            specs = MODULE.collect_artifact_specs(
                task,
                run_directory=run,
                raw_directory=raw,
            )
        self.assertEqual(
            [(spec.destination.name, spec.storage) for spec in specs],
            [
                ("raw-model.glb", "ignored-raw"),
                ("provider-preview.png", "tracked-preview"),
                ("provider-preview-right.png", "tracked-preview"),
                ("texture-0-base-color.png", "ignored-pbr-map"),
                ("texture-0-normal.png", "ignored-pbr-map"),
            ],
        )
        self.assertTrue(specs[0].destination.is_relative_to(raw))
        self.assertTrue(specs[1].destination.is_relative_to(run))
        self.assertTrue(specs[-1].destination.is_relative_to(raw))

    def test_artifact_urls_reject_userinfo_and_non_meshy_origins(self) -> None:
        for value in (
            "https://user:secret@assets.meshy.ai/task/model.glb",
            "https://example.com/task/model.glb",
            "http://assets.meshy.ai/task/model.glb",
        ):
            with self.subTest(value=value):
                with self.assertRaises(MODULE.RunnerError):
                    MODULE.validate_asset_url(value)

    def test_resume_raw_directory_cannot_escape_ignored_var_root(self) -> None:
        with self.assertRaisesRegex(MODULE.RunnerError, "escapes"):
            MODULE.resolve_recorded_raw_directory(
                "art/class-models/topology-experiments/raw",
                REPO_ROOT,
            )
        resolved = MODULE.resolve_recorded_raw_directory(
            (
                MODULE.RAW_ROOT_RELATIVE
                / "safe-task"
            ).as_posix(),
            REPO_ROOT,
        )
        self.assertTrue(
            resolved.is_relative_to((REPO_ROOT / MODULE.RAW_ROOT_RELATIVE).resolve())
        )

    def test_original_b1_record_remains_resume_compatible(self) -> None:
        record_path = (
            REPO_ROOT
            / MODULE.RUNS_RELATIVE
            / "019fb2f9-5ef2-7641-a4e6-0f59134f5c31"
            / "request-record.json"
        )
        record = json.loads(record_path.read_text(encoding="utf-8"))
        parsed = MODULE.parse_args(["--resume-run", str(record_path.parent)])
        self.assertEqual(parsed.resume_run, record_path.parent)
        self.assertIsNone(parsed.trial)
        self.assertNotIn("trial", record)
        self.assertNotIn("candidate", record)
        task_id, raw_directory, trial = MODULE.validate_resume_record(
            record,
            REPO_ROOT,
        )
        self.assertEqual(
            task_id,
            "019fb2f9-5ef2-7641-a4e6-0f59134f5c31",
        )
        self.assertEqual(trial, "oblique")
        self.assertEqual(
            raw_directory,
            (
                REPO_ROOT
                / MODULE.RAW_ROOT_RELATIVE
                / "019fb2f9-5ef2-7641-a4e6-0f59134f5c31"
            ).resolve(),
        )

    def test_request_record_contains_no_data_uri_or_signed_url(self) -> None:
        approved, ledger_path, _ = self.load_real_plan("top")
        record = MODULE.initial_request_record(
            approved,
            task_id="offline-test",
            submitted_at="2026-07-30T00:00:00Z",
            balance_before=45,
            raw_directory=REPO_ROOT / MODULE.RAW_ROOT_RELATIVE / "offline-test",
            ledger_sha256=MODULE.sha256_file(ledger_path),
        )
        encoded = json.dumps(record)
        self.assertNotIn("data:image", encoded)
        self.assertNotIn("Authorization", encoded)
        self.assertNotIn("image_enhancement", record["settings"])
        self.assertNotIn("remove_lighting", record["settings"])
        self.assertEqual(record["trial"], "top")
        self.assertEqual(record["candidate"], "B2")
        self.assertEqual(record["inputs"][0]["role"], "top")
        self.assertEqual(
            record["inputs"][0]["sha256"],
            MODULE.TRIAL_SPECS["top"]["sha256"],
        )
        self.assertIsInstance(record["balanceBefore"], int)
        self.assertIsInstance(record["expectedCredits"], int)
        self.assertIsInstance(record["maxCredits"], int)


if __name__ == "__main__":
    unittest.main()
