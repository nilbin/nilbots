#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const [baseArgument, outputArgument] = process.argv.slice(2);
if (!baseArgument || !outputArgument) {
  throw new Error(
    "usage: generate-home-siege-v3-sanitation-matrix.mjs "
      + "<base.json> <output-dir>",
  );
}

const root = process.cwd();
const output = path.resolve(outputArgument);
const playbooks = path.join(output, "playbooks");
const layouts = path.join(output, "layouts");
fs.mkdirSync(playbooks, { recursive: true });
fs.rmSync(layouts, { force: true, recursive: true });
fs.symlinkSync(
  path.join(root, "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/layouts"),
  layouts,
  "dir",
);

const base = JSON.parse(fs.readFileSync(path.resolve(baseArgument), "utf8"));
const baseline = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/controls/coordination-parity-baseline.json",
);
const tactical = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-mind-v1",
);
const stock = path.join(root, "arena-bots/arc-relay/stock-mind-v4");
const layoutPath = path.join(
  root,
  "arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/layouts/counterflow-home-siege-v3.json",
);
const layoutSha256 = crypto.createHash("sha256")
  .update(fs.readFileSync(layoutPath))
  .digest("hex");
const candidates = [];

for (const pickupRadius of [1, 2, 3, 4]) {
  for (const lineClasses of [
    [],
    ["repulsor", "sunder", "kestrel"],
    ["kestrel", "repulsor", "sunder"],
  ]) {
    const document = structuredClone(base);
    document.layout.sha256 = layoutSha256;
    document.authoring.predicates["enemy-bank-danger-loose-core"] = {
      fact: "visible-loose-cores-in-zone",
      operator: "at-least",
      value: 1,
      zone: "enemy-bank-danger",
    };
    document.authoring.conditionSets["task-bank-drop-visible"] = [[
      "enemy-bank-danger-loose-core",
      "five-in-siege",
    ]];
    document.authoring.conditionSets["local-enemy-home-sanitation"] = [[
      "five-in-siege",
    ]];
    document.custodyPolicies = document.custodyPolicies.filter(
      (policy) => policy.custodyId !== "bank-sanitation",
    );
    document.custodyPolicies.push({
      custodyId: "bank-sanitation",
      authorizedCarrierRoles: ["runner", "line"],
      escortGroups: [],
      sourceWells: ["north", "centre", "south"],
      pickupReservationTicks: 8,
      transferTimeoutTicks: 4,
      deliveryTimeoutTicks: 24,
      accidentalPickup: "drop-safe",
      dropRecovery: "nearest-authorized",
      unreachableFallback: "guard",
      safeConversionConditionSetId: "never-convert",
      emergencyRecoveryZones: ["enemy-bank-danger"],
      emergencyPickupRadius: pickupRadius,
      emergencyRecoveryConditionSetId: "local-enemy-home-sanitation",
      emergencyRecoveryRoles: ["line", "runner"],
      emergencyRecoverySourceWells: ["north", "centre", "south"],
      emergencyRecoveryDisposition: "displace",
      emergencyDisplacementTarget: "sanitation-release",
      emergencyDisplacementReleaseRadius: 0,
    });
    document.authoring.maneuvers["task-core-sanitize"]
      .tracks.runner.assignments["runner-task-core-sanitize"]
      .custodyId = "bank-sanitation";
    document.authoring.maneuvers["task-core-sanitize"]
      .tracks["line-flex"].assignments["line-task-core-sanitize"]
      .custodyId = "bank-sanitation";

    const task = document.coordination.tasks.find(
      (value) => value.taskId === "secure-bank-drop",
    );
    Object.assign(task, {
      priority: 5,
      activation: "while-true",
      preemption: "higher-priority",
      participantLoss: "replace",
      triggerStableTicks: 1,
      minimumTicks: 0,
      timeoutTicks: 24,
      cooldownTicks: 1,
      minimumPrimaryBodies: 5,
      minimumParticipants: 1,
      maximumParticipants: 1,
      eligiblePhases: ["assault", "occupy", "regroup", "breach"],
      assignments: [
        {
          assignmentId: "line-sanitizer",
          orderId: "line-task-core-sanitize",
          roles: ["line"],
          classes: lineClasses,
          minimum: 0,
          preferred: 1,
          maximum: 1,
          carrier: "forbid",
          distance: {
            kind: "visible-loose-core-in-zone",
            target: "enemy-bank-danger",
            maximum: pickupRadius,
          },
        },
        {
          assignmentId: "runner-sanitizer",
          orderId: "runner-task-core-sanitize",
          roles: ["runner"],
          classes: ["kestrel", "relay"],
          minimum: 0,
          preferred: 1,
          maximum: 1,
          carrier: "forbid",
          distance: {
            kind: "visible-loose-core-in-zone",
            target: "enemy-bank-danger",
            maximum: pickupRadius,
          },
        },
      ],
      whenConditionSetId: "task-bank-drop-visible",
      completionArmMode: "assigned-carrier",
      completionReleaseMode: "assigned-carrier-loss",
      completeConditionSetId: "never-convert",
      failConditionSetId: "",
      reintegration: {
        mode: "primary-order",
        orderIds: [],
        completeConditionSetId: "",
        timeoutTicks: 0,
      },
    });
    delete task.completionArmConditionSetId;

    const classId = lineClasses.length === 0 ? "nearest" : lineClasses[0];
    const id = `radius-${pickupRadius}-${classId}`;
    const bytes = `${JSON.stringify(document, null, 2)}\n`;
    const file = path.join(playbooks, `${id}.json`);
    fs.writeFileSync(file, bytes);
    candidates.push({
      id,
      file,
      pickupRadius,
      lineClasses,
      sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
    });
  }
}

const makePlan = (assignment) => ({
  schema: "arc-relay-screen-batch-v1",
  bot: assignment === 0 ? tactical : stock,
  opponent: assignment === 0 ? stock : tactical,
  loopProfile: "forward-combat",
  cells: candidates.map((candidate) => ({
    cellId: candidate.id,
    sheet0: assignment === 0 ? candidate.file : baseline,
    sheet1: assignment === 0 ? baseline : candidate.file,
    seed: "202608062",
  })),
});

fs.writeFileSync(
  path.join(output, "candidates.json"),
  `${JSON.stringify({ schema: "home-siege-v3-sanitation-matrix-v1", candidates }, null, 2)}\n`,
);
for (const assignment of [0, 1]) {
  fs.writeFileSync(
    path.join(output, `plan-a${assignment}.json`),
    `${JSON.stringify(makePlan(assignment), null, 2)}\n`,
  );
}
console.log(`Generated ${candidates.length} sanitation candidates.`);
