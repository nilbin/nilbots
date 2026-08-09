#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const packageRoot = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03",
);
const playbookDir = path.join(packageRoot, "playbooks");
const layoutDir = path.join(packageRoot, "layouts");

const names = [
  "home-siege-v3",
  "predator-v2",
  "ripen-harvest-v1",
  "breakwater-v1",
  "trifold-balanced-v1",
];

const source = Object.fromEntries(names.map((name) => [
  name,
  JSON.parse(fs.readFileSync(path.join(playbookDir, `${name}.json`), "utf8")),
]));

if (names.every((name) => source[name].conditions && source[name].doctrines?.roles)) {
  console.log("the five hard sheets already use the consolidated DSL");
  process.exit(0);
}
for (const name of names) {
  if (source[name].conditions || source[name].doctrines?.roles) {
    throw new Error(`${name} is only partly migrated; refusing a mixed rewrite`);
  }
}

function encode(value) {
  return `${JSON.stringify(value, null, 1)}\n`;
}

function writeJson(file, value) {
  const text = encode(value);
  fs.writeFileSync(file, text);
  return crypto.createHash("sha256").update(text).digest("hex");
}

function clone(value) {
  return structuredClone(value);
}

function compactMemory(value) {
  const copy = clone(value);
  delete copy.formationStableTicks;
  return copy;
}

function compactCustody(value) {
  return value.map((policy) => {
    const copy = clone(policy);
    delete copy.escortGroups;
    delete copy.escorts;
    return copy;
  });
}

function directSheet(legacy, layout, roles, roleDoctrines, cohorts, focus, conditions) {
  return {
    schema: legacy.schema,
    playbookId: legacy.playbookId,
    auditStatus: legacy.auditStatus,
    composition: legacy.composition,
    layout,
    memory: compactMemory(legacy.memory),
    roles,
    custodyPolicies: compactCustody(legacy.custodyPolicies),
    doctrines: {
      roles: roleDoctrines,
      cohorts,
      focus,
    },
    conditions,
  };
}

function withRole(roles, roleId, mutate) {
  return roles.map((role) => role.roleId === roleId
    ? { ...clone(role), ...mutate }
    : clone(role));
}

function insertBefore(roles, beforeRole, role) {
  const copy = clone(roles);
  const index = copy.findIndex((candidate) => candidate.roleId === beforeRole);
  if (index < 0) throw new Error(`missing role ${beforeRole}`);
  copy.splice(index, 0, role);
  return copy;
}

function baseLayout(layoutId, extras = []) {
  const layout = JSON.parse(fs.readFileSync(
    path.join(layoutDir, "wellwright-warren6-v1.json"),
    "utf8",
  ));
  layout.layoutId = layoutId;
  layout.zones.push(...extras);
  return layout;
}

const predatorLayoutName = "warren6-predator-v2.json";
const predatorLayoutSha = writeJson(
  path.join(layoutDir, predatorLayoutName),
  baseLayout("warren6-predator-v2", [
    { zoneId: "kill-pocket", rect: [8, 5, 11, 8] },
    { zoneId: "south-pocket", rect: [8, 18, 11, 21] },
  ]),
);

const ripenLayoutName = "warren6-ripen-harvest-v1.json";
const ripenLayoutSha = writeJson(
  path.join(layoutDir, ripenLayoutName),
  baseLayout("warren6-ripen-harvest-v1", [
    { zoneId: "orchard-core", rect: [14, 12, 16, 14] },
  ]),
);

