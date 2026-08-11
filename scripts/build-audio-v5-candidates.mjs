#!/usr/bin/env node

import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const labRoot = path.join(repositoryRoot, "art", "audio", "sound-lab");
const baseManifestPath = path.join(labRoot, "manifest-v4.json");
const generationPath = path.join(
  labRoot,
  "v5",
  "sfx-engine-generation.json",
);
const generationDirectory = path.dirname(generationPath);
const apiSpecPath = path.join(
  repositoryRoot,
  ".claude",
  "skills",
  "sfx-engine",
  "references",
  "openapi.yaml",
);

const baseManifest = JSON.parse(await readFile(baseManifestPath, "utf8"));
const generation = JSON.parse(await readFile(generationPath, "utf8"));
const expectedIds = baseManifest.packs[0].cues.flatMap((cue) =>
  cue.id === "impact" ? [cue.id, "healing"] : [cue.id],
);
const runtimeCueIds = [
  "attack-windup",
  "projectile",
  "impact",
  "healing",
  "destroyed",
  "level-up",
  "arc-birth",
  "arc-pickup",
  "arc-drop",
  "arc-bank",
  "arc-pulse",
  "arc-handoff",
  "deflect",
  "arrival",
];
const cuePresentation = {
  "attack-windup": {
    description:
      "A smooth low field gathers into one focused tone before a declared beam.",
  },
  projectile: {
    description:
      "A rounded locked-field hit confirms beam resolution without noisy travel.",
  },
  healing: {
    label: "Healing field",
    category: "COMBAT SUPPORT",
    status: "mapped",
    description:
      "Warm restorative energy draws inward, rises, and resolves as a healthy repair bloom.",
    mapping: "zone-healed and signature-repair authoritative facts",
  },
  "attack-fizzle": {
    status: "proposed",
    mapping:
      "review reference only; runtime fizzle / interrupt / miss aliases projectile",
  },
  "attack-cancel": {
    status: "proposed",
    mapping: "review reference only; voluntary attack cancellation is currently silent",
  },
  "arc-pickup": {
    description:
      "The Core levitates weightlessly into a carrier's calm magnetic halo.",
    mapping: "every core-picked-up fact, including an opposing-team pickup",
  },
  "arc-steal": {
    status: "proposed",
    description:
      "Historical alternate retained for comparison; stealing is physically a pickup.",
    mapping: "review reference only; runtime aliases an opposing pickup to arc-pickup",
  },
  "arc-drop": {
    description:
      "The levitation field releases as the Core settles back into free orbit.",
  },
};
const comparisonGenerationCues = generation.cues.filter(
  (cue) => cue.role !== "background-loop",
);
const backgroundGenerationCues = generation.cues.filter(
  (cue) => cue.role === "background-loop",
);
const expectedBackgroundIds = [
  "arena-ambience",
  "arena-ambience-ion-veil",
  "arena-ambience-signal-lattice",
];
const actualIds = comparisonGenerationCues.map((cue) => cue.id);
const locallyDerivedCues = comparisonGenerationCues.filter(
  (cue) => cue.result?.status === "derived",
);
const locallySafetyMasteredCues = comparisonGenerationCues.filter(
  (cue) => cue.result?.processing?.safetyMastering,
);
assert(
  JSON.stringify(actualIds) === JSON.stringify(expectedIds),
  "SFX Engine cue order must match the V4 vocabulary plus approved additions.",
);
assert(
  JSON.stringify(backgroundGenerationCues.map((cue) => cue.id)) ===
    JSON.stringify(expectedBackgroundIds),
  "V5 background-loop alternatives differ from the reviewed set.",
);
assert(generation.provider === "sfx-engine", "Unexpected audio provider.");
assert(
  generation.apiSpecSha256 === sha256(await readFile(apiSpecPath)),
  "The saved OpenAPI document differs from the generation pin.",
);
assert(
  generation.energyAudit?.script === "scripts/master-audio-v5-energy.mjs" &&
    JSON.stringify(generation.energyAudit.ambientLoopIds) ===
      JSON.stringify(expectedBackgroundIds) &&
    Object.values(generation.energyAudit.sequenceResults ?? {}).every(
      (result) =>
        result.approximateTruePeakDbtp <=
        generation.energyAudit.reviewSequenceMaximumApproximateTruePeakDbtp,
    ),
  "The V5 sequence energy audit is missing or exceeds its ceiling.",
);

const outputRoot = path.resolve(generationDirectory, generation.outputDirectory);
assert(
  outputRoot.startsWith(`${generationDirectory}${path.sep}`),
  "The SFX Engine output directory escapes V5.",
);

