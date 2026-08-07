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
import re
from pathlib import Path

KEEP_EVENTS = {"arc-relay", "damage", "destruction"}
DEFAULT_MAX_GZIP_BYTES = 300 * 1024
TARGET_GZIP_BYTES = 100 * 1024

# The longest reason text the broadcast will carry per body. A diagnostic is
# free-form author vocabulary; the panel showing it has one line.
REASON_MAX_CHARS = 48


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


DESTINATION_TAIL = re.compile(r"@(\d+),(\d+)$")


def command_reason(
    message: object,
) -> tuple[str | None, str, tuple[int, int] | None] | None:
    """Split a body's per-tick diagnostic into (order id, action, destination).

    The canonical replay's debug text is dropped by this projection on purpose,
    but "what is this body doing right now" is the one thing in it a spectator
    needs, and a selected body with no answer to that question reads as a bug.

    The tactical playbook writes ``tp:<phase>:<group>:<order>:<action>``, often
    behind a qualifier (``idle-break:tp:...``, ``turn for tp:...``) and usually
    with a ``via <Direction>`` suffix the arena already shows by pointing the
    body that way. Anything the shape does not match is carried WHOLE as the
    action: a transport must not decide that a vocabulary it has not been
    taught is not worth showing.

    A movement action additionally carries the RESOLVED destination as an
    ``@x,y`` tail (``formation-move@12,7``). It is split off here rather than
    shown, because "where is it going" is a place on the map and belongs on the
    map — the viewer draws it for the selected body while the order chip keeps
    reading ``formation-move``.
    """
    if not isinstance(message, str):
        return None
    text = message.split(" via ")[0].strip()
    if not text:
        return None
    marker = text.find("tp:")
    if marker < 0:
        return (None, text[:REASON_MAX_CHARS], None)
    parts = text[marker:].split(":")
    if len(parts) < 5:
        return (None, text[:REASON_MAX_CHARS], None)
    # "turn for idle-break:tp:..." is two stacked qualifiers and a preposition.
    # Keep the words, drop the grammar, and join them the way the tail reads.
    qualifier = "/".join(
        word
        for word in re.split(r"[\s:]+", text[:marker])
        if word and word != "for"
    )
    action = ":".join(parts[4:])
    # Strip the destination BEFORE the length cap, so a long action tail can
    # never silently eat the coordinates off the end of the lens.
    destination = None
    found = DESTINATION_TAIL.search(action)
    if found is not None:
        destination = (int(found.group(1)), int(found.group(2)))
        action = action[: found.start()]
    if qualifier:
        action = f"{qualifier}/{action}"
    return (parts[3] or None, action[:REASON_MAX_CHARS], destination)


def turn_reasons(
    tick: dict,
) -> list[tuple[int, int, int, tuple[str | None, str, tuple[int, int] | None]]]:
    """Every (teamId, unitId, lifeId, reason) this tick recorded, in order."""
    result = []
    for mind in tick.get("mindTurns") or []:
        team_id = mind["teamId"]
        for command in mind.get("commands") or []:
            reason = command_reason(command.get("debugMessage"))
            if reason is not None:
                result.append(
                    (team_id, command["unitId"], command["lifeId"], reason)
                )
    for turn in tick.get("actorTurns") or []:
        submitted = turn.get("submittedDecision") or {}
        reason = command_reason(submitted.get("debugMessage"))
        if reason is not None:
            actor_id = turn["actorId"]
            result.append(
                (
                    actor_id["teamId"],
                    actor_id["unitId"],
                    actor_id["lifeId"],
                    reason,
                )
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
    orders_by_tick = []
    destinations_by_tick = []
    # Last reason PUBLISHED per body, so the column carries changes rather than
    # a full table 600 times. Keyed by life, not by unit: a respawned body
    # restates its order even when it happens to resume the same one, which is
    # what keeps the viewer's running state honest across a death.
    published_reasons: dict[tuple[int, int, int], tuple[str | None, str]] = {}
    # The destination moves on its own clock — a formation slot hanging off a
    # marching anchor changes most ticks while the order holds for hundreds —
    # so it gets its own delta column rather than widening the order row and
    # costing that column its compression.
    published_destinations: dict[
        tuple[int, int, int], tuple[int, int] | None
    ] = {}
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
        orders = []
        destinations = []
        for team_id, unit_id, life_id, reason in turn_reasons(tick):
            key = (team_id, unit_id, life_id)
            order_id, action, destination = reason
            if published_reasons.get(key) != (order_id, action):
                published_reasons[key] = (order_id, action)
                orders.append([team_id, unit_id, order_id, action])
            # A body that stops naming a destination publishes that too: a
            # stale marker left standing over a body now doing something else
            # is worse than no marker at all.
            if (key not in published_destinations
                    or published_destinations[key] != destination):
                published_destinations[key] = destination
                destinations.append(
                    [team_id, unit_id, *(destination or (None, None))]
                )
        orders_by_tick.append(orders)
        destinations_by_tick.append(destinations)
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
        # Changes only. The viewer carries the running table forward, so a body
        # holding one order for 200 ticks costs one row, not two hundred.
        "orders": orders_by_tick,
        # Where each body is walking, on the same delta pattern. `[x, y]` is
        # null when the body's reason names no destination.
        "destinations": destinations_by_tick,
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
