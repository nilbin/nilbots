#!/usr/bin/env python3
"""Mechanize the agent-arena SS1.9 pre-flight gate for an authoring wave.

Authoring time is the expensive resource, so a wave must never launch against
an unverified mechanism or stale tooling. This runs the mechanical half of the
gate against the CURRENT tree and reports PASS/FAIL per check with evidence
paths (prose accuracy stays human):

1. contract-truth      --print-candidate-contract resolves per arm, the
                       identities are distinct across arms, and two
                       invocations agree byte for byte.
2. scaffold-self-match the `nilbots new --profile generic-actor` starter plays
                       itself on every arm and ACTS: moves, shoots, no runtime
                       fault, no idle freeze, replay verifies.
3. foreign-doctrine    the starter plays one frozen cohort artifact per arm.
                       When the frozen artifact faults on the arm (its embedded
                       exact-contract reader predates the arm's fields) the
                       check retries with that bot's REBUILT artifact, which is
                       what the wave will actually run.
4. artifact-rebuild    every cohort bot is rebuilt from its unchanged frozen
                       source into a scratch tree (the frozen out/bot.wasm is
                       never touched), old/new hashes are recorded, and each
                       rebuild plays one match on the rebuild arm.
5. doc-contract        the brief's pendulum and skills tokens match CLI help
                       and resolve through the real contract factory, and each
                       skill is accepted only on a cell holding its owner class.
6. tooling-freshness   the published sandbox CLI authors will use matches the
                       source CliVersion and ships the current packaged briefs
                       (WARN only; the republish itself stays manual).

Output is deterministic (no timestamps, no durations, sorted rows) and the exit
code is nonzero when any check FAILs. WARN never fails the gate.

What this gate does NOT do: an arm that adds a new VERB or skill still needs a
hand-written lifecycle probe (attempt -> windup -> completion/cancellation ->
events), because a starter that never uses the verb cannot exercise it, and
doc PROSE accuracy is still read by a human against the contract.

Example:
    python3 scripts/preflight-authoring-gate.py \
        --arm "" \
        --arm "--pendulum ratchet" \
        --arm "--pendulum ratchet-contest" \
        --arm "numbers-only=--capture-threshold 9 --prime-respawn-ticks 9" \
        --json sandbox/preflight-gate/report.json
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any

REPO = Path(__file__).resolve().parent.parent
SCHEMA_VERSION = 1
GATE_ID = "agent-arena-1.9-preflight"
DEFAULT_COHORT = (
    "arena-bots/frontline-labs/classes-wave-1-revision-2-2026-07-29"
)
DEFAULT_CLASSES_BRIEF = "docs/EXPERIMENTAL-FRONTLINE-CLASSES.md"
PROJECT_IGNORE = shutil.ignore_patterns(
    "out", "evidence", "bin", "obj", ".git", "*.wasm"
)
ACTION_MOVE = {"move"}
ACTION_SHOOT = {"shoot", "shoot-direction"}


class GateError(Exception):
    """A usage or environment problem that stops the gate before it judges."""


# --------------------------------------------------------------------------
# process plumbing
# --------------------------------------------------------------------------


def run(command: list[str], cwd: Path | None = None) -> subprocess.CompletedProcess:
    return subprocess.run(
        command,
        cwd=str(cwd) if cwd else str(REPO),
        capture_output=True,
        text=True,
        check=False,
    )


def rel(path: Path) -> str:
    """Repo-relative when possible, so reports diff cleanly across machines."""
    resolved = Path(path).resolve()
    try:
        return resolved.relative_to(REPO).as_posix()
    except ValueError:
        return resolved.as_posix()


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def fresh_dir(path: Path) -> Path:
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)
    return path


def write_log(path: Path, proc: subprocess.CompletedProcess) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        f"$ {shlex.join(proc.args)}\n"
        f"exit {proc.returncode}\n\n"
        f"--- stdout ---\n{proc.stdout}\n"
        f"--- stderr ---\n{proc.stderr}\n",
        encoding="utf-8",
    )
    return path


def first_error_line(proc: subprocess.CompletedProcess) -> str:
    stream = proc.stderr.strip() or proc.stdout.strip()
    for line in stream.splitlines():
        if line.strip():
            return line.strip()[:200]
    return f"exit {proc.returncode} with no output"


# --------------------------------------------------------------------------
# result model
# --------------------------------------------------------------------------


class Check:
    """One numbered SS1.9 item plus its per-arm / per-bot rows."""

    def __init__(self, check_id: str, title: str) -> None:
        self.check_id = check_id
        self.title = title
        self.items: list[dict[str, Any]] = []
        self.note: str | None = None

    def add(
        self,
        name: str,
        status: str,
        detail: str,
        evidence: list[Path] | None = None,
        **facts: Any,
    ) -> None:
        self.items.append(
            {
                "name": name,
                "status": status,
                "detail": detail,
                "evidence": sorted(rel(path) for path in evidence or []),
                **facts,
            }
        )

    @property
    def status(self) -> str:
        states = {item["status"] for item in self.items}
        if not self.items:
            return "SKIP"
        if "FAIL" in states:
            return "FAIL"
        if "WARN" in states:
            return "WARN"
        if states == {"SKIP"}:
            return "SKIP"
        return "PASS"

    def to_json(self) -> dict[str, Any]:
        return {
            "id": self.check_id,
            "title": self.title,
            "status": self.status,
            "note": self.note,
            "items": self.items,
        }


# --------------------------------------------------------------------------
# replay forensics (the template agent's action-mix approach)
# --------------------------------------------------------------------------


def analyze_replay(path: Path, max_idle_streak: int) -> dict[str, Any]:
    """Per-team action mix, faults, and idle streaks from a replay v3."""
    document = json.loads(path.read_text(encoding="utf-8"))
    teams: dict[int, dict[str, Any]] = {}
    idle_run: dict[int, int] = {}

    def team(team_id: int) -> dict[str, Any]:
        return teams.setdefault(
            team_id,
            {
                "teamId": team_id,
                "actions": {},
                "moves": 0,
                "shots": 0,
                "decisions": 0,
                "rejected": 0,
                "runtimeFaults": 0,
                "maxIdleStreak": 0,
            },
        )

    for tick in document["ticks"]:
        acted: dict[int, bool] = {}
        for turn in tick["actorTurns"]:
            team_id = turn["actorId"]["teamId"]
            row = team(team_id)
            resolution = turn.get("actionResolution") or {}
            action = (
                resolution.get("validatedAction")
                or resolution.get("acceptedAction")
                or resolution.get("submittedAction")
                or turn.get("submittedDecision")
                or {}
            )
            action_id = action.get("actionId", "<none>")
            row["actions"][action_id] = row["actions"].get(action_id, 0) + 1
            row["decisions"] += 1
            outcome = resolution.get("outcome")
            if outcome != "success":
                row["rejected"] += 1
            if resolution.get("runtimeFault"):
                row["runtimeFaults"] += 1
            if outcome == "success" and action_id in ACTION_MOVE:
                row["moves"] += 1
            if outcome == "success" and action_id in ACTION_SHOOT:
                row["shots"] += 1
            acted[team_id] = acted.get(team_id, False) or action_id != "wait"
        for team_id, did_act in acted.items():
            row = team(team_id)
            idle_run[team_id] = 0 if did_act else idle_run.get(team_id, 0) + 1
            row["maxIdleStreak"] = max(row["maxIdleStreak"], idle_run[team_id])

    initial = document.get("initialFrame") or {}
    final_state = (
        document["ticks"][-1]["postState"]
        if document["ticks"]
        else initial.get("state", initial)
    )
    participants = [
        {
            "participantId": entry["participantId"],
            "teamId": entry["teamId"],
            "runtimeFaultCount": int(entry["runtimeFaultCount"]),
            "disqualified": bool(entry["disqualified"]),
        }
        for entry in final_state["participants"]
    ]
    result = document["result"]
    faulted = any(
        entry["runtimeFaultCount"] or entry["disqualified"]
        for entry in participants
    ) or any(row["runtimeFaults"] for row in teams.values())
    frozen_teams = sorted(
        row["teamId"]
        for row in teams.values()
        if row["maxIdleStreak"] > max_idle_streak
    )
    silent_teams = sorted(
        row["teamId"]
        for row in teams.values()
        if row["moves"] == 0 or row["shots"] == 0
    )
    return {
        "replay": rel(path),
        "replayHash": document["replayHash"],
        "completionReason": result["completionReason"],
        "endTick": result["endTick"],
        "ticks": len(document["ticks"]),
        "participants": participants,
        "teams": [teams[key] for key in sorted(teams)],
        "faulted": faulted,
        "frozenTeams": frozen_teams,
        "silentTeams": silent_teams,
    }


def mix_summary(analysis: dict[str, Any]) -> str:
    parts = []
    for row in analysis["teams"]:
        actions = ", ".join(
            f"{name} {count}"
            for name, count in sorted(row["actions"].items())
        )
        parts.append(f"t{row['teamId']}[{actions}]")
    return " ".join(parts)


# --------------------------------------------------------------------------
# CLI wrappers
# --------------------------------------------------------------------------


class Cli:
    def __init__(self, executable: list[str]) -> None:
        self.executable = executable

    def run(self, args: list[str], cwd: Path | None = None):
        return run([*self.executable, *args], cwd=cwd)

    def labs(self, args: list[str]):
        return self.run(["experiment", "frontline-labs", *args])

    def contract(self, flags: list[str]):
        return self.labs([*flags, "--print-candidate-contract"])

    def verify(self, replay: Path):
        return self.run(["verify", str(replay)])


def resolve_cli(argument: str | None, build: bool) -> tuple[Cli, dict[str, Any]]:
    if argument:
        executable = shlex.split(argument)
    else:
        built = REPO / "src/BotArena.Cli/bin/Debug/net10.0/botarena"
        if build:
            proc = run(
                ["dotnet", "build", "src/BotArena.Cli", "-c", "Debug"]
            )
            if proc.returncode != 0:
                raise GateError(
                    "dotnet build src/BotArena.Cli failed; pass --cli to use "
                    f"an existing binary.\n{proc.stdout[-2000:]}"
                )
        if not built.exists():
            raise GateError(
                f"no CLI at {rel(built)}; build it or pass --cli."
            )
        executable = [str(built)]
    cli = Cli(executable)
    version = cli.run(["--version"])
    if version.returncode != 0:
        raise GateError(
            f"`{shlex.join(executable)} --version` failed: "
            f"{first_error_line(version)}"
        )
    return cli, {
        "command": shlex.join(executable),
        "version": version.stdout.strip(),
    }


# --------------------------------------------------------------------------
# arms
# --------------------------------------------------------------------------


ARM_NAME = re.compile(r"^([A-Za-z0-9][A-Za-z0-9._-]*)=(.*)$", re.DOTALL)


def parse_arm(raw: str) -> dict[str, Any]:
    match = ARM_NAME.match(raw.strip())
    if match:
        name, flags_text = match.group(1), match.group(2)
    else:
        flags_text = raw.strip()
        name = auto_arm_name(flags_text)
    flags = shlex.split(flags_text)
    return {"name": name, "flags": flags, "spec": " ".join(flags)}


def auto_arm_name(flags_text: str) -> str:
    tokens = shlex.split(flags_text)
    if not tokens:
        return "control"
    return "-".join(token.lstrip("-") for token in tokens)


# --------------------------------------------------------------------------
# checks
# --------------------------------------------------------------------------


def check_contract_truth(
    cli: Cli,
    arms: list[dict[str, Any]],
    evidence_root: Path,
) -> tuple[Check, dict[str, dict[str, Any]]]:
    check = Check("contract-truth", "Contract truth per arm")
    check.note = (
        "--print-candidate-contract resolves, is byte-stable across two "
        "invocations, and mints a distinct identity per arm. An arm that "
        "adds a VERB still needs its own lifecycle probe match (attempt -> "
        "windup -> completion/cancellation -> events); identity alone is not "
        "that proof."
    )
    contracts: dict[str, dict[str, Any]] = {}
    fingerprints: dict[str, list[str]] = {}
    for arm in arms:
        directory = fresh_dir(evidence_root / arm["name"])
        first = cli.contract(arm["flags"])
        write_log(directory / "print-1.log", first)
        if first.returncode != 0:
            check.add(
                arm["name"],
                "FAIL",
                f"--print-candidate-contract exited {first.returncode}: "
                f"{first_error_line(first)}",
                [directory / "print-1.log"],
            )
            continue
        second = cli.contract(arm["flags"])
        write_log(directory / "print-2.log", second)
        if first.stdout != second.stdout:
            check.add(
                arm["name"],
                "FAIL",
                "two invocations printed different contracts",
                [directory / "print-1.log", directory / "print-2.log"],
            )
            continue
        try:
            contract = json.loads(first.stdout)
        except json.JSONDecodeError as error:
            check.add(
                arm["name"],
                "FAIL",
                f"contract output is not JSON: {error}",
                [directory / "print-1.log"],
            )
            continue
        (directory / "contract.json").write_text(
            json.dumps(contract, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        contracts[arm["name"]] = contract
        fingerprints.setdefault(
            contract["matchContractFingerprint"], []
        ).append(arm["name"])
        check.add(
            arm["name"],
            "PASS",
            f"{contract['rulesetId']} "
            f"match {contract['matchContractFingerprint'][:12]} (stable)",
            [directory / "contract.json"],
            rulesetId=contract["rulesetId"],
            rulesFingerprint=contract["rulesFingerprint"],
            matchContractFingerprint=contract["matchContractFingerprint"],
            topologyProfileId=contract["topologyProfileId"],
        )
    collisions = {
        fingerprint: names
        for fingerprint, names in fingerprints.items()
        if len(names) > 1
    }
    if collisions:
        for fingerprint, names in sorted(collisions.items()):
            check.add(
                "distinctness",
                "FAIL",
                f"arms {', '.join(sorted(names))} share match fingerprint "
                f"{fingerprint[:12]}",
            )
    elif contracts:
        check.add(
            "distinctness",
            "PASS",
            f"{len(contracts)} arm identities are pairwise distinct",
        )
    return check, contracts


def check_scaffold_self_match(
    cli: Cli,
    arms: list[dict[str, Any]],
    scaffold: Path,
    evidence_root: Path,
    seed: int,
    max_idle_streak: int,
) -> Check:
    check = Check("scaffold-self-match", "Scaffold self-match per arm")
    check.note = (
        "the starter plays itself in-process on every arm and must ACT: "
        f"moves > 0, shots > 0, no fault, no idle streak > {max_idle_streak}."
    )
    for arm in arms:
        directory = fresh_dir(evidence_root / arm["name"])
        proc = cli.labs(
            [
                "--bot",
                str(scaffold),
                "--opponent",
                str(scaffold),
                *arm["flags"],
                "--seed",
                str(seed),
                "--runtime",
                "in-process",
                "--out",
                str(directory),
            ]
        )
        log = write_log(directory / "match.log", proc)
        replay = directory / "replay.json"
        if proc.returncode != 0 or not replay.exists():
            check.add(
                arm["name"],
                "FAIL",
                f"self-match exited {proc.returncode}: "
                f"{first_error_line(proc)}",
                [log],
            )
            continue
        analysis = analyze_replay(replay, max_idle_streak)
        verified = cli.verify(replay)
        write_log(directory / "verify.log", verified)
        problems = []
        if analysis["faulted"]:
            problems.append("runtime fault or disqualification")
        if analysis["silentTeams"]:
            problems.append(
                "teams that never moved or never shot: "
                + ", ".join(str(team) for team in analysis["silentTeams"])
            )
        if analysis["frozenTeams"]:
            problems.append(
                f"idle streak > {max_idle_streak} on teams "
                + ", ".join(str(team) for team in analysis["frozenTeams"])
            )
        if verified.returncode != 0:
            problems.append("replay does not verify")
        check.add(
            arm["name"],
            "FAIL" if problems else "PASS",
            "; ".join(problems)
            if problems
            else (
                f"{analysis['completionReason']} at tick "
                f"{analysis['endTick']}; {mix_summary(analysis)}"
            ),
            [replay, log, directory / "verify.log"],
            analysis=analysis,
        )
    return check


def check_artifact_rebuild(
    cli: Cli,
    cohort: Path,
    rebuild_root: Path,
    evidence_root: Path,
    rebuild_arm: dict[str, Any],
    opponent: Path,
    seed: int,
    max_idle_streak: int,
    reuse: bool,
) -> tuple[Check, dict[str, Path]]:
    check = Check("artifact-rebuild", "Frozen-cohort artifact rebuild")
    check.note = (
        f"each {cohort.name} bot rebuilt from unchanged frozen source into "
        f"{rel(rebuild_root)} (frozen out/ untouched), then one match on "
        f"`{rebuild_arm['spec'] or 'control'}`."
    )
    rebuilt: dict[str, Path] = {}
    bots = sorted(
        path
        for path in cohort.iterdir()
        if path.is_dir() and (path / "botarena.json").exists()
    )
    if not bots:
        check.add("cohort", "FAIL", f"no bot projects under {rel(cohort)}")
        return check, rebuilt
    for source in bots:
        name = source.name
        frozen = source / "out" / "bot.wasm"
        old_hash = file_sha256(frozen) if frozen.exists() else None
        target = rebuild_root / name
        artifact = target / "out" / "bot.wasm"
        if not (reuse and artifact.exists()):
            if target.exists():
                shutil.rmtree(target)
            shutil.copytree(source, target, ignore=PROJECT_IGNORE)
            build = cli.run(["build", str(target)])
            write_log(evidence_root / name / "build.log", build)
            if build.returncode != 0 or not artifact.exists():
                check.add(
                    name,
                    "FAIL",
                    f"rebuild failed: {first_error_line(build)}",
                    [evidence_root / name / "build.log"],
                    oldHash=old_hash,
                    newHash=None,
                )
                continue
        new_hash = file_sha256(artifact)
        rebuilt[name] = artifact
        directory = fresh_dir(evidence_root / name / "match")
        proc = cli.labs(
            [
                "--bot",
                str(artifact),
                "--opponent",
                str(opponent),
                *rebuild_arm["flags"],
                "--seed",
                str(seed),
                "--runtime",
                "wasm",
                "--out",
                str(directory),
            ]
        )
        log = write_log(directory / "match.log", proc)
        replay = directory / "replay.json"
        changed = "changed" if old_hash != new_hash else "unchanged"
        if proc.returncode != 0 or not replay.exists():
            check.add(
                name,
                "FAIL",
                f"rebuild-arm match exited {proc.returncode}: "
                f"{first_error_line(proc)}",
                [log],
                oldHash=old_hash,
                newHash=new_hash,
            )
            continue
        analysis = analyze_replay(replay, max_idle_streak)
        verified = cli.verify(replay)
        write_log(directory / "verify.log", verified)
        problems = []
        if analysis["faulted"]:
            problems.append("rebuilt artifact faulted on the rebuild arm")
        if verified.returncode != 0:
            problems.append("replay does not verify")
        check.add(
            name,
            "FAIL" if problems else "PASS",
            "; ".join(problems)
            if problems
            else (
                f"hash {changed}; {analysis['completionReason']} at tick "
                f"{analysis['endTick']}, no faults"
            ),
            [artifact, replay, log],
            oldHash=old_hash,
            newHash=new_hash,
            hashChanged=old_hash != new_hash,
            analysis=analysis,
        )
    return check, rebuilt


def check_foreign_doctrine(
    cli: Cli,
    arms: list[dict[str, Any]],
    scaffold_artifact: Path,
    frozen_name: str,
    frozen_artifact: Path,
    rebuilt: dict[str, Path],
    evidence_root: Path,
    seed: int,
    max_idle_streak: int,
) -> Check:
    check = Check("foreign-doctrine", "Foreign-doctrine smoke per arm")
    check.note = (
        f"starter vs frozen {frozen_name}; when the frozen artifact faults on "
        "an arm the check retries with its rebuild, which is what the wave "
        "will run."
    )
    for arm in arms:
        directory = fresh_dir(evidence_root / arm["name"])
        attempts: list[dict[str, Any]] = []
        candidates: list[tuple[str, Path]] = [("frozen", frozen_artifact)]
        if frozen_name in rebuilt:
            candidates.append(("rebuilt", rebuilt[frozen_name]))
        status, detail = "FAIL", "no opponent artifact ran cleanly"
        evidence: list[Path] = []
        for kind, artifact in candidates:
            attempt_dir = fresh_dir(directory / kind)
            proc = cli.labs(
                [
                    "--bot",
                    str(scaffold_artifact),
                    "--opponent",
                    str(artifact),
                    *arm["flags"],
                    "--seed",
                    str(seed),
                    "--runtime",
                    "wasm",
                    "--out",
                    str(attempt_dir),
                ]
            )
            log = write_log(attempt_dir / "match.log", proc)
            evidence.append(log)
            replay = attempt_dir / "replay.json"
            if proc.returncode != 0 or not replay.exists():
                attempts.append(
                    {
                        "opponent": kind,
                        "ok": False,
                        "reason": first_error_line(proc),
                    }
                )
                continue
            analysis = analyze_replay(replay, max_idle_streak)
            verified = cli.verify(replay)
            write_log(attempt_dir / "verify.log", verified)
            evidence.append(replay)
            ok = not analysis["faulted"] and verified.returncode == 0
            attempts.append(
                {
                    "opponent": kind,
                    "ok": ok,
                    "completionReason": analysis["completionReason"],
                    "endTick": analysis["endTick"],
                    "faulted": analysis["faulted"],
                    "verified": verified.returncode == 0,
                    "artifact": rel(artifact),
                }
            )
            if ok:
                note = (
                    ""
                    if kind == "frozen"
                    else " (frozen artifact faulted on this arm; "
                    "rebuild required)"
                )
                status = "PASS"
                detail = (
                    f"{kind} opponent completed "
                    f"{analysis['completionReason']} at tick "
                    f"{analysis['endTick']}, verified{note}"
                )
                break
        else:
            failures = "; ".join(
                f"{attempt['opponent']}: "
                f"{attempt.get('reason') or attempt.get('completionReason')}"
                for attempt in attempts
            )
            detail = f"no artifact completed cleanly ({failures})"
        check.add(
            arm["name"],
            status,
            detail,
            evidence,
            attempts=attempts,
            frozenFaulted=any(
                attempt["opponent"] == "frozen" and not attempt["ok"]
                for attempt in attempts
            ),
        )
    return check


TABLE_ROW = re.compile(r"^\|(.+)\|\s*$")
BACKTICKED = re.compile(r"`([a-z0-9][a-z0-9-]*)`")


def markdown_token_tables(text: str) -> list[dict[str, Any]]:
    """Every markdown table whose first header cell is `token`, with the
    heading it lives under and its first two columns."""
    tables: list[dict[str, Any]] = []
    heading = ""
    lines = text.splitlines()
    index = 0
    while index < len(lines):
        line = lines[index]
        if line.startswith("#"):
            heading = line.lstrip("#").strip()
            index += 1
            continue
        match = TABLE_ROW.match(line)
        if not match:
            index += 1
            continue
        header = [cell.strip() for cell in match.group(1).split("|")]
        if not header or header[0].strip("* ").lower() != "token":
            index += 1
            continue
        rows: list[list[str]] = []
        index += 2  # header separator
        while index < len(lines):
            row = TABLE_ROW.match(lines[index])
            if not row:
                break
            rows.append([cell.strip() for cell in row.group(1).split("|")])
            index += 1
        tables.append({"heading": heading, "header": header, "rows": rows})
    return tables


def table_tokens(table: dict[str, Any]) -> list[str]:
    tokens = []
    for row in table["rows"]:
        found = BACKTICKED.search(row[0])
        if found:
            tokens.append(found.group(1))
    return tokens


def help_tokens(help_text: str, option: str) -> list[str]:
    match = re.search(rf"\[--{option} ([a-z0-9|\-]+)\]", help_text)
    return match.group(1).split("|") if match else []


def check_doc_contract(
    cli: Cli,
    brief: Path,
    evidence_root: Path,
) -> Check:
    check = Check("doc-contract", "Doc-vs-contract spot checks")
    check.note = (
        "mechanical only: token sets and resolvability. Prose accuracy "
        "stays human."
    )
    if not brief.exists():
        check.add("brief", "FAIL", f"missing {rel(brief)}")
        return check
    text = brief.read_text(encoding="utf-8")
    tables = markdown_token_tables(text)
    help_proc = cli.run(["help", "experiment"])
    write_log(evidence_root / "cli-help.log", help_proc)
    if help_proc.returncode != 0:
        check.add(
            "cli-help",
            "FAIL",
            f"`help experiment` exited {help_proc.returncode}",
            [evidence_root / "cli-help.log"],
        )
        return check

    def table_for(keyword: str) -> dict[str, Any] | None:
        for table in tables:
            if keyword in table["heading"].lower():
                return table
        return None

    # 5a. pendulum tokens: doc set == CLI set, and every token resolves.
    pendulum_table = table_for("pendulum")
    doc_pendulum = table_tokens(pendulum_table) if pendulum_table else []
    cli_pendulum = help_tokens(help_proc.stdout, "pendulum")
    if not doc_pendulum:
        check.add("pendulum-tokens", "FAIL", "no pendulum token table found")
    elif sorted(doc_pendulum) != sorted(cli_pendulum):
        check.add(
            "pendulum-tokens",
            "FAIL",
            f"brief {sorted(doc_pendulum)} != CLI help {sorted(cli_pendulum)}",
            [evidence_root / "cli-help.log"],
        )
    else:
        check.add(
            "pendulum-tokens",
            "PASS",
            f"{len(doc_pendulum)} tokens agree with CLI help",
            [evidence_root / "cli-help.log"],
            tokens=sorted(doc_pendulum),
        )
    resolved: dict[str, str] = {}
    unresolved: list[str] = []
    for token in doc_pendulum:
        flags = [] if token == "control" else ["--pendulum", token]
        proc = cli.contract(flags)
        write_log(evidence_root / f"pendulum-{token}.log", proc)
        if proc.returncode != 0:
            unresolved.append(f"{token}: {first_error_line(proc)}")
            continue
        resolved[token] = json.loads(proc.stdout)["rulesetId"]
    if doc_pendulum:
        duplicates = len(set(resolved.values())) != len(resolved)
        if unresolved:
            check.add(
                "pendulum-resolves",
                "FAIL",
                "; ".join(unresolved),
            )
        elif duplicates:
            check.add(
                "pendulum-resolves",
                "FAIL",
                f"tokens share a ruleset id: {sorted(resolved.items())}",
            )
        else:
            check.add(
                "pendulum-resolves",
                "PASS",
                f"{len(resolved)} tokens resolve to distinct rulesets",
                rulesets=dict(sorted(resolved.items())),
            )

    # 5b/5c. skills tokens and per-class ownership.
    skills_table = table_for("skills")
    doc_skills = table_tokens(skills_table) if skills_table else []
    cli_skills = help_tokens(help_proc.stdout, "skills")
    if not doc_skills:
        check.add("skills-tokens", "FAIL", "no skills token table found")
        return check
    if sorted(doc_skills) != sorted(cli_skills):
        check.add(
            "skills-tokens",
            "FAIL",
            f"brief {sorted(doc_skills)} != CLI help {sorted(cli_skills)}",
            [evidence_root / "cli-help.log"],
        )
    else:
        check.add(
            "skills-tokens",
            "PASS",
            f"{len(doc_skills)} tokens agree with CLI help",
            [evidence_root / "cli-help.log"],
            tokens=sorted(doc_skills),
        )
    owners = {}
    for row in skills_table["rows"]:
        token = BACKTICKED.search(row[0])
        owner = row[1].strip() if len(row) > 1 else ""
        if token and re.fullmatch(r"[a-z]+", owner):
            owners[token.group(1)] = owner
    classes = sorted(set(owners.values()))
    ownership_problems = []
    for token in sorted(owners):
        owner = owners[token]
        pair = f"{owner}-vs-{owner}"
        accepted = cli.contract(["--classes", pair, "--skills", token])
        write_log(evidence_root / f"skill-{token}-owner.log", accepted)
        if accepted.returncode != 0:
            ownership_problems.append(
                f"{token} refused on its owner pair {pair}: "
                f"{first_error_line(accepted)}"
            )
            continue
        foreign = [name for name in classes if name != owner]
        if len(foreign) < 2:
            continue
        foreign_pair = "-vs-".join(sorted(foreign)[:2])
        refused = cli.contract(
            ["--classes", foreign_pair, "--skills", token]
        )
        write_log(evidence_root / f"skill-{token}-foreign.log", refused)
        if refused.returncode == 0:
            ownership_problems.append(
                f"{token} was accepted on {foreign_pair}, which holds no "
                f"{owner}"
            )
    if not owners:
        check.add("skills-ownership", "SKIP", "no owner column parsed")
    elif ownership_problems:
        check.add("skills-ownership", "FAIL", "; ".join(ownership_problems))
    else:
        check.add(
            "skills-ownership",
            "PASS",
            "each owned skill resolves on its owner's cell and is refused "
            "on a cell without it",
            owners=dict(sorted(owners.items())),
        )
    return check


def check_tooling_freshness(
    cli_version_line: str,
    publish_dir: Path,
) -> Check:
    check = Check("tooling-freshness", "Tooling freshness (WARN only)")
    check.note = (
        "the sandbox CLI authors use and the briefs it packages; the "
        "republish itself stays manual."
    )
    source = (REPO / "src/BotArena.Toolchain/BotProject.cs").read_text(
        encoding="utf-8"
    )
    match = re.search(r'CliVersion\s*=\s*"([^"]+)"', source)
    source_version = match.group(1) if match else None
    if source_version is None:
        check.add("source-version", "WARN", "no CliVersion found in source")
        return check
    if not publish_dir.exists():
        check.add(
            "published-cli",
            "WARN",
            f"no published CLI at {rel(publish_dir)}; authors have nothing "
            f"to run (source CliVersion {source_version})",
        )
        return check
    published_version = None
    binary = publish_dir / "botarena"
    if binary.exists() and os.access(binary, os.X_OK):
        proc = run([str(binary), "--version"])
        if proc.returncode == 0:
            published = re.search(r"nilbots (\S+)", proc.stdout)
            published_version = published.group(1) if published else None
    if published_version is None:
        deps = publish_dir / "botarena.deps.json"
        if deps.exists():
            found = re.search(
                r'"botarena/([^"]+)"', deps.read_text(encoding="utf-8")
            )
            published_version = found.group(1) if found else None
    if published_version is None:
        check.add(
            "published-cli",
            "WARN",
            f"cannot read the published CLI version in {rel(publish_dir)}",
        )
    elif published_version != source_version:
        check.add(
            "published-cli",
            "WARN",
            f"published {published_version} != source {source_version}; "
            "republish before commissioning authors",
        )
    else:
        check.add(
            "published-cli",
            "PASS",
            f"published CLI is {published_version}, matching source",
            publishedVersion=published_version,
            sourceVersion=source_version,
        )
    stale = []
    for packaged in sorted(publish_dir.glob("*.md")):
        origin = REPO / "docs" / packaged.name
        if not origin.exists():
            continue
        if file_sha256(origin) != file_sha256(packaged):
            stale.append(packaged.name)
    if stale:
        check.add(
            "packaged-briefs",
            "WARN",
            "packaged brief(s) differ from docs/: " + ", ".join(stale),
            staleBriefs=stale,
        )
    else:
        check.add(
            "packaged-briefs",
            "PASS",
            "packaged briefs match docs/",
        )
    check.add(
        "gate-cli",
        "PASS",
        f"gate ran against {cli_version_line}",
    )
    return check


# --------------------------------------------------------------------------
# reporting
# --------------------------------------------------------------------------


STATUS_ORDER = {"FAIL": 0, "WARN": 1, "SKIP": 2, "PASS": 3}


def frozen_fault_arms(checks: list[Check]) -> list[str]:
    """Arms where the frozen cohort artifact could not play at all — a PASS
    there means `rebuild first`, which the launch checklist must act on."""
    for check in checks:
        if check.check_id != "foreign-doctrine":
            continue
        return sorted(
            item["name"] for item in check.items if item.get("frozenFaulted")
        )
    return []


def print_report(
    checks: list[Check],
    arms: list[dict[str, Any]],
    cli_info: dict[str, Any],
    rebuild_rows: list[dict[str, Any]],
) -> None:
    print("PRE-FLIGHT AUTHORING GATE — agent-arena SS1.9")
    print(f"CLI:  {cli_info['version']}")
    print(f"      {cli_info['command']}")
    print(
        "Arms: "
        + ", ".join(f"{arm['name']} [{arm['spec'] or 'no flags'}]" for arm in arms)
    )
    print()
    width = max(
        [len(item["name"]) for check in checks for item in check.items]
        + [len(check.check_id) for check in checks]
        + [8]
    )
    for number, check in enumerate(checks, start=1):
        print(f"{number}. {check.status:<4}  {check.check_id} — {check.title}")
        if check.note:
            print(f"        ({check.note})")
        for item in check.items:
            print(
                f"        {item['status']:<4}  {item['name']:<{width}}  "
                f"{item['detail']}"
            )
        print()
    if rebuild_rows:
        print("Rebuilt artifacts (frozen source → scratch out/):")
        name_width = max(len(row["name"]) for row in rebuild_rows)
        print(
            f"  {'bot':<{name_width}}  {'frozen hash':<18}  "
            f"{'rebuilt hash':<18}  state"
        )
        for row in sorted(rebuild_rows, key=lambda entry: entry["name"]):
            old = (row.get("oldHash") or "-")[:16]
            new = (row.get("newHash") or "-")[:16]
            state = (
                "FAILED"
                if row["status"] == "FAIL"
                else ("changed" if row.get("hashChanged") else "identical")
            )
            print(
                f"  {row['name']:<{name_width}}  {old:<18}  {new:<18}  {state}"
            )
        print()
    stale_arms = frozen_fault_arms(checks)
    if stale_arms:
        print("Findings that the launch checklist must act on:")
        print(
            "  frozen cohort artifacts fault on: "
            + ", ".join(stale_arms)
            + " — every returning artifact must be REBUILT from source "
            "before it can play those arms."
        )
        print()
    failed = [check for check in checks if check.status == "FAIL"]
    warned = [check for check in checks if check.status == "WARN"]
    verdict = "GATE FAIL" if failed else "GATE PASS"
    print(
        f"{verdict}: {len(checks) - len(failed) - len(warned)} pass, "
        f"{len(warned)} warn, {len(failed)} fail"
    )
    if failed:
        print(
            "  do not commission authors until these are fixed: "
            + ", ".join(check.check_id for check in failed)
        )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--arm",
        action="append",
        required=True,
        metavar="[NAME=]FLAGS",
        help=(
            "One arm as literal `experiment frontline-labs` flags; repeat "
            'per arm. Empty flags mean the control contract. Example: --arm '
            '"--pendulum ratchet".'
        ),
    )
    parser.add_argument(
        "--scaffold",
        default="new",
        help=(
            "`new` (default) runs `nilbots new --profile generic-actor` into "
            "the work dir, or pass an existing generic-actor project dir."
        ),
    )
    parser.add_argument(
        "--scaffold-name",
        default="PreflightStarter",
        help="Project name for the generated scaffold.",
    )
    parser.add_argument(
        "--work",
        type=Path,
        default=Path("sandbox/preflight-gate"),
        help="Scratch root for the scaffold and every evidence directory.",
    )
    parser.add_argument(
        "--rebuild-root",
        type=Path,
        default=Path("sandbox/preflight-rebuilds"),
        help=(
            "Where frozen cohort sources are rebuilt. Never the frozen tree."
        ),
    )
    parser.add_argument(
        "--cohort",
        type=Path,
        default=Path(DEFAULT_COHORT),
        help="Frozen cohort whose artifacts the wave will meet.",
    )
    parser.add_argument(
        "--brief",
        type=Path,
        default=Path(DEFAULT_CLASSES_BRIEF),
        help="Player brief whose token tables are checked against the CLI.",
    )
    parser.add_argument(
        "--rebuild-arm",
        default="--pendulum ratchet",
        help=(
            "Arm the rebuilt artifacts must play cleanly — the one frozen "
            "artifacts fault on."
        ),
    )
    parser.add_argument(
        "--publish-dir",
        type=Path,
        default=Path("sandbox/cli-publish"),
        help="Published CLI the authors will run.",
    )
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument(
        "--max-idle-streak",
        type=int,
        default=40,
        help="Consecutive all-wait decision ticks that count as a freeze.",
    )
    parser.add_argument(
        "--cli",
        help=(
            "CLI command to drive (default: build src/BotArena.Cli and use "
            'its Debug binary). Example: --cli "dotnet run --project '
            'src/BotArena.Cli --".'
        ),
    )
    parser.add_argument(
        "--no-build-cli",
        action="store_true",
        help="Skip the dotnet build of the CLI (use the existing binary).",
    )
    parser.add_argument(
        "--reuse-rebuilds",
        action="store_true",
        help="Keep existing scratch rebuilds instead of recopying/rebuilding.",
    )
    parser.add_argument(
        "--skip-rebuilds",
        action="store_true",
        help=(
            "Skip check 4 entirely (fast rerun); check 3 then has no rebuilt "
            "fallback."
        ),
    )
    parser.add_argument("--json", type=Path, help="Write the JSON report.")
    args = parser.parse_args(argv)

    arms = [parse_arm(raw) for raw in args.arm]
    names = [arm["name"] for arm in arms]
    if len(set(names)) != len(names):
        raise GateError(f"duplicate arm names: {sorted(names)}")
    rebuild_arm = parse_arm(args.rebuild_arm)

    cli, cli_info = resolve_cli(args.cli, build=not args.no_build_cli)
    work = (REPO / args.work).resolve()
    work.mkdir(parents=True, exist_ok=True)
    cohort = (REPO / args.cohort).resolve()
    rebuild_root = (REPO / args.rebuild_root).resolve()
    rebuild_root.mkdir(parents=True, exist_ok=True)

    # Scaffold: either a fresh `nilbots new` starter or a named project.
    if args.scaffold == "new":
        scaffold = work / args.scaffold_name
        if scaffold.exists():
            shutil.rmtree(scaffold)
        created = cli.run(
            ["new", args.scaffold_name, "--profile", "generic-actor"],
            cwd=work,
        )
        write_log(work / "evidence" / "scaffold-new.log", created)
        if created.returncode != 0 or not scaffold.exists():
            raise GateError(
                f"`nilbots new` failed: {first_error_line(created)}"
            )
        scaffold_mode = "new"
    else:
        scaffold = (REPO / args.scaffold).resolve()
        if not (scaffold / "botarena.json").exists():
            raise GateError(f"{rel(scaffold)} is not a bot project")
        scaffold_mode = "project"

    evidence = work / "evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    checks: list[Check] = []

    contract_check, _ = check_contract_truth(
        cli, arms, evidence / "contract"
    )
    checks.append(contract_check)

    checks.append(
        check_scaffold_self_match(
            cli,
            arms,
            scaffold,
            evidence / "self-match",
            args.seed,
            args.max_idle_streak,
        )
    )

    # The starter's own WASM artifact backs checks 3 and 4.
    scaffold_build = cli.run(["build", str(scaffold)])
    write_log(evidence / "scaffold-build.log", scaffold_build)
    scaffold_artifact = scaffold / "out" / "bot.wasm"
    if scaffold_build.returncode != 0 or not scaffold_artifact.exists():
        raise GateError(
            "the scaffold does not build to WASM, which blocks checks 3-4: "
            f"{first_error_line(scaffold_build)}"
        )

    rebuilt: dict[str, Path] = {}
    if args.skip_rebuilds:
        skipped = Check("artifact-rebuild", "Frozen-cohort artifact rebuild")
        skipped.add("cohort", "SKIP", "--skip-rebuilds")
        rebuild_check = skipped
    else:
        rebuild_check, rebuilt = check_artifact_rebuild(
            cli,
            cohort,
            rebuild_root,
            evidence / "rebuild",
            rebuild_arm,
            scaffold_artifact,
            args.seed,
            args.max_idle_streak,
            args.reuse_rebuilds,
        )

    frozen_bots = sorted(
        path.name
        for path in cohort.iterdir()
        if path.is_dir() and (path / "out" / "bot.wasm").exists()
    )
    if not frozen_bots:
        raise GateError(f"no frozen artifacts under {rel(cohort)}")
    frozen_name = frozen_bots[0]
    foreign_check = check_foreign_doctrine(
        cli,
        arms,
        scaffold_artifact,
        frozen_name,
        cohort / frozen_name / "out" / "bot.wasm",
        rebuilt,
        evidence / "foreign",
        args.seed,
        args.max_idle_streak,
    )
    checks.append(foreign_check)
    checks.append(rebuild_check)
    checks.append(
        check_doc_contract(cli, (REPO / args.brief).resolve(), evidence / "doc")
    )
    checks.append(
        check_tooling_freshness(
            cli_info["version"], (REPO / args.publish_dir).resolve()
        )
    )

    ordered = sorted(
        checks,
        key=lambda check: [
            "contract-truth",
            "scaffold-self-match",
            "foreign-doctrine",
            "artifact-rebuild",
            "doc-contract",
            "tooling-freshness",
        ].index(check.check_id),
    )
    rebuild_rows = [
        item for item in rebuild_check.items if item["name"] != "cohort"
    ]
    print_report(ordered, arms, cli_info, rebuild_rows)

    failed = [check.check_id for check in ordered if check.status == "FAIL"]
    report = {
        "schemaVersion": SCHEMA_VERSION,
        "gate": GATE_ID,
        "cli": cli_info,
        "scaffold": {"mode": scaffold_mode, "path": rel(scaffold)},
        "cohort": rel(cohort),
        "rebuildRoot": rel(rebuild_root),
        "rebuildArm": rebuild_arm,
        "seed": args.seed,
        "maxIdleStreak": args.max_idle_streak,
        "arms": arms,
        "checks": [check.to_json() for check in ordered],
        "summary": {
            "pass": sum(1 for check in ordered if check.status == "PASS"),
            "warn": sum(1 for check in ordered if check.status == "WARN"),
            "fail": len(failed),
            "skip": sum(1 for check in ordered if check.status == "SKIP"),
            "failedChecks": failed,
            "frozenArtifactFaultArms": frozen_fault_arms(ordered),
        },
    }
    if args.json:
        destination = (REPO / args.json).resolve()
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(
            json.dumps(report, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        print()
        print(f"JSON: {rel(destination)}")
    return 1 if failed else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (GateError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2)
