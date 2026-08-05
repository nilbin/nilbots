#!/usr/bin/env python3
"""Project a canonical Arc Relay replay-v3 into a spectator broadcast slice.

The canonical replay remains the reproducibility and audit artifact. This
projection deliberately drops private observations, visibility masks, fuel,
debug text, and legality catalogs. It retains the immutable contract and
presentation, authoritative render state, Arc facts, damage/destruction facts,
projectile traversals, per-body chosen outcomes, role labels, and signature
readiness. The web viewer expands this compact transport into its existing
version-neutral replay model.

Output is deterministic gzip only (mtime 0). No gallery needs or should carry
the 100+ MB canonical JSON document.
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path

KEEP_EVENTS = {"arc-relay", "damage", "destruction"}
DEFAULT_MAX_GZIP_BYTES = 300 * 1024
TARGET_GZIP_BYTES = 100 * 1024


def read_json(path: Path) -> dict:
    with path.open("rb") as source:
        prefix = source.read(2)
    opener = gzip.open if prefix == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: replay root must be an object")
    return value


def actor(value: dict | None) -> list[int] | None:
    if value is None:
        return None
    return [value["teamId"], value["unitId"], value["lifeId"]]


def position(value: dict | None) -> list[int] | None:
    if value is None:
        return None
    return [value["x"], value["y"]]


def transition(value: dict | None) -> list | None:
    if value is None:
        return None
    return [
        value["transitionId"],
        value["operationId"],
        value["targetFormId"],
        value["startedTick"],
        value["dueTick"],
    ]


def slot_state(value: dict) -> list:
    kind = value["kind"]
    if kind == "active":
        return [kind, actor(value["actorId"]), value["generation"], value["formId"]]
    if kind == "availability-pending":
        return [kind, value["reason"], value["dueTick"]]
    if kind == "automatic-return-pending":
        return [kind, value["dueTick"], value["targetFormId"], value["generation"]]
    if kind in ("fabrication-pending", "replication-pending"):
        return [
            kind,
            value["dueTick"],
            actor(value["sourceActorId"]),
            value["transitionId"],
            value["operationId"],
            value["targetFormId"],
            position(value["reservedPosition"]),
        ]
    if kind in ("ready", "permanently-dormant"):
        return [kind]
    raise ValueError(f"unknown slot state {kind!r}")


def life(item: dict) -> list:
    return [
        *actor(item["actorId"]),
        item["participantId"],
        item["generation"],
        item["formId"],
        *position(item["position"]),
        item["facing"],
        item["health"],
        item["cooldown"],
        item["energy"],
        item["spawnedAtTick"],
        item["spawnReason"],
        actor(item["parentActorId"]),
        item["sourceTransitionId"],
        item["sourceOperationId"],
        transition(item["pendingSameLifeTransition"]),
    ]


def world(value: dict) -> list:
    participants = [
        [
            item["participantId"],
            item["teamId"],
            item["runtimeFaultCount"],
            item["disqualified"],
            item["classId"],
        ]
        for item in value["participants"]
    ]
    slots = [
        [
            item["teamId"],
            item["unitId"],
            item["participantId"],
            item["nextLifeId"],
            slot_state(item["state"]),
        ]
        for item in value["slots"]
    ]
    lives = [life(item) for item in value["activeLives"]]
    projectiles = [
        [
            item["projectileId"],
            item["ownerParticipantId"],
            item["ownerTeamId"],
            actor(item["ownerActorId"]),
            item["attackProfileId"],
            item["spawnedAtTick"],
            position(item["origin"]),
            position(item["position"]),
            item["launchHeading"],
            item["heading"],
            item["shotProgram"],
            [position(point) for point in item["committedPath"]],
            item["nextPathIndex"],
            item["remainingTiles"],
            item["ticksUntilAdvance"],
        ]
        for item in value["projectiles"]
    ]
    return [
        value["nextTick"],
        value["nextProjectileId"],
        participants,
        slots,
        lives,
        projectiles,
        value["scoreboard"],
        value["mode"],
    ]


def compact_action(value: dict) -> list:
    return [value["actionId"], value["actionCode"], value["arguments"]]


def compact_turns(tick: dict, signature_ids: set[str]) -> list:
    result = []
    for mind in tick.get("mindTurns", []):
        bodies = {
            (body["actorId"]["unitId"], body["actorId"]["lifeId"]): body
            for body in mind["observation"]["bodies"]
        }
        resolutions = {
            (item["unitId"], item["lifeId"]): item
            for item in mind["resolutions"]
        }
        for key, body in sorted(bodies.items()):
            resolution = resolutions[key]["actionResolution"]
            legalities = [
                [entry["actionId"], entry["actionCode"], entry["available"]]
                for entry in body["actionLegalities"]
                if entry["actionId"] in signature_ids
                and entry["allowedByForm"]
            ]
            result.append(
                [
                    actor(body["actorId"]),
                    mind["participantId"],
                    body.get("roleTag"),
                    legalities,
                    compact_action(resolution["acceptedAction"]),
                    compact_action(resolution["validatedAction"]),
                    resolution["outcome"],
                ]
            )
    return result


def project(replay: dict) -> dict:
    header = replay.get("header")
    if not isinstance(header, dict) or header.get("replayVersion") != 3:
        raise ValueError("broadcast input must be replay v3")
    if replay.get("partial") is not False or replay.get("result") is None:
        raise ValueError("broadcast input must be a complete replay")
    replay_hash = replay.get("replayHash")
    if not isinstance(replay_hash, str) or len(replay_hash) != 64:
        raise ValueError("broadcast input needs a canonical replay hash")
    mode = header["contract"]["rules"]["gameMode"]
    if mode.get("kind") != "arc-relay":
        raise ValueError("broadcast input must use Arc Relay")
    signature_ids = {
        signature["actionId"] for signature in mode.get("signatures", [])
    }
    worlds = []
    turns = []
    start_events_by_tick = []
    events_by_tick = []
    traversals_by_tick = []
    births_by_tick = []
    vision_by_tick = []
    previous_actor_ids = {
        tuple(actor(item["actorId"]))
        for item in replay["initialFrame"]["state"]["activeLives"]
    }
    for tick in replay["ticks"]:
        start_events = [
            event for event in tick["tickStart"]["events"]
            if event["kind"] in KEEP_EVENTS
        ]
        events = [
            event for event in tick["events"] if event["kind"] in KEEP_EVENTS
        ]
        # Columnar ordering is intentional. Keeping 600 similar world/mode
        # records adjacent lets gzip's 32 KiB window see their repetition;
        # interleaving a large turn/event block between them costs ~110 KiB on
        # the H0 all-signatures stress replay without changing one fact.
        worlds.append(world(tick["postState"]))
        turns.append(compact_turns(tick, signature_ids))
        start_events_by_tick.append(start_events)
        events_by_tick.append(events)
        traversals_by_tick.append(tick["traversals"])
        tick_start_lives = tick["tickStart"]["state"]["activeLives"]
        births_by_tick.append(
            [
                life(item)
                for item in tick_start_lives
                if tuple(actor(item["actorId"])) not in previous_actor_ids
            ]
        )
        previous_actor_ids = {
            tuple(actor(item["actorId"]))
            for item in tick["postState"]["activeLives"]
        }
        # Per-team visible-tile unions, straight from the recorded
        # observations, so the viewer's fog of war stays replay-honest.
        # Broadcasts drop the private turns; this compact union is all the
        # fog needs.
        team_tiles: dict[int, set] = {}
        for turn in tick.get("mindTurns") or tick.get("actorTurns") or []:
            observation = turn.get("observation") or {}
            team_id = observation.get("teamId", turn.get("teamId"))
            if team_id is None:
                continue
            tiles = team_tiles.setdefault(team_id, set())
            for visible in observation.get("visibleTiles") or []:
                position = visible.get("position", visible)
                tiles.add((position["x"], position["y"]))
        vision_by_tick.append(
            [
                [team_id, [[x, y] for (x, y) in sorted(tiles)]]
                for team_id, tiles in sorted(team_tiles.items())
            ]
        )
    return {
        "broadcastVersion": 1,
        "canonicalReplayHash": replay_hash,
        "header": header,
        "initial": world(replay["initialFrame"]["state"]),
        "worlds": worlds,
        "turns": turns,
        "startEvents": start_events_by_tick,
        "events": events_by_tick,
        "traversals": traversals_by_tick,
        "births": births_by_tick,
        "vision": vision_by_tick,
        "result": replay["result"],
    }


def encode(value: dict) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
    ).encode("utf-8")


def apply_sheet_labels(replay: dict, replay_path: Path) -> None:
    """Label participants by their tactical sheet, when the run receipt is
    beside the replay. Mirror matches otherwise show the same mind name on
    both sides, which makes the spectator view unreadable. The broadcast is
    a presentation artifact; the canonical replay is untouched."""
    run_path = replay_path.parent / "run.json"
    if not run_path.exists():
        return
    try:
        receipt = json.loads(run_path.read_text(encoding="utf-8"))
        by_participant = {
            entry["ParticipantId"]: entry.get("SheetPath")
            for entry in receipt.get("Participants", [])
        }
    except (ValueError, KeyError, TypeError):
        return
    for participant in (
        replay.get("header", {}).get("provenance", {}).get("participants", [])
    ):
        sheet = by_participant.get(participant.get("participantId"))
        if sheet:
            participant["name"] = Path(sheet).stem


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("replay", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--max-gzip-bytes",
        type=int,
        default=DEFAULT_MAX_GZIP_BYTES,
    )
    args = parser.parse_args()
    if args.max_gzip_bytes <= 0:
        raise SystemExit("--max-gzip-bytes must be positive")

    replay = read_json(args.replay)
    apply_sheet_labels(replay, args.replay)
    raw = encode(project(replay))
    compressed = gzip.compress(raw, compresslevel=9, mtime=0)
    if len(compressed) > args.max_gzip_bytes:
        raise SystemExit(
            f"broadcast exceeds ceiling: {len(compressed)} > "
            f"{args.max_gzip_bytes} gzip bytes"
        )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(compressed)
    label = "target met" if len(compressed) <= TARGET_GZIP_BYTES else "within ceiling"
    print(
        f"{args.output}: {len(raw)} raw bytes, {len(compressed)} gzip bytes "
        f"({label}), sha256={hashlib.sha256(compressed).hexdigest()}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
