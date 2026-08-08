#!/usr/bin/env python3
"""How long does a body stand in range before it shoots? - declare read.

Arc Relay's main guns are DECLARED STRIKES: the attack succeeds now and
the ray fires when the windup ends, and since the named-lock ruling the
engine locks the declared body anywhere inside the frozen 90-degree
wedge. A mind that still asks for exact 8-way alignment before declaring
therefore stands in range rotating, and the cost of that is invisible in
a replay unless you count it.

Per team this reports:

  contact windows   a stretch where at least one live enemy sat inside
                    this body's gun reach AND inside the wedge of SOME
                    heading (a shot that exists if the body turns).
  declare latency   ticks from a window opening to the body's first
                    attack in it. Windows that close with no attack at
                    all are counted separately - those are the ones a
                    spectator reads as "it just stood there".
  rotation ticks    ticks inside an open window whose chosen action was
                    a rotation. The dead ticks the gate costs.
  declare fates     every declared strike, attributed at its own
                    resolution tick by the engine's three cancel
                    conditions (GenericActorMatchSession,
                    LaunchMaturedStrikes): the lock DIED, the lock left
                    the FROZEN WEDGE, or the lock is outside the
                    SHOOTER'S OWN vision - and otherwise it resolved.
                    "Lock died" is a kill, not a miss. The other two are
                    the regression check on any change that makes
                    declaring easier: a wider gate that trades resolved
                    shots for boundary declares is a downgrade however
                    many more shots it fires.

Usage: arc-relay-declare-read.py REPLAY.json.gz [--team N] [--json]
"""
from __future__ import annotations

import argparse
import gzip
import json
import statistics
from pathlib import Path


def read_replay(path: Path) -> dict:
    with path.open("rb") as handle:
        magic = handle.read(2)
    opener = gzip.open if magic == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as handle:
        return json.load(handle)


def within_wedge(dx: int, dy: int, ux: int, uy: int) -> bool:
    """The engine's cone membership: within +-45 degrees, boundary inside."""
    return dx * ux + dy * uy >= abs(dx * uy - dy * ux)


HEADINGS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]

HEADING_VECTOR = {
    "north": (0, -1), "north-east": (1, -1), "east": (1, 0),
    "south-east": (1, 1), "south": (0, 1), "south-west": (-1, 1),
    "west": (-1, 0), "north-west": (-1, -1),
}
FACING_VECTOR = {
    "north": (0, -1), "east": (1, 0), "south": (0, 1), "west": (-1, 0),
}


def sees(vision: dict, origin, facing: str, tile) -> bool:
    """The shooter's OWN eyes: range, the point-blank ring, then the quadrant."""
    dx, dy = tile[0] - origin[0], tile[1] - origin[1]
    distance = max(abs(dx), abs(dy))
    if distance > vision["range"]:
        return False
    if distance <= max(1, vision.get("omnidirectionalProximityRange", 0)):
        return True
    if vision.get("shape") != "facing-quadrant":
        return True
    fx, fy = FACING_VECTOR[facing]
    return dx * fx + dy * fy >= abs(dx * fy) + abs(dy * fx)


def line_reaches(rows: list[str], source, target, strict: bool) -> bool:
    """The canonical integer-Bresenham strike line, wall-stopped."""
    x, y = source
    tx, ty = target
    dx, dy = abs(tx - x), abs(ty - y)
    sx = (tx > x) - (tx < x)
    sy = (ty > y) - (ty < y)
    error = dx - dy
    height = len(rows)
    width = len(rows[0]) if height else 0

    def open_tile(px: int, py: int) -> bool:
        return 0 <= px < width and 0 <= py < height and rows[py][px] != "#"

    while (x, y) != (tx, ty):
        doubled = 2 * error
        step_x = step_y = 0
        if doubled > -dy:
            error -= dy
            step_x = sx
        if doubled < dx:
            error += dx
            step_y = sy
        nx, ny = x + step_x, y + step_y
        if not open_tile(nx, ny):
            return False
        if (strict and step_x and step_y
                and not (open_tile(x + step_x, y) and open_tile(x, y + step_y))):
            return False
        x, y = nx, ny
    return True


def gun_of(contract: dict, form_id: str) -> dict | None:
    form = next((f for f in contract["rules"]["forms"]
                 if f["id"] == form_id), None)
    if form is None or not form.get("attackProfileId"):
        return None
    return next((a for a in contract["rules"]["attackProfiles"]
                 if a["id"] == form["attackProfileId"]), None)


