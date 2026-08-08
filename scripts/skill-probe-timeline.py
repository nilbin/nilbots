"""Reports the class-skill kit's adoption-grade mechanisms as timelines.

Usage: python3 scripts/skill-probe-timeline.py <replay.json>

Reads a replay v3 and reports the adoption-kit mechanisms as timelines.

Everything here comes out of the replay's own events, so it is evidence about
the rule rather than about the probe: a cast is an attack followed by an
automatic-threshold-return in the same tick, a shatter is the third deflection
by one guard doing the same, and a bend is a traversal whose shot program
carries a non-zero bend count.
"""
import json
import sys
from collections import defaultdict

path = sys.argv[1]
with open(path) as handle:
    replay = json.load(handle)

ticks = replay["ticks"]


def actor(value):
    return f"{value['teamId']}/{value['unitId']}/{value['lifeId']}"


def events(frame):
    for item in frame.get("tickStartEvents", []) + frame.get("events", []):
        yield item


def payload(item):
    return item.get("payload", item)


print(f"replay: {path}")
print(f"ticks: {len(ticks)}  hash: {replay.get('matchHash', '?')}")

# --- VOLLEY: the cast timeline -------------------------------------------
casts = []
entries = {}
for frame in ticks:
    tick = frame["tick"]
    for item in events(frame):
        body = payload(item)
        kind = body.get("kind")
        if kind != "form-transition":
            continue
        who = actor(body["actorId"])
        if item["kind"] == "form-transition-started" and "volley-stance" in body["toFormId"]:
            entries[who] = tick
        if (
            item["kind"] == "form-transition-started"
            and body.get("reason") == "automatic-threshold-return"
            and "volley-stance" in body["fromFormId"]
        ):
            fired = [
                payload(other)
                for other in events(frame)
                if other["kind"] == "attack"
                and actor(payload(other)["actorId"]) == who
            ]
            completed = [
                other
                for other in events(frame)
                if other["kind"] == "form-transition-completed"
                and actor(payload(other)["actorId"]) == who
                and payload(other).get("reason") == "automatic-threshold-return"
            ]
            casts.append(
                {
                    "actor": who,
                    "enterStarted": entries.get(who),
                    "fireTick": tick,
                    "bolts": len(fired),
                    "returnStarted": body["startedTick"],
                    "returnDue": body["dueTick"],
                    "returnCompleted": tick if completed else None,
                }
            )

print(f"\nVOLLEY casts: {len(casts)}")
for cast in casts[:6]:
    print(
        f"  {cast['actor']}: enter started t{cast['enterStarted']} -> "
        f"fire t{cast['fireTick']} ({cast['bolts']} bolts) -> "
        f"auto-return started t{cast['returnStarted']} due t{cast['returnDue']} "
        f"completed t{cast['returnCompleted']}"
    )

# --- SHELL: the shatter timeline -----------------------------------------
running = defaultdict(int)
shatters = []
per_guard = defaultdict(list)
for frame in ticks:
    tick = frame["tick"]
    for item in events(frame):
        body = payload(item)
        if item["kind"] == "projectile-deflected":
            who = actor(body["targetActorId"])
            running[who] += 1
            per_guard[who].append((tick, running[who]))
        if (
            item["kind"] == "form-transition-started"
            and body.get("reason") == "automatic-threshold-return"
            and "aegis-shell" in body["fromFormId"]
        ):
            who = actor(body["actorId"])
            completed = [
                other
                for other in events(frame)
                if other["kind"] == "form-transition-completed"
                and actor(payload(other)["actorId"]) == who
            ]
            shatters.append(
                {
                    "actor": who,
                    "deflections": list(per_guard[who]),
                    "breakTick": tick,
                    "returnCompleted": tick if completed else None,
                }
            )
            running[who] = 0
            per_guard[who] = []
        if item["kind"] == "form-transition-completed" and "aegis-shell" in body["toFormId"]:
            who = actor(body["actorId"])
            running[who] = 0
            per_guard[who] = []

print(f"\nAEGIS SHELL shatters: {len(shatters)}")
for shatter in shatters[:6]:
    marks = ", ".join(f"#{n}@t{t}" for t, n in shatter["deflections"])
    print(
        f"  {shatter['actor']}: deflections {marks} -> "
        f"forced return t{shatter['breakTick']} "
        f"completed t{shatter['returnCompleted']}"
    )
    window = [
        payload(item)
        for frame in ticks
        if shatter["breakTick"] < frame["tick"] <= shatter["breakTick"] + 4
        for item in events(frame)
        if item["kind"] == "damage"
        and actor(payload(item)["targetActorId"]) == shatter["actor"]
    ]
    print(
        f"      punish window t{shatter['breakTick'] + 1}..t{shatter['breakTick'] + 4}: "
        f"{len(window)} hits taken while unshielded"
    )

# --- BEND: a curve actually fired ----------------------------------------
bends = []
for frame in ticks:
    for traversal in frame.get("traversals", []):
        program = traversal.get("shotProgram")
        if not program or not program.get("bendCount"):
            continue
        bends.append(
            {
                "tick": frame["tick"],
                "owner": actor(traversal["ownerActorId"]) if traversal.get("ownerActorId") else "?",
                "profile": traversal.get("attackProfileId"),
                "program": program,
                "launch": traversal.get("launchHeading"),
                "final": traversal.get("finalHeading"),
            }
        )

by_profile = defaultdict(int)
for bend in bends:
    by_profile[bend["profile"]] += 1
print(f"\nBENT shots fired: {len(bends)}")
for profile, count in sorted(by_profile.items()):
    print(f"  {profile}: {count}")
for bend in bends[:6]:
    program = bend["program"]
    print(
        f"  t{bend['tick']} {bend['owner']} via {bend['profile']}: "
        f"bendAfter={program['bendAfterTiles']} dir={program['bendDirection']} "
        f"count={program['bendCount']}  {bend['launch']} -> {bend['final']}"
    )
