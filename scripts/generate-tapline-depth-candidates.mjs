#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const root = path.join(
  repo,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/strategy-depth",
);

const ref = (name) => ({ shared: name });
const target = (well, posture = "line") => ({ move: { to: { well }, posture } });
const move = (id, well, posture = "line", extra = {}) => ({
  id,
  ...extra,
  ...target(well, posture),
});
const role = (id, classes, upgradePath, orders) => ({
  id,
  classes,
  upgradePath,
  orders,
});
const use = (id, skill, targetValue, when = "signature-ready", extra = {}) => ({
  id,
  when,
  use: { skill, target: targetValue, ...extra },
});

function commonConditions(extra = {}) {
  return {
    "carrying-core": { fact: "self-carrying", operator: "equals", value: 1 },
    "empty-handed": { fact: "self-carrying", operator: "equals", value: 0 },
    "loose-core-known": { fact: "known-loose-cores", operator: "at-least", value: 1 },
    hurt: { fact: "self-health-percent", operator: "at-most", value: 35 },
    healthy: { fact: "self-health-percent", operator: "at-least", value: 75 },
    "enemy-nearby": {
      fact: "self-nearest-visible-enemy-distance",
      operator: "at-most",
      value: 7,
    },
    "signature-ready": { fact: "signature-ready", operator: "equals", value: 1 },
    ...extra,
  };
}

function commonOrders({
  looseTarget = "nearest-known-loose-core",
  fightTargets = ["carrier", "weakest", "nearest"],
  fightStyle = "advance",
  chaseLimit = 4,
  includeFight = true,
  scorePrefix = [],
  extra = {},
} = {}) {
  return {
    "score-a-core": {
      when: "carrying-core",
      until: "empty-handed",
      timeout: 180,
      cooldown: 2,
      urgency: "urgent",
      orders: [
        ...scorePrefix,
        { id: "return-to-reactor", move: { to: "own-reactor" } },
      ],
    },
    "collect-loose-core": {
      when: { all: ["empty-handed", "loose-core-known"] },
      capacity: "two-per-core",
      move: { to: looseTarget },
    },
    "recover-health": {
      when: "hurt",
      until: "healthy",
      timeout: 16,
      cooldown: 18,
      urgency: "urgent",
      orders: [
        {
          id: "reach-healing",
          move: { to: "nearest-heal-zone", posture: "rear" },
        },
      ],
    },
    ...(includeFight
      ? {
          "fight-visible-enemy": {
            when: { all: ["empty-handed", "enemy-nearby"] },
            fight: { targets: fightTargets, style: fightStyle, chaseLimit },
          },
        }
      : {}),
    ...extra,
  };
}

const standardPrefix = () => [
  ref("score-a-core"),
  ref("collect-loose-core"),
  ref("recover-health"),
];
const standardTail = (destination) => [
  ref("fight-visible-enemy"),
  destination,
];

function sheet(id, roles, sharedOrders, conditions, capacities = { "two-per-core": 2 }) {
  return {
    schema: "arc-relay-orders-v2",
    sheetId: id,
    mapPlan: "map-plan.json",
    roles,
    sharedOrders,
    capacities,
    conditions,
  };
}

function mapPlan(id, { zones = {}, routes = {}, anchors = {} } = {}) {
  return {
    schema: "arc-relay-map-plan-v2",
    mapPlanId: `${id}-map`,
    mapId: "arc-relay-tapline-01",
    zones,
    routes,
    anchors,
  };
}