def read(replay: dict, team: int) -> dict:
    contract = replay["header"]["contract"]
    rows = contract["map"]["tileRows"]
    ticks = replay["ticks"]

    vision_profiles = {v["id"]: v
                       for v in contract["rules"]["visionProfiles"]}
    forms = {f["id"]: f for f in contract["rules"]["forms"]}
    # Where every body stood at the start of each tick, so a declare can be
    # judged at ITS OWN resolution tick rather than at the tick it was made.
    standing = [
        {(l["actorId"]["teamId"], l["actorId"]["unitId"]):
            (l["position"]["x"], l["position"]["y"])
         for l in tick["tickStart"]["state"]["activeLives"]}
        for tick in ticks
    ]

    declares: dict[int, list[int]] = {}
    fates: dict[str, int] = {}
    for index, tick in enumerate(ticks):
        for event in tick["tickStart"]["events"] + tick["events"]:
            if event["kind"] != "attack":
                continue
            payload = event["payload"]
            actor = payload["actorId"]
            if actor["teamId"] != team:
                continue
            declares.setdefault(event["tick"], []).append(actor["unitId"])
            body = next(
                (l for l in tick["tickStart"]["state"]["activeLives"]
                 if l["actorId"]["teamId"] == team
                 and l["actorId"]["unitId"] == actor["unitId"]), None)
            gun = gun_of(contract, body["formId"]) if body else None
            windup = gun["projectile"].get("strikeWindupTicks", 0) if gun else 0
            if body is None or windup <= 0:
                continue
            arguments = payload["action"]["arguments"]
            heading = next((a["value"] for a in arguments
                            if a["kind"] == "projectile-heading"), None)
            locked = next((a["value"] for a in arguments
                           if a["kind"] == "unit-target"), None)
            if heading is None:
                continue
            if locked is None:
                fates["unnamed (no lock)"] = fates.get("unnamed (no lock)", 0) + 1
                continue
            resolve = index + windup
            if resolve >= len(standing):
                continue
            origin = (body["position"]["x"], body["position"]["y"])
            where = standing[resolve].get((locked["teamId"], locked["unitId"]))
            if where is None:
                fates["lock died"] = fates.get("lock died", 0) + 1
                continue
            ux, uy = HEADING_VECTOR[heading]
            dx, dy = where[0] - origin[0], where[1] - origin[1]
            strict = gun["projectile"].get("diagonalCornersMustBeClear", False)
            if (not within_wedge(dx, dy, ux, uy)
                    or max(abs(dx), abs(dy))
                        > gun["projectile"]["maxTravelTiles"]
                    or not line_reaches(rows, origin, where, strict)):
                fates["left the frozen wedge"] = (
                    fates.get("left the frozen wedge", 0) + 1)
                continue
            profile = vision_profiles.get(
                forms[body["formId"]].get("visionProfileId"))
            if profile is not None and not sees(
                    profile, origin, body["facing"], where):
                fates["outside shooter vision"] = (
                    fates.get("outside shooter vision", 0) + 1)
                continue
            fates["resolved"] = fates.get("resolved", 0) + 1

    windows: list[tuple[int, int | None]] = []
    silent = 0
    rotations = 0
    contact_ticks = 0
    open_at: dict[int, int] = {}
    declared_in_window: set[int] = set()

    for index, tick in enumerate(ticks):
        number = tick["tick"]
        lives = tick["tickStart"]["state"]["activeLives"]
        mine = [l for l in lives if l["actorId"]["teamId"] == team]
        theirs = [l for l in lives if l["actorId"]["teamId"] != team]
        chosen = {}
        for turn in tick.get("mindTurns") or []:
            if turn["teamId"] != team:
                continue
            for item in turn.get("resolutions") or []:
                chosen[item["unitId"]] = (
                    item["actionResolution"]["acceptedAction"]["actionId"])

        for body in mine:
            unit = body["actorId"]["unitId"]
            gun = gun_of(contract, body["formId"])
            if gun is None:
                continue
            reach = gun["projectile"]["maxTravelTiles"]
            strict = gun["projectile"].get("diagonalCornersMustBeClear", False)
            here = (body["position"]["x"], body["position"]["y"])
            in_contact = False
            for enemy in theirs:
                there = (enemy["position"]["x"], enemy["position"]["y"])
                dx, dy = there[0] - here[0], there[1] - here[1]
                if max(abs(dx), abs(dy)) > reach or (dx == 0 and dy == 0):
                    continue
                if not any(within_wedge(dx, dy, ux, uy) for ux, uy in HEADINGS):
                    continue
                if line_reaches(rows, here, there, strict):
                    in_contact = True
                    break
            if in_contact:
                contact_ticks += 1
                if unit not in open_at:
                    open_at[unit] = number
                    declared_in_window.discard(unit)
                if unit in declares.get(number, []):
                    if unit not in declared_in_window:
                        windows.append((open_at[unit], number - open_at[unit]))
                        declared_in_window.add(unit)
                elif unit not in declared_in_window and chosen.get(unit) == "rotate":
                    rotations += 1
            elif unit in open_at:
                if unit not in declared_in_window:
                    silent += 1
                open_at.pop(unit)
                declared_in_window.discard(unit)

    for unit in list(open_at):
        if unit not in declared_in_window:
            silent += 1

    latencies = [value for _, value in windows]
    return {
        "team": team,
        "contactTicks": contact_ticks,
        "windowsWithDeclare": len(windows),
        "windowsSilent": silent,
        "medianLatency": statistics.median(latencies) if latencies else None,
        "meanLatency": round(statistics.fmean(latencies), 2) if latencies else None,
        "latencyTicks": sum(latencies),
        "rotationTicksInContact": rotations,
        "strikeDeclares": sum(fates.values()),
        "fates": fates,
        "resolvedShare": (
            round(fates.get("resolved", 0) / sum(fates.values()), 4)
            if fates else None),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("replay", type=Path)
    parser.add_argument("--team", type=int, default=None)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    replay = read_replay(args.replay)
    teams = [args.team] if args.team is not None else [0, 1]
    results = [read(replay, team) for team in teams]
    if args.json:
        print(json.dumps(results))
        return 0
    for result in results:
        print(f"team {result['team']}: "
              f"{result['contactTicks']} contact body-ticks, "
              f"{result['windowsWithDeclare']} windows declared "
              f"(median {result['medianLatency']}t, "
              f"mean {result['meanLatency']}t), "
              f"{result['windowsSilent']} closed silent, "
              f"{result['rotationTicksInContact']} rotation ticks in contact, "
              f"{result['strikeDeclares']} strike declares "
              f"(resolved {result['resolvedShare']}; "
              + ", ".join(f"{value} {name}"
                          for name, value in sorted(result["fates"].items()))
              + ")")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
