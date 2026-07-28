#!/usr/bin/env python3
"""Run and archive a deterministic, non-ranked Frontline Labs cohort matrix.

The driver freezes every entrant into the output directory, executes every
unordered pairing in both participant assignments for every configured seed,
verifies each replay through `nilbots verify`, and writes W/D/L/points tables.
It deliberately has no Elo, ladder, champion, or automatic-crowning behavior.
"""

from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import re
import shlex
import shutil
import subprocess
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


DEFAULT_SEEDS = [104729, 130363, 155921]
DEFAULT_RUNNER = (
    "dotnet run --project src/BotArena.Cli -- "
    "experiment frontline-labs --bot {bot} --opponent {opponent} "
    "--seed {seed} --runtime wasm --out {out}"
)
DEFAULT_VERIFIER = (
    "dotnet run --project src/BotArena.Cli -- verify {replay}"
)
EXPECTED_PLAYLIST = {
    "id": "frontline-labs",
    "version": 1,
    "rulesetId": "frontline-labs-1",
    "mapId": "frontline-labs-01",
    "mapVersion": 1,
    "formatId": "head-to-head",
    "contractProfileId": "generic-actor-match-2",
}
EXPECTED_DOCTRINES = {"pressure", "fabricator", "bastion", "adapter"}
FINGERPRINT_FIELDS = (
    "rulesFingerprint",
    "mapFingerprint",
    "formatFingerprint",
    "matchContractFingerprint",
)
SOURCE_IDENTITY_PATHS = (
    "BotArena.sln",
    "Directory.Build.props",
    "global.json",
    "nuget.config",
    "scripts/labs-cohort-drive.py",
    "src",
)
SOURCE_EXCLUDED_DIRS = {
    ".git",
    "__pycache__",
    "bin",
    "obj",
    "out",
}


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _slug(value: str) -> str:
    normalized = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    if not normalized:
        raise ValueError(f"invalid empty entrant id from {value!r}")
    return normalized


def _command(template: str, values: dict[str, Any]) -> list[str]:
    try:
        return [
            token.format(**{key: str(value) for key, value in values.items()})
            for token in shlex.split(template)
        ]
    except KeyError as error:
        raise ValueError(
            f"unknown command-template placeholder {error}"
        ) from error


def _git_head(root: Path) -> str:
    completed = subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
    )
    return completed.stdout.strip()


def _source_files(root: Path) -> list[Path]:
    return sorted(
        (
            path
            for path in root.rglob("*")
            if path.is_file()
            and path.suffix != ".wasm"
            and not any(part in SOURCE_EXCLUDED_DIRS for part in path.parts)
            and path.name != ".DS_Store"
        ),
        key=lambda path: path.relative_to(root).as_posix(),
    )


def _source_tree_sha256(root: Path) -> str:
    """Hash path, executable bit, length, and bytes for authored source."""
    digest = hashlib.sha256()
    for path in _source_files(root):
        relative = path.relative_to(root).as_posix()
        payload = path.read_bytes()
        executable = bool(path.stat().st_mode & 0o111)
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(b"x" if executable else b"-")
        digest.update(b"\0")
        digest.update(str(len(payload)).encode("ascii"))
        digest.update(b"\0")
        digest.update(payload)
        digest.update(b"\0")
    return f"sha256:{digest.hexdigest()}"


def _repository_source_identity(root: Path) -> dict[str, Any]:
    """Describe the gameplay/pipeline checkout without calling dirty HEAD clean."""
    paths = [path for path in SOURCE_IDENTITY_PATHS if (root / path).exists()]
    status = subprocess.run(
        [
            "git",
            "status",
            "--porcelain=v1",
            "-z",
            "--untracked-files=all",
            "--",
            *paths,
        ],
        cwd=root,
        check=True,
        capture_output=True,
    ).stdout
    digest = hashlib.sha256()
    for relative in paths:
        path = root / relative
        if path.is_dir():
            files = _source_files(path)
        elif path.is_file():
            files = [path]
        else:
            files = []
        for file_path in files:
            repository_relative = file_path.relative_to(root).as_posix()
            payload = file_path.read_bytes()
            executable = bool(file_path.stat().st_mode & 0o111)
            digest.update(repository_relative.encode("utf-8"))
            digest.update(b"\0")
            digest.update(b"x" if executable else b"-")
            digest.update(b"\0")
            digest.update(str(len(payload)).encode("ascii"))
            digest.update(b"\0")
            digest.update(payload)
            digest.update(b"\0")
    return {
        "headCommit": _git_head(root),
        "worktreeDirty": bool(status),
        "sourceTreeSha256": f"sha256:{digest.hexdigest()}",
        "statusSha256": f"sha256:{hashlib.sha256(status).hexdigest()}",
        "paths": paths,
    }


