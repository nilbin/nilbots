#!/usr/bin/env node

import { createHash } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const packageRoot = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03",
);
const playbookDir = path.join(packageRoot, "playbooks");
const layoutDir = path.join(packageRoot, "layouts");
const basePlaybookPath = path.join(playbookDir, "home-siege-v3.json");
const baseLayoutPath = path.join(layoutDir, "counterflow-home-siege-v3.json");

const expectedBasePlaybook =
  "3829ce7cacb30a13543d3f4846b731c6a63cec4c22ddd5c994313fbe9b4e78e1";
const expectedBaseLayout =
  "89c92e7091b6c66a31bf56c09d624746c2c06aa760ca6c96c0ebe469836fb3e7";

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function readPinnedJson(file, expectedHash) {
  const bytes = readFileSync(file);
  const actual = sha256(bytes);
  if (actual !== expectedHash) {
    throw new Error(`${file}: expected ${expectedHash}, found ${actual}`);
  }
  return JSON.parse(bytes.toString("utf8"));
}

function clone(value) {
  return structuredClone(value);
}

function encode(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function writeJson(file, value) {
  const encoded = encode(value);
  writeFileSync(file, encoded);
  return sha256(encoded);
}

function replaceString(value, before, after) {
  if (Array.isArray(value)) {
    for (let index = 0; index < value.length; index += 1) {
      if (value[index] === before) value[index] = after;
      else replaceString(value[index], before, after);
    }
    return;
  }
  if (value && typeof value === "object") {
    for (const child of Object.values(value)) {
      replaceString(child, before, after);
    }
  }
}

const basePlaybook = readPinnedJson(basePlaybookPath, expectedBasePlaybook);
const baseLayout = readPinnedJson(baseLayoutPath, expectedBaseLayout);

const southLayout = clone(baseLayout);
southLayout.layoutId = "counterflow-home-siege-v3-south";
const scoreReturn = southLayout.routes.find(
  (route) => route.routeId === "score-return",
);
if (!scoreReturn) throw new Error("base layout has no score-return route");
southLayout.routes.push({
  ...clone(scoreReturn),
  routeId: "score-return-opposite",
  waypoints: scoreReturn.waypoints.map(([x, y]) => [x, 22 - y]),
});
for (const binding of southLayout.bindings) {
  binding.routeAliases =
    binding.ownReactorSide === "west"
      ? {
          "outer-rush": "outer-rush-opposite",
          "short-breach": "short-breach-opposite",
          "score-return": "score-return-opposite",
        }
      : {};
}
const southLayoutPath = path.join(
  layoutDir,
  "counterflow-home-siege-v3-south.json",
);
const southLayoutHash = writeJson(southLayoutPath, southLayout);

const southPlaybook = clone(basePlaybook);
southPlaybook.playbookId = "home-siege-v3-south-mirror";
southPlaybook.layout = {
  path: "../layouts/counterflow-home-siege-v3-south.json",
  sha256: southLayoutHash,
};
const securedConversion = southPlaybook.custodyPolicies.find(
  (policy) => policy.custodyId === "secured-conversion",
);
if (!securedConversion) {
  throw new Error("base playbook has no secured-conversion custody policy");
}
securedConversion.sourceWells = ["south"];
const predicates = southPlaybook.authoring.predicates;
const southOutstanding = predicates["north-core-outstanding"];
if (!southOutstanding) {
  throw new Error("base playbook has no north-core-outstanding predicate");
}
predicates["south-core-outstanding"] = {
  ...southOutstanding,
  subject: "south",
};
delete predicates["north-core-outstanding"];
replaceString(
  southPlaybook.authoring.conditionSets,
  "north-core-outstanding",
  "south-core-outstanding",
);
const southPlaybookPath = path.join(
  playbookDir,
  "home-siege-v3-south-mirror.json",
);
const southPlaybookHash = writeJson(southPlaybookPath, southPlaybook);

const thresholdComposition = clone(basePlaybook);
thresholdComposition.playbookId = "home-siege-v3-four-down-double-relay";
thresholdComposition.composition = [
  "relay",
  "relay",
  "patchbay",
  "patchbay",
  "sunder",
  "sunder",
  "repulsor",
  "repulsor",
];
thresholdComposition.authoring.parameters[
  "conversion-front-enemy-unavailable"
].value = 4;
const thresholdCompositionPath = path.join(
  playbookDir,
  "home-siege-v3-four-down-double-relay.json",
);
const thresholdCompositionHash = writeJson(
  thresholdCompositionPath,
  thresholdComposition,
);

console.log(
  JSON.stringify(
    {
      schema: "home-siege-v3-prefreeze-generation-v1",
      source: {
        playbook: expectedBasePlaybook,
        layout: expectedBaseLayout,
      },
      variants: {
        southMirror: {
          playbook: path.relative(root, southPlaybookPath),
          playbookSha256: southPlaybookHash,
          layout: path.relative(root, southLayoutPath),
          layoutSha256: southLayoutHash,
        },
        thresholdComposition: {
          playbook: path.relative(root, thresholdCompositionPath),
          playbookSha256: thresholdCompositionHash,
          layout: path.relative(root, baseLayoutPath),
          layoutSha256: expectedBaseLayout,
        },
      },
    },
    null,
    2,
  ),
);
