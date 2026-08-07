#!/usr/bin/env python3
"""Strike-cancel audit (DECISIONS #215, #221).

    scripts/arc-relay-strike-cancel-audit.py <cell>/replay.json.gz ...

Walks raw replay v3 documents, reconstructs every declared strike from the
pendingStrike wire state, decides whether the engine RESOLVED or CANCELLED
it, and independently recomputes the three lawful cancel rules:

  (a) the lock died,
  (b) the lock left the frozen wedge,
  (c) the lock left the shooter's line of sight (VisibleTilesFor).

plus the dead-shooter precedent (the declaring life itself is gone).

Two LOS models are evaluated, because the engine's projection caches
VisibleTilesFor at tick start:
  stale  - LOS from the shooter's tile/facing at the START of the resolve
           tick (what the cache holds),
  fresh  - LOS from the shooter's tile/facing AFTER this tick's rotation
           and movement.
Any strike whose engine outcome the two models disagree about is reported.
Since #221 rooted the windup a declarer cannot move without abandoning, so
the two models can only differ by a rotation; before it they differed by
whole tiles, and the engine matched `stale` in 90 of 90 cases.

The smoke term of VisibleTilesFor is NOT modelled: a ruleset with an active
smoke canister will report false sight-loss disagreements.
"""

import gzip
import json
import sys
from collections import Counter, defaultdict

HEADINGS = {
    "north": (0, -1),
    "north-east": (1, -1),
    "east": (1, 0),
    "south-east": (1, 1),
    "south": (0, 1),
    "south-west": (-1, 1),
    "west": (-1, 0),
    "north-west": (-1, -1),
}
FACINGS = {"north": (0, -1), "east": (1, 0), "south": (0, 1), "west": (-1, 0)}


def chebyshev(a, b):
    return max(abs(a[0] - b[0]), abs(a[1] - b[1]))


