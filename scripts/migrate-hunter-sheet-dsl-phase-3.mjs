#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const target = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-v1.json",
);
const source = JSON.parse(fs.readFileSync(target, "utf8"));

function splitWellResponse(document) {
  const plane = document.doctrines;
  const response = plane.operations["well-response"];
  if (!response) return false;
  const [home, centre, north, south, surge] = response.variants;
  const shared = structuredClone(response);
  delete shared.variants;
  const homeDefense = { ...structuredClone(shared), variants: [home] };
  const wellRace = {
    ...structuredClone(shared),
    fight: "home-defense",
    staff: structuredClone(centre.staff),
    variants: [centre, north, south],
  };
  const surgeOperation = {
    ...structuredClone(shared),
    fight: "home-defense",
    staff: structuredClone(surge.staff),
    variants: [{ ...surge, staff: undefined }],
  };
  plane.operations = {
    assassin: plane.operations.assassin,
    "home-defense": homeDefense,
    "well-race": wellRace,
    surge: surgeOperation,
    "well-patrol": plane.operations["well-patrol"],
    "reserve-patrol": plane.operations["reserve-patrol"],
  };
  for (const [role, doctrine] of Object.entries(plane.roles)) {
    for (const mode of doctrine.modes) {
      if (!Array.isArray(mode.muster)) continue;
      mode.muster = mode.muster.flatMap((operation) => {
        if (operation !== "well-response") return [operation];
        return role === "medic"
          ? ["home-defense", "well-race"]
          : ["home-defense", "well-race", "surge"];
      });
    }
  }
  return true;
}

function addTeamTempo(document) {
  const plane = document.doctrines;
  if (plane.tempo) return false;
  plane.tempo = {
    cycle: [
      { modeId: "prowl", forTicks: 90 },
      { modeId: "prowl-south", forTicks: 90 },
    ],
    interrupts: [
      {
        modeId: "apex",
        while: "shadow-op or well-op",
        until: "shadow-cooled and well-cooled",
        minimumTicks: 8,
        stableTicks: 2,
      },
    ],
  };
  plane.operations["well-patrol"].variants = [
    {
      variantId: "apex",
      during: "apex",
      patrol: { route: "stage-loop" },
      chaseLeash: 14,
    },
    {
      variantId: "prowl",
      during: "prowl",
      patrol: { route: "stage-loop" },
      chaseLeash: 1,
    },
    {
      variantId: "prowl-south",
      during: "prowl-south",
      patrol: { route: "stage-loop" },
      chaseLeash: 1,
    },
  ];
  plane.operations["reserve-patrol"].variants = [
    {
      variantId: "apex",
      during: "apex",
      patrol: { zone: "own-approach" },
      chaseLeash: 14,
    },
    {
      variantId: "prowl",
      during: "prowl",
      patrol: { zone: "own-approach" },
      chaseLeash: 2,
    },
    {
      variantId: "prowl-south",
      during: "prowl-south",
      patrol: { zone: "own-approach" },
      chaseLeash: 2,
    },
  ];
  return true;
}

function addSquadAimPolicy(document) {
  let changed = false;
  for (const operationId of ["well-patrol", "reserve-patrol"]) {
    const fight = document.doctrines.operations[operationId]?.fight;
    if (typeof fight !== "object" || fight.aim) continue;
    fight.aim = "facing";
    changed = true;
  }
  return changed;
}

function renameOperationReach(document) {
  let changed = false;
  for (const operation of Object.values(document.doctrines.operations)) {
    if (Object.hasOwn(operation, "chaseLeash")) {
      operation.reach = operation.chaseLeash;
      delete operation.chaseLeash;
      changed = true;
    }
    for (const variant of operation.variants) {
      if (!Object.hasOwn(variant, "chaseLeash")) continue;
      variant.reach = variant.chaseLeash;
      delete variant.chaseLeash;
      changed = true;
    }
  }
  const assassin = document.doctrines.operations.assassin;
  if (
    assassin
    && !Object.hasOwn(assassin, "reach")
    && !Object.hasOwn(assassin.variants[0], "reach")
  ) {
    assassin.variants[0].reach = 14;
    changed = true;
  }
  return changed;
}

function addOperationRepairConsent(document) {
  const assassin = document.doctrines.operations.assassin;
  if (!assassin || assassin.repair === true) return false;
  assassin.repair = true;
  return true;
}

function addTrafficPolicy(document) {
  let changed = false;
  const desired = {
    assassin: "yield",
    "reserve-patrol": "priority",
  };
  for (const [operationId, traffic] of Object.entries(desired)) {
    const operation = document.doctrines.operations[operationId];
    if (!operation || operation.traffic === traffic) continue;
    operation.traffic = traffic;
    changed = true;
  }
  return changed;
}

