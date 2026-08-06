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


SCHEMA = "arc-relay-scorecard-v2"
TICKS_PER_SECOND = 5
DEFAULT_BARS_PATH = (
    Path(__file__).resolve().parent.parent
    / "balance/arc-relay-felt-degeneracy-bars-v4.json"
)


def configure_bars(path: Path) -> None:
    global BARS_PATH, BARS
    global PING_PONG_REVERSAL_BAR, PING_PONG_GAP_INTERVALS
    global PASSIVITY_WINDOW_TICKS, PASSIVITY_MIN_QUIET_TICKS
    global PASSIVITY_WAIT_SHARE, CONTEST_DISTANCE
    global FREEZE_WINDOW_TICKS, FREEZE_MIN_HIGH_WAIT_TICKS
    global FREEZE_WAIT_SHARE, STUCK_CARRIER_TICKS
    global HOME_PROGRESS_RADIUS, HOME_PROGRESS_CONTEST_DISTANCE
    global HOME_PROGRESS_TICKS
    global PICKUP_DROP_MAX_HOLD_TICKS, PICKUP_DROP_MAX_GAP_TICKS
    global PICKUP_DROP_CYCLE_BAR

    BARS_PATH = path.resolve()
    BARS = json.loads(BARS_PATH.read_text(encoding="utf-8"))
    PING_PONG_REVERSAL_BAR = BARS["handoffPingPong"][
        "tripAtReversalsInOneSamePairEpisode"]
    PING_PONG_GAP_INTERVALS = BARS["handoffPingPong"][
        "maximumGapCoreRelocationIntervals"]
    PASSIVITY_WINDOW_TICKS = BARS["sustainedPassivity"]["windowTicks"]
    PASSIVITY_MIN_QUIET_TICKS = BARS["sustainedPassivity"][
        "tripAtQuietTicksInWindow"]
    PASSIVITY_WAIT_SHARE = BARS["sustainedPassivity"][
        "quietTickMinimumWaitShare"]
    CONTEST_DISTANCE = BARS["sustainedPassivity"][
        "liveCoreTheaterChebyshevDistance"]
    formation_freeze = BARS.get("formationFreeze")
    FREEZE_WINDOW_TICKS = (
        formation_freeze["windowTicks"] if formation_freeze else 1)
    FREEZE_MIN_HIGH_WAIT_TICKS = (
        formation_freeze["tripAtHighWaitTicksInWindow"]
        if formation_freeze else 2)
    FREEZE_WAIT_SHARE = (
        formation_freeze["highWaitMinimumShare"]
        if formation_freeze else 1.0)
    stuck_carrier = BARS.get("stuckCarrier")
    STUCK_CARRIER_TICKS = (
        stuck_carrier["tripAtConsecutiveSameCarrierPositionTicks"]
        if stuck_carrier else 2**31 - 1)
    home_progress = BARS.get("homeCarrierNonProgress")
    HOME_PROGRESS_RADIUS = (
        home_progress["homeRadiusShortestPathTiles"]
        if home_progress is not None else None
    )
    HOME_PROGRESS_CONTEST_DISTANCE = (
        home_progress["enemyContestChebyshevDistance"]
        if home_progress is not None else None
    )
    HOME_PROGRESS_TICKS = (
        home_progress["tripAtUncontestedTicksWithoutNewBestDistance"]
        if home_progress is not None else None
    )
    pickup_drop = BARS.get("pickupDropCycle")
    PICKUP_DROP_MAX_HOLD_TICKS = (
        pickup_drop["maximumPickupToVoluntaryDropTicks"]
        if pickup_drop is not None else None
    )
    PICKUP_DROP_MAX_GAP_TICKS = (
        pickup_drop["maximumGapTicksBetweenCycles"]
        if pickup_drop is not None else None
    )
    PICKUP_DROP_CYCLE_BAR = (
        pickup_drop["tripAtCyclesInOneSameCoreActorPositionEpisode"]
        if pickup_drop is not None else None
    )


configure_bars(DEFAULT_BARS_PATH)


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