const candidates = [
  {
    id: "tapline-two-heists-v1",
    profile: "tapline-depth-score-124",
    title: "03 Two Heists and a Tip",
    orders: sheet(
      "tapline-two-heists-v1",
      [
        role(
          "north-heist",
          ["kestrel", "kestrel", "veil", "hush"],
          ["reach", "damage", "vision", "vitality"],
          [
            ...standardPrefix(),
            use("dash-heist-contact", "vector-dash", "nearest-enemy", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            use("smoke-heist-exit", "smoke-canister", "self", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            use("silence-heist-screen", "null-field", "self", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            ...standardTail({
              id: "raid-enemy-well-north",
              move: {
                patrol: "enemy-well-loop",
                via: "north-shadow-ingress",
                posture: "front",
              },
            }),
          ],
        ),
        role(
          "south-extraction",
          ["relay", "lantern", "towline", "patchbay"],
          ["vitality", "vision", "reach", "damage"],
          [
            ...standardPrefix(),
            use("pass-premium-core", "arc-toss", "nearest-ally-toward-own-reactor", {
              all: ["carrying-core", "signature-ready"],
            }),
            use("reveal-heist-route", "survey-flare", "nearest-enemy", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            use("hook-heist-pursuer", "tractor-hook", "enemy-carrier", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            use("repair-extraction", "repair-beam", "weakest-ally"),
            ref("fight-visible-enemy"),
            move("take-the-final-tip", "own", "rear", {
              when: { all: ["two-pulses", "two-charge-held"] },
            }),
            {
              id: "raid-enemy-well-south",
              move: {
                patrol: "enemy-well-loop",
                via: "south-shadow-ingress",
                posture: "front",
              },
            },
          ],
        ),
      ],
      commonOrders({ looseTarget: "highest-bank-value-known-loose-core" }),
      commonConditions({
        "two-pulses": { fact: "reactor-pulses", operator: "at-least", value: 2 },
        "two-charge-held": { fact: "reactor-charge", operator: "at-least", value: 2 },
      }),
    ),
    map: mapPlan("tapline-two-heists-v1", {
      routes: {
        "north-shadow-ingress": {
          waypoints: [[3, 3], [3, 1], [15, 1], [27, 1], [27, 3], [27, 7], [25, 10]],
        },
        "south-shadow-ingress": {
          waypoints: [[3, 9], [5, 14], [4, 18], [3, 23], [3, 25], [15, 25], [27, 25], [27, 23], [25, 23]],
        },
        "enemy-well-loop": {
          waypoints: [[19, 20], [20, 20], [20, 21], [19, 21], [18, 21], [18, 20], [18, 19], [19, 19], [19, 20]],
        },
      },
    }),
  },
  {
    id: "tapline-turncoat-intercept-v1",
    profile: "tapline-depth-score-124",
    title: "07 Turncoat Intercept",
    orders: sheet(
      "tapline-turncoat-intercept-v1",
      [
        role(
          "return-cut",
          ["towline", "towline", "sunder", "longshot", "hush"],
          ["damage", "vision", "reach", "vitality"],
          [
            ...standardPrefix(),
            {
              id: "spring-return-cut",
              when: { all: ["empty-handed", "enemy-carrier-visible", "return-lane-live"] },
              timeout: 90,
              cooldown: 3,
              urgency: "urgent",
              orders: [
                use("hook-cheap-carrier", "tractor-hook", "enemy-carrier"),
                use("paint-cheap-carrier", "target-paint", "enemy-carrier"),
                use("rail-cheap-carrier", "rail-line", "weakest-enemy"),
                use("silence-carrier-escort", "null-field", "self"),
                {
                  id: "kill-cheap-carrier",
                  when: "enemy-nearby",
                  fight: {
                    targets: ["carrier", "marked", "weakest", "nearest"],
                    style: "advance",
                    chaseLimit: 6,
                  },
                },
                { id: "close-return-lane", move: { to: { zone: "enemy-return-cut" }, posture: "front" } },
              ],
            },
            ...standardTail({
              id: "patrol-enemy-well-screen",
              move: { patrol: "enemy-well-loop", posture: "front" },
            }),
          ],
        ),
        role(
          "turncoat-courier",
          ["relay", "lantern", "kestrel"],
          ["vitality", "vision", "reach", "damage"],
          [
            ...standardPrefix(),
            use("pass-stolen-core", "arc-toss", "nearest-ally-toward-own-reactor", {
              all: ["carrying-core", "signature-ready"],
            }),
            use("reveal-return-cut", "survey-flare", "nearest-enemy", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            use("dash-to-spill", "vector-dash", "nearest-enemy", {
              all: ["empty-handed", "enemy-nearby", "signature-ready"],
            }),
            ...standardTail({ id: "shadow-return-cut", move: { to: { zone: "enemy-return-cut" }, posture: "rear" } }),
          ],
        ),
      ],
      commonOrders({
        looseTarget: "highest-bank-value-known-loose-core",
        scorePrefix: [
          {
            id: "pass-stalled-core-homeward",
            when: "carrier-stalled",
            handoff: { target: "nearest-ally-toward-own-reactor" },
          },
          {
            id: "drop-stalled-core-homeward",
            when: "carrier-stalled",
            drop: true,
          },
          {
            id: "fight-through-carrier-pocket",
            when: "carrier-stalled",
            fight: {
              targets: ["nearest", "weakest", "carrier"],
              style: "advance",
              chaseLimit: 3,
            },
          },
        ],
      }),
      commonConditions({
        "carrier-stalled": {
          fact: "self-stuck-ticks",
          operator: "at-least",
          value: 4,
        },
        "enemy-carrier-visible": { fact: "visible-enemy-carriers", operator: "at-least", value: 1 },
        "return-lane-live": {
          fact: "remembered-enemies-in-zone",
          operator: "at-least",
          value: 1,
          zone: "enemy-return-cut",
          freshnessTicks: 45,
        },
      }),
    ),
    map: mapPlan("tapline-turncoat-intercept-v1", {
      zones: { "enemy-return-cut": { rect: [18, 16, 25, 23] } },
      routes: {
        "enemy-well-loop": {
          waypoints: [[19, 20], [20, 20], [20, 21], [19, 21], [18, 21], [18, 20], [18, 19], [19, 19], [19, 20]],
        },
      },
    }),
  },
  {
    id: "tapline-crowned-rail-v1",
    profile: "tapline-depth-veteran-command",
    title: "13 Crowned Rail",
    orders: sheet(
      "tapline-crowned-rail-v1",
      [
        role("ace", ["longshot"], ["damage", "reach", "vitality", "vision"], [
          ...standardPrefix(),
          use("rail-for-the-crown", "rail-line", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          move("cash-out-the-crown", "enemy", "front", { when: "ace-awakened" }),
          { id: "earn-the-crown-on-patrol", move: { patrol: "centre-rail-loop", via: "north-rail-ingress", posture: "line" } },
        ]),
        role("ace-medic", ["patchbay"], ["reach", "vitality", "vision"], [
          ...standardPrefix(),
          use("repair-the-ace", "repair-beam", { role: "ace" }, {
            all: ["ace-hurt", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          { id: "trail-the-ace", move: { follow: "ace", posture: "rear" } },
        ]),
        role("designator", ["sunder"], ["reach", "damage", "vision"], [
          ...standardPrefix(),
          use("paint-the-ace-target", "target-paint", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          { id: "patrol-the-designator-line", move: { patrol: "north-rail-loop", via: "north-rail-ingress", posture: "front" } },
        ]),
        role("firing-line", ["mason", "palisade"], ["vitality", "vision", "damage"], [
          ...standardPrefix(),
          use("raise-rail-cover", "hardlight-block", "nearest-ally", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("project-rail-screen", "prism-wall", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          { id: "patrol-the-rail-line", move: { patrol: "south-rail-loop", via: "south-rail-ingress", posture: "line" } },
        ]),
        role("spotter", ["lantern"], ["vision", "reach", "vitality"], [
          ...standardPrefix(),
          use("light-the-ace-target", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          { id: "spot-the-centre-rail", move: { patrol: "north-rail-loop", via: "north-rail-ingress", posture: "rear" } },
        ]),
        role("rail-guard", ["repulsor"], ["vitality", "damage", "vision"], [
          ...standardPrefix(),
          use("clear-the-rail", "kinetic-burst", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("guard-the-far-rail", "enemy", "front")),
        ]),
        role("quartermaster", ["relay"], ["vitality", "vision", "reach"], [
          ...standardPrefix(),
          use("pass-the-ace-a-core", "arc-toss", { role: "ace" }, {
            all: ["carrying-core", "signature-ready"],
          }),
          ...standardTail(move("work-home-supply", "own", "rear")),
        ]),
      ],
      commonOrders({ fightTargets: ["carrier", "veteran", "marked", "weakest", "nearest"] }),
      commonConditions({
        "ace-awakened": { fact: "role-highest-level", operator: "at-least", value: 2, role: "ace" },
        "ace-hurt": { fact: "role-lowest-health-percent", operator: "at-most", value: 80, role: "ace" },
      }),
    ),
    map: mapPlan("tapline-crowned-rail-v1", {
      routes: {
        "north-rail-ingress": {
          waypoints: [[3, 3], [8, 3], [12, 11]],
        },
        "south-rail-ingress": {
          waypoints: [[3, 9], [8, 9], [12, 14]],
        },
        "centre-rail-loop": {
          waypoints: [[15, 13], [16, 12], [17, 13], [16, 14], [15, 13], [14, 14], [13, 13], [14, 12], [15, 13]],
        },
        "north-rail-loop": {
          waypoints: [[12, 11], [15, 11], [18, 11], [18, 12], [15, 12], [12, 12], [12, 11]],
        },
        "south-rail-loop": {
          waypoints: [[12, 14], [15, 14], [18, 14], [18, 15], [15, 15], [12, 15], [12, 14]],
        },
      },
    }),
  },
  {
    id: "tapline-emergent-standard-v1",
    profile: "tapline-depth-veteran-command",
    title: "16 Emergent Standard",
    orders: sheet(
      "tapline-emergent-standard-v1",
      [
        role("first-contact", ["kestrel", "towline", "sunder"], ["damage", "reach", "vitality", "vision"], [
          ...standardPrefix(),
          use("dash-first-contact", "vector-dash", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("hook-first-contact", "tractor-hook", "enemy-carrier", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("paint-first-contact", "target-paint", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("contest-centre", "centre", "front")),
        ]),
        role("standard-support", ["patchbay", "palisade", "hush"], ["vitality", "reach", "vision"], [
          ...standardPrefix(),
          use("repair-emergent-standard", "repair-beam", { selector: "most-experienced-ally" }),
          use("screen-emergent-standard", "prism-wall", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("silence-around-standard", "null-field", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          {
            id: "rally-to-emergent-standard",
            move: { follow: { selector: "most-experienced-ally" }, posture: "rear" },
          },
          move("hold-centre-until-a-standard-emerges", "centre", "rear"),
        ]),
        role("objective-reserve", ["lantern", "relay"], ["vision", "vitality", "reach"], [
          ...standardPrefix(),
          use("reveal-for-standard", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("pass-objective-forward", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          ...standardTail(move("work-own-supply", "own", "rear")),
        ]),
      ],
      commonOrders({ fightTargets: ["veteran", "carrier", "weakest", "nearest"] }),
      commonConditions(),
    ),
    map: mapPlan("tapline-emergent-standard-v1"),
  },
  {
    id: "tapline-cutoff-execution-v1",
    profile: "tapline-depth-respawn-wave",
    title: "20 Cutoff Execution",
    orders: sheet(
      "tapline-cutoff-execution-v1",
      [
        role("execution-line", ["sunder", "sunder", "longshot", "towline", "hush"], ["damage", "reach", "vision", "vitality"], [
          ...standardPrefix(),
          {
            id: "execute-after-cutoff",
            when: { all: ["empty-handed", "enemy-nearby", "wave-cutoff-passed"] },
            timeout: 15,
            urgency: "urgent",
            orders: [
              use("paint-cutoff-target", "target-paint", "weakest-enemy"),
              use("rail-cutoff-target", "rail-line", "weakest-enemy"),
              use("hook-cutoff-target", "tractor-hook", "nearest-enemy"),
              use("silence-cutoff-target", "null-field", "self"),
              {
                id: "finish-cutoff-target",
                fight: { targets: ["marked", "weakest", "nearest"], style: "advance", chaseLimit: 6 },
              },
            ],
          },
          ...standardTail(move("hold-centre-before-cutoff", "centre", "front")),
        ]),
        role("execution-support", ["lantern", "patchbay", "relay"], ["vision", "vitality", "reach", "damage"], [
          ...standardPrefix(),
          use("reveal-cutoff-target", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "wave-cutoff-passed", "signature-ready"],
          }),
          use("repair-execution-line", "repair-beam", "weakest-ally"),
          use("pass-before-reinforcement", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          ref("fight-visible-enemy"),
          move("work-own-well-between-cutoffs", "own", "rear"),
        ]),
      ],
      commonOrders({ fightStyle: "kite", chaseLimit: 3 }),
      commonConditions({
        "wave-cutoff-passed": { fact: "ticks-until-respawn-wave", operator: "at-most", value: 15 },
      }),
    ),
    map: mapPlan("tapline-cutoff-execution-v1"),
  },
  {
    id: "tapline-pre-wave-evacuation-v1",
    profile: "tapline-depth-respawn-wave",
    title: "23 Pre-Wave Evacuation",
    orders: sheet(
      "tapline-pre-wave-evacuation-v1",
      [
        role("exchange-guard", ["switchback", "switchback", "patchbay", "veil"], ["vitality", "reach", "vision", "damage"], [
          ...standardPrefix(),
          use("exchange-weak-before-wave", "exchange", "weakest-ally", {
            all: ["enemy-wave-arriving", "signature-ready"],
          }),
          use("repair-evacuation", "repair-beam", "weakest-ally"),
          use("smoke-evacuation", "smoke-canister", "self", {
            all: ["enemy-wave-arriving", "signature-ready"],
          }),
          ref("evacuate-before-wave"),
          ...standardTail(move("contest-centre-between-waves", "centre", "line")),
        ]),
        role("survivor-couriers", ["kestrel", "relay", "palisade", "lantern"], ["vitality", "vision", "reach", "damage"], [
          ...standardPrefix(),
          use("dash-covering-action", "vector-dash", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("pass-carrier-home", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          use("screen-the-withdrawal", "prism-wall", "nearest-enemy", {
            all: ["enemy-wave-arriving", "signature-ready"],
          }),
          use("reveal-pursuit", "survey-flare", "nearest-enemy", {
            all: ["enemy-wave-arriving", "signature-ready"],
          }),
          ref("evacuate-before-wave"),
          ...standardTail(move("work-enemy-well-between-waves", "enemy", "front")),
        ]),
      ],
      commonOrders({
        fightStyle: "kite",
        chaseLimit: 3,
        extra: {
          "evacuate-before-wave": {
            when: { all: ["two-enemies-returning", "enemy-wave-arriving"] },
            timeout: 8,
            urgency: "urgent",
            orders: [{ id: "withdraw-home", move: { to: "own-reactor", posture: "rear" } }],
          },
        },
      }),
      commonConditions({
        "two-enemies-returning": { fact: "enemy-respawns-next-wave", operator: "at-least", value: 2 },
        "enemy-wave-arriving": { fact: "ticks-until-respawn-wave", operator: "at-most", value: 6 },
      }),
    ),
    map: mapPlan("tapline-pre-wave-evacuation-v1"),
  },
  {
    id: "tapline-double-mason-lockbox-v1",
    profile: "tapline-depth-durable-deployments",
    title: "25 Double Mason Lockbox",
    orders: sheet(
      "tapline-double-mason-lockbox-v1",
      [
        role("own-well-mason", ["mason"], ["vitality", "vision", "damage"], [
          ...standardPrefix(),
          {
            id: "maintain-own-well-lock",
            when: { any: ["deployment-missing", "deployment-expiring", "deployment-damaged"] },
            orders: [
              use("place-own-well-lock", "hardlight-block", "nearest-ally", "signature-ready", { at: "own-well-lock" }),
              { id: "stage-own-well-lock", urgency: "routine", move: { to: { adjacentTo: "own-well-lock" }, posture: "rear" } },
            ],
          },
          ...standardTail(move("hold-own-well", "own", "rear")),
        ]),
        role("centre-mason", ["mason"], ["vitality", "vision", "damage"], [
          ...standardPrefix(),
          {
            id: "maintain-centre-return-lock",
            when: { any: ["deployment-missing", "deployment-expiring", "deployment-damaged"] },
            orders: [
              use("place-centre-return-lock", "hardlight-block", "nearest-ally", "signature-ready", { at: "centre-return-lock" }),
              { id: "stage-centre-return-lock", urgency: "routine", move: { to: { adjacentTo: "centre-return-lock" }, posture: "front" } },
            ],
          },
          ...standardTail(move("hold-centre-well", "centre", "line")),
        ]),
        role("deployment-net", ["nest", "minesmith"], ["vision", "damage", "vitality"], [
          ...standardPrefix(),
          {
            id: "maintain-support-deployment",
            when: "deployment-missing",
            orders: [
              use("seed-own-sentry", "sentinel-seed", "nearest-ally", "signature-ready", { at: "own-sentry" }),
              use("seed-centre-trip", "trip-node", "nearest-ally", "signature-ready", { at: "centre-trip" }),
              { id: "stage-support-deployment", move: { to: { adjacentTo: "centre-trip" }, posture: "line" } },
            ],
          },
          ...standardTail(move("watch-centre-lock", "centre", "line")),
        ]),
        role("lockbox-support", ["hush", "relay", "patchbay", "lantern"], ["vitality", "vision", "reach", "damage"], [
          ...standardPrefix(),
          use("silence-lockbox", "null-field", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("pass-lockbox-core", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          use("repair-lockbox", "repair-beam", "weakest-ally"),
          use("reveal-demolition", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("work-own-well-lockbox", "own", "rear")),
        ]),
      ],
      commonOrders(),
      commonConditions({
        "deployment-missing": { fact: "deployment-active", operator: "equals", value: 0 },
        "deployment-expiring": { fact: "deployment-lifetime-percent", operator: "at-most", value: 20 },
        "deployment-damaged": { fact: "deployment-durability-percent", operator: "at-most", value: 34 },
      }),
    ),
    map: mapPlan("tapline-double-mason-lockbox-v1", {
      anchors: {
        "own-well-lock": [12, 6],
        "centre-return-lock": [18, 14],
        "own-sentry": [12, 7],
        "centre-trip": [17, 13],
      },
    }),
  },
  {
    id: "tapline-demolition-march-v1",
    profile: "tapline-depth-durable-deployments",
    title: "30 Demolition March",
    orders: sheet(
      "tapline-demolition-march-v1",
      [
        role("demolition-line", ["longshot", "mortar", "hush"], ["damage", "reach", "vision", "vitality"], [
          ...standardPrefix(),
          use("rail-through-deployment", "rail-line", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("lob-over-deployment", "falling-star", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("suppress-deployment", "null-field", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          {
            id: "clear-the-lock",
            when: "empty-handed",
            fight: {
              targets: ["deployment", "carrier", "veteran", "weakest", "nearest"],
              style: "advance",
              chaseLimit: 7,
            },
          },
          move("march-to-defended-well", "enemy", "front"),
        ]),
        role("demolition-overwatch", ["longshot", "lantern", "sunder"], ["damage", "vision", "reach", "vitality"], [
          ...standardPrefix(),
          use("rail-from-overwatch", "rail-line", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("reveal-deployment-line", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("paint-builder-screen", "target-paint", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          {
            id: "clear-the-lock-from-overwatch",
            when: "empty-handed",
            fight: {
              targets: ["deployment", "carrier", "veteran", "weakest", "nearest"],
              style: "advance",
              chaseLimit: 6,
            },
          },
          move("hold-demolition-overwatch", "centre", "line"),
        ]),
        role("raid-conversion", ["relay", "kestrel"], ["vitality", "reach", "damage", "vision"], [
          ...standardPrefix(),
          use("pass-through-cleared-lane", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          use("dash-through-cleared-lane", "vector-dash", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          {
            id: "fight-through-cleared-lane",
            when: { all: ["empty-handed", "enemy-nearby"] },
            fight: {
              targets: ["deployment", "carrier", "weakest", "nearest"],
              style: "advance",
              chaseLimit: 5,
            },
          },
          move("convert-demolition-to-raid", "enemy", "front"),
        ]),
      ],
      commonOrders({ includeFight: false }),
      commonConditions(),
    ),
    map: mapPlan("tapline-demolition-march-v1"),
  },
  {
    id: "tapline-twin-relay-circuit-v1",
    profile: "tapline-depth-roster-wildcard",
    title: "Roster proof — Twin Relay Circuit",
    orders: sheet(
      "tapline-twin-relay-circuit-v1",
      [
        role("north-courier", ["relay"], ["vitality", "reach", "vision", "damage"], [
          ...standardPrefix(),
          use("pass-north-cargo", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          ...standardTail(move("work-north-supply", "own", "rear")),
        ]),
        role("south-courier", ["relay"], ["reach", "vitality", "vision", "damage"], [
          ...standardPrefix(),
          use("pass-south-cargo", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          ...standardTail(move("work-centre-supply", "centre", "rear")),
        ]),
        role("relay-screen", ["kestrel", "patchbay", "mason"], ["vitality", "reach", "damage", "vision"], [
          ...standardPrefix(),
          use("dash-relay-gap", "vector-dash", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("repair-relay-line", "repair-beam", "weakest-ally"),
          use("raise-relay-cover", "hardlight-block", "nearest-ally", "signature-ready", {
            at: "relay-cover",
          }),
          ...standardTail(move("screen-relay-route", "centre", "front")),
        ]),
        role("relay-watch", ["hush", "lantern", "towline"], ["vision", "reach", "damage", "vitality"], [
          ...standardPrefix(),
          use("silence-relay-pursuit", "null-field", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("reveal-relay-pursuit", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("hook-relay-pursuit", "tractor-hook", "enemy-carrier", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("watch-relay-route", "centre", "line")),
        ]),
      ],
      commonOrders(),
      commonConditions(),
    ),
    map: mapPlan("tapline-twin-relay-circuit-v1", {
      anchors: { "relay-cover": [13, 12] },
    }),
  },
  {
    id: "tapline-depth-relay-circuit-v1",
    profile: "tapline-depth-pack",
    title: "Combined proof — Depth Relay Circuit",
    orders: sheet(
      "tapline-depth-relay-circuit-v1",
      [
        role("depth-raider", ["relay"], ["vitality", "damage", "reach", "vitality"], [
          ref("score-premium-core"),
          ref("collect-loose-core"),
          ref("recover-health"),
          ...standardTail(move("raid-depth-well", "enemy", "rear")),
        ]),
        role("depth-hauler", ["relay"], ["reach", "vitality", "vision", "damage"], [
          ref("score-premium-core"),
          ref("collect-loose-core"),
          ref("recover-health"),
          ...standardTail(move("work-depth-centre", "centre", "rear")),
        ]),
        role("depth-ace", ["kestrel"], ["damage", "damage", "vitality", "reach"], [
          ...standardPrefix(),
          use("dash-depth-contact", "vector-dash", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("hunt-depth-well", "enemy", "front")),
        ]),
        role("depth-vanguard", ["towline", "hush", "lantern"], ["damage", "reach", "vision", "vitality"], [
          ...standardPrefix(),
          {
            id: "reset-before-depth-wave",
            when: { all: ["empty-handed", "friendly-wave-due", "wave-close"] },
            timeout: 8,
            cooldown: 12,
            urgency: "urgent",
            orders: [move("yield-before-wave", "own", "rear")],
          },
          use("hook-depth-carrier", "tractor-hook", "enemy-carrier", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("silence-depth-pack", "null-field", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("reveal-depth-pack", "survey-flare", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("pressure-depth-centre", "centre", "front")),
        ]),
        role("depth-medic", ["patchbay"], ["reach", "vitality", "vitality", "damage"], [
          ...standardPrefix(),
          use("repair-depth-ace", "repair-beam", { role: "depth-ace" }),
          ref("fight-visible-enemy"),
          {
            id: "trail-depth-ace",
            when: "depth-ace-online",
            move: { follow: "depth-ace", posture: "rear" },
          },
          move("hold-depth-centre-rear", "centre", "rear"),
        ]),
        role("depth-builder", ["mason"], ["vitality", "vitality", "damage", "vision"], [
          ...standardPrefix(),
          use("place-depth-cover", "hardlight-block", "nearest-ally", {
            all: ["signature-ready", "cover-missing"],
          }, {
            at: "depth-cover",
          }),
          use("refresh-depth-cover", "hardlight-block", "nearest-ally", {
            all: ["signature-ready", "cover-worn"],
          }, {
            at: "depth-cover",
          }),
          ...standardTail(move("hold-depth-cover", "centre", "front")),
        ]),
      ],
      commonOrders({
        looseTarget: "highest-bank-value-known-loose-core",
        fightTargets: ["veteran", "carrier", "deployment", "weakest", "nearest"],
        extra: {
          "score-premium-core": {
            when: "carrying-core",
            until: "empty-handed",
            timeout: 180,
            cooldown: 2,
            urgency: "urgent",
            orders: [
              use("pass-premium-depth-core", "arc-toss", "nearest-ally-toward-own-reactor", {
                all: ["premium-cargo", "signature-ready"],
              }),
              { id: "return-premium-core", move: { to: "own-reactor" } },
            ],
          },
        },
      }),
      commonConditions({
        "wave-close": { fact: "ticks-until-respawn-wave", operator: "at-most", value: 8 },
        "friendly-wave-due": { fact: "friendly-respawns-next-wave", operator: "at-least", value: 1 },
        "premium-cargo": { fact: "self-carried-core-bank-value", operator: "at-least", value: 4 },
        "depth-ace-online": {
          fact: "role-highest-level",
          role: "depth-ace",
          operator: "at-least",
          value: 2,
        },
        "cover-missing": { fact: "deployment-active", operator: "equals", value: 0 },
        "cover-worn": { fact: "deployment-durability-percent", operator: "at-most", value: 35 },
      }),
    ),
    map: mapPlan("tapline-depth-relay-circuit-v1", {
      anchors: { "depth-cover": [13, 12] },
    }),
  },
  {
    id: "tapline-tempered-standard-v1",
    profile: "tapline-depth-combat-scale-10",
    title: "Scale proof — Tempered Standard",
    orders: sheet(
      "tapline-tempered-standard-v1",
      [
        role("tempered-gun", ["longshot", "mortar"], ["damage", "damage", "vitality", "vitality"], [
          ...standardPrefix(),
          use("fire-tempered-rail", "rail-line", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("drop-tempered-star", "falling-star", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail({
            id: "cycle-tempered-firing-line",
            move: {
              patrol: "tempered-firing-loop",
              via: "north-tempered-ingress",
              posture: "rear",
            },
          }),
        ]),
        role("tempered-support", ["patchbay", "sunder"], ["reach", "vitality", "damage", "vitality"], [
          ...standardPrefix(),
          use("restore-ten-hull", "repair-beam", "weakest-ally"),
          use("paint-ten-more", "target-paint", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("support-tempered-line", "centre", "line")),
        ]),
        role("tempered-screen", ["mason", "palisade"], ["vitality", "vitality", "damage", "vision"], [
          ...standardPrefix(),
          use("raise-thirty-hull-cover", "hardlight-block", "nearest-ally", "signature-ready", {
            at: "tempered-cover",
          }),
          use("screen-tempered-line", "prism-wall", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("hold-tempered-centre", "centre", "front")),
        ]),
        role("tempered-courier", ["relay", "kestrel"], ["vitality", "reach", "vision", "damage"], [
          ...standardPrefix(),
          use("pass-tempered-core", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          use("dash-tempered-gap", "vector-dash", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("work-tempered-home", "own", "rear")),
        ]),
      ],
      commonOrders({ fightTargets: ["marked", "weakest", "carrier", "nearest"] }),
      commonConditions(),
    ),
    map: mapPlan("tapline-tempered-standard-v1", {
      routes: {
        "north-tempered-ingress": {
          waypoints: [[3, 3], [8, 3], [12, 11]],
        },
        "tempered-firing-loop": {
          waypoints: [[12, 11], [15, 11], [18, 11], [18, 12], [15, 12], [12, 12], [12, 11]],
        },
      },
      anchors: { "tempered-cover": [13, 12] },
    }),
  },
  {
    id: "tapline-iron-interval-v1",
    profile: "tapline-depth-durability-plus-10",
    title: "Durability proof — Iron Interval",
    orders: sheet(
      "tapline-iron-interval-v1",
      [
        role("iron-centre", ["palisade", "mason", "repulsor"], ["vitality"], [
          ...standardPrefix(),
          use("project-iron-wall", "prism-wall", "nearest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("raise-iron-cover", "hardlight-block", "nearest-ally", "signature-ready", {
            at: "iron-cover",
          }),
          use("clear-iron-ring", "kinetic-burst", "self", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("anchor-iron-centre", "centre", "front")),
        ]),
        role("repair-rotation", ["patchbay", "patchbay"], ["reach", "vitality", "vitality", "vision"], [
          ...standardPrefix(),
          use("rotate-twenty-repair", "repair-beam", "weakest-ally"),
          ref("fight-visible-enemy"),
          { id: "trail-iron-centre", move: { follow: "iron-centre", posture: "rear" } },
        ]),
        role("iron-interdiction", ["towline", "sunder"], ["damage", "reach", "vitality", "vision"], [
          ...standardPrefix(),
          use("hook-iron-target", "tractor-hook", "enemy-carrier", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          use("paint-iron-target", "target-paint", "weakest-enemy", {
            all: ["empty-handed", "enemy-nearby", "signature-ready"],
          }),
          ...standardTail(move("interdict-iron-centre", "centre", "line")),
        ]),
        role("iron-courier", ["relay"], ["vitality", "reach", "vision", "damage"], [
          ...standardPrefix(),
          use("pass-through-iron-line", "arc-toss", "nearest-ally-toward-own-reactor", {
            all: ["carrying-core", "signature-ready"],
          }),
          ...standardTail(move("work-iron-home", "own", "rear")),
        ]),
      ],
      commonOrders({ fightStyle: "hold", chaseLimit: 3 }),
      commonConditions(),
    ),
    map: mapPlan("tapline-iron-interval-v1", {
      anchors: { "iron-cover": [13, 12] },
    }),
  },
];

for (const candidate of candidates) {
  const directory = path.join(root, candidate.id);
  fs.mkdirSync(directory, { recursive: true });
  fs.writeFileSync(path.join(directory, "orders.json"), `${JSON.stringify(candidate.orders, null, 2)}\n`);
  fs.writeFileSync(path.join(directory, "map-plan.json"), `${JSON.stringify(candidate.map, null, 2)}\n`);
  fs.writeFileSync(
    path.join(directory, "candidate.json"),
    `${JSON.stringify({
      schema: "arc-relay-tapline-depth-candidate-v1",
      strategy: candidate.title,
      profile: candidate.profile,
      orders: "orders.json",
      mapPlan: "map-plan.json",
      status: "experimental-exhibit-only",
    }, null, 2)}\n`,
  );
}

console.log(`wrote ${candidates.length} Tapline depth candidates under ${root}`);