function addHunterTempoOverrides(document) {
  const hunter = document.doctrines.roles.hunter;
  if (!hunter || hunter.tempo) return false;
  hunter.tempo = {
    "prowl-south": {
      patrol: "shadow-south-long",
      reach: 3,
    },
    apex: {
      assault: "strike-line",
      reach: 14,
      fight: {
        targets: {
          prefer: ["carrier", "weakest"],
          maximumAttackers: 1,
        },
        chase: { leash: 6 },
        defense: { radius: 2, return: true },
        signature: "damage-first",
        release: { hiddenTicks: 2, unreachableTicks: 3 },
      },
    },
  };
  return true;
}

function deduplicateOperationDefaults(document) {
  let changed = false;
  const defaults = {
    assassin: 14,
    "home-defense": 4,
    "well-race": 1,
    surge: 1,
    "well-patrol": 1,
    "reserve-patrol": 2,
  };
  for (const [operationId, reach] of Object.entries(defaults)) {
    const operation = document.doctrines.operations[operationId];
    if (!operation) continue;
    if (operation.reach !== reach) {
      operation.reach = reach;
      changed = true;
    }
    for (const variant of operation.variants) {
      if (variant.reach === reach) {
        delete variant.reach;
        changed = true;
      }
      if (
        variant.staff
        && operation.staff
        && JSON.stringify(variant.staff) === JSON.stringify(operation.staff)
      ) {
        delete variant.staff;
        changed = true;
      }
    }
  }
  return changed;
}

if (source.conditions && source.doctrines?.roles && source.doctrines?.operations) {
  const split = splitWellResponse(source);
  const tempo = addTeamTempo(source);
  const aim = addSquadAimPolicy(source);
  const reach = renameOperationReach(source);
  const repair = addOperationRepairConsent(source);
  const traffic = addTrafficPolicy(source);
  const hunterTempo = addHunterTempoOverrides(source);
  const deduplicated = deduplicateOperationDefaults(source);
  if (
    split || tempo || aim || reach || repair || traffic || hunterTempo
    || deduplicated
  ) {
    fs.writeFileSync(target, `${JSON.stringify(source, null, 1)}\n`);
    console.log("updated Hunter's Phase 3 doctrine grammar");
  } else {
    console.log("hunter-v1 is already on the Phase 3 DSL");
  }
  process.exit(0);
}

const byRole = Object.fromEntries(source.roles.map((role) => [role.roleId, role]));
const tags = {
  hunter: ["shadow"],
  medic: ["well"],
  hauler: ["well", "runner", "assassin"],
  carrier: ["well", "runner", "assassin"],
  lancer: ["well", "runner", "assassin"],
  eyes: ["well", "runner"],
  reserve: ["reserves"],
};
const postures = {
  hunter: "scout",
  medic: "support",
  hauler: "wide",
  carrier: "core",
  lancer: "vanguard",
  eyes: "rear",
  reserve: "tail",
};

const ghost = source.doctrines.ghost;
delete ghost.role;

const roleDoctrines = {
  hunter: ghost,
  medic: {
    custody: "well-custody",
    repair: {
      prefer: ["weakest", "carrier", "any"],
      maximumProvidersPerTarget: 1,
      minimumProviderSeparation: 2,
      reserveHealthPercent: 30,
      survivalFallback: "evade",
    },
    modes: [
      { recover: "auto" },
      { muster: "escort" },
      { muster: ["assassin", "well-response", "well-patrol"] },
    ],
  },
  hauler: {
    custody: "well-custody",
    modes: [
      { fetch: "well-custody" },
      { muster: ["assassin", "well-response", "well-patrol"] },
    ],
  },
  carrier: {
    custody: "well-custody",
    modes: [
      { fetch: "well-custody" },
      { muster: ["assassin", "well-response", "well-patrol"] },
    ],
  },
  lancer: {
    custody: "well-custody",
    modes: [
      { fetch: "well-custody" },
      { muster: "escort" },
      { muster: ["assassin", "well-response", "well-patrol"] },
    ],
  },
  eyes: {
    custody: "well-custody",
    modes: [
      { fetch: "well-custody" },
      { muster: ["well-response", "well-patrol"] },
    ],
  },
  reserve: {
    custody: "well-custody",
    modes: [
      { fetch: "well-custody" },
      { muster: ["reserve-patrol"] },
    ],
  },
};