const homeLayoutName = "warren6-home-siege-v3.json";
const homeLayoutSha = writeJson(path.join(layoutDir, homeLayoutName), {
  schema: "arc-relay-tactical-layout-v1",
  layoutId: "warren6-home-siege-v3",
  mapId: "arc-relay-ambush-warren-06",
  bindings: [
    {
      matchContractFingerprint: "any-composition",
      ownReactorSide: "east",
      transform: "rotate-180",
      routeAliases: {
        "outer-rush": "outer-rush-opposite",
        "short-breach": "short-breach-opposite",
      },
      formationAliases: {},
    },
    {
      matchContractFingerprint: "any-composition",
      ownReactorSide: "west",
      transform: "identity",
      routeAliases: {},
      formationAliases: {},
    },
  ],
  zones: [
    { zoneId: "enemy-home", rect: [24, 9, 29, 17] },
    { zoneId: "enemy-bank-danger", rect: [27, 12, 29, 14] },
    { zoneId: "bank-seal", rect: [24, 10, 26, 16] },
    { zoneId: "forward-rally", rect: [22, 9, 25, 17] },
    { zoneId: "siege-leash", rect: [21, 7, 29, 19] },
    { zoneId: "well-line", rect: [12, 4, 18, 22] },
  ],
  routes: [
    {
      routeId: "outer-rush",
      corridorWidth: 2,
      waypoints: [
        [4, 8], [8, 8], [11, 8], [14, 6], [15, 7],
        [19, 8], [22, 8], [25, 10], [26, 12], [27, 13],
      ],
    },
    {
      routeId: "outer-rush-opposite",
      corridorWidth: 2,
      waypoints: [
        [4, 18], [8, 18], [13, 18], [14, 20], [15, 19],
        [19, 18], [22, 18], [25, 16], [26, 14], [27, 13],
      ],
    },
    {
      routeId: "short-breach",
      corridorWidth: 2,
      waypoints: [[23, 10], [25, 10], [26, 12], [27, 13]],
    },
    {
      routeId: "short-breach-opposite",
      corridorWidth: 2,
      waypoints: [[23, 16], [25, 16], [26, 14], [27, 13]],
    },
    {
      routeId: "score-return",
      corridorWidth: 2,
      waypoints: [
        [25, 10], [22, 8], [19, 8], [15, 7], [14, 6],
        [11, 8], [8, 8], [4, 10], [2, 13],
      ],
    },
  ],
  anchors: [
    { anchorId: "enemy-perimeter", position: [26, 13] },
    { anchorId: "rally-centre", position: [24, 13] },
    { anchorId: "sanitation-release", position: [22, 13] },
    { anchorId: "own-bank", position: [2, 13] },
    { anchorId: "well-line-centre", position: [15, 13] },
  ],
});

const openFight = {
  targets: { prefer: ["carrier", "anchor", "weakest"] },
  chase: { leash: 2, persistTicks: 1 },
  defense: { radius: 3, return: true },
};
const runFight = clone(openFight);
const homeFight = {
  targets: { prefer: ["carrier", "anchor", "weakest"] },
  chase: { leash: 1, persistTicks: 1 },
  defense: { radius: 2, return: true },
};
const repair = {
  prefer: ["carrier", "medic", "weakest", "focus", "any"],
  maximumProvidersPerTarget: 1,
  minimumProviderSeparation: 3,
  reserveHealthPercent: 20,
  survivalFallback: "evade",
};
const inline = (take) => ({ take, select: "nearest" });
const max = (maximumAttackers) => ({ maximumAttackers });