const cues = [];
for (const generatedCue of comparisonGenerationCues) {
  const baseline =
    baseManifest.packs[0].cues.find(
      (cue) => cue.id === generatedCue.id,
    ) ?? cuePresentation[generatedCue.id];
  assert(
    baseline?.label && baseline?.category,
    `${generatedCue.id} lacks review presentation metadata.`,
  );
  const result = generatedCue.result;
  assert(
    ["generated", "derived"].includes(result?.status),
    `${generatedCue.id} is neither generated nor locally derived.`,
  );
  if (result.status === "derived") {
    const source = generation.cues.find(
      (cue) => cue.id === result.derivation?.sourceCueId,
    );
    assert(
      generatedCue.generationMode === "local-derivation" && source,
      `${generatedCue.id} lacks local-derivation provenance.`,
    );
    assert(
      source.result?.outputSha256 === result.derivation.sourceOutputSha256,
      `${generatedCue.id} derivation source hash is stale.`,
    );
    assert(
      result.derivation?.derivedBy === "scripts/derive-audio-v5-revisions.mjs",
      `${generatedCue.id} names an unexpected derivation workflow.`,
    );
  }
  const safetyMastering = result.processing?.safetyMastering;
  if (safetyMastering) {
    assert(
      generatedCue.safetyMastering?.masteredBy ===
        "scripts/master-audio-v5-energy.mjs" &&
        safetyMastering.masteredBy ===
          "scripts/master-audio-v5-energy.mjs",
      `${generatedCue.id} names an unexpected safety-mastering workflow.`,
    );
    assert(
      generatedCue.safetyMastering.sourceOutputSha256 ===
          safetyMastering.sourceOutputSha256 &&
        generatedCue.safetyMastering.sourceResult?.outputSha256 ===
          safetyMastering.sourceOutputSha256,
      `${generatedCue.id} lacks its pre-safety source result.`,
    );
    const safetySourcePath = path.resolve(
      generationDirectory,
      safetyMastering.sourceFile,
    );
    assert(
      safetySourcePath.startsWith(`${generationDirectory}${path.sep}`),
      `${generatedCue.id} safety source escapes V5.`,
    );
    assert(
      sha256(await readFile(safetySourcePath)) ===
        safetyMastering.sourceOutputSha256,
      `${generatedCue.id} safety source differs from its saved provenance.`,
    );
    assert(
      result.metrics?.energy?.approximateTruePeakDbtp <= -3 &&
        result.metrics?.energy?.energyAbove12kPercent <=
          generation.localSafetyMastering.thresholds
            .maximumEnergyAbove12kPercent[generatedCue.id],
      `${generatedCue.id} no longer satisfies its saved safety thresholds.`,
    );
  }
  assert(result.metrics?.sampleRate === 48_000, `${generatedCue.id} is not 48 kHz.`);
  assert(result.metrics?.channels === 2, `${generatedCue.id} is not stereo.`);
  assert(
    result.metrics?.bitsPerSample === 16,
    `${generatedCue.id} is not signed 16-bit PCM.`,
  );
  const wavPath = path.join(outputRoot, generatedCue.file);
  const wav = await readFile(wavPath);
  assert(
    sha256(wav) === result.outputSha256,
    `${generatedCue.id} WAV hash differs from its saved provenance.`,
  );
  assert(
    wav.length === result.outputBytes,
    `${generatedCue.id} WAV size differs from its saved provenance.`,
  );
  cues.push({
    id: generatedCue.id,
    label: baseline.label,
    category: baseline.category,
    status: runtimeCueIds.includes(generatedCue.id)
      ? "mapped"
      : cuePresentation[generatedCue.id]?.status ?? baseline.status,
    description:
      cuePresentation[generatedCue.id]?.description ?? baseline.description,
    mapping: cuePresentation[generatedCue.id]?.mapping ?? baseline.mapping,
    file: `./v5/${generation.outputDirectory}/${generatedCue.file}`,
    durationSeconds: result.metrics.durationSeconds,
    peak: result.metrics.peak,
    rms: result.metrics.rms,
    stereoDifferenceRms: result.metrics.stereoDifferenceRms,
    dcOffset: result.metrics.dcOffset,
    origin: result.status === "derived" ? "local-derivation" : "provider-generation",
    safetyMastered: Boolean(safetyMastering),
    ...(result.metrics.energy ? { energy: result.metrics.energy } : {}),
    ...(result.status === "derived"
      ? { derivedFromCueId: result.derivation.sourceCueId }
      : {}),
  });
}

