#!/usr/bin/env python3
"""Tests for the non-ranked Frontline Labs cohort driver."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "scripts" / "labs-cohort-drive.py"
SCHEMA = ROOT / "arena-bots" / "frontline-labs" / "cohort.schema.json"
EXAMPLE = ROOT / "arena-bots" / "frontline-labs" / "cohort.example.json"
V3 = (
    ROOT
    / "tests"
    / "BotArena.Engine.Tests"
    / "Fixtures"
    / "generic-frontline-replay-v3.json"
)
SPEC = importlib.util.spec_from_file_location("labs_cohort_drive", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load {SCRIPT}")
DRIVER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DRIVER)


class LabsCohortDriveTests(unittest.TestCase):
    @staticmethod
    def playlist() -> dict[str, object]:
        return {
            **DRIVER.EXPECTED_PLAYLIST,
            **{
                key: "0" * 64
                for key in DRIVER.FINGERPRINT_FIELDS
            },
        }

    def test_four_entrants_three_seeds_make_mirrored_36_game_plan(
        self,
    ) -> None:
        entrants = [{"id": value} for value in ("a", "b", "c", "d")]
        plan = DRIVER.build_plan(entrants, DRIVER.DEFAULT_SEEDS)

        self.assertEqual(36, len(plan))
        assignments = {
            (
                row["bot"],
                row["opponent"],
                row["seed"],
            )
            for row in plan
        }
        self.assertIn(("a", "b", 104729), assignments)
        self.assertIn(("b", "a", 104729), assignments)
        self.assertEqual(len(assignments), len(plan))

    def test_manifest_freezes_every_source_dx_and_artifact(self) -> None:
        head = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            entrants = []
            for entrant_id in (
                "pressure",
                "fabricator",
                "bastion",
                "adapter",
            ):
                entrant_root = root / entrant_id
                entrant_root.mkdir()
                (entrant_root / "Bot.cs").write_text(
                    f"// {entrant_id}\n",
                    encoding="utf-8",
                )
                (entrant_root / "DX.md").write_text(
                    "Frozen DX\n",
                    encoding="utf-8",
                )
                artifact = entrant_root / "built.wasm"
                artifact.write_bytes(entrant_id.encode("utf-8"))
                (entrant_root / "smoke").mkdir()
                (entrant_root / "smoke" / "viewer.html").write_text(
                    "generated smoke output\n",
                    encoding="utf-8",
                )
                (entrant_root / "evidence").mkdir()
                (entrant_root / "evidence" / "replay.json").write_text(
                    "generated evidence\n",
                    encoding="utf-8",
                )
                source_revision = DRIVER._source_tree_sha256(entrant_root)
                entrants.append(
                    {
                        "id": entrant_id,
                        "name": entrant_id.title(),
                        "doctrine": entrant_id,
                        "root": entrant_id,
                        "artifact": f"{entrant_id}/built.wasm",
                        "artifactSha256": hashlib.sha256(
                            entrant_id.encode("utf-8")
                        ).hexdigest(),
                        "sourceRevision": source_revision,
                        "sourceTreeSha256": source_revision,
                        "dxReport": f"{entrant_id}/DX.md",
                    }
                )
            manifest_path = root / "cohort.json"
            manifest_path.write_text(
                json.dumps(
                    {
                        "schemaVersion": 1,
                        "cohortId": "test-cohort",
                        "engineCommit": head,
                        "playlist": self.playlist(),
                        "seeds": [7],
                        "entrants": entrants,
                    }
                ),
                encoding="utf-8",
            )
            manifest = DRIVER.load_manifest(manifest_path, ROOT)
            plan = DRIVER.build_plan(
                manifest["entrants"],
                manifest["seeds"],
            )
            output = root / "evidence"
            artifacts = DRIVER.freeze_run(
                manifest_path,
                manifest,
                output,
                DRIVER.DEFAULT_RUNNER,
                DRIVER.DEFAULT_VERIFIER,
                plan,
            )

            self.assertEqual(12, len(plan))
            self.assertEqual(b"pressure", artifacts["pressure"].read_bytes())
            self.assertTrue(
                (output / "entrants" / "bastion" / "Bot.cs").is_file()
            )
            self.assertTrue(
                (output / "entrants" / "bastion" / "DX.md").is_file()
            )
            self.assertFalse(
                (output / "entrants" / "bastion" / "smoke").exists()
            )
            self.assertFalse(
                (output / "entrants" / "bastion" / "evidence").exists()
            )
            self.assertTrue((output / "run.json").is_file())
            run = json.loads(
                (output / "run.json").read_text(encoding="utf-8")
            )
            self.assertEqual(
                head,
                run["repositorySource"]["headCommit"],
            )
            self.assertIn(
                "worktreeDirty",
                run["repositorySource"],
            )

    def test_source_tree_mutation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            entrant_root = root / "pressure"
            entrant_root.mkdir()
            source = entrant_root / "Bot.cs"
            source.write_text("// frozen\n", encoding="utf-8")
            revision = DRIVER._source_tree_sha256(entrant_root)
            (entrant_root / "smoke").mkdir()
            (entrant_root / "smoke" / "replay.json").write_text(
                "generated\n",
                encoding="utf-8",
            )
            self.assertEqual(
                revision,
                DRIVER._source_tree_sha256(entrant_root),
            )
            source.write_text("// changed\n", encoding="utf-8")

            self.assertNotEqual(
                revision,
                DRIVER._source_tree_sha256(entrant_root),
            )

    def test_dirty_repository_identity_never_claims_clean_head_only(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            script = root / "scripts" / "labs-cohort-drive.py"
            script.parent.mkdir()
            shutil.copy2(SCRIPT, script)
            subprocess.run(["git", "init", "-q"], cwd=root, check=True)
            subprocess.run(
                ["git", "add", "."],
                cwd=root,
                check=True,
            )
            subprocess.run(
                [
                    "git",
                    "-c",
                    "user.name=Test",
                    "-c",
                    "user.email=test@example.invalid",
                    "commit",
                    "-qm",
                    "fixture",
                ],
                cwd=root,
                check=True,
            )
            clean = DRIVER._repository_source_identity(root)
            script.write_text(
                script.read_text(encoding="utf-8") + "\n# dirty\n",
                encoding="utf-8",
            )
            dirty = DRIVER._repository_source_identity(root)

        self.assertFalse(clean["worktreeDirty"])
        self.assertTrue(dirty["worktreeDirty"])
        self.assertEqual(clean["headCommit"], dirty["headCommit"])
        self.assertNotEqual(
            clean["sourceTreeSha256"],
            dirty["sourceTreeSha256"],
        )

    def test_exact_playlist_and_replay_header_identity_are_enforced(
        self,
    ) -> None:
        playlist = self.playlist()
        self.assertEqual(playlist, DRIVER._validate_playlist(playlist))
        wrong = {**playlist, "mapVersion": 2}
        with self.assertRaisesRegex(ValueError, "mapVersion"):
            DRIVER._validate_playlist(wrong)

        document = json.loads(V3.read_text(encoding="utf-8"))
        header = document["header"]
        contract = header["contract"]
        header["gameRulesVersion"] = playlist["rulesetId"]
        header["seed"] = "7"
        contract["rules"]["rulesetId"] = playlist["rulesetId"]
        contract["rules"]["rulesFingerprint"] = playlist["rulesFingerprint"]
        contract["map"]["mapId"] = playlist["mapId"]
        contract["map"]["mapVersion"] = playlist["mapVersion"]
        contract["map"]["mapFingerprint"] = playlist["mapFingerprint"]
        contract["format"]["formatId"] = playlist["formatId"]
        contract["format"]["formatFingerprint"] = playlist[
            "formatFingerprint"
        ]
        contract["matchContractFingerprint"] = playlist[
            "matchContractFingerprint"
        ]
        header["runtime"]["contractProfileId"] = playlist[
            "contractProfileId"
        ]
        contract["capabilityVersions"]["contractProfileId"] = playlist[
            "contractProfileId"
        ]
        artifacts = {
            "a": header["provenance"]["participants"][0]["artifactHash"],
            "b": header["provenance"]["participants"][1]["artifactHash"],
        }
        for participant in header["provenance"]["participants"]:
            participant["runtimeKind"] = "wasm"
        item = {
            "seed": 7,
            "teamAssignments": {"0": "a", "1": "b"},
        }

        self.assertEqual(
            [],
            DRIVER.replay_identity_issues(
                document,
                item,
                playlist,
                artifacts,
            ),
        )
        contract["map"]["mapFingerprint"] = "f" * 64
        self.assertIn(
            "mapFingerprint",
            DRIVER.replay_identity_issues(
                document,
                item,
                playlist,
                artifacts,
            ),
        )

    def test_schema_and_example_register_the_exact_contract(self) -> None:
        schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
        example = json.loads(EXAMPLE.read_text(encoding="utf-8"))
        playlist_properties = schema["properties"]["playlist"]["properties"]

        for key, value in DRIVER.EXPECTED_PLAYLIST.items():
            self.assertEqual(value, playlist_properties[key]["const"])
            self.assertEqual(value, example["playlist"][key])
        for key in DRIVER.FINGERPRINT_FIELDS:
            self.assertIn(
                key,
                schema["properties"]["playlist"]["required"],
            )
            self.assertRegex(example["playlist"][key], r"^[0-9a-f]{64}$")
        source_pattern = (
            schema["properties"]["entrants"]["items"]["properties"]
            ["sourceRevision"]["pattern"]
        )
        for entrant in example["entrants"]:
            self.assertRegex(entrant["sourceRevision"], source_pattern)
            self.assertRegex(entrant["sourceTreeSha256"], source_pattern)

    def test_resume_reverifies_and_rejects_changed_replay_bytes(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            attempt = root / "matches" / "one" / "attempt-01"
            attempt.mkdir(parents=True)
            replay = attempt / "replay.json"
            replay.write_text('{"fixture":1}\n', encoding="utf-8")
            (attempt / "verification.json").write_text(
                json.dumps(
                    {
                        "verified": True,
                        "exitCode": 0,
                        "replaySha256": DRIVER._sha256(replay),
                    }
                ),
                encoding="utf-8",
            )
            verifier = root / "verify.py"
            verifier.write_text(
                "import pathlib, sys\n"
                "replay = pathlib.Path(sys.argv[1])\n"
                "counter = replay.parent / 'verify-count.txt'\n"
                "count = int(counter.read_text()) if counter.exists() else 0\n"
                "counter.write_text(str(count + 1))\n",
                encoding="utf-8",
            )
            template = f"{sys.executable} {verifier} {{replay}}"
            values = {
                "bot": root / "a.wasm",
                "opponent": root / "b.wasm",
                "seed": 7,
                "out": attempt,
                "replay": replay,
            }

            accepted = DRIVER._latest_verified_attempt(
                attempt.parent,
                template,
                values,
                root,
                reverify=True,
            )
            self.assertEqual(attempt, accepted)
            self.assertEqual(
                "1",
                (attempt / "verify-count.txt").read_text(encoding="utf-8"),
            )
            replay.write_text('{"fixture":2}\n', encoding="utf-8")
            rejected = DRIVER._latest_verified_attempt(
                attempt.parent,
                template,
                values,
                root,
                reverify=True,
            )

            self.assertIsNone(rejected)
            self.assertEqual(
                "2",
                (attempt / "verify-count.txt").read_text(encoding="utf-8"),
            )
            second_audit = json.loads(
                (attempt / "resume-verification-02.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertFalse(second_audit["replayUnchanged"])


if __name__ == "__main__":
    unittest.main()