// Predator: the old bait assignment selected one Relay out of a broad Runner
// pool. Make that intent a role so the body declares its own policy and floor.
{
  const legacy = source["predator-v2"];
  let roles = withRole(legacy.roles, "runner", { preferred: 5 });
  roles = insertBefore(roles, "runner", {
    roleId: "bait",
    candidateClasses: ["relay"],
    minimum: 0,
    preferred: 1,
    maximum: 1,
    carrierPreference: "prefer",
    deathPolicy: "promote-best",
    respawnPolicy: "replace",
    overflowRoleId: "runner",
  });
  const orchardRole = (posture) => ({
    custody: "farm-custody",
    fight: {
      ...clone(openFight),
      engage: { within: 4 },
      chase: { leash: 1, persistTicks: 1 },
    },
    modes: [
      {
        patrol: { zone: "kill-pocket" },
        while: "always",
        cohort: "orchard",
        focus: "trap",
        posture,
      },
      { patrol: { zone: "centre-farm" }, focus: "run", posture },
    ],
    conceal: true,
  });
  const roleDoctrines = {
    anchor: orchardRole("front"),
    sharp: orchardRole("rear"),
    hook: orchardRole("front"),
    eyes: orchardRole("core"),
    bait: {
      custody: "bait-custody",
      fight: runFight,
      modes: [
        { fetch: "bait-custody" },
        { patrol: { zone: "kill-pocket" }, focus: "run", posture: "core" },
      ],
      conceal: false,
    },
    runner: {
      custody: "farm-custody",
      fight: runFight,
      modes: [
        { fetch: "farm-custody" },
        {
          patrol: "north-out",
          while: "always",
          cohort: inline(2),
          focus: "run",
          posture: "core",
        },
        {
          patrol: "south-out",
          while: "always",
          cohort: inline(2),
          focus: "run",
          posture: "core",
        },
        { patrol: { zone: "centre-farm" }, focus: "run", posture: "rear" },
      ],
      conceal: false,
    },
  };
  const migrated = directSheet(
    legacy,
    { path: `../layouts/${predatorLayoutName}`, sha256: predatorLayoutSha },
    roles,
    roleDoctrines,
    { orchard: { take: 2, select: "nearest" } },
    { trap: max(4), run: max(2) },
    {},
  );
  writeJson(path.join(playbookDir, "predator-v2.json"), migrated);
}

// Ripen Harvest: the runner's two fetch rungs remain ordered and every
// specialist states centre-orchard duty in its own doctrine.
{
  const legacy = source["ripen-harvest-v1"];
  const orchardRole = (posture) => ({
    custody: "orchard-custody",
    fight: openFight,
    modes: [
      { fetch: "orchard-custody" },
      { patrol: { zone: "centre-farm" }, focus: "orchard", posture },
    ],
    conceal: false,
  });
  const roleDoctrines = {
    anchor: orchardRole("front"),
    sharp: orchardRole("rear"),
    hook: orchardRole("front"),
    eyes: orchardRole("core"),
    runner: {
      custody: "farm-outer",
      fight: runFight,
      modes: [
        { fetch: "orchard-custody" },
        { fetch: "farm-outer" },
        {
          patrol: "north-out",
          while: "always",
          cohort: inline(2),
          focus: "run",
          posture: "core",
        },
        {
          patrol: "south-out",
          while: "always",
          cohort: inline(2),
          focus: "run",
          posture: "core",
        },
        { patrol: { zone: "centre-farm" }, focus: "run", posture: "rear" },
      ],
      conceal: false,
    },
  };
  const migrated = directSheet(
    legacy,
    { path: `../layouts/${ripenLayoutName}`, sha256: ripenLayoutSha },
    clone(legacy.roles),
    roleDoctrines,
    {},
    { orchard: max(2), run: max(2) },
    {
      "orchard-worth": {
        fact: "visible-loose-core-value-in-zone",
        operator: "at-least",
        value: 3,
        zone: "centre-farm",
      },
    },
  );
  writeJson(path.join(playbookDir, "ripen-harvest-v1.json"), migrated);
}