const wellFight = {
  targets: {
    prefer: ["carrier", "anchor", "weakest"],
    maximumAttackers: 3,
  },
  chase: { leash: 1 },
  defense: { radius: 3, return: true },
  signature: "control-first",
  release: { hiddenTicks: 1, unreachableTicks: 3 },
};
const disruptFight = {
  aim: "facing",
  targets: {
    prefer: ["carrier", "weakest"],
    maximumAttackers: 2,
  },
  chase: { leash: 0 },
  defense: { radius: 4, return: true },
  lockTicks: 3,
  signature: "control-first",
  release: { hiddenTicks: 1, unreachableTicks: 2 },
};

const operations = {
  assassin: {
    custody: "well-custody",
    formation: "file",
    pace: "slowest",
    conceal: true,
    fight: {
      aim: "facing",
      targets: {
        prefer: ["carrier", "weakest"],
        maximumAttackers: 1,
      },
      chase: { leash: 6 },
      defense: { radius: 2, return: true },
      signature: "damage-first",
      release: { hiddenTicks: 2, unreachableTicks: 3 },
    },
    staff: [
      {
        tag: "assassin",
        minimum: 1,
        preferred: 1,
        maximum: 1,
        carrier: "forbid",
        select: "most-experienced",
      },
      {
        role: "medic",
        minimum: 0,
        preferred: 1,
        maximum: 1,
        carrier: "forbid",
      },
    ],
    variants: [
      {
        variantId: "strike",
        while: "shadow-op or well-op",
        assault: { route: "strike-line" },
        hold: { stableTicks: 2, minimumTicks: 10, cooldownTicks: 4 },
      },
    ],
  },
  "well-response": {
    custody: "well-custody",
    formation: "screen",
    pace: "free",
    fight: wellFight,
    staff: [
      {
        tag: "well",
        minimum: 1,
        preferred: 4,
        maximum: 6,
        carrier: "allow",
        near: "home-gate",
      },
    ],
    variants: [
      {
        variantId: "home",
        while: "gate-threat or home-threat",
        until: "approach-clear and home-clear",
        patrol: { zone: "own-home" },
        chaseLeash: 4,
      },
      {
        variantId: "centre",
        while: "centre-birth-soon",
        patrol: { route: "centre-well-loop" },
        arrivalRadius: 1,
        chaseLeash: 1,
        completion: "carrier-loss",
        hold: { timeoutTicks: 110, cooldownTicks: 20 },
        staff: [
          {
            tag: "runner",
            minimum: 1,
            preferred: 3,
            maximum: 3,
            carrier: "allow",
            near: "home-gate",
          },
          {
            role: "medic",
            minimum: 0,
            preferred: 1,
            maximum: 1,
            carrier: "forbid",
            near: "home-gate",
          },
        ],
      },
      {
        variantId: "north",
        while: "north-birth-soon",
        patrol: { route: "north-well-loop" },
        arrivalRadius: 1,
        chaseLeash: 1,
        completion: "carrier-loss",
        hold: { timeoutTicks: 110, cooldownTicks: 20 },
        staff: [
          {
            tag: "runner",
            minimum: 1,
            preferred: 3,
            maximum: 3,
            carrier: "allow",
            near: "home-gate",
          },
          {
            role: "medic",
            minimum: 0,
            preferred: 1,
            maximum: 1,
            carrier: "forbid",
            near: "home-gate",
          },
        ],
      },
      {
        variantId: "south",
        while: "south-birth-soon",
        patrol: { route: "south-well-loop" },
        arrivalRadius: 1,
        chaseLeash: 1,
        completion: "carrier-loss",
        hold: { timeoutTicks: 110, cooldownTicks: 20 },
        staff: [
          {
            tag: "runner",
            minimum: 1,
            preferred: 3,
            maximum: 3,
            carrier: "allow",
            near: "home-gate",
          },
          {
            role: "medic",
            minimum: 0,
            preferred: 1,
            maximum: 1,
            carrier: "forbid",
            near: "home-gate",
          },
        ],
      },
      {
        variantId: "surge",
        while: "window-open",
        patrol: { zone: "well-line" },
        chaseLeash: 1,
        hold: { minimumTicks: 6 },
        staff: [
          {
            tag: "runner",
            minimum: 1,
            preferred: 4,
            maximum: 5,
            carrier: "allow",
            near: "home-gate",
          },
        ],
      },
    ],
  },
  "well-patrol": {
    custody: "well-custody",
    formation: "screen",
    pace: "free",
    fight: disruptFight,
    staff: [
      {
        tag: "well",
        minimum: 1,
        preferred: 7,
        maximum: 7,
        carrier: "allow",
      },
    ],
    variants: [
      {
        variantId: "apex",
        while: "shadow-op or well-op",
        until: "shadow-cooled and well-cooled",
        patrol: { route: "stage-loop" },
        chaseLeash: 14,
        hold: { stableTicks: 2, minimumTicks: 8 },
      },
      {
        variantId: "farm",
        patrol: { route: "stage-loop" },
        chaseLeash: 1,
      },
    ],
  },
  "reserve-patrol": {
    custody: "well-custody",
    formation: "file",
    pace: "free",
    orientation: "own-reactor",
    fight: {
      targets: {
        prefer: ["carrier", "weakest"],
        maximumAttackers: 2,
      },
      chase: { leash: 2 },
      defense: { radius: 3, return: true },
      signature: "damage-first",
      release: { hiddenTicks: 1, unreachableTicks: 3 },
    },
    staff: [
      {
        tag: "reserves",
        minimum: 0,
        preferred: 0,
        maximum: 8,
        carrier: "allow",
      },
    ],
    variants: [
      {
        variantId: "hold",
        patrol: { zone: "own-approach" },
        chaseLeash: 2,
      },
    ],
  },
};