def _validate_playlist(value: Any) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError("playlist must be an object")
    for key, expected in EXPECTED_PLAYLIST.items():
        if value.get(key) != expected:
            raise ValueError(
                f"playlist {key} must be exactly {expected!r}"
            )
    for key in FINGERPRINT_FIELDS:
        if not re.fullmatch(r"[0-9a-f]{64}", str(value.get(key, ""))):
            raise ValueError(
                f"playlist {key} must be a lowercase SHA-256 fingerprint"
            )
    unexpected = set(value) - set(EXPECTED_PLAYLIST) - set(FINGERPRINT_FIELDS)
    if unexpected:
        raise ValueError(
            f"playlist has unsupported fields: {', '.join(sorted(unexpected))}"
        )
    return value


def load_manifest(path: Path, repository_root: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if document.get("schemaVersion") != 1:
        raise ValueError("cohort schemaVersion must be 1")
    cohort_id = document.get("cohortId")
    if not isinstance(cohort_id, str) or _slug(cohort_id) != cohort_id:
        raise ValueError("cohortId must be a lowercase kebab-case slug")
    engine_commit = document.get("engineCommit")
    if not isinstance(engine_commit, str) or not re.fullmatch(
        r"[0-9a-f]{40}",
        engine_commit,
    ):
        raise ValueError("engineCommit must be a full lowercase Git SHA")
    if _git_head(repository_root) != engine_commit:
        raise ValueError(
            "manifest engineCommit does not match the checked-out HEAD"
        )
    document["playlist"] = _validate_playlist(document.get("playlist"))
    seeds = document.get("seeds", DEFAULT_SEEDS)
    if (
        not isinstance(seeds, list)
        or not seeds
        or any(
            not isinstance(seed, int)
            or isinstance(seed, bool)
            or seed < 0
            for seed in seeds
        )
        or len(set(seeds)) != len(seeds)
    ):
        raise ValueError("seeds must be distinct non-negative integers")
    entrants = document.get("entrants")
    if not isinstance(entrants, list) or len(entrants) != 4:
        raise ValueError("entrants must contain exactly four bots")
    doctrines = [
        entrant.get("doctrine")
        for entrant in entrants
        if isinstance(entrant, dict)
    ]
    if (
        len(doctrines) != len(entrants)
        or any(not isinstance(doctrine, str) for doctrine in doctrines)
        or set(doctrines) != EXPECTED_DOCTRINES
    ):
        raise ValueError(
            "entrants must contain each registered doctrine exactly once"
        )

    ids = set()
    manifest_root = path.parent.resolve()
    normalized = []
    for raw in entrants:
        if not isinstance(raw, dict):
            raise ValueError("every entrant must be an object")
        entrant_id = raw.get("id")
        if (
            not isinstance(entrant_id, str)
            or _slug(entrant_id) != entrant_id
            or entrant_id in ids
        ):
            raise ValueError("entrant ids must be unique kebab-case slugs")
        ids.add(entrant_id)
        root = (manifest_root / str(raw.get("root"))).resolve()
        artifact = (manifest_root / str(raw.get("artifact"))).resolve()
        dx_report = (manifest_root / str(raw.get("dxReport"))).resolve()
        if (
            not root.is_relative_to(manifest_root)
            or not artifact.is_relative_to(root)
            or not dx_report.is_relative_to(root)
        ):
            raise ValueError(
                f"{entrant_id}: root, artifact, and DX paths must stay "
                "inside the cohort and entrant root"
            )
        if not root.is_dir():
            raise ValueError(f"{entrant_id}: root is not a directory")
        if not artifact.is_file():
            raise ValueError(f"{entrant_id}: artifact does not exist")
        if not dx_report.is_file():
            raise ValueError(
                f"{entrant_id}: DX report must be frozen before disclosure"
            )
        expected_hash = raw.get("artifactSha256")
        actual_hash = _sha256(artifact)
        if expected_hash != actual_hash:
            raise ValueError(
                f"{entrant_id}: artifactSha256 mismatch "
                f"(expected {expected_hash}, got {actual_hash})"
            )
        source_revision = raw.get("sourceRevision")
        if not re.fullmatch(
            r"sha256:[0-9a-f]{64}",
            str(source_revision or ""),
        ):
            raise ValueError(
                f"{entrant_id}: sourceRevision must be a source-tree SHA-256"
            )
        actual_source_tree = _source_tree_sha256(root)
        expected_source_tree = raw.get("sourceTreeSha256")
        if expected_source_tree != actual_source_tree:
            raise ValueError(
                f"{entrant_id}: sourceTreeSha256 mismatch "
                f"(expected {expected_source_tree}, got {actual_source_tree})"
            )
        normalized.append(
            {
                **raw,
                "id": entrant_id,
                "rootPath": root,
                "artifactPath": artifact,
                "dxReportPath": dx_report,
                "artifactSha256": actual_hash,
                "sourceTreeSha256": actual_source_tree,
            }
        )
    document["seeds"] = seeds
    document["entrants"] = sorted(
        normalized,
        key=lambda entrant: entrant["id"],
    )
    document["repositorySource"] = _repository_source_identity(repository_root)
    return document


def build_plan(
    entrants: list[dict[str, Any]],
    seeds: list[int],
) -> list[dict[str, Any]]:
    plan = []
    sequence = 0
    for first, second in itertools.combinations(entrants, 2):
        for seed in seeds:
            for bot, opponent in ((first, second), (second, first)):
                sequence += 1
                match_id = (
                    f"{sequence:03}--{bot['id']}-vs-{opponent['id']}"
                    f"--s{seed}"
                )
                plan.append(
                    {
                        "id": match_id,
                        "seed": seed,
                        "bot": bot["id"],
                        "opponent": opponent["id"],
                        "teamAssignments": {
                            "0": bot["id"],
                            "1": opponent["id"],
                        },
                    }
                )
    return plan


def _copy_entrant(entrant: dict[str, Any], destination: Path) -> Path:
    shutil.copytree(
        entrant["rootPath"],
        destination,
        ignore=shutil.ignore_patterns(
            ".git",
            ".DS_Store",
            "bin",
            "obj",
            "out",
            "__pycache__",
        ),
    )
    frozen_source_revision = _source_tree_sha256(destination)
    if frozen_source_revision != entrant["sourceTreeSha256"]:
        raise ValueError(
            f"{entrant['id']}: frozen source copy identity mismatch"
        )
    artifact = destination / "bot.wasm"
    shutil.copy2(entrant["artifactPath"], artifact)
    if _sha256(artifact) != entrant["artifactSha256"]:
        raise ValueError(f"{entrant['id']}: frozen artifact copy mismatch")
    return artifact


def _archived_entrants(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    return [
        {
            key: value
            for key, value in entrant.items()
            if not key.endswith("Path")
        }
        for entrant in manifest["entrants"]
    ]


def freeze_run(
    manifest_path: Path,
    manifest: dict[str, Any],
    output: Path,
    runner_command: str,
    verify_command: str,
    plan: list[dict[str, Any]],
) -> dict[str, Path]:
    if output.exists():
        raise ValueError(
            f"output already exists: {output}; use --resume to continue it"
        )
    output.mkdir(parents=True)
    artifacts = {}
    frozen_entrants = _archived_entrants(manifest)
    for entrant in manifest["entrants"]:
        destination = output / "entrants" / entrant["id"]
        artifacts[entrant["id"]] = _copy_entrant(entrant, destination)
    run_manifest = {
        "schemaVersion": 1,
        "cohortId": manifest["cohortId"],
        "engineCommit": manifest["engineCommit"],
        "repositorySource": manifest["repositorySource"],
        "sourceManifest": str(manifest_path.resolve()),
        "sourceManifestSha256": _sha256(manifest_path),
        "playlist": manifest.get("playlist"),
        "authoringBudget": manifest.get("authoringBudget"),
        "seeds": manifest["seeds"],
        "runtime": "wasm",
        "runnerCommand": runner_command,
        "verifyCommand": verify_command,
        "entrants": frozen_entrants,
        "matchPlan": plan,
    }
    (output / "run.json").write_text(
        json.dumps(run_manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    return artifacts


def resume_run(
    output: Path,
    manifest_path: Path,
    manifest: dict[str, Any],
    plan: list[dict[str, Any]],
    runner_command: str,
    verify_command: str,
) -> dict[str, Path]:
    run_path = output / "run.json"
    if not run_path.is_file():
        raise ValueError(f"{output} is not a resumable cohort run")
    run = json.loads(run_path.read_text(encoding="utf-8"))
    if (
        run.get("cohortId") != manifest["cohortId"]
        or run.get("engineCommit") != manifest["engineCommit"]
        or run.get("repositorySource") != manifest["repositorySource"]
        or run.get("playlist") != manifest["playlist"]
        or run.get("sourceManifestSha256") != _sha256(manifest_path)
        or run.get("entrants") != _archived_entrants(manifest)
        or run.get("matchPlan") != plan
        or run.get("runnerCommand") != runner_command
        or run.get("verifyCommand") != verify_command
    ):
        raise ValueError(
            "resume manifest, plan, or commands do not match run.json"
        )
    artifacts = {}
    for entrant in manifest["entrants"]:
        artifact = output / "entrants" / entrant["id"] / "bot.wasm"
        if (
            not artifact.is_file()
            or _sha256(artifact) != entrant["artifactSha256"]
        ):
            raise ValueError(
                f"{entrant['id']}: frozen resume artifact is missing or changed"
            )
        frozen_source_revision = _source_tree_sha256(artifact.parent)
        if frozen_source_revision != entrant["sourceTreeSha256"]:
            raise ValueError(
                f"{entrant['id']}: frozen resume source is missing or changed"
            )
        artifacts[entrant["id"]] = artifact
    return artifacts


def _verification_run_name(attempt: Path) -> str:
    number = len(list(attempt.glob("resume-verification-*.json"))) + 1
    return f"resume-verification-{number:02}"


def _run_verifier(
    attempt: Path,
    verifier: list[str],
    repository_root: Path,
    *,
    name: str,
) -> dict[str, Any]:
    replay = attempt / "replay.json"
    replay_hash_before = _sha256(replay)
    with (attempt / f"{name}.stdout.log").open("w") as stdout, (
        attempt / f"{name}.stderr.log"
    ).open("w") as stderr:
        completed = subprocess.run(
            verifier,
            cwd=repository_root,
            stdout=stdout,
            stderr=stderr,
            check=False,
            text=True,
        )
    replay_hash_after = _sha256(replay)
    replay_unchanged = replay_hash_before == replay_hash_after
    return {
        "verified": completed.returncode == 0 and replay_unchanged,
        "exitCode": completed.returncode,
        "replaySha256": replay_hash_after,
        "replayUnchangedDuringVerification": replay_unchanged,
    }


def _latest_verified_attempt(
    match_dir: Path,
    verifier_template: str,
    values: dict[str, Any],
    repository_root: Path,
    *,
    reverify: bool,
) -> Path | None:
    for attempt in sorted(match_dir.glob("attempt-*"), reverse=True):
        verification = attempt / "verification.json"
        replay = attempt / "replay.json"
        if verification.is_file() and replay.is_file():
            status = json.loads(verification.read_text(encoding="utf-8"))
            if status.get("verified") is True:
                current_hash = _sha256(replay)
                recorded_hash = status.get("replaySha256")
                if reverify:
                    audit_name = _verification_run_name(attempt)
                    audit = _run_verifier(
                        attempt,
                        _command(
                            verifier_template,
                            {
                                **values,
                                "replay": replay.resolve(),
                            },
                        ),
                        repository_root,
                        name=audit_name,
                    )
                    audit["recordedReplaySha256"] = recorded_hash
                    audit["replayUnchanged"] = (
                        audit["replayUnchangedDuringVerification"]
                        and recorded_hash is not None
                        and recorded_hash == audit["replaySha256"]
                    )
                    (attempt / f"{audit_name}.json").write_text(
                        json.dumps(audit, indent=2, sort_keys=True) + "\n",
                        encoding="utf-8",
                    )
                    if not audit["verified"] or not audit["replayUnchanged"]:
                        continue
                elif recorded_hash != current_hash:
                    continue
                return attempt
    return None


def execute_plan(
    plan: list[dict[str, Any]],
    artifacts: dict[str, Path],
    output: Path,
    runner_template: str,
    verifier_template: str,
    repository_root: Path,
    *,
    dry_run: bool,
    reverify_existing: bool = False,
) -> list[dict[str, Any]]:
    executions = []
    for item in plan:
        match_dir = output / "matches" / item["id"]
        values = {
            "bot": artifacts[item["bot"]].resolve(),
            "opponent": artifacts[item["opponent"]].resolve(),
            "seed": item["seed"],
            "out": match_dir.resolve(),
            "replay": (match_dir / "replay.json").resolve(),
        }
        verified = _latest_verified_attempt(
            match_dir,
            verifier_template,
            values,
            repository_root,
            reverify=reverify_existing and not dry_run,
        )
        if verified is not None:
            executions.append(
                {"plan": item, "attempt": verified, "status": "verified"}
            )
            continue
        attempt_number = len(list(match_dir.glob("attempt-*"))) + 1
        attempt = match_dir / f"attempt-{attempt_number:02}"
        attempt.mkdir(parents=True)
        values = {
            **values,
            "out": attempt.resolve(),
            "replay": (attempt / "replay.json").resolve(),
        }
        runner = _command(runner_template, values)
        verifier = _command(verifier_template, values)
        (attempt / "command.json").write_text(
            json.dumps(
                {"runner": runner, "verifier": verifier},
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
        if dry_run:
            executions.append(
                {"plan": item, "attempt": attempt, "status": "planned"}
            )
            continue
        with (attempt / "runner.stdout.log").open("w") as stdout, (
            attempt / "runner.stderr.log"
        ).open("w") as stderr:
            completed = subprocess.run(
                runner,
                cwd=repository_root,
                stdout=stdout,
                stderr=stderr,
                check=False,
                text=True,
            )
        if completed.returncode != 0 or not (attempt / "replay.json").is_file():
            executions.append(
                {
                    "plan": item,
                    "attempt": attempt,
                    "status": "runner-failed",
                    "exitCode": completed.returncode,
                }
            )
            continue
        verification_status = _run_verifier(
            attempt,
            verifier,
            repository_root,
            name="verify",
        )
        verified_ok = verification_status["verified"]
        (attempt / "verification.json").write_text(
            json.dumps(
                verification_status,
                indent=2,
                sort_keys=True,
            ) + "\n",
            encoding="utf-8",
        )
        executions.append(
            {
                "plan": item,
                "attempt": attempt,
                "status": "verified" if verified_ok else "verify-failed",
                "exitCode": verification_status["exitCode"],
            }
        )
    return executions


def replay_identity_issues(
    document: dict[str, Any],
    item: dict[str, Any],
    playlist: dict[str, Any],
    artifact_hashes: dict[str, str],
) -> list[str]:
    header = document.get("header", {})
    contract = header.get("contract", {})
    rules = contract.get("rules", {})
    map_contract = contract.get("map", {})
    format_contract = contract.get("format", {})
    runtime = header.get("runtime", {})
    capabilities = contract.get("capabilityVersions", {})
    issues = []
    exact_values = (
        ("replayVersion", header.get("replayVersion"), 3),
        ("partial", document.get("partial"), False),
        ("seed", header.get("seed"), str(item["seed"])),
        (
            "gameRulesVersion",
            header.get("gameRulesVersion"),
            playlist["rulesetId"],
        ),
        ("rulesetId", rules.get("rulesetId"), playlist["rulesetId"]),
        (
            "rulesFingerprint",
            rules.get("rulesFingerprint"),
            playlist["rulesFingerprint"],
        ),
        ("mapId", map_contract.get("mapId"), playlist["mapId"]),
        (
            "mapVersion",
            map_contract.get("mapVersion"),
            playlist["mapVersion"],
        ),
        (
            "mapFingerprint",
            map_contract.get("mapFingerprint"),
            playlist["mapFingerprint"],
        ),
        (
            "formatId",
            format_contract.get("formatId"),
            playlist["formatId"],
        ),
        (
            "formatFingerprint",
            format_contract.get("formatFingerprint"),
            playlist["formatFingerprint"],
        ),
        (
            "matchContractFingerprint",
            contract.get("matchContractFingerprint"),
            playlist["matchContractFingerprint"],
        ),
        (
            "runtime.contractProfileId",
            runtime.get("contractProfileId"),
            playlist["contractProfileId"],
        ),
        (
            "contract.capabilityVersions.contractProfileId",
            capabilities.get("contractProfileId"),
            playlist["contractProfileId"],
        ),
        ("gameMode.kind", rules.get("gameMode", {}).get("kind"), "frontline"),
    )
    issues.extend(
        name
        for name, actual, expected in exact_values
        if actual != expected
    )

    provenance = header.get("provenance", {}).get("participants", [])
    provenance_by_team = {
        str(participant.get("teamId")): participant
        for participant in provenance
    }
    for team_id, entrant_id in item["teamAssignments"].items():
        participant = provenance_by_team.get(team_id, {})
        if participant.get("artifactHash") != artifact_hashes[entrant_id]:
            issues.append(f"team-{team_id}.artifactHash")
        if "wasm" not in str(participant.get("runtimeKind")).lower():
            issues.append(f"team-{team_id}.runtimeKind")
    return issues


def replay_result(
    execution: dict[str, Any],
    artifact_hashes: dict[str, str],
    playlist: dict[str, Any],
) -> dict[str, Any]:
    item = execution["plan"]
    row = {
        "matchId": item["id"],
        "seed": item["seed"],
        "teamAssignments": item["teamAssignments"],
        "status": execution["status"],
        "replay": str((execution["attempt"] / "replay.json").resolve()),
    }
    if execution["status"] != "verified":
        return row
    document = json.loads(
        (execution["attempt"] / "replay.json").read_text(encoding="utf-8")
    )
    identity_issues = replay_identity_issues(
        document,
        item,
        playlist,
        artifact_hashes,
    )
    final_state = (
        document.get("ticks", [])[-1].get("postState", {})
        if document.get("ticks")
        else document.get("initialFrame", {}).get("state", {})
    )
    has_fault = any(
        int(str(participant.get("runtimeFaultCount", "0"))) != 0
        or participant.get("disqualified") is True
        for participant in final_state.get("participants", [])
    )
    if identity_issues or has_fault:
        row["status"] = "invalid-replay"
        row["identityIssues"] = identity_issues
        row["runtimeFaultOrDisqualification"] = has_fault
        return row
    result = document.get("result", {})
    winner_team = result.get("standings", {}).get("winnerTeamId")
    winner = (
        item["teamAssignments"].get(str(winner_team))
        if winner_team is not None
        else None
    )
    mode_reason = result.get("mode", {}).get("reason")
    row.update(
        {
            "winner": winner,
            "draw": winner is None,
            "winnerTeamId": winner_team,
            "completionReason": result.get("completionReason"),
            "terminalReason": mode_reason or result.get("completionReason"),
            "endTick": result.get("endTick"),
            "durationTicks": (
                result["endTick"] + 1
                if result.get("endTick") is not None
                else 0
            ),
            "teamScores": {
                str(team["teamId"]): team.get("scores", [])
                for team in result.get("standings", {}).get("teams", [])
            },
            "replayHash": document.get("replayHash"),
        }
    )
    return row


def summarize(
    manifest: dict[str, Any],
    executions: list[dict[str, Any]],
) -> dict[str, Any]:
    artifact_hashes = {
        entrant["id"]: entrant["artifactSha256"]
        for entrant in manifest["entrants"]
    }
    rows = [
        replay_result(execution, artifact_hashes, manifest["playlist"])
        for execution in executions
    ]
    valid = [row for row in rows if row["status"] == "verified"]
    records = {
        entrant["id"]: {
            "wins": 0,
            "draws": 0,
            "losses": 0,
            "points": 0.0,
            "games": 0,
        }
        for entrant in manifest["entrants"]
    }
    side_decisive = Counter()
    reasons = Counter()
    head_to_head: dict[tuple[str, str], Counter[str]] = defaultdict(Counter)
    for row in valid:
        team0 = row["teamAssignments"]["0"]
        team1 = row["teamAssignments"]["1"]
        pair = tuple(sorted((team0, team1)))
        records[team0]["games"] += 1
        records[team1]["games"] += 1
        reasons[str(row["terminalReason"])] += 1
        if row["draw"]:
            for entrant_id in (team0, team1):
                records[entrant_id]["draws"] += 1
                records[entrant_id]["points"] += 0.5
            head_to_head[pair]["draws"] += 1
        else:
            winner = row["winner"]
            loser = team1 if winner == team0 else team0
            records[winner]["wins"] += 1
            records[winner]["points"] += 1.0
            records[loser]["losses"] += 1
            side_decisive[str(row["winnerTeamId"])] += 1
            head_to_head[pair][f"wins:{winner}"] += 1
    total_points = sum(record["points"] for record in records.values())
    for record in records.values():
        record["pointShare"] = (
            record["points"] / total_points if total_points else 0.0
        )
    decisive = sum(side_decisive.values())
    warnings = []
    if any(row["status"] != "verified" for row in rows):
        warnings.append("one or more planned matches are missing or invalid")
    if decisive and max(side_decisive.values(), default=0) / decisive > 0.65:
        warnings.append("one participant assignment won over 65% of decisions")
    if any(record["pointShare"] > 0.50 for record in records.values()):
        warnings.append("one entrant earned over 50% of match points")
    if any(
        record["wins"] == 0 and record["draws"] == 0
        for record in records.values()
    ):
        warnings.append("one entrant has neither a win nor a draw")
    max_tick = reasons["max-ticks"]
    if valid and max_tick / len(valid) > 0.25:
        warnings.append("more than 25% of matches reached max ticks")
    breach = reasons["base-breach"] + reasons["breach"]
    if valid and breach / len(valid) < 0.50:
        warnings.append("fewer than 50% of matches ended by breach")
    return {
        "schemaVersion": 1,
        "cohortId": manifest["cohortId"],
        "plannedMatches": len(rows),
        "validMatches": len(valid),
        "records": records,
        "participantAssignmentWins": dict(sorted(side_decisive.items())),
        "terminalReasons": dict(sorted(reasons.items())),
        "headToHead": [
            {
                "entrants": list(pair),
                **dict(sorted(counts.items())),
            }
            for pair, counts in sorted(head_to_head.items())
        ],
        "warnings": warnings,
        "matches": rows,
    }


def write_results(output: Path, report: dict[str, Any]) -> None:
    (output / "results.json").write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    lines = [
        f"# {report['cohortId']} cohort results",
        "",
        (
            f"Valid matches: {report['validMatches']} / "
            f"{report['plannedMatches']}"
        ),
        "",
        "| Entrant | W | D | L | Points | Point share |",
        "| --- | ---: | ---: | ---: | ---: | ---: |",
    ]
    for entrant_id, record in sorted(
        report["records"].items(),
        key=lambda item: (-item[1]["points"], item[0]),
    ):
        lines.append(
            f"| {entrant_id} | {record['wins']} | {record['draws']} | "
            f"{record['losses']} | {record['points']:.1f} | "
            f"{record['pointShare']:.1%} |"
        )
    lines.extend(["", "## Diagnostic warnings", ""])
    lines.extend(
        f"- {warning}" for warning in report["warnings"]
    )
    if not report["warnings"]:
        lines.append("- None from the pre-registered exploratory gates.")
    lines.extend(
        [
            "",
            "This is an exploratory cohort screen, not a balance or ship verdict.",
            "",
        ]
    )
    (output / "results.md").write_text(
        "\n".join(lines),
        encoding="utf-8",
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--runner-command", default=DEFAULT_RUNNER)
    parser.add_argument("--verify-command", default=DEFAULT_VERIFIER)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args(argv)

    repository_root = Path(__file__).resolve().parent.parent
    manifest_path = args.manifest.resolve()
    output = args.output.resolve()
    manifest = load_manifest(manifest_path, repository_root)
    plan = build_plan(manifest["entrants"], manifest["seeds"])
    if args.resume:
        artifacts = resume_run(
            output,
            manifest_path,
            manifest,
            plan,
            args.runner_command,
            args.verify_command,
        )
    else:
        artifacts = freeze_run(
            manifest_path,
            manifest,
            output,
            args.runner_command,
            args.verify_command,
            plan,
        )
    executions = execute_plan(
        plan,
        artifacts,
        output,
        args.runner_command,
        args.verify_command,
        repository_root,
        dry_run=args.dry_run,
        reverify_existing=args.resume,
    )
    report = summarize(manifest, executions)
    write_results(output, report)
    print(
        f"{manifest['cohortId']}: {report['validMatches']}/"
        f"{report['plannedMatches']} verified matches"
    )
    print(f"Results: {(output / 'results.json').resolve()}")
    return 0 if report["validMatches"] == report["plannedMatches"] else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2)