// Breakwater: the old class-filtered counter-courier assignment becomes an
// explicit Interceptor role. The role's own ladder now says both its screen
// route and the condition that pulls it off that route.
{
  const legacy = source["breakwater-v1"];
  let roles = withRole(legacy.roles, "runner", { preferred: 1 });
  roles = insertBefore(roles, "runner", {
    roleId: "interceptor",
    candidateClasses: ["kestrel", "relay"],
    minimum: 0,
    preferred: 2,
    maximum: 2,
    carrierPreference: "prefer",
    deathPolicy: "promote-best",
    respawnPolicy: "replace",
    overflowRoleId: "runner",
  });
  const defender = (posture, extra = {}) => ({
    custody: "incidental-delivery",
    fight: openFight,
    ...extra,
    modes: [
      { fetch: "incidental-delivery" },
      {
        patrol: { zone: "own-home" },
        while: "approach-mass",
        until: "approach-clear",
        focus: "choke",
        posture,
        fight: homeFight,
      },
      { patrol: { zone: "well-line" }, focus: "front", posture },
    ],
    conceal: false,
  });
  const roleDoctrines = {
    anchor: defender("front"),
    medic: defender("rear", { repair }),
    eyes: defender("core"),
    hook: defender("front"),
    interceptor: {
      custody: "incidental-delivery",
      fight: runFight,
      modes: [
        { fetch: "farm-north" },
        { fetch: "farm-south" },
        { fetch: "incidental-delivery" },
        {
          intercept: "enemy-carriers",
          from: "home-gate",
          while: "visible-enemy-carrier and approach-lingering",
          until: "no-known-enemy-carrier",
          focus: max(1),
          fight: { chase: { leash: 4, persistTicks: 3 } },
        },
        {
          patrol: "north-out",
          while: "always",
          cohort: inline(1),
          focus: "run",
          posture: "front",
        },
        { patrol: "south-out", focus: "run", posture: "front" },
      ],
      conceal: false,
    },
    runner: {
      custody: "incidental-delivery",
      fight: runFight,
      modes: [
        { fetch: "incidental-delivery" },
        { patrol: { zone: "well-line" }, focus: "run", posture: "rear" },
      ],
      conceal: false,
    },
  };
  const migrated = directSheet(
    legacy,
    {
      path: "../layouts/wellwright-warren6-v1.json",
      sha256: crypto.createHash("sha256").update(fs.readFileSync(
        path.join(layoutDir, "wellwright-warren6-v1.json"),
      )).digest("hex"),
    },
    roles,
    roleDoctrines,
    {},
    { choke: max(2), front: max(2), run: max(2) },
    {
      "approach-mass": {
        fact: "remembered-enemies-in-zone",
        operator: "at-least",
        value: 5,
        zone: "own-approach",
      },
      "approach-clear": {
        fact: "remembered-enemies-in-zone",
        operator: "at-most",
        value: 2,
        zone: "own-approach",
        freshnessTicks: 18,
      },
      "approach-lingering": {
        fact: "remembered-enemies-in-zone",
        operator: "at-least",
        value: 3,
        zone: "own-approach",
      },
      "visible-enemy-carrier": {
        fact: "visible-enemy-carriers",
        operator: "at-least",
        value: 1,
      },
      "no-known-enemy-carrier": {
        fact: "known-enemy-carriers",
        operator: "equals",
        value: 0,
      },
    },
  );
  writeJson(path.join(playbookDir, "breakwater-v1.json"), migrated);
}

// Trifold: its old phase history watched prototype-only socket state that the
// current ambush-11 rules do not publish. Two shared cohort rows instead make
// a stable 3/3/2 north/south/centre split directly from each role's ladder.
{
  const legacy = source["trifold-balanced-v1"];
  const spread = (focus, posture) => [
    {
      patrol: { zone: "north-farm" },
      while: "always",
      cohort: "north-wing",
      focus,
      posture,
    },
    {
      patrol: { zone: "south-farm" },
      while: "always",
      cohort: "south-wing",
      focus,
      posture,
    },
    { patrol: { zone: "centre-farm" }, focus, posture },
  ];
  const specialist = (posture, extra = {}) => ({
    custody: "incidental-delivery",
    fight: openFight,
    ...extra,
    modes: [
      { fetch: "incidental-delivery" },
      ...spread("front", posture),
    ],
    conceal: false,
  });
  const roleDoctrines = {
    anchor: specialist("front"),
    medic: specialist("rear", { repair }),
    eyes: specialist("core"),
    hook: specialist("front"),
    runner: {
      custody: "farm-north",
      fight: runFight,
      modes: [
        { fetch: "farm-north" },
        { fetch: "farm-south" },
        { fetch: "incidental-delivery" },
        { fetch: "farm-centre" },
        ...spread("run", "core"),
      ],
      conceal: false,
    },
  };
  const migrated = directSheet(
    legacy,
    {
      path: "../layouts/wellwright-warren6-v1.json",
      sha256: crypto.createHash("sha256").update(fs.readFileSync(
        path.join(layoutDir, "wellwright-warren6-v1.json"),
      )).digest("hex"),
    },
    clone(legacy.roles),
    roleDoctrines,
    {
      "north-wing": { take: 3, select: "nearest" },
      "south-wing": { take: 3, select: "nearest" },
    },
    { front: max(2), run: max(2) },
    {},
  );
  writeJson(path.join(playbookDir, "trifold-balanced-v1.json"), migrated);
}