def map_analysis_layout(contract: dict) -> dict:
    """Derive descriptive theater/camp bands from the resolved map.

    The original Threefold map happened to use y<=7/y>=15 and x<=9/x>=21.
    Keeping those as literals would silently mismeasure a taller design arm.
    Deriving them from named contract regions reproduces the original bands
    byte-for-byte while remaining honest for registered geometry candidates.
    """
    map_contract = contract["map"]
    regions = {
        item["regionId"]: [position(tile) for tile in item["tiles"]]
        for item in map_contract["regions"]
    }
    required = (
        "well-north",
        "well-centre",
        "well-south",
        "home-west",
        "home-east",
    )
    missing = [region_id for region_id in required if region_id not in regions]
    if missing:
        raise ValueError(f"Arc Relay map is missing analysis regions: {missing}")
    well_y = {
        name: regions[f"well-{name}"][0][1]
        for name in ("north", "centre", "south")
    }
    if not well_y["north"] < well_y["centre"] < well_y["south"]:
        raise ValueError(f"Arc Relay Well ordering is invalid: {well_y}")
    width = len(map_contract["tileRows"][0])
    camp_depth = max(1, width // 5)
    west_home_max_x = max(tile[0] for tile in regions["home-west"])
    east_home_min_x = min(tile[0] for tile in regions["home-east"])
    return {
        "theaterNorthMaximumY": (
            well_y["north"] + well_y["centre"]
        ) // 2,
        "theaterSouthMinimumY": (
            well_y["centre"] + well_y["south"] + 1
        ) // 2,
        "westHomeCampMaximumX": west_home_max_x + camp_depth,
        "eastHomeCampMinimumX": east_home_min_x - camp_depth,
        "wellY": well_y,
    }


def theater(pos: tuple[int, int], layout: dict) -> str:
    if pos[1] <= layout["theaterNorthMaximumY"]:
        return "north"
    if pos[1] >= layout["theaterSouthMinimumY"]:
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


def ping_pong_metrics(
    handoff_events: dict[str, list[dict]],
    team_ids: list[int],
    maximum_gap_ticks: int,
) -> dict:
    episodes: list[dict] = []
    reversals = collections.Counter()
    max_reversals = collections.Counter()
    for core_id, events in sorted(handoff_events.items()):
        active: dict | None = None
        previous: dict | None = None
        for event in events:
            reverse = (
                previous is not None
                and event["epoch"] == previous["epoch"]
                and event["tick"] - previous["tick"] <= maximum_gap_ticks
                and event["source"] == previous["target"]
                and event["target"] == previous["source"]
            )
            pair = (
                tuple(sorted((event["source"], event["target"])))
                if reverse
                else None
            )
            if reverse and active is not None and active["pair"] == pair:
                active["endTick"] = event["tick"]
                active["reversals"] += 1
                active["handoffs"] += 1
            elif reverse:
                if active is not None:
                    episodes.append(active)
                active = {
                    "coreId": core_id,
                    "teamId": event["source"][0],
                    "pair": pair,
                    "startTick": previous["tick"],
                    "endTick": event["tick"],
                    "reversals": 1,
                    "handoffs": 2,
                }
            elif active is not None:
                episodes.append(active)
                active = None
            previous = event
        if active is not None:
            episodes.append(active)

    serialized = []
    for episode in episodes:
        team = episode["teamId"]
        reversals[team] += episode["reversals"]
        max_reversals[team] = max(
            max_reversals[team], episode["reversals"])
        serialized.append({
            **{key: value for key, value in episode.items() if key != "pair"},
            "actorPair": [list(actor) for actor in episode["pair"]],
            "barTripped": episode["reversals"] >= PING_PONG_REVERSAL_BAR,
        })
    return {
        "definition": (
            "consecutive A->B, B->A handoff reversals for one Core and life "
            f"pair, each no more than {maximum_gap_ticks} ticks apart"
        ),
        "bar": {
            "operator": ">=",
            "reversalsInOneEpisode": PING_PONG_REVERSAL_BAR,
        },
        "episodes": serialized,
        "totalReversalsByTeam": team_counter(reversals, team_ids),
        "maxEpisodeReversalsByTeam": team_counter(max_reversals, team_ids),
        "barTrippedByTeam": {
            str(team): max_reversals[team] >= PING_PONG_REVERSAL_BAR
            for team in team_ids
        },
    }


def pickup_drop_cycle_metrics(
    events_by_tick: list[list[dict]],
    team_ids: list[int],
) -> dict:
    """Find rapid same-body pickup/drop loops hidden by loose post-states.

    Automatic pickup is a tick-start event while a voluntary drop is a
    post-decision event. Looking only at spectator worlds therefore sees a
    loose Core after every cycle and incorrectly resets carrier-stall state.
    Correlating both event columns preserves that causal interval.
    """
    if (PICKUP_DROP_MAX_HOLD_TICKS is None
            or PICKUP_DROP_MAX_GAP_TICKS is None
            or PICKUP_DROP_CYCLE_BAR is None):
        return {
            "enabled": False,
            "definition": "not registered by this eligibility-bar version",
            "barTrippedByTeam": {str(team): False for team in team_ids},
            "maxCyclesInOneEpisodeByTeam": {
                str(team): 0 for team in team_ids
            },
            "trippingEpisodes": [],
        }

    pickups: dict[str, dict] = {}
    active: dict[str, dict] = {}
    tripping: list[dict] = []
    maximum = collections.Counter()

    def finish(key: str) -> None:
        episode = active.pop(key, None)
        if episode is None:
            return
        team = episode["teamId"]
        maximum[team] = max(maximum[team], episode["cycles"])
        if episode["cycles"] < PICKUP_DROP_CYCLE_BAR:
            return
        tripping.append({
            "coreId": key,
            "teamId": team,
            "carrierActorId": list(episode["carrier"]),
            "position": list(episode["position"]),
            "fromTick": episode["fromTick"],
            "throughTick": episode["throughTick"],
            "cycles": episode["cycles"],
            "maximumPickupToDropTicks": episode["maximumHoldTicks"],
        })

    for tick, events in enumerate(events_by_tick):
        for event in events:
            kind, value = fact(event)
            core_id = value.get("coreId")
            if core_id is None:
                continue
            key = core_key(core_id)
            if kind == "core-picked-up":
                carrier = actor_key(value.get("carrierActorId"))
                if carrier is None:
                    pickups.pop(key, None)
                    finish(key)
                    continue
                pickups[key] = {
                    "tick": tick,
                    "carrier": carrier,
                    "position": position(value["position"]),
                }
                continue
            if kind == "core-dropped":
                pickup = pickups.pop(key, None)
                carrier = actor_key(value.get("sourceActorId"))
                drop_position = position(value["position"])
                is_cycle = (
                    value.get("dropKind") == "voluntary"
                    and pickup is not None
                    and carrier is not None
                    and pickup["carrier"] == carrier
                    and pickup["position"] == drop_position
                    and tick - pickup["tick"]
                        <= PICKUP_DROP_MAX_HOLD_TICKS
                )
                if not is_cycle:
                    finish(key)
                    continue
                prior = active.get(key)
                same_episode = (
                    prior is not None
                    and prior["carrier"] == carrier
                    and prior["position"] == drop_position
                    and tick - prior["throughTick"]
                        <= PICKUP_DROP_MAX_GAP_TICKS
                )
                if not same_episode:
                    finish(key)
                    active[key] = prior = {
                        "teamId": carrier[0],
                        "carrier": carrier,
                        "position": drop_position,
                        "fromTick": pickup["tick"],
                        "throughTick": tick,
                        "cycles": 0,
                        "maximumHoldTicks": 0,
                    }
                prior["throughTick"] = tick
                prior["cycles"] += 1
                prior["maximumHoldTicks"] = max(
                    prior["maximumHoldTicks"], tick - pickup["tick"])
                continue
            if kind in {
                "core-born",
                "core-handed-off",
                "core-banked",
                "core-relocated",
            }:
                pickups.pop(key, None)
                finish(key)

    for key in list(active):
        finish(key)

    return {
        "enabled": True,
        "definition": (
            "rapid automatic pickup followed by voluntary drop for the same "
            "Core, carrier life, and tile, correlated across tick-start and "
            "post-decision events"
        ),
        "bar": {
            "operator": ">=",
            "cyclesInOneEpisode": PICKUP_DROP_CYCLE_BAR,
            "maximumPickupToVoluntaryDropTicks":
                PICKUP_DROP_MAX_HOLD_TICKS,
            "maximumGapTicksBetweenCycles": PICKUP_DROP_MAX_GAP_TICKS,
        },
        "maxCyclesInOneEpisodeByTeam": team_counter(maximum, team_ids),
        "trippingEpisodes": tripping,
        "barTrippedByTeam": {
            str(team): maximum[team] >= PICKUP_DROP_CYCLE_BAR
            for team in team_ids
        },
    }


def threshold_windows(
    flags: list[bool],
    window_ticks: int,
    minimum_true_ticks: int,
) -> tuple[int, list[dict]]:
    counts = [
        sum(flags[start:start + window_ticks])
        for start in range(max(0, len(flags) - window_ticks + 1))
    ]
    maximum = max(counts, default=0)
    tripped = [
        index for index, count in enumerate(counts)
        if count >= minimum_true_ticks
    ]
    windows: list[dict] = []
    if not tripped:
        return maximum, windows
    start = prior = tripped[0]
    for index in tripped[1:] + [None]:
        if index is not None and index == prior + 1:
            prior = index
            continue
        windows.append({
            "firstWindowStartTick": start,
            "lastWindowStartTick": prior,
            "throughTick": prior + window_ticks - 1,
            "maxMatchingTicksInWindow": max(counts[start:prior + 1]),
        })
        if index is not None:
            start = prior = index
    return maximum, windows


def sustained_passivity_windows(flags: list[bool]) -> tuple[int, list[dict]]:
    return threshold_windows(
        flags,
        PASSIVITY_WINDOW_TICKS,
        PASSIVITY_MIN_QUIET_TICKS,
    )


def passivity_metrics(
    broadcast: dict,
    team_ids: list[int],
    first_birth_tick: int,
) -> tuple[dict, dict]:
    quiet_flags: dict[int, list[bool]] = {team: [] for team in team_ids}
    high_wait_flags: dict[int, list[bool]] = {team: [] for team in team_ids}
    no_theater_ticks = collections.Counter()
    high_wait_ticks = collections.Counter()
    quiet_ticks = collections.Counter()

    for tick, world in enumerate(broadcast["worlds"]):
        lives = active_lives(world)
        visible_cores = mode(world)["visibleCores"]
        for team in team_ids:
            own = [life for life in lives if life["team"] == team]
            turns = [
                turn for turn in broadcast["turns"][tick]
                if turn[0][0] == team
            ]
            wait_count = sum(turn[4][0] == "wait" for turn in turns)
            high_wait = (
                bool(turns)
                and wait_count / len(turns) >= PASSIVITY_WAIT_SHARE
            )
            owns_carrier = any(
                actor_key(core.get("carrierActorId")) is not None
                and actor_key(core.get("carrierActorId"))[0] == team
                for core in visible_cores
            )
            near_live_core = any(
                chebyshev(life["position"], position(core["position"]))
                <= CONTEST_DISTANCE
                for life in own
                for core in visible_cores
            )
            has_objective_theater_presence = owns_carrier or near_live_core
            if own and not has_objective_theater_presence:
                no_theater_ticks[team] += 1
            if high_wait:
                high_wait_ticks[team] += 1
            quiet = (
                tick >= first_birth_tick
                and bool(own)
                and high_wait
                and not has_objective_theater_presence
            )
            quiet_flags[team].append(quiet)
            high_wait_flags[team].append(
                tick >= first_birth_tick and bool(own) and high_wait)
            if quiet:
                quiet_ticks[team] += 1

    maximum: dict[int, int] = {}
    windows: dict[int, list[dict]] = {team: [] for team in team_ids}
    for team in team_ids:
        maximum[team], windows[team] = sustained_passivity_windows(
            quiet_flags[team])

    freeze_maximum: dict[int, int] = {}
    freeze_windows: dict[int, list[dict]] = {team: [] for team in team_ids}
    for team in team_ids:
        freeze_maximum[team], freeze_windows[team] = threshold_windows(
            high_wait_flags[team],
            FREEZE_WINDOW_TICKS,
            FREEZE_MIN_HIGH_WAIT_TICKS,
        )

    passivity = {
        "definition": (
            "from the first scheduled Core birth, a quiet tick has at least "
            f"{PASSIVITY_WAIT_SHARE:.0%} of commanded bodies waiting while "
            "the team carries no Core and has no body within Chebyshev "
            f"{CONTEST_DISTANCE} of a live Core"
        ),
        "bar": {
            "windowTicks": PASSIVITY_WINDOW_TICKS,
            "operator": ">=",
            "quietTicks": PASSIVITY_MIN_QUIET_TICKS,
        },
        "noTheaterPresenceTicksByTeam": team_counter(
            no_theater_ticks, team_ids),
        "highWaitTicksByTeam": team_counter(high_wait_ticks, team_ids),
        "quietTicksByTeam": team_counter(quiet_ticks, team_ids),
        "maxQuietTicksInWindowByTeam": {
            str(team): maximum[team] for team in team_ids
        },
        "trippingWindowRunsByTeam": {
            str(team): windows[team] for team in team_ids
        },
        "barTrippedByTeam": {
            str(team): maximum[team] >= PASSIVITY_MIN_QUIET_TICKS
            for team in team_ids
        },
    }
    formation_freeze = {
        "definition": (
            "from the first scheduled Core birth, a high-wait tick has at "
            f"least {FREEZE_WAIT_SHARE:.0%} of commanded bodies waiting; "
            "Core proximity and possession do not excuse a frozen formation"
        ),
        "bar": {
            "windowTicks": FREEZE_WINDOW_TICKS,
            "operator": ">=",
            "highWaitTicks": FREEZE_MIN_HIGH_WAIT_TICKS,
        },
        "maxHighWaitTicksInWindowByTeam": {
            str(team): freeze_maximum[team] for team in team_ids
        },
        "trippingWindowRunsByTeam": {
            str(team): freeze_windows[team] for team in team_ids
        },
        "barTrippedByTeam": {
            str(team): freeze_maximum[team] >= FREEZE_MIN_HIGH_WAIT_TICKS
            for team in team_ids
        },
    }
    return passivity, formation_freeze


def team_dance_metrics(broadcast, team_ids):
    """Dancer detector (bars v6): displacement without progress. A body
    confined to a small radius for a long window counts however busily it
    steps; the wedge-shake taught that motion alone proves nothing.
    """
    bar = BARS.get("teamDance")
    if bar is None:
        return {
            "enforced": False,
            "barTrippedByTeam": {str(team): False for team in team_ids},
        }
    window = bar["windowTicks"]
    radius = bar["confinementRadius"]
    minimum = bar["minimumConfinedBodies"]
    share = bar["tripAtConfinedShareOfLiveBodies"]
    anchors = {}
    worst = {team: 0 for team in team_ids}
    tripped = {team: False for team in team_ids}
    for _tick, world in enumerate(broadcast["worlds"]):
        lives = active_lives(world)
        confined_by_team = {team: 0 for team in team_ids}
        live_by_team = {team: 0 for team in team_ids}
        for life in lives:
            key = life["actor"]
            spot = tuple(life["position"])
            entry = anchors.get(key)
            if entry is None or chebyshev(spot, entry[0]) > radius:
                anchors[key] = (spot, 1)
            else:
                anchors[key] = (entry[0], entry[1] + 1)
            live_by_team[life["team"]] += 1
            if anchors[key][1] >= window:
                confined_by_team[life["team"]] += 1
        for team in team_ids:
            worst[team] = max(worst[team], confined_by_team[team])
            if (
                live_by_team[team] > 0
                and confined_by_team[team] >= minimum
                and confined_by_team[team] / live_by_team[team] >= share
            ):
                tripped[team] = True
    return {
        "enforced": True,
        "windowTicks": window,
        "confinementRadius": radius,
        "worstSimultaneousConfinedByTeam": {
            str(team): worst[team] for team in team_ids
        },
        "barTrippedByTeam": {
            str(team): tripped[team] for team in team_ids
        },
    }


def unit_parked_metrics(broadcast, team_ids):
    """Parked detector (bars v7): one body confined to a small radius for a
    very long window. The team-level statue/dance bars need several bodies
    at once; the parked ghost the owner caught twice on replay was a single
    unit camping while its team played on. The window sits far above every
    intended stationary behavior: heal channels finish in ~15 ticks and
    ambush perches relocate within ~32 under the mind's no-idle invariant.
    """
    bar = BARS.get("unitParked")
    if bar is None:
        return {
            "enforced": False,
            "barTrippedByTeam": {str(team): False for team in team_ids},
        }
    window = bar["windowTicks"]
    radius = bar["confinementRadius"]
    anchors = {}
    worst = {team: 0 for team in team_ids}
    tripped = {team: False for team in team_ids}
    for _tick, world in enumerate(broadcast["worlds"]):
        for life in active_lives(world):
            key = life["actor"]
            spot = tuple(life["position"])
            entry = anchors.get(key)
            if entry is None or chebyshev(spot, entry[0]) > radius:
                anchors[key] = (spot, 1)
            else:
                anchors[key] = (entry[0], entry[1] + 1)
            streak = anchors[key][1]
            team = life["team"]
            worst[team] = max(worst[team], streak)
            if streak >= window:
                tripped[team] = True
    return {
        "enforced": True,
        "windowTicks": window,
        "confinementRadius": radius,
        "worstConfinementStreakByTeam": {
            str(team): worst[team] for team in team_ids
        },
        "barTrippedByTeam": {
            str(team): tripped[team] for team in team_ids
        },
    }


def team_statue_metrics(broadcast, team_ids):
    """Busy-statue detector (bars v5): a livelocked body never waits, so the
    wait-share detectors stay silent while half a team repaths in place for
    hundreds of ticks. This one watches displacement only: a live body
    frozen on one tile for the whole window counts, whatever it commanded.
    """
    bar = BARS.get("teamStatue")
    if bar is None:
        return {
            "enforced": False,
            "barTrippedByTeam": {str(team): False for team in team_ids},
        }
    window = bar["windowTicks"]
    minimum = bar["minimumFrozenBodies"]
    share = bar["tripAtFrozenShareOfLiveBodies"]
    streaks = {}
    worst = {team: 0 for team in team_ids}
    tripped = {team: False for team in team_ids}
    for _tick, world in enumerate(broadcast["worlds"]):
        lives = active_lives(world)
        frozen_by_team = {team: 0 for team in team_ids}
        live_by_team = {team: 0 for team in team_ids}
        for life in lives:
            key = life["actor"]
            spot = tuple(life["position"])
            count = streaks.get(key)
            streak = count[1] + 1 if count and count[0] == spot else 1
            streaks[key] = (spot, streak)
            live_by_team[life["team"]] += 1
            if streak >= window:
                frozen_by_team[life["team"]] += 1
        for team in team_ids:
            worst[team] = max(worst[team], frozen_by_team[team])
            if (
                live_by_team[team] > 0
                and frozen_by_team[team] >= minimum
                and frozen_by_team[team] / live_by_team[team] >= share
            ):
                tripped[team] = True
    return {
        "enforced": True,
        "windowTicks": window,
        "worstSimultaneousFrozenByTeam": {
            str(team): worst[team] for team in team_ids
        },
        "barTrippedByTeam": {
            str(team): tripped[team] for team in team_ids
        },
    }


def stuck_carrier_metrics(broadcast: dict, team_ids: list[int]) -> dict:
    active: dict[str, dict] = {}
    completed: list[dict] = []
    maximum = collections.Counter()

    def finish(key: str, through_tick: int) -> None:
        state = active.pop(key, None)
        if state is None:
            return
        maximum[state["teamId"]] = max(maximum[state["teamId"]], state["ticks"])
        if state["ticks"] >= STUCK_CARRIER_TICKS:
            completed.append({
                "coreId": key,
                "teamId": state["teamId"],
                "carrierActorId": list(state["carrier"]),
                "position": list(state["position"]),
                "fromTick": state["fromTick"],
                "throughTick": through_tick,
                "ticks": state["ticks"],
            })

    for tick, world in enumerate(broadcast["worlds"]):
        present: set[str] = set()
        for core in mode(world)["visibleCores"]:
            carrier = actor_key(core.get("carrierActorId"))
            if core["disposition"] != "carried" or carrier is None:
                continue
            key = core_key(core["coreId"])
            present.add(key)
            current = {
                "teamId": carrier[0],
                "carrier": carrier,
                "position": position(core["position"]),
            }
            prior = active.get(key)
            if (prior is not None
                    and prior["teamId"] == current["teamId"]
                    and prior["carrier"] == current["carrier"]
                    and prior["position"] == current["position"]):
                prior["ticks"] += 1
                continue
            finish(key, tick - 1)
            active[key] = {
                **current,
                "fromTick": tick,
                "ticks": 1,
            }
        for key in list(active):
            if key not in present:
                finish(key, tick - 1)

    final_tick = len(broadcast["worlds"]) - 1
    for key in list(active):
        finish(key, final_tick)

    return {
        "definition": (
            "one Core remains carried by the same life on the same tile for "
            "consecutive spectator worlds"
        ),
        "bar": {
            "operator": ">=",
            "consecutiveTicks": STUCK_CARRIER_TICKS,
        },
        "maxConsecutiveTicksByTeam": team_counter(maximum, team_ids),
        "trippingRuns": completed,
        "barTrippedByTeam": {
            str(team): maximum[team] >= STUCK_CARRIER_TICKS
            for team in team_ids
        },
    }


def shortest_distance_field(
    rows: list[str], goal: tuple[int, int]
) -> dict[tuple[int, int], int]:
    """Static eight-way distance without walking diagonally through corners."""
    height = len(rows)
    width = len(rows[0])
    if rows[goal[1]][goal[0]] == "#":
        return {}
    distances = {goal: 0}
    queue = collections.deque([goal])
    headings = (
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    )
    while queue:
        current = queue.popleft()
        for dx, dy in headings:
            target = (current[0] + dx, current[1] + dy)
            x, y = target
            if not (0 <= x < width and 0 <= y < height):
                continue
            if rows[y][x] == "#" or target in distances:
                continue
            if dx != 0 and dy != 0:
                side_x = (current[0] + dx, current[1])
                side_y = (current[0], current[1] + dy)
                if (rows[side_x[1]][side_x[0]] == "#"
                        or rows[side_y[1]][side_y[0]] == "#"):
                    continue
            distances[target] = distances[current] + 1
            queue.append(target)
    return distances


def home_carrier_non_progress_metrics(
    broadcast: dict,
    team_ids: list[int],
    rows: list[str],
    reactors: dict[int, tuple[int, int]],
) -> dict:
    """Find owned Cores that loiter near a legal bank without getting closer.

    Unlike the original same-life/same-tile sentinel, the episode belongs to
    the Core and team. Allied handoffs and equal-distance tile oscillations do
    not clear it. Nearby enemies pause the felt clock because a visible fight
    is a reason for delayed progress; they do not erase progress debt.
    """
    if (HOME_PROGRESS_RADIUS is None
            or HOME_PROGRESS_CONTEST_DISTANCE is None
            or HOME_PROGRESS_TICKS is None):
        return {
            "enabled": False,
            "definition": "not registered by this eligibility-bar version",
            "barTrippedByTeam": {str(team): False for team in team_ids},
            "maxUncontestedTicksWithoutProgressByTeam": {
                str(team): 0 for team in team_ids
            },
            "trippingRuns": [],
        }

    fields = {
        team: shortest_distance_field(rows, reactors[team])
        for team in team_ids
    }
    active: dict[str, dict] = {}
    tripping_runs: list[dict] = []
    maximum = collections.Counter()

    def record(state: dict, through_tick: int) -> None:
        team = state["teamId"]
        ticks = state["uncontestedTicks"]
        maximum[team] = max(maximum[team], ticks)
        if ticks < HOME_PROGRESS_TICKS:
            return
        tripping_runs.append({
            "coreId": state["coreId"],
            "teamId": team,
            "fromTick": state["fromTick"],
            "throughTick": through_tick,
            "uncontestedTicksWithoutProgress": ticks,
            "contestedTicksPaused": state["contestedTicks"],
            "bestDistanceToReactor": state["bestDistance"],
            "lastDistanceToReactor": state["lastDistance"],
            "carrierChanges": state["carrierChanges"],
            "distinctPositions": len(state["positions"]),
            "lastPosition": list(state["lastPosition"]),
        })

    def begin(
        key: str,
        team: int,
        carrier: tuple[int, int, int],
        core_position: tuple[int, int],
        distance: int,
        tick: int,
    ) -> dict:
        state = {
            "coreId": key,
            "teamId": team,
            "carrier": carrier,
            "carrierChanges": 0,
            "fromTick": tick,
            "bestDistance": distance,
            "lastDistance": distance,
            "lastPosition": core_position,
            "positions": {core_position},
            "uncontestedTicks": 0,
            "contestedTicks": 0,
        }
        active[key] = state
        return state

    for tick, world in enumerate(broadcast["worlds"]):
        lives = active_lives(world)
        present: set[str] = set()
        for core in mode(world)["visibleCores"]:
            carrier = actor_key(core.get("carrierActorId"))
            if core["disposition"] != "carried" or carrier is None:
                continue
            key = core_key(core["coreId"])
            present.add(key)
            team = carrier[0]
            core_position = position(core["position"])
            distance = fields.get(team, {}).get(core_position)
            if distance is None:
                continue
            state = active.get(key)
            if state is None or state["teamId"] != team:
                if state is not None:
                    record(state, tick - 1)
                state = begin(
                    key, team, carrier, core_position, distance, tick)
            elif carrier != state["carrier"]:
                state["carrierChanges"] += 1
                state["carrier"] = carrier

            if distance < state["bestDistance"]:
                record(state, tick - 1)
                state.update({
                    "fromTick": tick,
                    "bestDistance": distance,
                    "uncontestedTicks": 0,
                    "contestedTicks": 0,
                    "positions": set(),
                })
            state["lastDistance"] = distance
            state["lastPosition"] = core_position
            state["positions"].add(core_position)
            if distance > HOME_PROGRESS_RADIUS:
                continue
            contested = any(
                life["team"] != team
                and chebyshev(life["position"], core_position)
                    <= HOME_PROGRESS_CONTEST_DISTANCE
                for life in lives
            )
            if contested:
                state["contestedTicks"] += 1
            else:
                state["uncontestedTicks"] += 1

        for key in list(active):
            if key not in present:
                record(active.pop(key), tick - 1)

    final_tick = len(broadcast["worlds"]) - 1
    for key in list(active):
        record(active.pop(key), final_tick)

    return {
        "enabled": True,
        "definition": (
            "during same-team carried possession, count uncontested worlds "
            f"within {HOME_PROGRESS_RADIUS} static walkable tiles of that "
            "team's reactor since the Core last reached a strictly lower "
            "distance; allied handoffs and equal-distance movement do not "
            "reset the run"
        ),
        "bar": {
            "operator": ">=",
            "uncontestedTicksWithoutNewBestDistance": HOME_PROGRESS_TICKS,
            "homeRadiusShortestPathTiles": HOME_PROGRESS_RADIUS,
            "enemyContestChebyshevDistance":
                HOME_PROGRESS_CONTEST_DISTANCE,
        },
        "maxUncontestedTicksWithoutProgressByTeam": team_counter(
            maximum, team_ids),
        "trippingRuns": tripping_runs,
        "barTrippedByTeam": {
            str(team): maximum[team] >= HOME_PROGRESS_TICKS
            for team in team_ids
        },
    }


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
    analysis_layout = map_analysis_layout(header["contract"])
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
            current_theater = theater(life["position"], analysis_layout)
            body_ticks[(team, current_theater)] += 1
            tick_theaters[team].add(current_theater)
            actor = life["actor"]
            prior = previous_theater_by_actor.get(actor)
            if prior is not None and prior != current_theater:
                theater_transitions[team] += 1
            previous_theater_by_actor[actor] = current_theater
            if (
                team == 0
                and life["position"][0]
                >= analysis_layout["eastHomeCampMinimumX"]
            ) or (
                team == 1
                and life["position"][0]
                <= analysis_layout["westHomeCampMaximumX"]
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
    core_handoff_epoch = collections.Counter()
    handoff_events: dict[str, list[dict]] = collections.defaultdict(list)
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
                core_handoff_epoch[key] += 1
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
                core_handoff_epoch[key] += 1
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
                source = actor_key(value["sourceActorId"])
                target = actor_key(value["targetActorId"])
                if source is not None and target is not None:
                    handoff_events[key].append({
                        "tick": tick,
                        "epoch": core_handoff_epoch[key],
                        "source": source,
                        "target": target,
                    })
                    handoffs[target[0]] += 1
                    carrier_changes[target[0]] += 1
                    current_carrier[key] = target
                    last_owner_team[key] = target[0]
            elif kind == "core-dropped":
                key = core_key(value["coreId"])
                core_handoff_epoch[key] += 1
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
                core_handoff_epoch[key] += 1
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
                damage[(
                    source_team,
                    theater(position(value["position"]), analysis_layout),
                )] += int(value["amount"])
            elif kind == "destruction":
                source_team = value.get("sourceTeamId")
                event_theater = theater(
                    position(value["position"]), analysis_layout)
                destruction[(source_team, event_theater)] += 1
                event_x = position(value["position"])[0]
                if source_team is not None and (
                    (
                        source_team == 0
                        and event_x
                        >= analysis_layout["eastHomeCampMinimumX"]
                    )
                    or (
                        source_team == 1
                        and event_x
                        <= analysis_layout["westHomeCampMaximumX"]
                    )
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
    ping_pong = ping_pong_metrics(
        handoff_events,
        team_ids,
        maximum_gap_ticks=max(
            1,
            rules["coreRelocationIntervalTicks"] * PING_PONG_GAP_INTERVALS,
        ),
    )
    pickup_drop_cycles = pickup_drop_cycle_metrics(all_events, team_ids)
    first_birth_tick = min(
        well["firstBirthTick"] for well in rules["wells"])
    passivity, formation_freeze = passivity_metrics(
        broadcast, team_ids, first_birth_tick)
    stuck_carrier = stuck_carrier_metrics(broadcast, team_ids)
    team_statue = team_statue_metrics(broadcast, team_ids)
    team_dance = team_dance_metrics(broadcast, team_ids)
    unit_parked = unit_parked_metrics(broadcast, team_ids)
    home_non_progress = home_carrier_non_progress_metrics(
        broadcast,
        team_ids,
        header["contract"]["map"]["tileRows"],
        reactors,
    )
    eligibility_by_team = {
        str(team): not (
            ping_pong["barTrippedByTeam"][str(team)]
            or pickup_drop_cycles["barTrippedByTeam"][str(team)]
            or passivity["barTrippedByTeam"][str(team)]
            or formation_freeze["barTrippedByTeam"][str(team)]
            or stuck_carrier["barTrippedByTeam"][str(team)]
            or team_statue["barTrippedByTeam"][str(team)]
            or team_dance["barTrippedByTeam"][str(team)]
            or unit_parked["barTrippedByTeam"][str(team)]
            or home_non_progress["barTrippedByTeam"][str(team)]
        )
        for team in team_ids
    }
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
            "analysisLayout": analysis_layout,
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
        "feltDegeneracy": {
            "handoffPingPong": ping_pong,
            "pickupDropCycle": pickup_drop_cycles,
            "sustainedPassivity": passivity,
            "formationFreeze": formation_freeze,
            "stuckCarrier": stuck_carrier,
            "teamStatue": team_statue,
            "teamDance": team_dance,
            "unitParked": unit_parked,
            "homeCarrierNonProgress": home_non_progress,
            "cohortEligibilityByTeam": eligibility_by_team,
            "matchEligibleForCohortRead": all(eligibility_by_team.values()),
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
            "theaters": (
                "derived from midpoints between the three named Well y "
                "coordinates; exact bands are recorded in "
                "identity.analysisLayout"
            ),
            "homeCamp": (
                "derived from each home-region edge plus one fifth of map "
                "width; exact thresholds are recorded in identity.analysisLayout"
            ),
            "convoy": "allied non-carrier bodies at Chebyshev distance <=2",
            "routeDistance": "sum of authoritative Core relocation Chebyshev distances",
            "routeBaseline": "shortest traversable eight-way tile distance from source Well to scoring reactor",
            "contestedPickup": "proxy only: enemy body within Chebyshev distance 2 immediately before pickup",
            "birthAccess": "proxy only: number of teams within Chebyshev distance 3 at the birth post-state",
            "signatureUsefulEffects": "counted effect facts/transitions; not a causal value judgment",
            "feltDegeneracyEligibility": (
                "the frozen registration in balance/arc-relay-felt-"
                f"degeneracy-bars-{BARS['schema'].rsplit('-', 1)[-1]}.json "
                "excludes a team whenever any registered felt-degeneracy "
                "bar trips. The versioned metric blocks above carry the "
                "exact thresholds and causal definitions"
            ),
            "feltDegeneracyBars": str(BARS_PATH),
            "feltDegeneracyBarsSchema": BARS["schema"],
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("broadcast", type=Path)
    parser.add_argument("--record", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument(
        "--bars",
        type=Path,
        default=DEFAULT_BARS_PATH,
        help="frozen felt-degeneracy registration used for eligibility",
    )
    args = parser.parse_args()
    configure_bars(args.bars)
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