def supercover(a, b):
    """Engine Visibility.SupercoverLine, corner-strict."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    nx, ny = abs(dx), abs(dy)
    sx = (dx > 0) - (dx < 0)
    sy = (dy > 0) - (dy < 0)
    x, y = ax, ay
    out = [(x, y)]
    ix = iy = 0
    while ix < nx or iy < ny:
        cross_x = (1 + 2 * ix) * ny
        cross_y = (1 + 2 * iy) * nx
        if cross_x == cross_y:
            out.append((x + sx, y))
            out.append((x, y + sy))
            x += sx
            y += sy
            ix += 1
            iy += 1
        elif cross_x < cross_y:
            x += sx
            ix += 1
        else:
            y += sy
            iy += 1
        out.append((x, y))
    return out


def in_quadrant(dx, dy, facing):
    fx, fy = FACINGS[facing]
    forward = dx * fx + dy * fy
    lateral = abs(dx * fy) + abs(dy * fx)
    return forward >= 0 and lateral <= forward


def in_cone(origin, target, facing):
    dx, dy = target[0] - origin[0], target[1] - origin[1]
    if max(abs(dx), abs(dy)) <= 1:
        return True
    return in_quadrant(dx, dy, facing)


class Arena:
    def __init__(self, doc):
        contract = doc["header"]["contract"]
        self.map = contract["map"]
        rows = self.map["tileRows"]
        self.width = self.map["width"]
        self.height = self.map["height"]
        self.walls = {
            (x, y)
            for y, row in enumerate(rows)
            for x, ch in enumerate(row)
            if ch == "#"
        }
        rules = contract["rules"]
        self.vision = {v["id"]: v for v in rules["visionProfiles"]}
        self.attacks = {a["id"]: a for a in rules["attackProfiles"]}
        self.forms = {f["id"]: f for f in rules["forms"]}
        self._los_cache = {}

    def is_wall(self, p):
        return p in self.walls or not (
            0 <= p[0] < self.width and 0 <= p[1] < self.height
        )

    def visible_tiles(self, origin, facing, form_id):
        """GenericActorMatchSession.VisibleTilesFor, smoke-free."""
        vision = self.vision[self.forms[form_id]["visionProfileId"]]
        key = (origin, facing, vision["id"], vision["range"])
        cached = self._los_cache.get(key)
        if cached is not None:
            return cached
        rng = vision["range"]
        shape = vision["shape"]
        omni = vision["omnidirectionalProximityRange"]
        visible = set()
        for y in range(max(0, origin[1] - rng), min(self.height - 1, origin[1] + rng) + 1):
            for x in range(max(0, origin[0] - rng), min(self.width - 1, origin[0] + rng) + 1):
                target = (x, y)
                distance = chebyshev(origin, target)
                if (
                    shape == "facing-quadrant"
                    and distance > omni
                    and not in_cone(origin, target, facing)
                ):
                    continue
                ray = supercover(origin, target)
                if any(
                    p != origin and p != target and p in self.walls for p in ray
                ):
                    continue
                visible.add(target)
        self._los_cache[key] = visible
        return visible

    def strike_line(self, origin, target, diagonal_corners_must_be_clear):
        """GenericActorStrikeCone.LineTo."""
        if origin == target:
            return []
        x, y = origin
        dx = abs(target[0] - x)
        dy = abs(target[1] - y)
        sx = (target[0] > x) - (target[0] < x)
        sy = (target[1] > y) - (target[1] < y)
        error = dx - dy
        path = []
        while (x, y) != target:
            doubled = 2 * error
            step_x = step_y = 0
            if doubled > -dy:
                error -= dy
                step_x = sx
            if doubled < dx:
                error += dx
                step_y = sy
            nxt = (x + step_x, y + step_y)
            if self.is_wall(nxt) or (
                step_x != 0
                and step_y != 0
                and diagonal_corners_must_be_clear
                and (
                    self.is_wall((x + step_x, y))
                    or self.is_wall((x, y + step_y))
                )
            ):
                break
            x, y = nxt
            path.append(nxt)
        return path

    def cone_tiles(self, origin, heading, rng, diagonal_corners_must_be_clear):
        """GenericActorStrikeCone.Tiles."""
        ux, uy = HEADINGS[heading]
        tiles = []
        for dy in range(-rng, rng + 1):
            for dx in range(-rng, rng + 1):
                if dx == 0 and dy == 0:
                    continue
                dot = dx * ux + dy * uy
                cross = dx * uy - dy * ux
                if dot < abs(cross):
                    continue
                tile = (origin[0] + dx, origin[1] + dy)
                line = self.strike_line(
                    origin, tile, diagonal_corners_must_be_clear
                )
                if not line or line[-1] != tile:
                    continue
                tiles.append(tile)
        return set(tiles)


def actor_key(a):
    return (a["teamId"], a["unitId"], a["lifeId"])


def pos(p):
    return (p["x"], p["y"])


def strike_key(s):
    return (actor_key(s["shooter"]), s["resolveAtTick"])


def whiff_cause(arena, ticks, entry, strike, shooter_id, wedge):
    """Why a declare locked nothing, read at the tick it was DECLARED.

    Movement resolves before attacks inside a tick, so the mind decides from
    the tick's opening state and the engine reads the declare after everyone
    has stepped. That one phase is where a whiff is usually born, and the
    answers are different problems: the aimed body left the reach wedge
    entirely (the counterplay working), it died mid-tick, nothing was ever
    aimed at (a suppressive declare down an empty lane), or it was STILL
    inside the wedge - which under the named lock (#222b) is a lock that
    should have landed, and on a pre-#222b replay is one the old
    first-enemy-on-the-ray geometry threw away.
    """
    tick = ticks.get(entry["declaredAt"])
    if tick is None or not wedge:
        return "declare tick unavailable"
    origin = pos(strike["origin"])
    rng = max(chebyshev(origin, tile) for tile in wedge)
    ux, uy = HEADINGS[strike["centralHeading"]]
    ray = []
    x, y = origin
    for _ in range(rng):
        x, y = x + ux, y + uy
        if arena.is_wall((x, y)):
            break
        ray.append((x, y))
    opening = {
        actor_key(life["actorId"]): pos(life["position"])
        for life in tick["tickStart"]["state"]["activeLives"]
    }
    current = dict(opening)
    for event in tick["events"]:
        if event["kind"] == "movement":
            who = actor_key(event["payload"]["actorId"])
            if who in current:
                current[who] = pos(event["payload"]["to"])
        elif event["kind"] == "destruction":
            current.pop(actor_key(event["payload"]["actorId"]), None)
    aimed = next(
        (
            who
            for tile in ray
            for who, where in opening.items()
            if where == tile and who[0] != shooter_id[0]
        ),
        None,
    )
    if aimed is None:
        return "nothing was on the aim at the declare tick's opening state"
    if aimed not in current:
        return "the aimed body died inside the declare tick"
    if current[aimed] in wedge:
        return "the aimed body was still inside the frozen wedge"
    return "the aimed body left the reach wedge inside the declare tick"


def analyse(path, verbose=False):
    doc = json.load(gzip.open(path))
    arena = Arena(doc)
    ticks = {t["tick"]: t for t in doc["ticks"]}

    # Every declared strike, keyed by (shooter, resolveAtTick). `declared` is
    # its first appearance in a postState; `last_seen` the final one.
    declares = {}
    for tick in doc["ticks"]:
        for strike in tick["postState"]["mode"].get("pendingStrikes") or []:
            key = strike_key(strike)
            entry = declares.setdefault(
                key, {"declaredAt": tick["tick"], "strike": strike}
            )
            entry["lastSeen"] = tick["tick"]
            entry["strike"] = strike

    findings = []
    counts = Counter()
    for key, entry in sorted(declares.items()):
        strike = entry["strike"]
        resolve_at = strike["resolveAtTick"]
        shooter_id = actor_key(strike["shooter"])
        resolve_tick = ticks.get(resolve_at)
        if resolve_tick is None:
            counts["never-matured (match ended)"] += 1
            continue

        # ---- world state at the strike phase of the resolve tick ----------
        # Order inside a tick: rotations, movement, mode/signature effects,
        # lifecycle, signature bolts, MATURED STRIKES, projectile advance,
        # attacks, deflections, damage. So rotation+movement are applied, and
        # damage-phase destruction is NOT.
        lives = {}
        for life in resolve_tick["tickStart"]["state"]["activeLives"]:
            lives[actor_key(life["actorId"])] = {
                "pos": pos(life["position"]),
                "facing": life["facing"],
                "form": life["formId"],
            }
        start_lives = {k: dict(v) for k, v in lives.items()}

        launch_ordinals = [
            int(t["globalOrdinal"]) for t in resolve_tick["traversals"]
        ] + [
            int(e["globalOrdinal"])
            for e in resolve_tick["events"]
            if e["kind"] == "attack"
        ]
        launch_boundary = min(launch_ordinals) if launch_ordinals else None

        for event in resolve_tick["events"]:
            payload = event["payload"]
            kind = event["kind"]
            ordinal = int(event["globalOrdinal"])
            if kind == "rotation":
                who = actor_key(payload["actorId"])
                if who in lives:
                    lives[who]["facing"] = payload["toFacing"]
            elif kind == "movement":
                who = actor_key(payload["actorId"])
                if who in lives:
                    lives[who]["pos"] = pos(payload["to"])
                    lives[who]["facing"] = payload["facing"]
            elif kind == "destruction":
                who = actor_key(payload["actorId"])
                pre_strike = (
                    payload.get("projectileId") is None
                    if launch_boundary is None
                    else ordinal < launch_boundary
                )
                if pre_strike:
                    lives.pop(who, None)

        # ---- what the engine did -----------------------------------------
        resolved_events = [
            e
            for e in resolve_tick["events"]
            if e["kind"] == "attack"
            and actor_key(e["payload"]["actorId"]) == shooter_id
        ]
        resolved = bool(resolved_events)

        # ---- what the rules say -------------------------------------------
        shooter_now = lives.get(shooter_id)
        shooter_start = start_lives.get(shooter_id)
        # With a one-tick windup the shooter's tick-start state at the resolve
        # tick IS its state at declare, so the cached (stale) LOS is exactly
        # "LOS from the frozen origin, with the declare facing".
        if shooter_start is not None:
            counts[
                "tick-start shooter tile == frozen origin"
                if shooter_start["pos"] == pos(strike["origin"])
                else "TICK-START SHOOTER TILE != FROZEN ORIGIN"
            ] += 1
        locked = strike.get("target")
        wedge = {pos(t) for t in strike["tiles"]}
        record = {
            "replay": path,
            "declaredAt": entry["declaredAt"],
            "resolveAt": resolve_at,
            "shooter": shooter_id,
            "lock": actor_key(locked) if locked else None,
            "origin": pos(strike["origin"]),
            "heading": strike["centralHeading"],
            "resolved": resolved,
            "shooterMoved": bool(
                shooter_now
                and shooter_start
                and shooter_now["pos"] != shooter_start["pos"]
            ),
            "shooterRotated": bool(
                shooter_now
                and shooter_start
                and shooter_now["facing"] != shooter_start["facing"]
            ),
            # #221 abandons on the COMMAND, so a move that resolved Blocked
            # spends the windup while leaving the body where it was.
            "shooterCommandedMove": any(
                event["kind"] in ("movement", "movement-blocked")
                and actor_key(event["payload"]["actorId"]) == shooter_id
                for event in resolve_tick["events"]
            ),
        }

        if shooter_now is None:
            record["cause"] = "shooter-dead"
            counts["cancel: shooter dead"] += 1
            if resolved:
                findings.append(("RESOLVED-WITH-DEAD-SHOOTER", record))
            continue

        if locked is None:
            # An unlocked declare - the mind named nobody the engine could
            # lock (#222 and its 2026-08-08 correction) - fires the
            # theatrical whiff down the centre. The only thing that stops it
            # is the shooter itself: dying, or walking away from its own
            # windup (#221).
            counts["whiff cause: " + whiff_cause(
                arena, ticks, entry, strike, shooter_id, wedge)] += 1
            abandoned = not resolved and record["shooterCommandedMove"]
            record["cause"] = (
                "whiff abandoned by the shooter's move command"
                if abandoned
                else "empty-aim whiff"
            )
            counts[
                "resolved: empty-aim whiff" if resolved
                else "cancel: whiff abandoned by a move" if abandoned
                else "CANCEL: empty-aim whiff with no cause"
            ] += 1
            if not resolved and not abandoned:
                findings.append(("EMPTY-AIM-WHIFF-CANCELLED", record))
            continue

        lock_id = actor_key(locked)
        lock_now = lives.get(lock_id)
        lock_dead = lock_now is None
        in_wedge = (not lock_dead) and lock_now["pos"] in wedge
        stale_los = fresh_los = None
        if not lock_dead:
            stale_los = lock_now["pos"] in arena.visible_tiles(
                shooter_start["pos"], shooter_start["facing"], shooter_start["form"]
            )
            fresh_los = lock_now["pos"] in arena.visible_tiles(
                shooter_now["pos"], shooter_now["facing"], shooter_now["form"]
            )
        record.update(
            {
                "lockDead": lock_dead,
                "inWedge": in_wedge,
                "staleLos": stale_los,
                "freshLos": fresh_los,
                "lockPos": None if lock_dead else lock_now["pos"],
            }
        )

        predicted_stale_cancel = lock_dead or not in_wedge or not stale_los
        predicted_fresh_cancel = lock_dead or not in_wedge or not fresh_los

        if lock_dead:
            cause = "lock dead"
        elif not in_wedge:
            cause = "lock left the wedge"
        elif not stale_los:
            cause = "lock left LOS (tick-start LOS)"
        elif not fresh_los:
            cause = "lock left LOS (post-move LOS only)"
        else:
            cause = "no rule fired"
        record["cause"] = cause

        if lock_id[0] == shooter_id[0]:
            counts["lock was a TEAMMATE (bodyguard geometry)"] += 1
        if record["shooterMoved"]:
            counts[
                "shooter moved on the resolve tick: "
                + ("resolved" if resolved else f"cancel ({cause})")
            ] += 1

        if resolved:
            counts["resolved"] += 1
            if predicted_stale_cancel and predicted_fresh_cancel:
                findings.append(("RESOLVED-BUT-A-RULE-HELD", record))
            elif predicted_stale_cancel != predicted_fresh_cancel:
                findings.append(("RESOLVED-LOS-MODELS-DISAGREE", record))
        else:
            counts[f"cancel: {cause}"] += 1
            if cause == "no rule fired":
                findings.append(("CANCEL-WITH-NO-RULE", record))
            elif predicted_stale_cancel != predicted_fresh_cancel:
                findings.append(("CANCEL-LOS-MODELS-DISAGREE", record))

        # ---- geometry audit: is the frozen wedge the boundary-inclusive
        # 90 degree cone the decision describes? ---------------------------
        if wedge:
            rng = max(chebyshev(pos(strike["origin"]), t) for t in wedge)
            profile = arena.attacks[
                arena.forms[shooter_start["form"]]["attackProfileId"]
            ]
            recomputed = arena.cone_tiles(
                pos(strike["origin"]),
                strike["centralHeading"],
                rng,
                profile["projectile"]["diagonalCornersMustBeClear"],
            )
            if recomputed != wedge:
                findings.append(
                    (
                        "WEDGE-SHAPE-MISMATCH",
                        dict(
                            record,
                            missing=sorted(recomputed - wedge)[:6],
                            extra=sorted(wedge - recomputed)[:6],
                        ),
                    )
                )
            counts["wedge shape verified"] += 1

    return counts, findings


def main(paths):
    total = Counter()
    all_findings = []
    for path in paths:
        counts, findings = analyse(path)
        cell = path.split("/")[-2]
        print(f"== {cell}")
        for name, value in sorted(counts.items()):
            print(f"   {value:5d}  {name}")
        total.update(counts)
        all_findings.extend((cell, name, record) for name, record in findings)
    print("\n== totals")
    for name, value in sorted(total.items()):
        print(f"   {value:5d}  {name}")
    print(f"\n== findings ({len(all_findings)})")
    grouped = defaultdict(list)
    for cell, name, record in all_findings:
        grouped[name].append((cell, record))
    for name, rows in sorted(grouped.items()):
        print(f"-- {name}: {len(rows)}")
        for cell, record in rows[:12]:
            print(f"   {cell} t{record['declaredAt']}->{record['resolveAt']} "
                  f"shooter={record['shooter']} lock={record.get('lock')} "
                  f"lockPos={record.get('lockPos')} origin={record['origin']} "
                  f"heading={record['heading']} moved={record['shooterMoved']} "
                  f"rotated={record['shooterRotated']} wedge={record.get('inWedge')} "
                  f"stale={record.get('staleLos')} fresh={record.get('freshLos')}")


if __name__ == "__main__":
    main(sys.argv[1:])