const conditions = {
  "north-birth-soon": {
    fact: "well-ticks-until-birth",
    operator: "at-most",
    value: 28,
    subject: "north",
  },
  "centre-birth-soon": {
    fact: "well-ticks-until-birth",
    operator: "at-most",
    value: 28,
    subject: "centre",
  },
  "south-birth-soon": {
    fact: "well-ticks-until-birth",
    operator: "at-most",
    value: 28,
    subject: "south",
  },
  "window-open": {
    fact: "known-enemies-unavailable",
    operator: "at-least",
    value: 2,
  },
  "shadow-op": {
    fact: "max-level",
    operator: "at-least",
    value: 3,
    subject: "hunter",
  },
  "well-op": {
    fact: "max-level",
    operator: "at-least",
    value: 3,
    subject: "well",
  },
  "shadow-cooled": {
    fact: "max-level",
    operator: "at-most",
    value: 2,
    subject: "hunter",
  },
  "well-cooled": {
    fact: "max-level",
    operator: "at-most",
    value: 2,
    subject: "well",
  },
  "gate-threat": {
    fact: "remembered-enemies-in-zone",
    operator: "at-least",
    value: 1,
    zone: "own-approach",
    freshnessTicks: 21,
  },
  "home-threat": {
    fact: "remembered-enemies-in-zone",
    operator: "at-least",
    value: 1,
    zone: "own-home",
    freshnessTicks: 21,
  },
  "approach-clear": {
    fact: "remembered-enemies-in-zone",
    operator: "at-most",
    value: 0,
    zone: "own-approach",
    freshnessTicks: 21,
  },
  "home-clear": {
    fact: "remembered-enemies-in-zone",
    operator: "at-most",
    value: 0,
    zone: "own-home",
    freshnessTicks: 21,
  },
  "ghost-hurt": {
    fact: "self-health",
    operator: "at-most",
    value: 2,
  },
  "ghost-whole": {
    fact: "self-health",
    operator: "at-least",
    value: 3,
  },
};

const custodyPolicies = source.custodyPolicies.map((policy) => {
  const migrated = structuredClone(policy);
  delete migrated.escortGroups;
  migrated.escorts = ["well", "reserves"];
  return migrated;
});

const migrated = {
  schema: source.schema,
  playbookId: source.playbookId,
  auditStatus: source.auditStatus,
  composition: source.composition,
  layout: source.layout,
  memory: {
    enemyUnavailableTicks: source.memory.enemyUnavailableTicks,
    lastSeenEnemyTicks: source.memory.lastSeenEnemyTicks,
    securedCoreTicks: source.memory.securedCoreTicks,
    objectiveProgressTicks: source.memory.objectiveProgressTicks,
  },
  roles: source.roles.map((role) => ({
    ...role,
    tags: tags[role.roleId],
    formationPosture: postures[role.roleId],
  })),
  custodyPolicies,
  doctrines: {
    roles: roleDoctrines,
    operations,
  },
  conditions,
};

splitWellResponse(migrated);
addTeamTempo(migrated);
addSquadAimPolicy(migrated);
renameOperationReach(migrated);
addOperationRepairConsent(migrated);
addTrafficPolicy(migrated);
addHunterTempoOverrides(migrated);
deduplicateOperationDefaults(migrated);
fs.writeFileSync(target, `${JSON.stringify(migrated, null, 1)}\n`);
console.log(`migrated ${path.relative(root, target)}`);
