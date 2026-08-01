#!/usr/bin/env python3
"""Measure the Gate-3 Arc Relay scorecard from a durable broadcast slice.

The broadcast is spectator-authoritative but intentionally omits private mind
observations and legality masks. Metrics that cannot be observed directly are
named as proximity proxies in both the schema and method notes.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import hashlib
import json
import math
from pathlib import Path
from statistics import mean, median


SCHEMA = "arc-relay-scorecard-v1"
TICKS_PER_SECOND = 5


def read_json(path: Path) -> dict:
    with path.open("rb") as source:
        prefix = source.read(2)
    opener = gzip.open if prefix == b"\x1f\x8b" else open
    with opener(path, "rt", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: JSON root must be an object")
    return value


def actor_key(value: dict | list | None) -> tuple[int, int, int] | None:
    if value is None:
        return None
    if isinstance(value, list):
        return (value[0], value[1], value[2])
    return (value["teamId"], value["unitId"], value["lifeId"])


def core_key(value: dict) -> str:
    return f"{value['sourceWellId']}:{value['sourceOrdinal']}"


def position(value: dict | list) -> tuple[int, int]:
    if isinstance(value, list):
        return (value[0], value[1])
    return (value["x"], value["y"])


def chebyshev(a: tuple[int, int], b: tuple[int, int]) -> int:
    return max(abs(a[0] - b[0]), abs(a[1] - b[1]))


def theater(pos: tuple[int, int]) -> str:
    if pos[1] <= 7:
        return "north"
    if pos[1] >= 15:
        return "south"
    return "centre"


def counter_dict(counter: collections.Counter) -> dict:
    return {str(key): counter[key] for key in sorted(counter, key=str)}


def team_counter(counter: collections.Counter, team_ids: list[int]) -> dict:
    return {str(team): int(counter[team]) for team in team_ids}


def nested_counter(counter: collections.Counter) -> dict:
    result: dict[str, dict[str, int]] = {}
    for key, value in sorted(counter.items(), key=lambda item: str(item[0])):
        outer, inner = key
        result.setdefault(str(outer), {})[str(inner)] = int(value)
    return result


def percentile(values: list[int | float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = math.ceil(fraction * len(ordered)) - 1
    return float(ordered[max(0, min(index, len(ordered) - 1))])


def series_stats(values: list[int | float]) -> dict:
    return {
        "count": len(values),
        "min": min(values) if values else None,
        "median": median(values) if values else None,
        "mean": mean(values) if values else None,
        "p90": percentile(values, 0.9),
        "max": max(values) if values else None,
    }


def entropy_from_counts(values: list[int]) -> float:
    total = sum(values)
    if total <= 0:
        return 0.0
    probabilities = [value / total for value in values if value]
    raw = -sum(value * math.log2(value) for value in probabilities)
    return raw / math.log2(len(values)) if len(values) > 1 else 0.0


def active_lives(world: list) -> list[dict]:
    result = []
    for item in world[4]:
        result.append(
            {
                "actor": (item[0], item[1], item[2]),
                "team": item[0],
                "unit": item[1],
                "form": item[5],
                "position": (item[6], item[7]),
                "health": item[9],
            }
        )
    return result


def scoreboard(world: list) -> dict[int, dict[str, int]]:
    return {
        team["teamId"]: {
            item["channel"]: int(item["value"])
            for item in team["scores"]
        }
        for team in world[6]["teams"]
    }


def mode(world: list) -> dict:
    value = world[7]
    if value.get("kind") != "arc-relay":
        raise ValueError("broadcast world is not Arc Relay")
    return value


def fact(event: dict) -> tuple[str, dict]:
    if event["kind"] == "arc-relay":
        value = event["payload"]["fact"]
        return value["kind"], value
    return event["kind"], event["payload"]


def shortest_distance(
    rows: list[str], start: tuple[int, int], goal: tuple[int, int]
) -> int | None:
    if start == goal:
        return 0
    height = len(rows)
    width = len(rows[0])
    queue = collections.deque([(start, 0)])
    seen = {start}
    for_current = (
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    )
    while queue:
        current, distance = queue.popleft()
        for dx, dy in for_current:
            target = (current[0] + dx, current[1] + dy)
            x, y = target
            if not (0 <= x < width and 0 <= y < height):
                continue
            if rows[y][x] == "#" or target in seen:
                continue
            if target == goal:
                return distance + 1
            seen.add(target)
            queue.append((target, distance + 1))
    return None


def signature_metrics(
    broadcast: dict,
    events_by_tick: list[list[dict]],
    team_ids: list[int],
) -> dict:
    attempts = collections.Counter()
    terminal = collections.Counter()
    counters = collections.Counter()
    useful = collections.Counter()
    terminal_reasons = collections.Counter()
    active_operations: dict[str, tuple[int, str]] = {}

    counter_reasons = {
        "interrupted-damage",
        "interrupted-movement",
        "replaced",
        "destroyed-projectile",
    }
    useful_reasons = {
        "projectile-contact",
        "repair-tick",
        "consumed",
        "segment-consumed",
        "destroyed-projectile",
    }
    for events in events_by_tick:
        for event in events:
            kind, value = fact(event)
            if kind == "signature-changed":
                owner = actor_key(value.get("ownerActorId"))
                if owner is None:
                    continue
                team = owner[0]
                signature_id = value["signatureId"]
                operation = value["operationId"]
                reason = value.get("reason")
                if reason == "started":
                    attempts[(team, signature_id)] += 1
                    active_operations[operation] = (team, signature_id)
                if value.get("phase") is None:
                    terminal[(team, signature_id)] += 1
                    terminal_reasons[(signature_id, reason or "none")] += 1
                    if reason in counter_reasons or str(reason).startswith("interrupted"):
                        counters[(team, signature_id)] += 1
                    active_operations.pop(operation, None)
                if reason in useful_reasons:
                    useful[(team, signature_id)] += 1
            elif kind in ("signature-damage", "signature-repair", "body-relocated"):
                owner = actor_key(value.get("ownerActorId"))
                if owner is not None:
                    useful[(owner[0], value["signatureId"])] += 1
            elif kind == "core-relocated" and value.get("relocationKind") in (
                "forced-displacement",
                "arc-toss-landing",
            ):
                carrier = actor_key(value.get("carrierActorId"))
                if carrier is not None:
                    signature_id = (
                        "arc-toss"
                        if value["relocationKind"] == "arc-toss-landing"
                        else "forced-displacement"
                    )
                    useful[(carrier[0], signature_id)] += 1

    stack_max = collections.Counter()
    stack_body_ticks = collections.Counter()
    for world in broadcast["worlds"]:
        tick_counts = collections.Counter(
            (item["ownerTeamId"], item["signatureId"])
            for item in mode(world)["visibleSignatures"]
        )
        for key, value in tick_counts.items():
            stack_max[key] = max(stack_max[key], value)
            if value > 1:
                stack_body_ticks[key] += value

    by_team = {}
    for team in team_ids:
        signature_ids = sorted(
            {
                signature
                for collection in (attempts, terminal, counters, useful, stack_max)
                for candidate_team, signature in collection
                if candidate_team == team
            }
        )
        by_team[str(team)] = {
            signature: {
                "attempts": attempts[(team, signature)],
                "terminalTransitions": terminal[(team, signature)],
                "counteredOrReplaced": counters[(team, signature)],
                "usefulEffectFacts": useful[(team, signature)],
                "maxVisibleStack": stack_max[(team, signature)],
                "stackedBodyTicks": stack_body_ticks[(team, signature)],
            }
            for signature in signature_ids
        }
    return {
        "byTeamAndSignature": by_team,
        "terminalReasonsBySignature": nested_counter(terminal_reasons),
        "operationsStillVisibleAtEnd": len(active_operations),
    }


def measure(broadcast: dict, record: dict | None, source_path: Path) -> dict:
    if broadcast.get("broadcastVersion") != 1:
        raise ValueError("expected Arc Relay broadcast v1")
    header = broadcast["header"]
    rules = header["contract"]["rules"]["gameMode"]
    if rules.get("kind") != "arc-relay":
        raise ValueError("expected Arc Relay rules")
    team_ids = sorted(item["teamId"] for item in header["contract"]["topology"]["teams"])
    class_by_slot = {
        (item["teamId"], item["unitId"]): item["classId"]
        for item in header["contract"]["topology"]["unitSlots"]
    }
    compositions = {
        str(team): [
            class_by_slot[(team, unit)]
            for unit in sorted(
                unit
                for candidate_team, unit in class_by_slot
                if candidate_team == team
            )
        ]
        for team in team_ids
    }
    entrant_by_team = {}
    if record:
        entrant_by_team = {
            str(item["teamId"]): item["entrantId"]
            for item in record.get("participants", [])
        }

    all_events = [
        [*broadcast["startEvents"][tick], *broadcast["events"][tick]]
        for tick in range(len(broadcast["worlds"]))
    ]
    final_result = broadcast["result"]
    end_tick = int(final_result["endTick"])
    scheduled_births = sum(
        1
        for well in rules["wells"]
        for tick in range(
            well["firstBirthTick"],
            min(end_tick, well["finalBirthTick"]) + 1,
            well["cadenceTicks"],
        )
    )

    births = collections.Counter()
    birth_access = collections.Counter()
    pending_ticks = collections.Counter()
    rearm_ticks = collections.Counter()
    live_core_histogram = collections.Counter()
    possession_ticks = collections.Counter()
    possession_class_ticks = collections.Counter()
    body_ticks = collections.Counter()
    camp_body_ticks = collections.Counter()
    convoy_samples = collections.Counter()
    convoy_escort_sum = collections.Counter()
    convoy_escort_max = collections.Counter()
    role_ticks = collections.Counter()
    role_deaths = collections.Counter()
    theater_transitions = collections.Counter()
    active_theaters_histogram = collections.Counter()
    previous_theater_by_actor: dict[tuple[int, int, int], str] = {}
    role_by_actor: dict[tuple[int, int, int], str | None] = {}

    # Populate published roles before reading destruction facts on each tick.
    for tick, world in enumerate(broadcast["worlds"]):
        lives = active_lives(world)
        lives_by_team = {
            team: [life for life in lives if life["team"] == team]
            for team in team_ids
        }
        tick_roles = {
            actor_key(turn[0]): turn[2]
            for turn in broadcast["turns"][tick]
        }
        for actor, role in tick_roles.items():
            if actor is not None:
                role_by_actor[actor] = role
                if role:
                    role_ticks[(actor[0], role)] += 1

        tick_theaters = collections.defaultdict(set)
        for life in lives:
            team = life["team"]
            current_theater = theater(life["position"])
            body_ticks[(team, current_theater)] += 1
            tick_theaters[team].add(current_theater)
            actor = life["actor"]
            prior = previous_theater_by_actor.get(actor)
            if prior is not None and prior != current_theater:
                theater_transitions[team] += 1
            previous_theater_by_actor[actor] = current_theater
            if (team == 0 and life["position"][0] >= 21) or (
                team == 1 and life["position"][0] <= 9
            ):
                camp_body_ticks[team] += 1
        for team in team_ids:
            active_theaters_histogram[(team, len(tick_theaters[team]))] += 1

        current_mode = mode(world)
        live_core_histogram[len(current_mode["visibleCores"])] += 1
        for well in current_mode["wells"]:
            if well["pendingCharge"]:
                pending_ticks[well["wellId"]] += 1
            if well["rearmCompletesAtTick"] is not None:
                rearm_ticks[well["wellId"]] += 1
        for core in current_mode["visibleCores"]:
            carrier = actor_key(core.get("carrierActorId"))
            if core["disposition"] != "carried" or carrier is None:
                continue
            team = carrier[0]
            possession_ticks[team] += 1
            possession_class_ticks[(team, class_by_slot[(team, carrier[1])])] += 1
            escorts = sum(
                1
                for life in lives_by_team[team]
                if life["actor"] != carrier
                and chebyshev(life["position"], position(core["position"])) <= 2
            )
            convoy_samples[team] += 1
            convoy_escort_sum[team] += escorts
            convoy_escort_max[team] = max(convoy_escort_max[team], escorts)

        for event in all_events[tick]:
            kind, value = fact(event)
            if kind == "core-born":
                source_id = value["coreId"]["sourceWellId"]
                births[source_id] += 1
                near_teams = {
                    life["team"]
                    for life in lives
                    if chebyshev(life["position"], position(value["position"])) <= 3
                }
                label = (
                    "contested"
                    if len(near_teams) >= 2
                    else "uncontested"
                    if len(near_teams) == 1
                    else "unattended"
                )
                birth_access[(source_id, label)] += 1
            elif kind == "destruction":
                destroyed = actor_key(value["actorId"])
                if destroyed is not None and role_by_actor.get(destroyed):
                    role_deaths[(destroyed[0], role_by_actor[destroyed])] += 1

    pickup = collections.Counter()
    pickup_proximity_proxy = collections.Counter()
    carrier_changes = collections.Counter()
    steals = collections.Counter()
    drops = collections.Counter()
    handoffs = collections.Counter()
    arc_tosses = collections.Counter()
    forced_displacements = collections.Counter()
    banks = collections.Counter()
    banks_by_source = collections.Counter()
    damage = collections.Counter()
    destruction = collections.Counter()
    camp_kills = collections.Counter()
    reactor_near_drops = collections.Counter()
    relocation_kind = collections.Counter()
    relocation_intervals: list[int] = []
    impossible_intervals: list[dict] = []
    delivery_ticks = collections.defaultdict(list)
    pulses = collections.Counter()
    core_histories: dict[str, dict] = {}
    last_owner_team: dict[str, int] = {}
    current_carrier: dict[str, tuple[int, int, int] | None] = {}
    pulse_sequence = []
    reactors = {
        item["teamId"]: position(item["position"])
        for item in mode(broadcast["initial"])["reactors"]
    }
    well_positions = {
        item["wellId"]: position(item["position"])
        for item in mode(broadcast["initial"])["wells"]
    }

    for tick, events in enumerate(all_events):
        before = broadcast["initial"] if tick == 0 else broadcast["worlds"][tick - 1]
        before_lives = active_lives(before)
        for event in events:
            kind, value = fact(event)
            if kind == "core-born":
                key = core_key(value["coreId"])
                core_histories[key] = {
                    "coreId": value["coreId"],
                    "bornTick": tick,
                    "lastPosition": position(value["position"]),
                    "pathTiles": 0,
                    "relocationTicks": [],
                    "deliveries": [],
                }
                current_carrier[key] = None
            elif kind == "core-picked-up":
                key = core_key(value["coreId"])
                carrier = actor_key(value["carrierActorId"])
                if carrier is None:
                    continue
                team, unit, _ = carrier
                class_id = class_by_slot[(team, unit)]
                source = value["coreId"]["sourceWellId"]
                pickup[(team, source, class_id)] += 1
                enemies_near = any(
                    life["team"] != team
                    and chebyshev(life["position"], position(value["position"])) <= 2
                    for life in before_lives
                )
                pickup_proximity_proxy[(team, "contested" if enemies_near else "clear")] += 1
                prior_carrier = current_carrier.get(key)
                if prior_carrier != carrier:
                    carrier_changes[team] += 1
                prior_owner = last_owner_team.get(key)
                if prior_owner is not None and prior_owner != team:
                    steals[team] += 1
                current_carrier[key] = carrier
                last_owner_team[key] = team
            elif kind == "core-handed-off":
                key = core_key(value["coreId"])
                target = actor_key(value["targetActorId"])
                if target is not None:
                    handoffs[target[0]] += 1
                    carrier_changes[target[0]] += 1
                    current_carrier[key] = target
                    last_owner_team[key] = target[0]
            elif kind == "core-dropped":
                key = core_key(value["coreId"])
                source_actor = actor_key(value["sourceActorId"])
                if source_actor is not None:
                    drops[(source_actor[0], value["dropKind"])] += 1
                drop_pos = position(value["position"])
                for reactor_team, reactor_pos in reactors.items():
                    if chebyshev(drop_pos, reactor_pos) <= 6:
                        reactor_near_drops[reactor_team] += 1
                current_carrier[key] = None
            elif kind == "core-relocated":
                key = core_key(value["coreId"])
                carrier = actor_key(value.get("carrierActorId"))
                team = carrier[0] if carrier is not None else last_owner_team.get(key)
                relocation_kind[value["relocationKind"]] += 1
                if team is not None and value["relocationKind"] == "forced-displacement":
                    forced_displacements[team] += 1
                if team is not None and value["relocationKind"] == "arc-toss-landing":
                    arc_tosses[team] += 1
                history = core_histories.get(key)
                if history is not None:
                    from_pos = position(value["from"])
                    to_pos = position(value["to"])
                    history["pathTiles"] += chebyshev(from_pos, to_pos)
                    previous_ticks = history["relocationTicks"]
                    if previous_ticks:
                        interval = tick - previous_ticks[-1]
                        relocation_intervals.append(interval)
                        if interval < rules["coreRelocationIntervalTicks"]:
                            impossible_intervals.append(
                                {"coreId": key, "fromTick": previous_ticks[-1], "toTick": tick}
                            )
                    previous_ticks.append(tick)
                    history["lastPosition"] = to_pos
            elif kind == "core-banked":
                key = core_key(value["coreId"])
                team = value["teamId"]
                banks[team] += 1
                banks_by_source[(team, value["coreId"]["sourceWellId"])] += 1
                delivery_ticks[team].append(tick)
                history = core_histories.get(key)
                if history is not None:
                    ideal = shortest_distance(
                        header["contract"]["map"]["tileRows"],
                        well_positions[value["coreId"]["sourceWellId"]],
                        reactors[team],
                    )
                    history["deliveries"].append(
                        {
                            "tick": tick,
                            "teamId": team,
                            "ageTicks": tick - history["bornTick"],
                            "pathTiles": history["pathTiles"],
                            "shortestEightWayTiles": ideal,
                            "routeStretch": (
                                history["pathTiles"] / ideal
                                if ideal not in (None, 0)
                                else None
                            ),
                        }
                    )
                current_carrier.pop(key, None)
            elif kind == "pulse":
                team = value["teamId"]
                pulses[team] += 1
                pulse_sequence.append({"tick": tick, "teamId": team})
            elif kind == "damage":
                source_team = value.get("sourceTeamId")
                damage[(source_team, theater(position(value["position"])))] += int(value["amount"])
            elif kind == "destruction":
                source_team = value.get("sourceTeamId")
                event_theater = theater(position(value["position"]))
                destruction[(source_team, event_theater)] += 1
                event_x = position(value["position"])[0]
                if source_team is not None and (
                    (source_team == 0 and event_x >= 21)
                    or (source_team == 1 and event_x <= 9)
                ):
                    camp_kills[source_team] += 1

    final_scores = scoreboard(broadcast["worlds"][-1])
    winner = final_result["standings"]["winnerTeamId"]
    pulse_lead_changes = 0
    behind_to_ahead = 0
    pulse_totals = collections.Counter()
    prior_leader: int | None = None
    for item in pulse_sequence:
        team = item["teamId"]
        was_behind = any(pulse_totals[team] < pulse_totals[other] for other in team_ids if other != team)
        pulse_totals[team] += 1
        leaders = [candidate for candidate in team_ids if pulse_totals[candidate] == max(pulse_totals.values())]
        leader = leaders[0] if len(leaders) == 1 else None
        if was_behind and leader == team:
            behind_to_ahead += 1
        if prior_leader is not None and leader is not None and leader != prior_leader:
            pulse_lead_changes += 1
        if leader is not None:
            prior_leader = leader

    deliveries = [
        delivery
        for history in core_histories.values()
        for delivery in history["deliveries"]
    ]
    delivered_keys = {
        key for key, history in core_histories.items() if history["deliveries"]
    }
    unresolved_ages = [
        end_tick - history["bornTick"]
        for key, history in core_histories.items()
        if key not in delivered_keys
    ]
    delivery_intervals = [
        later - earlier
        for ticks in delivery_ticks.values()
        for earlier, later in zip(ticks, ticks[1:])
    ]

    theater_output = {}
    for team in team_ids:
        counts = [body_ticks[(team, name)] for name in ("north", "centre", "south")]
        theater_output[str(team)] = {
            "bodyTicks": {
                name: body_ticks[(team, name)]
                for name in ("north", "centre", "south")
            },
            "normalizedAllocationEntropy": entropy_from_counts(counts),
            "crossTheaterTransitions": theater_transitions[team],
            "activeTheaterCountHistogram": {
                str(count): active_theaters_histogram[(team, count)]
                for count in range(4)
            },
        }

    faults = {
        str(item[1]): {"teamId": item[1], "runtimeFaultCount": int(item[2]), "disqualified": item[3]}
        for item in broadcast["worlds"][-1][2]
    }
    first_pulse_team = pulse_sequence[0]["teamId"] if pulse_sequence else None
    return {
        "schema": SCHEMA,
        "source": {
            "broadcast": str(source_path),
            "broadcastSha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
            "broadcastGzipBytes": source_path.stat().st_size,
            "canonicalReplayHash": broadcast["canonicalReplayHash"],
            "matchId": record.get("matchId") if record else None,
            "recordSha256": (
                hashlib.sha256(
                    json.dumps(record, ensure_ascii=False, separators=(",", ":")).encode()
                ).hexdigest()
                if record
                else None
            ),
        },
        "identity": {
            "rulesetId": header["contract"]["rules"]["rulesetId"],
            "rulesFingerprint": header["contract"]["rules"]["rulesFingerprint"],
            "mapId": header["contract"]["map"]["mapId"],
            "mapFingerprint": header["contract"]["map"]["mapFingerprint"],
            "seed": header["seed"],
            "entrantsByTeam": entrant_by_team,
            "compositionsByTeam": compositions,
        },
        "outcome": {
            "winnerTeamId": winner,
            "completionReason": final_result["completionReason"],
            "endTick": end_tick,
            "durationSecondsAtFiveTicksPerSecond": (end_tick + 1) / TICKS_PER_SECOND,
            "scoresByTeam": {str(team): final_scores[team] for team in team_ids},
            "eligibleTeamIds": final_result["eligibleTeamIds"],
            "runtimeByParticipant": faults,
        },
        "coreCadence": {
            "scheduledBirthsThroughEnd": scheduled_births,
            "actualBirths": sum(births.values()),
            "actualBirthsBySource": counter_dict(births),
            "birthAccessProximityProxyBySource": nested_counter(birth_access),
            "pendingTicksBySource": counter_dict(pending_ticks),
            "rearmTicksBySource": counter_dict(rearm_ticks),
            "liveCoreCountHistogram": counter_dict(live_core_histogram),
            "unresolvedCoreAgeTicks": series_stats(unresolved_ages),
        },
        "possession": {
            "ticksByTeam": team_counter(possession_ticks, team_ids),
            "ticksByTeamAndClass": nested_counter(possession_class_ticks),
            "pickupsByTeamSourceClass": {
                str(team): {
                    source_id: {
                        class_id: count
                        for (candidate_team, candidate_source, class_id), count in sorted(pickup.items())
                        if candidate_team == team and candidate_source == source_id
                    }
                    for source_id in sorted(well_positions)
                }
                for team in team_ids
            },
            "pickupEnemyWithinTwoTilesProxy": nested_counter(pickup_proximity_proxy),
            "carrierChangesByTeam": team_counter(carrier_changes, team_ids),
            "stealsByTeam": team_counter(steals, team_ids),
            "handoffsByTeam": team_counter(handoffs, team_ids),
            "dropsByTeamAndKind": nested_counter(drops),
            "arcTossLandingsByTeam": team_counter(arc_tosses, team_ids),
            "forcedCarrierDisplacementsByCarrierTeam": team_counter(forced_displacements, team_ids),
        },
        "routes": {
            "relocationsByKind": counter_dict(relocation_kind),
            "relocationIntervalTicks": series_stats(relocation_intervals),
            "impossibleRelocationIntervals": impossible_intervals,
            "deliveries": deliveries,
            "deliveryIntervalTicks": series_stats(delivery_intervals),
            "deliveredRouteStretch": series_stats(
                [item["routeStretch"] for item in deliveries if item["routeStretch"] is not None]
            ),
        },
        "scoring": {
            "deliveriesByTeam": team_counter(banks, team_ids),
            "deliveriesByTeamAndSource": nested_counter(banks_by_source),
            "pulsesByTeam": team_counter(pulses, team_ids),
            "pulseSequence": pulse_sequence,
            "pulseLeadChanges": pulse_lead_changes,
            "behindToAheadPulseReversals": behind_to_ahead,
            "firstPulseTeamId": first_pulse_team,
            "firstPulseConvertedToMatchWinner": (
                first_pulse_team is not None and first_pulse_team == winner
            ),
        },
        "fieldShape": {
            "theatersByTeam": theater_output,
            "homeCampBodyTicksByTeam": team_counter(camp_body_ticks, team_ids),
            "campKillsByTeam": team_counter(camp_kills, team_ids),
            "dropsWithinSixTilesOfReactorByReactorTeam": team_counter(reactor_near_drops, team_ids),
            "convoyEscortCountWithinTwoTiles": {
                str(team): {
                    "samples": convoy_samples[team],
                    "mean": (
                        convoy_escort_sum[team] / convoy_samples[team]
                        if convoy_samples[team]
                        else 0.0
                    ),
                    "max": convoy_escort_max[team],
                }
                for team in team_ids
            },
            "publishedRoleTicksByTeamAndRole": nested_counter(role_ticks),
            "publishedRoleDeathsByTeamAndRole": nested_counter(role_deaths),
            "damageBySourceTeamAndTheater": nested_counter(damage),
            "destructionsBySourceTeamAndTheater": nested_counter(destruction),
        },
        "signatures": signature_metrics(broadcast, all_events, team_ids),
        "method": {
            "authority": "durable spectator broadcast; no mind observations or legality masks",
            "theaters": "north y<=7; centre 8<=y<=14; south y>=15",
            "homeCamp": "team 0 x>=21; team 1 x<=9",
            "convoy": "allied non-carrier bodies at Chebyshev distance <=2",
            "routeDistance": "sum of authoritative Core relocation Chebyshev distances",
            "routeBaseline": "shortest traversable eight-way tile distance from source Well to scoring reactor",
            "contestedPickup": "proxy only: enemy body within Chebyshev distance 2 immediately before pickup",
            "birthAccess": "proxy only: number of teams within Chebyshev distance 3 at the birth post-state",
            "signatureUsefulEffects": "counted effect facts/transitions; not a causal value judgment",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("broadcast", type=Path)
    parser.add_argument("--record", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    broadcast = read_json(args.broadcast)
    record = read_json(args.record) if args.record else None
    output = measure(broadcast, record, args.broadcast)
    encoded = json.dumps(output, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
        print(args.output)
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
