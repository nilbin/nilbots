#!/usr/bin/env python3
"""Generate the 32-sheet Arc Relay evaluation population.

The source historical twelve-sheet pack remains untouched. This population
copies those controls, applies explicitly registered population-only repairs,
and adds twenty evaluation-grade sheets for the frozen, data-driven stock
mind. A structural-distance gate rejects exact or near duplicates before any
outcomes are run. This is not the player-facing sheet format.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
from typing import Any


REPO = Path(__file__).resolve().parent.parent
BASE_PACK = REPO / "arena-bots/arc-relay/balance-audit-v2-2026-08-01/sheets"
ARCHIVE = REPO / "arena-bots/arc-relay/sheet-population-v3-2026-08-02"
PACK = ARCHIVE / "sheets"
ARTIFACT = (
    REPO
    / "arena-bots/arc-relay/balance-audit-v2-2026-08-01/out/bot.wasm"
)
ARTIFACT_FROM_ARCHIVE = "../balance-audit-v2-2026-08-01/out/bot.wasm"
COHORT = ARCHIVE / "cohort.json"
DISTINCTNESS = ARCHIVE / "static-distinctness.json"
VALIDATOR = REPO / "scripts/generate-arc-relay-sheet.py"
MINIMUM_NEW_STATIC_DISTANCE = 0.18
POPULATION_MAP_ID = "arc-relay-threefold-home-gates-wide-01"

BASE_ENTRANTS = [
    "balanced", "control-grid", "convoy", "interception", "split",
    "relay-chain", "fortress-counterattack", "trap-punish",
    "fireline-picks", "displacement-control", "sustain-attrition",
    "feint-switch",
]

PATHS = {
    "north-fast": (
        [[4, 6], [8, 6], [13, 6], [15, 4]],
        [[13, 6], [9, 6], [5, 6], [2, 11]],
    ),
    "north-safe": (
        [[4, 9], [8, 9], [13, 8], [15, 4]],
        [[13, 6], [9, 9], [5, 9], [3, 10], [2, 11]],
    ),
    "north-high": (
        [[4, 8], [8, 7], [12, 6], [15, 4]],
        [[12, 6], [8, 8], [5, 10], [2, 11]],
    ),
    "north-screen": (
        [[4, 9], [8, 8], [12, 7], [14, 5]],
        [[11, 7], [7, 9], [4, 10], [2, 12]],
    ),
    "north-hook": (
        [[4, 11], [8, 10], [12, 8], [15, 4]],
        [[12, 6], [9, 9], [5, 11], [2, 11]],
    ),
    "centre-fast": (
        [[4, 9], [8, 9], [13, 9], [15, 11]],
        [[13, 9], [9, 9], [5, 9], [2, 11]],
    ),
    "centre-safe": (
        [[4, 13], [8, 13], [13, 13], [15, 11]],
        [[13, 13], [9, 13], [5, 13], [3, 12], [2, 11]],
    ),
    "centre-high": (
        [[5, 10], [8, 9], [12, 9], [15, 11]],
        [[12, 13], [8, 13], [5, 12], [2, 11]],
    ),
    "centre-flat": (
        [[4, 11], [8, 11], [12, 11], [14, 11]],
        [[11, 11], [7, 11], [4, 11], [2, 10]],
    ),
    "centre-low": (
        [[5, 13], [8, 12], [11, 12], [14, 12]],
        [[10, 12], [6, 12], [3, 12], [2, 11]],
    ),
    "south-fast": (
        [[4, 16], [8, 16], [13, 16], [15, 18]],
        [[13, 16], [9, 16], [5, 16], [2, 11]],
    ),
    "south-safe": (
        [[4, 13], [8, 13], [13, 14], [15, 18]],
        [[13, 16], [9, 13], [5, 13], [3, 12], [2, 11]],
    ),
    "south-low": (
        [[4, 13], [8, 15], [12, 16], [15, 18]],
        [[12, 16], [8, 14], [5, 12], [2, 11]],
    ),
    "south-screen": (
        [[5, 14], [9, 15], [13, 16], [15, 18]],
        [[12, 15], [8, 13], [4, 12], [2, 12]],
    ),
    "south-hook": (
        [[4, 11], [8, 12], [12, 14], [15, 18]],
        [[12, 16], [9, 13], [5, 11], [2, 11]],
    ),
}


def s(theater: str, role: str, partner: int, path: str) -> tuple:
    return theater, role, partner, path


def gambit(
    priority: int,
    name: str,
    trigger: str,
    duration: int,
    cooldown: int,
    roles: list[str],
    override: str,
    rally: str,
) -> dict[str, Any]:
    return {
        "priority": priority,
        "id": name,
        "trigger": trigger,
        "durationTicks": duration,
        "cooldownTicks": cooldown,
        "scopeRoles": roles,
        "roleOverride": override,
        "rallyLine": rally,
    }


def d(
    entrant_id: str,
    execution_style: str,
    composition: list[str],
    slots: list[tuple],
    policy: tuple[int, bool, int, int, bool, bool, bool],
    thesis: str,
    counter: str,
    failure: str,
    gambits: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    return {
        "entrantId": entrant_id,
        "executionStyle": execution_style,
        "composition": composition,
        "slots": slots,
        "policy": policy,
        "thesis": thesis,
        "counter": counter,
        "failure": failure,
        "gambits": gambits or [],
    }


NEW_DOCTRINES = [
    d(
        "courier-sprint",
        "split",
        ["kestrel", "kestrel", "relay", "relay", "towline",
         "switchback", "lantern", "patchbay"],
        [s("north", "carrier", 2, "north-high"),
         s("south", "carrier", 3, "south-low"),
         s("north", "carrier", 0, "north-fast"),
         s("south", "carrier", 1, "south-fast"),
         s("centre", "intercept", 5, "centre-fast"),
         s("centre", "reserve", 4, "centre-flat"),
         s("centre", "screen", 2, "centre-high"),
         s("centre", "screen", 3, "centre-low")],
        (1, False, 7, 1, True, True, True),
        "four fast couriers race the outer births while a central catch pair keeps width",
        "contest the outer pickup tiles and force fragile couriers to fight before they turn",
        "minimal escort density can turn early contact into repeated loose Cores",
        [gambit(10, "release-after-pulse", "after-own-pulse", 16, 45,
                ["reserve"], "carrier", "forward")],
    ),
    d(
        "smoke-convoy",
        "convoy",
        ["relay", "relay", "veil", "veil", "patchbay", "patchbay",
         "palisade", "towline"],
        [s("centre", "carrier", 2, "centre-high"),
         s("south", "carrier", 3, "south-low"),
         s("centre", "screen", 0, "centre-flat"),
         s("south", "screen", 1, "south-screen"),
         s("centre", "screen", 0, "centre-safe"),
         s("south", "screen", 1, "south-safe"),
         s("centre", "reserve", 0, "centre-low"),
         s("south", "intercept", 1, "south-fast")],
        (3, True, 15, 2, True, True, True),
        "two protected Core columns use smoke and repair to advance through centre and south",
        "split north and punish the slow screen before both columns establish spacing",
        "dense escorts surrender the unused theater and may arrive late to a new birth",
        [gambit(10, "smoke-backstop", "after-enemy-pulse", 22, 60,
                ["reserve"], "intercept", "home"),
         gambit(20, "convoy-collapse", "double-enemy-possession", 14, 36,
                ["screen"], "intercept", "middle")],
    ),
    d(
        "beacon-hunt",
        "interception",
        ["lantern", "lantern", "kestrel", "kestrel", "sunder",
         "sunder", "longshot", "towline"],
        [s("north", "intercept", 2, "north-screen"),
         s("south", "intercept", 3, "south-screen"),
         s("north", "carrier", 0, "north-fast"),
         s("south", "carrier", 1, "south-fast"),
         s("centre", "intercept", 6, "centre-high"),
         s("centre", "intercept", 7, "centre-low"),
         s("north", "screen", 2, "north-safe"),
         s("south", "reserve", 3, "south-safe")],
        (1, False, 8, 1, True, True, True),
        "reveal, paint, and collapse on enemy carriers while two darts handle births",
        "use smoke and cover to deny sight, then pressure the lightly screened Kestrels",
        "information hardware loses value when fights remain split beyond shared vision",
    ),
    d(
        "centre-phalanx",
        "fortress-counterattack",
        ["palisade", "palisade", "mason", "mason", "patchbay",
         "patchbay", "relay", "nest"],
        [s("centre", "intercept", 6, "centre-flat"),
         s("north", "carrier", 6, "north-hook"),
         s("centre", "screen", 6, "centre-high"),
         s("south", "carrier", 6, "south-hook"),
         s("centre", "screen", 6, "centre-safe"),
         s("centre", "intercept", 6, "centre-low"),
         s("centre", "carrier", 7, "centre-fast"),
         s("centre", "screen", 6, "centre-flat")],
        (3, True, 16, 2, True, True, True),
        "a six-body central phalanx builds a protected relay lane with two outer reserves",
        "take both outer births and force the construction ball to rotate through distance",
        "the centre stack can win its lane yet lose two simultaneous outer deliveries",
        [gambit(10, "outer-recall", "after-enemy-pulse", 24, 64,
                ["reserve"], "screen", "home"),
         gambit(20, "phalanx-release", "after-own-pulse", 18, 48,
                ["reserve"], "carrier", "forward")],
    ),
    d(
        "outer-pincers",
        "displacement-control",
        ["kestrel", "kestrel", "switchback", "switchback", "towline",
         "towline", "relay", "repulsor"],
        [s("north", "carrier", 2, "north-fast"),
         s("south", "carrier", 3, "south-fast"),
         s("north", "intercept", 0, "north-high"),
         s("south", "intercept", 1, "south-low"),
         s("north", "intercept", 6, "north-hook"),
         s("south", "intercept", 6, "south-hook"),
         s("centre", "reserve", 7, "centre-flat"),
         s("centre", "intercept", 6, "centre-fast")],
        (1, False, 6, 1, True, True, True),
        "paired outer wings race their own Core and displace the opposing return at once",
        "hold centre and refuse isolated outer fights until the two wings overextend",
        "almost no protective screen leaves a wing brittle after its first failed pickup",
        [gambit(10, "pincer-collapse", "double-enemy-possession", 14, 34,
                ["reserve"], "intercept", "middle")],
    ),
    d(
        "mine-crescent",
        "trap-punish",
        ["minesmith", "minesmith", "mortar", "mortar", "veil", "veil",
         "lantern", "relay"],
        [s("north", "screen", 2, "north-safe"),
         s("south", "screen", 3, "south-safe"),
         s("north", "intercept", 0, "north-screen"),
         s("south", "intercept", 1, "south-screen"),
         s("north", "screen", 2, "north-hook"),
         s("south", "screen", 3, "south-hook"),
         s("centre", "reserve", 7, "centre-flat"),
         s("centre", "carrier", 6, "centre-fast")],
        (2, True, 11, 2, True, True, True),
        "a mine-and-smoke crescent controls both outer Well approaches around one central courier",
        "attack centre before the outer denial field matures or reveal and clear one crescent",
        "only one dedicated carrier makes the formation sensitive to a central denial chain",
    ),
    d(
        "pod-lattice",
        "control-grid",
        ["nest", "nest", "mason", "mason", "palisade", "palisade",
         "patchbay", "relay"],
        [s("north", "screen", 4, "north-screen"),
         s("south", "screen", 5, "south-screen"),
         s("centre", "screen", 7, "centre-high"),
         s("centre", "screen", 7, "centre-low"),
         s("north", "carrier", 0, "north-fast"),
         s("south", "carrier", 1, "south-fast"),
         s("centre", "reserve", 7, "centre-safe"),
         s("centre", "carrier", 6, "centre-fast")],
        (3, True, 14, 2, True, True, True),
        "three carrier lanes grow a distributed lattice of pods, blocks, repair, and walls",
        "break one branch quickly, then rotate before the other lattice arms can reinforce",
        "spreading construction across all Wells can leave every local cluster incomplete",
    ),
    d(
        "rail-screen",
        "fireline-picks",
        ["longshot", "longshot", "palisade", "palisade", "lantern",
         "lantern", "sunder", "relay"],
        [s("north", "intercept", 2, "north-safe"),
         s("south", "intercept", 3, "south-safe"),
         s("north", "screen", 0, "north-screen"),
         s("south", "screen", 1, "south-screen"),
         s("north", "carrier", 2, "north-high"),
         s("south", "carrier", 3, "south-low"),
         s("centre", "intercept", 7, "centre-flat"),
         s("centre", "carrier", 6, "centre-fast")],
        (1, True, 10, 2, True, True, True),
        "projector faces and sensor masts establish two public rails around a central courier",
        "smoke or flank the firing bases and make their slow carrier solve multiple births",
        "fixed firing geometry can be bypassed after the opponent reads the chosen corridors",
    ),
    d(
        "hook-burst",
        "displacement-control",
        ["towline", "towline", "repulsor", "repulsor", "sunder",
         "sunder", "kestrel", "relay"],
        [s("north", "intercept", 6, "north-fast"),
         s("south", "intercept", 7, "south-fast"),
         s("centre", "intercept", 6, "centre-high"),
         s("centre", "intercept", 7, "centre-low"),
         s("north", "screen", 6, "north-hook"),
         s("south", "screen", 7, "south-hook"),
         s("centre", "carrier", 4, "centre-fast"),
         s("centre", "carrier", 5, "centre-safe")],
        (2, False, 8, 1, True, True, True),
        "hooks expose a carrier, designators mark it, and radial bursts break its escort geometry",
        "travel in separated pairs and punish displacement bodies when they cross the screen",
        "a mistimed pull or burst can improve the enemy route and strand the two central carriers",
    ),
    d(
        "null-veil",
        "sustain-attrition",
        ["hush", "hush", "veil", "veil", "patchbay", "relay",
         "switchback", "palisade"],
        [s("north", "intercept", 2, "north-high"),
         s("south", "intercept", 3, "south-low"),
         s("north", "carrier", 5, "north-screen"),
         s("south", "carrier", 5, "south-screen"),
         s("centre", "screen", 5, "centre-high"),
         s("centre", "carrier", 4, "centre-fast"),
         s("centre", "carrier", 5, "centre-low"),
         s("centre", "carrier", 6, "centre-safe")],
        (2, False, 9, 2, True, True, True),
        "smoke hides a two-carrier centre while null fields erase the signatures used to collapse it",
        "use basic fire and wide pickups so suppression cannot deny every relevant action",
        "low direct reach lets revealed opponents dictate engagement distance",
        [gambit(10, "veil-collapse", "double-enemy-possession", 16, 38,
                ["screen", "reserve"], "intercept", "middle")],
    ),
    d(
        "repair-web",
        "sustain-attrition",
        ["patchbay", "patchbay", "towline", "kestrel", "palisade", "nest",
         "hush", "relay"],
        [s("north", "screen", 2, "north-safe"),
         s("south", "screen", 3, "south-safe"),
         s("north", "carrier", 0, "north-high"),
         s("south", "carrier", 1, "south-low"),
         s("centre", "screen", 7, "centre-high"),
         s("centre", "screen", 7, "centre-low"),
         s("centre", "reserve", 7, "centre-flat"),
         s("centre", "carrier", 6, "centre-fast")],
        (3, True, 15, 1, True, True, True),
        "three carriers sit inside overlapping repair, projectile cover, sentry, and suppression webs",
        "split damage across distant theaters, then focus the healer that rotates late",
        "the web trades chase speed for survival and can lose births without ever losing a fight",
    ),
    d(
        "mortar-wheel",
        "trap-punish",
        ["mortar", "mortar", "kestrel", "kestrel", "switchback",
         "switchback", "lantern", "relay"],
        [s("north", "intercept", 2, "north-hook"),
         s("south", "intercept", 3, "south-hook"),
         s("north", "carrier", 0, "north-fast"),
         s("south", "carrier", 1, "south-fast"),
         s("centre", "reserve", 7, "centre-high"),
         s("centre", "reserve", 7, "centre-low"),
         s("centre", "screen", 2, "centre-flat"),
         s("centre", "carrier", 6, "centre-fast")],
        (1, False, 7, 1, True, True, True),
        "mobile carriers wheel between births while twin mortars punish the route they just vacated",
        "close on the artillery and hold the centre so rotations cross a contested seam",
        "the fire base and courier wing can separate into two individually fragile groups",
        [gambit(10, "wheel-forward", "after-own-pulse", 18, 46,
                ["reserve"], "carrier", "forward"),
         gambit(20, "wheel-recall", "after-enemy-pulse", 20, 58,
                ["reserve"], "intercept", "home")],
    ),
    d(
        "counter-courier",
        "balanced",
        ["relay", "relay", "towline", "towline", "kestrel", "kestrel",
         "patchbay", "hush"],
        [s("north", "carrier", 2, "north-safe"),
         s("south", "carrier", 3, "south-safe"),
         s("north", "screen", 0, "north-screen"),
         s("south", "screen", 1, "south-screen"),
         s("centre", "intercept", 6, "centre-high"),
         s("centre", "intercept", 7, "centre-low"),
         s("centre", "screen", 0, "centre-flat"),
         s("centre", "reserve", 1, "centre-safe")],
        (2, True, 12, 1, True, True, True),
        "safe outer couriers advance only behind hooks and repair, then counter a stolen tempo centrally",
        "race the centre birth before the paired outer screens establish their protected returns",
        "conservative routes give aggressive wide sheets uncontested initiative",
        [gambit(10, "courier-counter", "after-enemy-pulse", 22, 62,
                ["reserve", "screen"], "intercept", "home")],
    ),
    d(
        "sensor-grid",
        "control-grid",
        ["lantern", "lantern", "minesmith", "minesmith", "nest", "nest",
         "longshot", "relay"],
        [s("north", "screen", 2, "north-safe"),
         s("south", "screen", 3, "south-safe"),
         s("north", "screen", 6, "north-hook"),
         s("south", "screen", 6, "south-hook"),
         s("centre", "screen", 7, "centre-high"),
         s("centre", "screen", 7, "centre-low"),
         s("centre", "intercept", 7, "centre-flat"),
         s("centre", "carrier", 6, "centre-fast")],
        (2, True, 13, 3, True, True, True),
        "sensors reveal routes into a three-Well mine-and-sentry grid for one protected runner",
        "overload the single courier or clear one theater faster than the grid can be rebuilt",
        "six static controllers can accumulate information without converting it into deliveries",
    ),
    d(
        "breach-column",
        "displacement-control",
        ["mortar", "mortar", "repulsor", "repulsor", "palisade",
         "palisade", "sunder", "relay"],
        [s("centre", "intercept", 7, "centre-high"),
         s("centre", "intercept", 7, "centre-low"),
         s("centre", "carrier", 6, "centre-fast"),
         s("centre", "intercept", 6, "centre-safe"),
         s("centre", "screen", 7, "centre-flat"),
         s("centre", "screen", 7, "centre-flat"),
         s("north", "carrier", 7, "north-hook"),
         s("south", "carrier", 6, "south-fast")],
        (2, False, 9, 2, True, True, True),
        "a six-body centre breach column uses blast, burst, projection, and paint before releasing south",
        "take north and avoid the centre collision until the lone carrier has to leave its column",
        "the narrow column advertises its axis and has only one native pickup runner",
        [gambit(10, "breach-release", "after-own-pulse", 20, 50,
                ["reserve"], "carrier", "forward")],
    ),
    d(
        "elastic-reserve",
        "feint-switch",
        ["switchback", "switchback", "relay", "relay", "towline",
         "towline", "veil", "patchbay"],
        [s("north", "reserve", 2, "north-high"),
         s("south", "reserve", 3, "south-low"),
         s("north", "carrier", 0, "north-fast"),
         s("south", "carrier", 1, "south-fast"),
         s("centre", "intercept", 2, "centre-high"),
         s("centre", "intercept", 3, "centre-low"),
         s("centre", "screen", 2, "centre-flat"),
         s("centre", "screen", 3, "centre-safe")],
        (2, False, 8, 1, True, True, True),
        "paired reserves repeatedly change jobs while a stable towline-and-smoke centre protects two carriers",
        "force both reserve triggers together so one bounded response necessarily arrives late",
        "role elasticity can become travel churn if pulses alternate faster than the cooldown windows",
        [gambit(10, "elastic-backstop", "after-enemy-pulse", 20, 60,
                ["reserve"], "intercept", "home"),
         gambit(20, "elastic-collapse", "double-enemy-possession", 14, 34,
                ["screen"], "intercept", "middle"),
         gambit(30, "elastic-release", "after-own-pulse", 16, 46,
                ["reserve"], "carrier", "forward")],
    ),
    d(
        "three-well-race",
        "split",
        ["kestrel", "kestrel", "relay", "relay", "lantern", "switchback",
         "towline", "veil"],
        [s("north", "carrier", 6, "north-fast"),
         s("south", "carrier", 7, "south-fast"),
         s("centre", "carrier", 4, "centre-fast"),
         s("north", "carrier", 0, "north-high"),
         s("centre", "reserve", 2, "centre-high"),
         s("south", "intercept", 1, "south-low"),
         s("north", "screen", 0, "north-screen"),
         s("centre", "screen", 2, "centre-low")],
        (1, True, 6, 1, True, True, True),
        "four assigned runners compete for all three births rather than waiting to identify one best lane",
        "win local fights against the thin screens and turn one failed race into an exposed return",
        "too many pickup candidates can leave live carriers without enough protection",
    ),
    d(
        "home-counterpunch",
        "interception",
        ["longshot", "longshot", "sunder", "sunder", "towline", "towline",
         "palisade", "relay"],
        [s("north", "intercept", 7, "north-safe"),
         s("south", "intercept", 7, "south-safe"),
         s("centre", "intercept", 7, "centre-high"),
         s("centre", "intercept", 7, "centre-low"),
         s("north", "intercept", 7, "north-hook"),
         s("south", "intercept", 7, "south-hook"),
         s("centre", "screen", 7, "centre-flat"),
         s("centre", "carrier", 6, "centre-safe")],
        (2, False, 13, 3, True, True, False),
        "six interceptors form a deep rail, paint, and hook net around one counter-courier",
        "decline the offered carrier fight and bank from multiple Wells before the net can turn",
        "waiting for an enemy route can concede neutral births and create off-theater passivity",
    ),
    d(
        "smoke-and-mines",
        "trap-punish",
        ["veil", "veil", "minesmith", "minesmith", "mortar", "mortar",
         "hush", "relay"],
        [s("north", "screen", 2, "north-screen"),
         s("south", "screen", 3, "south-screen"),
         s("north", "screen", 4, "north-hook"),
         s("south", "screen", 5, "south-hook"),
         s("centre", "intercept", 7, "centre-high"),
         s("centre", "intercept", 7, "centre-low"),
         s("centre", "reserve", 7, "centre-flat"),
         s("centre", "carrier", 6, "centre-fast")],
        (2, True, 12, 2, True, True, True),
        "smoke conceals mines and delayed fire while a null-backed central pair converts the trap",
        "reveal the setup or stay wide enough that the trap package cannot cover both returns",
        "the formation may spend cooldowns on empty route assumptions and leave its courier alone",
        [gambit(10, "mine-collapse", "after-enemy-pulse", 20, 62,
                ["reserve", "screen"], "intercept", "middle")],
    ),
    d(
        "rotating-bastions",
        "fortress-counterattack",
        ["mason", "mason", "palisade", "palisade", "switchback",
         "switchback", "relay", "relay"],
        [s("north", "screen", 6, "north-safe"),
         s("south", "screen", 7, "south-safe"),
         s("north", "screen", 6, "north-high"),
         s("south", "screen", 7, "south-low"),
         s("centre", "carrier", 6, "centre-high"),
         s("centre", "carrier", 7, "centre-low"),
         s("north", "carrier", 0, "north-fast"),
         s("south", "carrier", 1, "south-fast")],
        (2, True, 10, 2, True, True, True),
        "two planted outer bastions feed a four-body centre that rotates into the next scoring lane",
        "break a bastion before its centre reserve arrives or pull the rotation away with a loose Core",
        "the construct pair can be stranded after the mobile half commits to a distant birth",
        [gambit(10, "bastion-recall", "after-enemy-pulse", 22, 64,
                ["reserve"], "intercept", "home"),
         gambit(20, "bastion-roll", "after-own-pulse", 18, 48,
                ["reserve"], "carrier", "forward")],
    ),
]

TARGETED_REPAIR_REVISIONS = {
    "breach-column": 3,
    "centre-phalanx": 4,
    "null-veil": 4,
    "rail-screen": 3,
    "repair-web": 3,
    "rotating-bastions": 3,
}

CONTROL_REVISIONS = {
    "sustain-attrition": d(
        "sustain-attrition",
        "sustain-attrition",
        ["patchbay", "kestrel", "palisade", "palisade", "hush", "nest",
         "relay", "towline"],
        [s("north", "screen", 1, "north-safe"),
         s("north", "carrier", 0, "north-fast"),
         s("north", "screen", 1, "north-screen"),
         s("south", "screen", 7, "south-screen"),
         s("centre", "intercept", 6, "centre-high"),
         s("centre", "reserve", 6, "centre-low"),
         s("centre", "carrier", 4, "centre-fast"),
         s("south", "carrier", 3, "south-low")],
        (3, True, 14, 1, True, True, True),
        "one mobile outer courier converts births while repair, paired projection, suppression, and a sentry preserve the attrition web",
        "split the two outer carriers and focus the lone healer before the projector pair can establish both return lanes",
        "one healer must choose between theaters, while the central web can still concede a fast opposite-side birth",
    ),
}


def load_validator():
    spec = importlib.util.spec_from_file_location("arc_sheet", VALIDATOR)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot import {VALIDATOR}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def make_slot(unit_id: int, definition: tuple) -> dict[str, Any]:
    theater, role, partner, path_id = definition
    outbound, return_path = PATHS[path_id]
    return {
        "unitId": unit_id,
        "theater": theater,
        "role": role,
        "partnerUnitId": partner,
        "outboundPath": outbound,
        "returnPath": return_path,
    }


def make_sheet(base: dict[str, Any], doctrine: dict[str, Any]) -> dict[str, Any]:
    handoff, assigned, failure_ticks, follow, escort_focus, carrier_focus, fallback = (
        doctrine["policy"]
    )
    value = json.loads(json.dumps(base))
    revision = TARGETED_REPAIR_REVISIONS.get(doctrine["entrantId"], 2)
    value["sheetId"] = (
        f"population-{doctrine['entrantId']}-"
        f"style-{doctrine['executionStyle']}-v{revision}"
    )
    value["mapId"] = POPULATION_MAP_ID
    value["composition"] = doctrine["composition"]
    value["slots"] = [
        make_slot(index, definition)
        for index, definition in enumerate(doctrine["slots"])
    ]
    value["gambits"] = doctrine["gambits"]
    value["policies"] = {
        "carrier": {
            "handoffHealthAtOrBelow": handoff,
            "preferAssignedTheater": assigned,
            "routeFailureTicks": failure_ticks,
        },
        "escort": {
            "followDistance": follow,
            "focusEnemyCarrier": escort_focus,
        },
        "interception": {
            "focusEnemyCarrier": carrier_focus,
            "looseCoreFallback": fallback,
        },
    }
    allocation = Counter(slot[0] for slot in doctrine["slots"])
    value["auditDimensions"] = {
        "adaptationStyle": (
            "bounded rising-edge gambits" if doctrine["gambits"] else "static"
        ),
        "allocation": " / ".join(
            f"{allocation.get(theater, 0)} {theater}"
            for theater in ("north", "centre", "south")
        ),
        "policyStyle": doctrine["thesis"],
        "executionStyle": doctrine["executionStyle"],
        "sheetRevision": revision,
        "visibleCounter": doctrine["counter"],
        "failureMode": doctrine["failure"],
    }
    value["auditStatus"] = {
        "playerFacingProductSchema": False,
        "provisionalEvaluationOnly": True,
        "purpose": "32-sheet structural diversity and balance population",
    }
    return value


def encoded(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def multiset_distance(left: Counter, right: Counter) -> float:
    keys = set(left) | set(right)
    total = sum(left.values()) + sum(right.values())
    return 0.0 if total == 0 else sum(
        abs(left[key] - right[key]) for key in keys
    ) / total


def policy_vector(sheet: dict[str, Any]) -> list[float]:
    carrier = sheet["policies"]["carrier"]
    escort = sheet["policies"]["escort"]
    interception = sheet["policies"]["interception"]
    return [
        carrier["handoffHealthAtOrBelow"] / 3,
        1.0 if carrier["preferAssignedTheater"] else 0.0,
        min(carrier["routeFailureTicks"], 18) / 18,
        min(max(escort["followDistance"], 0), 3) / 3,
        1.0 if escort["focusEnemyCarrier"] else 0.0,
        1.0 if interception["focusEnemyCarrier"] else 0.0,
        1.0 if interception["looseCoreFallback"] else 0.0,
    ]


def structural_distance(left: dict[str, Any], right: dict[str, Any]) -> float:
    composition = multiset_distance(
        Counter(left["composition"]), Counter(right["composition"])
    )
    assignments = multiset_distance(
        Counter(
            (klass, slot["theater"], slot["role"])
            for klass, slot in zip(left["composition"], left["slots"])
        ),
        Counter(
            (klass, slot["theater"], slot["role"])
            for klass, slot in zip(right["composition"], right["slots"])
        ),
    )
    routes = multiset_distance(
        Counter(
            (slot["theater"], slot["role"],
             tuple(map(tuple, slot["outboundPath"])),
             tuple(map(tuple, slot["returnPath"])))
            for slot in left["slots"]
        ),
        Counter(
            (slot["theater"], slot["role"],
             tuple(map(tuple, slot["outboundPath"])),
             tuple(map(tuple, slot["returnPath"])))
            for slot in right["slots"]
        ),
    )
    policies = sum(
        abs(a - b) for a, b in zip(policy_vector(left), policy_vector(right))
    ) / 7
    gambits = multiset_distance(
        Counter(
            (entry["trigger"], tuple(entry["scopeRoles"]),
             entry["roleOverride"], entry["rallyLine"])
            for entry in left["gambits"]
        ),
        Counter(
            (entry["trigger"], tuple(entry["scopeRoles"]),
             entry["roleOverride"], entry["rallyLine"])
            for entry in right["gambits"]
        ),
    )
    return (
        composition * 0.28
        + assignments * 0.36
        + routes * 0.12
        + policies * 0.10
        + gambits * 0.14
    )


def gameplay_identity(sheet: dict[str, Any]) -> bytes:
    return encoded({
        key: sheet[key]
        for key in (
            "composition", "slots", "zones", "rallyLines", "policies",
            "gambits",
        )
    })


def distinctness(sheets: dict[str, dict[str, Any]]) -> dict[str, Any]:
    exact = Counter(gameplay_identity(sheet) for sheet in sheets.values())
    if any(count > 1 for count in exact.values()):
        raise ValueError("population contains exact gameplay duplicates")
    rows = []
    new_ids = {entry["entrantId"] for entry in NEW_DOCTRINES}
    for entrant_id in sorted(new_ids):
        candidates = sorted(
            (structural_distance(sheets[entrant_id], other), other_id)
            for other_id, other in sheets.items()
            if other_id != entrant_id
        )
        distance, closest = candidates[0]
        rows.append({
            "entrantId": entrant_id,
            "closestEntrantId": closest,
            "distance": round(distance, 6),
            "passes": distance >= MINIMUM_NEW_STATIC_DISTANCE,
        })
    failures = [row for row in rows if not row["passes"]]
    if failures:
        raise ValueError(
            "new sheets below static distance gate: "
            + ", ".join(
                f"{row['entrantId']}={row['distance']} to "
                f"{row['closestEntrantId']}" for row in failures
            )
        )
    return {
        "schema": "arc-relay-sheet-static-distinctness-v1",
        "populationSize": len(sheets),
        "historicalControls": len(BASE_ENTRANTS),
        "newSheets": len(NEW_DOCTRINES),
        "exactGameplayDuplicates": 0,
        "minimumNewSheetDistance": MINIMUM_NEW_STATIC_DISTANCE,
        "metric": {
            "compositionWeight": 0.28,
            "classRoleTheaterWeight": 0.36,
            "routeShapeWeight": 0.12,
            "policyWeight": 0.10,
            "gambitGrammarWeight": 0.14,
            "authority": (
                "pre-outcome structural gate; empirical execution-shape "
                "distance is evaluated separately after the anchor sweep"
            ),
        },
        "newSheetClosestPairs": rows,
    }


def cohort(sheets: dict[str, dict[str, Any]]) -> dict[str, Any]:
    artifact_hash = sha256(ARTIFACT)
    return {
        "schema": "arc-relay-native-eligible-population-v1",
        "cohortId": "arc-relay-sheet-population-v3",
        "eligibilityBars": "../../../balance/arc-relay-felt-degeneracy-bars-v2.json",
        "excludedBeforeOutcomeReads": [],
        "provenance": {
            "claimScope": (
                "shared-stock-mind sheet-space diversity and balance screen; "
                "not independent-lineage or human-fun authority"
            ),
            "sharedExecutionEngine": ARTIFACT_FROM_ARCHIVE,
            "sharedExecutionEngineSha256": artifact_hash,
            "authoringBoundary": (
                "evaluation-grade ARS1 sheets, not player-facing product schema"
            ),
            "rulesChange": "none",
            "staticDistinctness": "static-distinctness.json",
        },
        "entrants": [
            {
                "entrantId": entrant_id,
                "artifact": ARTIFACT_FROM_ARCHIVE,
                "artifactSha256": artifact_hash,
                "sheet": f"sheets/{entrant_id}.json",
                "sheetSha256": sha256(PACK / f"{entrant_id}.json"),
            }
            for entrant_id in sorted(sheets)
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail when generated population files differ instead of writing",
    )
    args = parser.parse_args()
    if len(NEW_DOCTRINES) != 20:
        raise ValueError("population expansion must define exactly twenty sheets")
    validator = load_validator()
    base = json.loads((BASE_PACK / "balanced.json").read_text(encoding="utf-8"))
    sheets: dict[str, dict[str, Any]] = {}
    expected: dict[Path, bytes] = {}
    for entrant_id in BASE_ENTRANTS:
        value = json.loads(
            (BASE_PACK / f"{entrant_id}.json").read_text(encoding="utf-8")
        )
        value["mapId"] = POPULATION_MAP_ID
        if entrant_id in CONTROL_REVISIONS:
            value = make_sheet(value, CONTROL_REVISIONS[entrant_id])
        validator.validate(value)
        sheets[entrant_id] = value
        expected[PACK / f"{entrant_id}.json"] = encoded(value)
    for doctrine in NEW_DOCTRINES:
        entrant_id = doctrine["entrantId"]
        if entrant_id in sheets:
            raise ValueError(f"duplicate entrant id: {entrant_id}")
        value = make_sheet(base, doctrine)
        validator.validate(value)
        sheets[entrant_id] = value
        expected[PACK / f"{entrant_id}.json"] = encoded(value)

    distance_value = distinctness(sheets)
    expected[DISTINCTNESS] = encoded(distance_value)
    changed = []
    for path, content in expected.items():
        if not path.is_file() or path.read_bytes() != content:
            changed.append(path)
            if not args.check:
                path.parent.mkdir(parents=True, exist_ok=True)
                temporary = path.with_suffix(path.suffix + ".tmp")
                temporary.write_bytes(content)
                temporary.replace(path)
    if args.check and changed:
        raise SystemExit("stale sheet population: " + ", ".join(map(str, changed)))

    cohort_content = encoded(cohort(sheets))
    if not COHORT.is_file() or COHORT.read_bytes() != cohort_content:
        if args.check:
            raise SystemExit(f"stale sheet population cohort: {COHORT}")
        COHORT.parent.mkdir(parents=True, exist_ok=True)
        temporary = COHORT.with_suffix(COHORT.suffix + ".tmp")
        temporary.write_bytes(cohort_content)
        temporary.replace(COHORT)
    verb = "checked" if args.check else "generated"
    closest = min(
        row["distance"] for row in distance_value["newSheetClosestPairs"]
    )
    print(
        f"{verb} 20 new sheets + 12 controls; static minimum={closest:.6f}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