// Home Siege: the old line-group's two-state machine is not promoted into a
// shared primitive. Each role owns the same regroup latch; a restored line
// releases it and exposes the breach rung below.
{
  const legacy = source["home-siege-v3"];
  let roles = withRole(legacy.roles, "line", { preferred: 4, maximum: 4 });
  roles = insertBefore(roles, "line", {
    roleId: "interceptor",
    candidateClasses: ["repulsor", "sunder"],
    minimum: 0,
    preferred: 1,
    maximum: 1,
    carrierPreference: "forbid",
    deathPolicy: "hold-vacancy",
    respawnPolicy: "rejoin",
    overflowRoleId: "line",
  });

  const siegeFight = {
    targets: { prefer: ["carrier", "freshest", "closest", "weakest"] },
    chase: { leash: 2, persistTicks: 1 },
    defense: { radius: 3, return: true },
  };
  const rushFight = {
    targets: { prefer: ["carrier", "anchor", "weakest"] },
    chase: { leash: 3, persistTicks: 1 },
    defense: { radius: 3, return: true },
  };
  const regroupFight = {
    targets: { prefer: ["carrier", "strongest-threat", "weakest"] },
    chase: { leash: 1, persistTicks: 1 },
    defense: { radius: 2, return: true },
  };
  const regroup = (posture) => ({
    patrol: { zone: "forward-rally" },
    while: "four-live-or-fewer or line-cohesion-low",
    until: "line-two-at-rally and five-live and line-cohesion-restored",
    focus: "regroup",
    posture,
    fight: regroupFight,
  });
  const occupy = (posture) => ({
    patrol: { zone: "bank-seal" },
    while: "six-in-siege",
    until: "three-or-fewer-at-bank-seal",
    focus: "siege",
    posture,
    fight: siegeFight,
  });
  const breach = (posture) => ({
    assault: "short-breach",
    while: "line-two-at-rally and five-live and line-cohesion-restored",
    until: "six-in-siege or four-live-or-fewer",
    focus: "rush",
    posture,
    fight: rushFight,
  });
  const floor = (posture) => ({
    patrol: "outer-rush",
    focus: "rush",
    posture,
    fight: rushFight,
  });
  const roleDoctrines = {
    runner: {
      custody: "secured-conversion",
      fight: rushFight,
      modes: [
        regroup("rear"),
        { fetch: "secured-conversion" },
        { fetch: "surge-harvest" },
        { fetch: "incidental-delivery" },
        occupy("rear"),
        breach("rear"),
        floor("rear"),
      ],
      conceal: false,
      scavenge: [
        "enemy-home", "enemy-bank-danger", "bank-seal", "siege-leash",
      ],
    },
    medic: {
      custody: "incidental-delivery",
      fight: rushFight,
      repair,
      modes: [
        regroup("rear"),
        { fetch: "surge-harvest" },
        { fetch: "incidental-delivery" },
        occupy("rear"),
        breach("rear"),
        floor("rear"),
      ],
      conceal: false,
    },
    interceptor: {
      custody: "incidental-delivery",
      fight: rushFight,
      modes: [
        regroup("front"),
        {
          intercept: "enemy-carriers",
          from: "enemy-perimeter",
          while: "visible-enemy-carrier and five-at-bank-seal",
          until: "no-known-enemy-carrier",
          focus: max(1),
          fight: { chase: { leash: 6, persistTicks: 3 } },
        },
        {
          follow: "runner",
          posture: "screen",
          while: "occupied-attrition-safe and five-at-bank-seal and core-outstanding",
          until: "front-attrition-danger or four-or-fewer-at-bank-seal or no-core-outstanding",
          focus: "rush",
        },
        occupy("front"),
        breach("front"),
        floor("front"),
      ],
      conceal: false,
    },
    line: {
      custody: "incidental-delivery",
      fight: rushFight,
      modes: [
        regroup("front"),
        { fetch: "surge-harvest" },
        { fetch: "incidental-delivery" },
        occupy("front"),
        breach("front"),
        floor("front"),
      ],
      conceal: false,
    },
  };
  const conditions = {
    "front-attrition-safe": {
      fact: "known-enemies-unavailable", operator: "at-least", value: 5,
    },
    "front-attrition-danger": {
      fact: "known-enemies-unavailable", operator: "at-most", value: 1,
    },
    "occupied-attrition-safe": {
      fact: "known-enemies-unavailable", operator: "at-least", value: 2,
    },
    "core-outstanding": {
      fact: "outstanding-well-count", operator: "at-least", value: 1,
    },
    "no-core-outstanding": {
      fact: "outstanding-well-count", operator: "equals", value: 0,
    },
    "six-in-siege": {
      fact: "friendlies-in-zone-count", operator: "at-least", value: 6,
      zone: "siege-leash",
    },
    "five-in-siege": {
      fact: "friendlies-in-zone-count", operator: "at-least", value: 5,
      zone: "siege-leash",
    },
    "five-at-bank-seal": {
      fact: "friendlies-in-zone-count", operator: "at-least", value: 5,
      zone: "bank-seal",
    },
    "four-or-fewer-at-bank-seal": {
      fact: "friendlies-in-zone-count", operator: "at-most", value: 4,
      zone: "bank-seal",
    },
    "three-or-fewer-at-bank-seal": {
      fact: "friendlies-in-zone-count", operator: "at-most", value: 3,
      zone: "bank-seal",
    },
    "five-live": {
      fact: "live-friendlies", operator: "at-least", value: 5,
    },
    "four-live-or-fewer": {
      fact: "live-friendlies", operator: "at-most", value: 4,
    },
    "line-two-at-rally": {
      fact: "in-zone-count", operator: "at-least", value: 2,
      subject: "line", zone: "forward-rally",
    },
    "line-cohesion-low": {
      fact: "cohesion", operator: "at-most", value: 60, subject: "line",
    },
    "line-cohesion-restored": {
      fact: "cohesion", operator: "at-least", value: 80, subject: "line",
    },
    "visible-enemy-carrier": {
      fact: "visible-enemy-carriers", operator: "at-least", value: 1,
    },
    "no-known-enemy-carrier": {
      fact: "known-enemy-carriers", operator: "equals", value: 0,
    },
    "secured-core": {
      fact: "secured-cores", operator: "at-least", value: 1,
    },
    "no-progress-for-60": {
      fact: "ticks-without-objective-progress", operator: "at-least", value: 60,
    },
    "north-core-outstanding": {
      fact: "well-has-outstanding", operator: "at-least", value: 1,
      subject: "north",
    },
  };
  const migrated = directSheet(
    legacy,
    { path: `../layouts/${homeLayoutName}`, sha256: homeLayoutSha },
    roles,
    roleDoctrines,
    {},
    { regroup: max(2), rush: max(1), siege: max(8) },
    conditions,
  );
  writeJson(path.join(playbookDir, "home-siege-v3.json"), migrated);
}

console.log("migrated the five hard sheets to the consolidated DSL");