const backgroundLoops = [];
for (const generatedCue of backgroundGenerationCues) {
  const result = generatedCue.result;
  assert(result?.status === "generated", `${generatedCue.id} is not generated.`);
  assert(result.metrics?.sampleRate === 48_000, `${generatedCue.id} is not 48 kHz.`);
  assert(result.metrics?.channels === 2, `${generatedCue.id} is not stereo.`);
  assert(
    result.metrics?.bitsPerSample === 16,
    `${generatedCue.id} is not signed 16-bit PCM.`,
  );
  assert(
    result.processing?.endpointTreatment ===
      "circular-equal-power-crossfade",
    `${generatedCue.id} does not carry the required circular crossfade.`,
  );
  const wavPath = path.join(outputRoot, generatedCue.file);
  const wav = await readFile(wavPath);
  assert(
    sha256(wav) === result.outputSha256,
    `${generatedCue.id} WAV hash differs from its provider provenance.`,
  );
  assert(
    wav.length === result.outputBytes,
    `${generatedCue.id} WAV size differs from its provider provenance.`,
  );
  const reviewRelativeGain =
    generation.energyAudit.ambientRelativeGains[generatedCue.id];
  assert(
    Number.isFinite(reviewRelativeGain) &&
      reviewRelativeGain > 0 &&
      reviewRelativeGain <= generation.energyAudit.maximumAmbientReviewGain,
    `${generatedCue.id} lacks a safe audited review gain.`,
  );
  backgroundLoops.push({
    id: generatedCue.id,
    label: generatedCue.label,
    category: generatedCue.category,
    status: "proposed",
    description: generatedCue.description,
    mapping: "Sound-lab comparison only; no runtime mapping",
    file: `./v5/${generation.outputDirectory}/${generatedCue.file}`,
    durationSeconds: result.metrics.durationSeconds,
    peak: result.metrics.peak,
    rms: result.metrics.rms,
    stereoDifferenceRms: result.metrics.stereoDifferenceRms,
    dcOffset: result.metrics.dcOffset,
    ...(result.metrics.energy ? { energy: result.metrics.energy } : {}),
    reviewRelativeGain,
    loop: true,
    loopCrossfadeSeconds: result.processing.loopCrossfadeSeconds,
  });
}

const manifest = {
  version: 5,
  generation: "arc-relay-space-ambient-sfx-engine-addition",
  generatedBy: [
    ".claude/skills/sfx-engine/scripts/generate-pack.mjs",
    ...(locallyDerivedCues.length === 0
      ? []
      : ["scripts/derive-audio-v5-revisions.mjs"]),
    ...(locallySafetyMasteredCues.length === 0
      ? []
      : ["scripts/master-audio-v5-energy.mjs"]),
    "scripts/build-audio-v5-candidates.mjs",
  ],
  additiveTo: "manifest-v4.json",
  sampleRate: 48_000,
  channels: 2,
  format: "pcm-s16le-wav",
  reviewScope: {
    ...baseManifest.reviewScope,
    runtimePack: "astral-relay",
    runtimeStatus: "promoted as the sole/default gameplay SFX pack",
    mappedCueIds: runtimeCueIds,
    proposedCueIds: [],
    reviewOnlyCueIds: ["attack-fizzle", "attack-cancel", "arc-steal"],
    silentOutcomeCueIds: ["attack-cancel"],
    runtimeAliases: {
      "attack-fizzle": "projectile",
      "arc-steal": "arc-pickup",
    },
    selectedBackgroundLoop: null,
  },
  sourceComposition: {
    providerGeneratedCues: comparisonGenerationCues.length - locallyDerivedCues.length,
    locallyDerivedCues: locallyDerivedCues.length,
    locallySafetyMasteredCues: locallySafetyMasteredCues.length,
  },
  energyAudit: generation.energyAudit,
  backgroundLoops,
  packs: [
    {
      ...generation.pack,
      provider: [
        `${generation.providerDisplayName} sources`,
        ...(locallyDerivedCues.length === 0 ? [] : ["local derivation"]),
        ...(locallySafetyMasteredCues.length === 0
          ? []
          : ["local safety mastering"]),
      ].join(" + "),
      provenance: "./v5/sfx-engine-generation.json",
      apiSpecSha256: generation.apiSpecSha256,
      energySafety: generation.localSafetyMastering,
      cues,
    },
  ],
};

const json = `${JSON.stringify(manifest, null, 2)}\n`;
await writeFile(path.join(labRoot, "manifest-v5.json"), json);
await writeFile(
  path.join(labRoot, "manifest-v5.js"),
  `window.SOUND_LAB_V5_MANIFEST = ${JSON.stringify(manifest)};\n`,
);
console.log(
  `Built V5 SFX Engine addition with ${cues.length} cues and ` +
    `${backgroundLoops.length} ambience loops; ${locallyDerivedCues.length} ` +
    `cue(s) are locally derived and ${locallySafetyMasteredCues.length} ` +
    `cue(s) are locally safety-mastered. Output: ${labRoot}.`,
);

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
