#!/usr/bin/env node

// Deterministic local delivery mastering for approved signature sources.
// This script never reads credentials, contacts SFX Engine, or synthesizes audio.

import { createHash } from "node:crypto";
import { readFile, rename, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const generationPath = path.join(
  repositoryRoot,
  "art",
  "audio",
  "sound-lab",
  "v5",
  "signature-sfx-engine-generation.json",
);
const generationDirectory = path.dirname(generationPath);
const generation = JSON.parse(await readFile(generationPath, "utf8"));
const addendumPath = path.join(
  generationDirectory,
  "signature-v2-sfx-engine-generation.json",
);
const addendum = JSON.parse(await readFile(addendumPath, "utf8"));
const sourceDirectory = path.resolve(
  generationDirectory,
  generation.outputDirectory,
);
const addendumSourceDirectory = path.resolve(
  generationDirectory,
  addendum.outputDirectory,
);
const outputDirectory = path.join(
  generationDirectory,
  "packs",
  "astral-relay",
);
const scriptPath = "scripts/derive-audio-v5-signatures.mjs";
const runtimeGain = 0.16;

const sources = {
  materialize: {
    cueId: "signature-source-materialize",
    outputSha256:
      "979a44a0213c8e25515e6e38e1ff7a5bd1ea634636dfc71ab354acfebcf5fc9f",
  },
  tether: {
    cueId: "signature-source-tether",
    outputSha256:
      "beea309dd3d5922cb4574e0f50623a7a8a6dc81a4bb891905859feced89f8b6c",
  },
  field: {
    cueId: "signature-source-field",
    outputSha256:
      "46ac5971e0b4887e63a826d3551dfc8ed203bbbb81802646bbe263d34a3373bf",
  },
  transfer: {
    cueId: "signature-source-transfer",
    outputSha256:
      "41c7385252fbcf1a6700ed98d17dd0f3742fa24f43e620609d25c95f5f18c489",
  },
  target: {
    cueId: "signature-source-target",
    outputSha256:
      "fcc750b86bb5e56ac952be0b1ac163cc9c381241bbc81a9246851a9f4a966b0f",
  },
  kinetic: {
    cueId: "signature-source-kinetic",
    outputSha256:
      "5e7b84b908c2b741e732604dbdf49c87e12bdc8eea2fab7349dbbeccda6bbb71",
  },
  mason: {
    cueId: "signature-v2-source-mason",
    outputSha256:
      "2a3d46e6827883c6ea77325956cdb0ce17d05797f6826b0bac64dcf75a1f3702",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  repair: {
    cueId: "signature-v2-source-repair",
    outputSha256:
      "2e52e37e87ec53f55b858f01e59d2c21d054a5fec9ff2aaed56066b6ac374464",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  fallingStar: {
    cueId: "signature-v2-source-falling-star",
    outputSha256:
      "8f82573435ebdc2e7b4d549186b7c83d4c0ddef8c4d2b57be9a190b1357bf62a",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  tripNode: {
    cueId: "signature-v2-source-trip-node",
    outputSha256:
      "2ff3336169d2395d4a5405f52ddabb4350c8d139c6df51a0d4828da7f637c663",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  tripTrigger: {
    cueId: "signature-v2-source-trip-trigger",
    outputSha256:
      "5f1d8fe5dba67c78b9253a1e86297a7d8276d89b978f08f405693538cf144dea",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  arcToss: {
    cueId: "signature-v2-source-arc-toss",
    outputSha256:
      "ae628c82cb0771bbd45d5d129d30eb335ef0c63255a7a5af8602db06eb312abe",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  railLine: {
    cueId: "signature-v2-source-rail-line",
    outputSha256:
      "6726b2842d6aff1e58d2243b51779c34d49459e21de300bae7db455199c88c61",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
  sentinel: {
    cueId: "signature-v2-source-sentinel",
    outputSha256:
      "12fbf70f08338a3e5ddc17c005caa34b65ed2e3b7c50eada35d6dac66492bb70",
    generation: addendum,
    directory: addendumSourceDirectory,
  },
};

const deliveries = [
  {
    id: "signature-construct",
    source: "materialize",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.07,
    playbackRate: 0.75,
    durationSeconds: 0.58,
    lowPassHz: 850,
    fadeInSeconds: 0.012,
    fadeOutSeconds: 0.12,
    targetSamplePeak: 0.22,
    description:
      "The generated panel materialization compacted into one matte low-mid lock.",
  },
  {
    id: "signature-tether",
    source: "tether",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.12,
    playbackRate: 0.95,
    durationSeconds: 0.58,
    lowPassHz: 850,
    fadeInSeconds: 0.01,
    fadeOutSeconds: 0.14,
    targetSamplePeak: 0.22,
    description:
      "The generated grapple body, cable tension, and latch folded into one restrained launch.",
  },
  {
    id: "signature-survey",
    source: "field",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.55,
    durationSeconds: 0.62,
    lowPassHz: 800,
    fadeInSeconds: 0.025,
    fadeOutSeconds: 0.16,
    targetSamplePeak: 0.22,
    description:
      "The generated field aperture's opening tonal centre, used for a landed survey field.",
  },
  {
    id: "signature-null",
    source: "field",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 1.05,
    playbackRate: 0.65,
    durationSeconds: 0.62,
    lowPassHz: 650,
    fadeInSeconds: 0.04,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.22,
    description:
      "The generated field aperture's darker late centre, used for Null activation.",
  },
  {
    id: "signature-smoke",
    source: "field",
    channelTransform: "side-to-dual-mono",
    sourceStartSeconds: 1.35,
    playbackRate: 0.7,
    durationSeconds: 0.68,
    lowPassHz: 700,
    fadeInSeconds: 0.035,
    fadeOutSeconds: 0.2,
    targetSamplePeak: 0.22,
    description:
      "The generated field aperture's filtered side air, collapsed safely to mono for smoke landing.",
  },
  {
    id: "signature-motion",
    source: "transfer",
    channelTransform: "side-to-dual-mono",
    sourceStartSeconds: 0.05,
    playbackRate: 0.9,
    durationSeconds: 0.42,
    lowPassHz: 750,
    fadeInSeconds: 0.02,
    fadeOutSeconds: 0.13,
    targetSamplePeak: 0.22,
    description:
      "The generated displacement source's first pressure fold, compacted for Vector Dash completion.",
  },
  {
    id: "signature-exchange",
    source: "transfer",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.18,
    playbackRate: 0.7,
    durationSeconds: 0.66,
    lowPassHz: 750,
    fadeInSeconds: 0.025,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.22,
    description:
      "The generated displacement source's paired pressure body and servo settle for Exchange completion.",
  },
  {
    id: "signature-target",
    source: "target",
    channelTransform: "side-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.65,
    durationSeconds: 0.45,
    lowPassHz: 750,
    fadeInSeconds: 0.015,
    fadeOutSeconds: 0.14,
    targetSamplePeak: 0.22,
    description:
      "The generated optical confirmation's rounded pulse and latch, with unsafe anti-phase width removed.",
  },
  {
    id: "signature-kinetic",
    source: "kinetic",
    channelTransform: "side-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.45,
    durationSeconds: 0.28,
    lowPassHz: 700,
    fadeInSeconds: 0.008,
    fadeOutSeconds: 0.11,
    targetSamplePeak: 0.22,
    description:
      "The generated repulsion's compact pressure onset, with its near-inverted stereo corrected.",
  },
  {
    id: "signature-mason",
    source: "mason",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.04,
    playbackRate: 0.82,
    durationSeconds: 0.56,
    lowPassHz: 600,
    lowPassPasses: 2,
    fadeInSeconds: 0.1,
    fadeOutSeconds: 0.15,
    targetSamplePeak: 0.17,
    description:
      "The framed pane rise and cushioned hardlight field lock, shaped as a soft low-mid projection distinct from Palisade's panel materialization.",
  },
  {
    id: "signature-repair",
    source: "repair",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.62,
    durationSeconds: 0.66,
    lowPassHz: 650,
    fadeInSeconds: 0.04,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.18,
    description:
      "The generated warm braided transfer compacted into a soft low restoration pulse for medic repairs only.",
  },
  {
    id: "signature-falling-star",
    source: "fallingStar",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.14,
    playbackRate: 0.78,
    durationSeconds: 0.68,
    lowPassHz: 700,
    fadeInSeconds: 0.025,
    fadeOutSeconds: 0.2,
    targetSamplePeak: 0.19,
    description:
      "The generated orbital descent and matte arrival retained as one compact completion gesture.",
  },
  {
    id: "signature-trip-deploy",
    source: "tripNode",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.9,
    durationSeconds: 0.38,
    lowPassHz: 650,
    fadeInSeconds: 0.02,
    fadeOutSeconds: 0.12,
    targetSamplePeak: 0.17,
    description:
      "The replacement gravity sensor's soft levitating unfold and magnetic settle, used only when a Trip Node is placed.",
  },
  {
    id: "signature-trip-trigger",
    source: "tripTrigger",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.9,
    durationSeconds: 0.32,
    lowPassHz: 600,
    fadeInSeconds: 0.01,
    fadeOutSeconds: 0.11,
    targetSamplePeak: 0.18,
    description:
      "The replacement local gravity pinch and muted pressure bloom, reserved for an authoritative triggered removal.",
  },
  {
    id: "signature-arc-toss",
    source: "arcToss",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.02,
    playbackRate: 0.7,
    durationSeconds: 0.64,
    lowPassHz: 700,
    fadeInSeconds: 0.035,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.18,
    description:
      "The generated levitation lift and release, used when the authoritative Core leaves its carrier.",
  },
  {
    id: "signature-rail-charge",
    source: "railLine",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.58,
    durationSeconds: 0.38,
    lowPassHz: 600,
    fadeInSeconds: 0.05,
    fadeOutSeconds: 0.12,
    targetSamplePeak: 0.16,
    description:
      "The generated rail bed's low opening pressure, replacing the ordinary attack wind-up.",
  },
  {
    id: "signature-rail-release",
    source: "railLine",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 1.28,
    playbackRate: 0.58,
    durationSeconds: 0.62,
    lowPassHz: 500,
    fadeInSeconds: 0.018,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.18,
    description:
      "The generated rail's late discharge, slowed, mono-folded, and softened for the completed line.",
  },
  {
    id: "signature-sentinel-deploy",
    source: "sentinel",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0,
    playbackRate: 0.38,
    durationSeconds: 0.64,
    lowPassHz: 400,
    fadeInSeconds: 0.025,
    fadeOutSeconds: 0.18,
    targetSamplePeak: 0.18,
    description:
      "The generated seed-to-sentry unfold, aligned with the authoritative deployed fact.",
  },
  {
    id: "signature-sentinel-fire",
    source: "sentinel",
    channelTransform: "mid-to-dual-mono",
    sourceStartSeconds: 0.02,
    playbackRate: 0.38,
    durationSeconds: 0.24,
    lowPassHz: 400,
    fadeInSeconds: 0.01,
    fadeOutSeconds: 0.1,
    targetSamplePeak: 0.15,
    description:
      "The generated sentry's compact low pulse, shortened for repeated bolt launches.",
  },
];

assert(generation.provider === "sfx-engine", "Unexpected signature provider.");
assert(
  generation.batch?.maximumProviderJobs === 6 &&
    generation.batch?.automaticRerolls === false &&
    generation.cues?.length === 6,
  "Signature generation authorization differs from the approved six-job batch.",
);
assert(addendum.provider === "sfx-engine", "Unexpected signature addendum provider.");
const addendumProviderJobs = addendum.cues.reduce(
  (total, cue) => total + 1 + (cue.attempts?.length ?? 0),
  0,
);
assert(
  addendum.batch?.maximumProviderJobs === 11 &&
    addendum.batch?.automaticRerolls === false &&
    addendum.batch?.ambience === false &&
    addendum.cues?.length === 8 &&
    addendumProviderJobs === 11,
  "Signature addendum differs from the approved eleven-job batch.",
);

const decodedSources = new Map();
for (const [name, pin] of Object.entries(sources)) {
  const sourceGeneration = pin.generation ?? generation;
  const sourceRoot = pin.directory ?? sourceDirectory;
  const cue = sourceGeneration.cues.find(
    (candidate) => candidate.id === pin.cueId,
  );
  assert(cue?.result?.status === "generated", `${pin.cueId} is not generated.`);
  assert(
    cue.result.outputSha256 === pin.outputSha256,
    `${pin.cueId} generation result differs from the checked-in source pin.`,
  );
  const sourcePath = path.join(sourceRoot, cue.file);
  const bytes = await readFile(sourcePath);
  assert(
    sha256(bytes) === pin.outputSha256,
    `${pin.cueId} WAV differs from its checked-in source pin.`,
  );
  const wav = parsePcm16Wav(bytes, pin.cueId);
  assert(
    wav.sampleRate === 48_000 && wav.channels === 2,
    `${pin.cueId} is not 48 kHz stereo.`,
  );
  const samples = decodePcm16(bytes, wav);
  decodedSources.set(name, {
    cue,
    generation: sourceGeneration,
    samples,
    wav,
    interchannelCorrelation: interchannelCorrelation(samples, wav.channels),
  });
}

const previousById = new Map(
  [...(generation.deliveries ?? []), ...(addendum.deliveries ?? [])].map(
    (delivery) => [delivery.id, delivery],
  ),
);
const baseDeliveryRecords = [];
const addendumDeliveryRecords = [];
for (const delivery of deliveries) {
  const source = decodedSources.get(delivery.source);
  assert(source, `${delivery.id} names an unknown source.`);
  const samples = shapeDelivery(source.samples, source.wav, delivery);
  const bytes = encodePcm16Wav(
    samples,
    source.wav.sampleRate,
    source.wav.channels,
  );
  const outputPath = path.join(outputDirectory, `${delivery.id}.wav`);
  await writeFile(outputPath, bytes);
  const metrics = analyzeDeliveryWav(bytes, delivery.id, runtimeGain);
  console.log(
    `${delivery.id} candidate: ` +
      `${JSON.stringify({
        durationSeconds: metrics.durationSeconds,
        spectralCentroidHz: metrics.spectralCentroidHz,
        energyAbove4kPercent: metrics.energyAbove4kPercent,
        energyAbove8kPercent: metrics.energyAbove8kPercent,
        energyAbove12kPercent: metrics.energyAbove12kPercent,
        effectiveSamplePeakDbfs: metrics.effectiveSamplePeakDbfs,
        effectiveRmsDbfs: metrics.effectiveRmsDbfs,
        effectiveMaximumHighBandWindowRmsDbfs:
          metrics.effectiveMaximumHighBandWindowRmsDbfs,
        endpointDiscontinuity: metrics.endpointDiscontinuity,
        dcOffset: metrics.dcOffset,
      })}`,
  );
  validateDelivery(delivery.id, metrics);
  const previous = previousById.get(delivery.id);
  const record = {
    id: delivery.id,
    file: `${delivery.id}.wav`,
    status: "derived",
    description: delivery.description,
    derivedBy: scriptPath,
    sourceCueId: source.cue.id,
    sourceOutputSha256: source.cue.result.outputSha256,
    outputSha256: sha256(bytes),
    outputBytes: bytes.length,
    recordedAt:
      previous?.outputSha256 === sha256(bytes)
        ? previous.recordedAt
        : new Date().toISOString(),
    operations: {
      channelTransform: delivery.channelTransform,
      sourceStartSeconds: delivery.sourceStartSeconds,
      playbackRate: delivery.playbackRate,
      durationSeconds: delivery.durationSeconds,
      lowPass: {
        type: "zero-phase-one-pole",
        cutoffHz: delivery.lowPassHz,
        ...(delivery.lowPassPasses === undefined
          ? {}
          : { passes: delivery.lowPassPasses }),
      },
      fadeInSeconds: delivery.fadeInSeconds,
      fadeOutSeconds: delivery.fadeOutSeconds,
      targetSamplePeak: delivery.targetSamplePeak,
      sourceInterchannelCorrelation: round(
        source.interchannelCorrelation,
        4,
      ),
    },
    intendedRuntimeMix: {
      gain: runtimeGain,
      voiceLimit: 1,
      roomSend: false,
    },
    metrics,
  };
  (source.generation === addendum
    ? addendumDeliveryRecords
    : baseDeliveryRecords
  ).push(record);
  console.log(
    `${delivery.id}: ${metrics.durationSeconds.toFixed(2)}s, ` +
      `${metrics.spectralCentroidHz} Hz centroid, ` +
      `${metrics.energyAbove4kPercent.toFixed(2)}% above 4 kHz, ` +
      `${metrics.effectiveSamplePeakDbfs.toFixed(2)} dBFS effective peak.`,
  );
}

const derivation = {
  script: scriptPath,
  policy:
    "Hash-pinned source selection, channel fold, resampling, low-pass, endpoint fades, and conservative peak normalization only; no synthesized layer and no provider request.",
  runtimeGain,
  limits: {
    maximumDurationSeconds: 0.7,
    maximumSpectralCentroidHz: 1_000,
    maximumEnergyAbove4kPercent: 0.5,
    maximumEnergyAbove8kPercent: 0.05,
    maximumEnergyAbove12kPercent: 0.01,
    maximumEffectiveSamplePeakDbfs: -22,
    maximumEffectiveRmsDbfs: -34,
    maximumEffectiveHighBandWindowRmsDbfs: -42,
    maximumCueGain: 0.2,
    voiceLimit: 1,
    roomSend: false,
  },
};
generation.derivation = derivation;
generation.deliveries = baseDeliveryRecords;
addendum.derivation = derivation;
addendum.deliveries = addendumDeliveryRecords;
const temporaryGenerationPath = `${generationPath}.tmp`;
const temporaryAddendumPath = `${addendumPath}.tmp`;
await writeFile(
  temporaryGenerationPath,
  `${JSON.stringify(generation, null, 2)}\n`,
);
await writeFile(
  temporaryAddendumPath,
  `${JSON.stringify(addendum, null, 2)}\n`,
);
await rename(temporaryGenerationPath, generationPath);
await rename(temporaryAddendumPath, addendumPath);
console.log(
  `Derived and validated ${baseDeliveryRecords.length + addendumDeliveryRecords.length} signature cues.`,
);

function shapeDelivery(source, wav, processing) {
  const frameCount = Math.round(
    processing.durationSeconds * wav.sampleRate,
  );
  const sourceStartFrame = processing.sourceStartSeconds * wav.sampleRate;
  const finalSourcePosition =
    sourceStartFrame + (frameCount - 1) * processing.playbackRate;
  const sourceFrameCount = source.length / wav.channels;
  assert(
    frameCount > 0 &&
      sourceStartFrame >= 0 &&
      processing.playbackRate > 0 &&
      finalSourcePosition <= sourceFrameCount - 1,
    `${processing.id}: delivery window exceeds its source.`,
  );
  const output = new Float64Array(frameCount * wav.channels);
  for (let frame = 0; frame < frameCount; frame += 1) {
    const position = sourceStartFrame + frame * processing.playbackRate;
    const sourceFrame = Math.floor(position);
    const nextFrame = Math.min(sourceFrame + 1, sourceFrameCount - 1);
    const fraction = position - sourceFrame;
    const left =
      source[sourceFrame * 2] * (1 - fraction) +
      source[nextFrame * 2] * fraction;
    const right =
      source[sourceFrame * 2 + 1] * (1 - fraction) +
      source[nextFrame * 2 + 1] * fraction;
    let mono;
    if (processing.channelTransform === "mid-to-dual-mono") {
      mono = (left + right) * 0.5;
    } else if (processing.channelTransform === "side-to-dual-mono") {
      mono = (left - right) * 0.5;
    } else {
      throw new Error(
        `${processing.id}: unsupported channel transform ` +
          `'${processing.channelTransform}'.`,
      );
    }
    output[frame * 2] = mono;
    output[frame * 2 + 1] = mono;
  }
  removeDc(output, wav.channels);
  const lowPassPasses = processing.lowPassPasses ?? 1;
  assert(
    Number.isInteger(lowPassPasses) && lowPassPasses >= 1 && lowPassPasses <= 3,
    `${processing.id}: low-pass passes must be an integer from 1 through 3.`,
  );
  for (let pass = 0; pass < lowPassPasses; pass += 1) {
    lowPassZeroPhase(
      output,
      wav.channels,
      wav.sampleRate,
      processing.lowPassHz,
    );
  }
  applyEndpointFades(
    output,
    wav.channels,
    wav.sampleRate,
    processing.fadeInSeconds,
    processing.fadeOutSeconds,
  );
  removeDc(output, wav.channels);
  // DC removal shifts every sample, including the exact-zero endpoints. Reapply a
  // short final taper so the committed PCM cannot acquire a click at either cut.
  applyEndpointFades(
    output,
    wav.channels,
    wav.sampleRate,
    Math.min(0.008, processing.fadeInSeconds),
    Math.min(0.04, processing.fadeOutSeconds),
  );
  removeDc(output, wav.channels);
  for (let channel = 0; channel < wav.channels; channel += 1) {
    output[channel] = 0;
    output[output.length - wav.channels + channel] = 0;
  }
  normalizePeak(output, processing.targetSamplePeak);
  return output;
}

function validateDelivery(id, metrics) {
  const limits = {
    maximumDurationSeconds: 0.7,
    maximumSpectralCentroidHz: 1_000,
    maximumEnergyAbove4kPercent: 0.5,
    maximumEnergyAbove8kPercent: 0.05,
    maximumEnergyAbove12kPercent: 0.01,
    maximumEffectiveSamplePeakDbfs: -22,
    maximumEffectiveRmsDbfs: -34,
    maximumEffectiveHighBandWindowRmsDbfs: -42,
  };
  assert(
    metrics.durationSeconds <= limits.maximumDurationSeconds,
    `${id}: delivery exceeds ${limits.maximumDurationSeconds}s.`,
  );
  assert(
    metrics.spectralCentroidHz <= limits.maximumSpectralCentroidHz,
    `${id}: spectral centroid is too high.`,
  );
  assert(
    metrics.energyAbove4kPercent <= limits.maximumEnergyAbove4kPercent &&
      metrics.energyAbove8kPercent <= limits.maximumEnergyAbove8kPercent &&
      metrics.energyAbove12kPercent <= limits.maximumEnergyAbove12kPercent,
    `${id}: delivery exceeds an upper-band energy ceiling.`,
  );
  assert(
    metrics.effectiveSamplePeakDbfs <=
      limits.maximumEffectiveSamplePeakDbfs &&
      metrics.effectiveRmsDbfs <= limits.maximumEffectiveRmsDbfs &&
    metrics.effectiveMaximumHighBandWindowRmsDbfs <=
        limits.maximumEffectiveHighBandWindowRmsDbfs,
    `${id}: delivery exceeds an effective level ceiling.`,
  );
  assert(metrics.dcOffset <= 0.00001, `${id}: DC offset is too large.`);
  assert(metrics.clippedSamples === 0, `${id}: delivery contains clipping.`);
  assert(
    metrics.interchannelCorrelation >= 0.999,
    `${id}: delivery is not pan-safe dual mono.`,
  );
  assert(
    metrics.endpointDiscontinuity <= 0.002,
    `${id}: delivery endpoint is discontinuous.`,
  );
}

function parsePcm16Wav(bytes, label) {
  assert(
    bytes.toString("ascii", 0, 4) === "RIFF" &&
      bytes.toString("ascii", 8, 12) === "WAVE",
    `${label}: source is not RIFF/WAVE audio.`,
  );
  let offset = 12;
  let format;
  let dataOffset;
  let dataLength;
  while (offset + 8 <= bytes.length) {
    const id = bytes.toString("ascii", offset, offset + 4);
    const length = bytes.readUInt32LE(offset + 4);
    const body = offset + 8;
    if (id === "fmt ") {
      format = {
        audioFormat: bytes.readUInt16LE(body),
        channels: bytes.readUInt16LE(body + 2),
        sampleRate: bytes.readUInt32LE(body + 4),
        bitsPerSample: bytes.readUInt16LE(body + 14),
      };
    } else if (id === "data") {
      dataOffset = body;
      dataLength = length;
    }
    offset = body + length + (length % 2);
  }
  assert(
    format && dataOffset !== undefined && dataLength !== undefined,
    `${label}: source is missing WAV chunks.`,
  );
  assert(
    format.audioFormat === 1 && format.bitsPerSample === 16,
    `${label}: source must be signed 16-bit PCM.`,
  );
  assert(
    dataOffset + dataLength <= bytes.length && dataLength % 2 === 0,
    `${label}: source has an invalid data chunk.`,
  );
  return {
    ...format,
    dataOffset,
    sampleCount: dataLength / 2,
  };
}

function decodePcm16(bytes, wav) {
  const samples = new Float64Array(wav.sampleCount);
  for (let index = 0; index < samples.length; index += 1) {
    samples[index] =
      bytes.readInt16LE(wav.dataOffset + index * 2) / 32_768;
  }
  return samples;
}

function removeDc(samples, channels) {
  const frames = samples.length / channels;
  const means = new Float64Array(channels);
  for (let index = 0; index < samples.length; index += 1) {
    means[index % channels] += samples[index];
  }
  for (let channel = 0; channel < channels; channel += 1) {
    means[channel] /= frames;
  }
  for (let index = 0; index < samples.length; index += 1) {
    samples[index] -= means[index % channels];
  }
}

function lowPassZeroPhase(samples, channels, sampleRate, cutoffHz) {
  const frames = samples.length / channels;
  const alpha = 1 - Math.exp((-2 * Math.PI * cutoffHz) / sampleRate);
  for (let channel = 0; channel < channels; channel += 1) {
    let state = samples[channel];
    for (let frame = 1; frame < frames; frame += 1) {
      const index = frame * channels + channel;
      state += alpha * (samples[index] - state);
      samples[index] = state;
    }
    state = samples[(frames - 1) * channels + channel];
    for (let frame = frames - 2; frame >= 0; frame -= 1) {
      const index = frame * channels + channel;
      state += alpha * (samples[index] - state);
      samples[index] = state;
    }
  }
}

function applyEndpointFades(
  samples,
  channels,
  sampleRate,
  fadeInSeconds,
  fadeOutSeconds,
) {
  const frames = samples.length / channels;
  const fadeInFrames = Math.min(
    frames,
    Math.round(fadeInSeconds * sampleRate),
  );
  const fadeOutFrames = Math.min(
    frames,
    Math.round(fadeOutSeconds * sampleRate),
  );
  for (let frame = 0; frame < frames; frame += 1) {
    const fadeIn =
      fadeInFrames === 0
        ? 1
        : Math.min(1, frame / Math.max(1, fadeInFrames - 1));
    const remaining = frames - 1 - frame;
    const fadeOut =
      fadeOutFrames === 0
        ? 1
        : Math.min(1, remaining / Math.max(1, fadeOutFrames - 1));
    const gain = Math.sin(Math.min(fadeIn, fadeOut) * Math.PI * 0.5);
    for (let channel = 0; channel < channels; channel += 1) {
      samples[frame * channels + channel] *= gain;
    }
  }
}

function normalizePeak(samples, targetPeak) {
  let peak = 0;
  for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
  assert(peak > 0.0001, "Signature delivery audio is effectively silent.");
  const gain = targetPeak / peak;
  for (let index = 0; index < samples.length; index += 1) {
    samples[index] *= gain;
  }
}

function encodePcm16Wav(samples, sampleRate, channels) {
  const pcm = Buffer.alloc(samples.length * 2);
  for (let index = 0; index < samples.length; index += 1) {
    const sample = Math.max(-1, Math.min(1, samples[index]));
    pcm.writeInt16LE(Math.round(sample * 32_767), index * 2);
  }
  const header = Buffer.alloc(44);
  header.write("RIFF", 0, "ascii");
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write("WAVE", 8, "ascii");
  header.write("fmt ", 12, "ascii");
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(channels, 22);
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(sampleRate * channels * 2, 28);
  header.writeUInt16LE(channels * 2, 32);
  header.writeUInt16LE(16, 34);
  header.write("data", 36, "ascii");
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
}

function analyzeDeliveryWav(bytes, label, gain) {
  const wav = parsePcm16Wav(bytes, label);
  const frames = wav.sampleCount / wav.channels;
  let peak = 0;
  let sumSquares = 0;
  let sum = 0;
  let clippedSamples = 0;
  let leftSquares = 0;
  let rightSquares = 0;
  let cross = 0;
  let endpointDiscontinuity = 0;
  const mono = new Float64Array(frames);
  for (let frame = 0; frame < frames; frame += 1) {
    const left =
      bytes.readInt16LE(wav.dataOffset + frame * wav.channels * 2) /
      32_768;
    const right =
      bytes.readInt16LE(
        wav.dataOffset + (frame * wav.channels + 1) * 2,
      ) / 32_768;
    peak = Math.max(peak, Math.abs(left), Math.abs(right));
    sumSquares += left * left + right * right;
    sum += left + right;
    leftSquares += left * left;
    rightSquares += right * right;
    cross += left * right;
    clippedSamples += Number(Math.abs(left) >= 1 || Math.abs(right) >= 1);
    mono[frame] = (left + right) * 0.5;
    if (frame === 0 || frame === frames - 1) {
      endpointDiscontinuity = Math.max(
        endpointDiscontinuity,
        Math.abs(left),
        Math.abs(right),
      );
    }
  }
  const rms = Math.sqrt(sumSquares / (frames * wav.channels));
  const runtimeGainDb = 20 * Math.log10(gain);
  const spectrum = analyzeSpectrum(bytes, wav);
  return {
    durationSeconds: round(frames / wav.sampleRate, 3),
    sampleRate: wav.sampleRate,
    channels: wav.channels,
    bitsPerSample: wav.bitsPerSample,
    samplePeakDbfs: round(20 * Math.log10(peak), 2),
    rmsDbfs: round(20 * Math.log10(rms), 2),
    dcOffset: round(Math.abs(sum / (frames * wav.channels)), 6),
    clippedSamples,
    endpointDiscontinuity: round(endpointDiscontinuity, 6),
    dominantFrequencyHz: dominantFrequencyHz(mono, wav.sampleRate),
    interchannelCorrelation: round(
      cross / Math.sqrt(leftSquares * rightSquares),
      4,
    ),
    ...spectrum,
    effectiveSamplePeakDbfs: round(
      20 * Math.log10(peak) + runtimeGainDb,
      2,
    ),
    effectiveRmsDbfs: round(
      20 * Math.log10(rms) + runtimeGainDb,
      2,
    ),
    effectiveMaximumHighBandWindowRmsDbfs: round(
      spectrum.maximumHighBandWindowRmsDbfs + runtimeGainDb,
      2,
    ),
  };
}

function analyzeSpectrum(bytes, wav) {
  const frames = wav.sampleCount / wav.channels;
  const fftSize = 4_096;
  const hop = fftSize / 2;
  const starts =
    frames < fftSize
      ? [0]
      : Array.from(
          { length: Math.floor((frames - fftSize) / hop) + 1 },
          (_, index) => index * hop,
        );
  const spectrum = new Float64Array(fftSize / 2 + 1);
  let maximumWindowAbove4kPercent = 0;
  let maximumWindowAbove4kAtSeconds = 0;
  let maximumWindowAbove4kRmsDbfs = -Infinity;
  let maximumHighBandWindowRmsDbfs = -Infinity;
  for (const start of starts) {
    const windowSpectrum = new Float64Array(fftSize / 2 + 1);
    let windowSquares = 0;
    for (let channel = 0; channel < wav.channels; channel += 1) {
      const real = new Float64Array(fftSize);
      const imaginary = new Float64Array(fftSize);
      for (let index = 0; index < fftSize; index += 1) {
        const frame = start + index;
        const sample =
          frame < frames
            ? bytes.readInt16LE(
                wav.dataOffset +
                  (frame * wav.channels + channel) * 2,
              ) / 32_768
            : 0;
        windowSquares += sample * sample;
        const window =
          0.5 - 0.5 * Math.cos((2 * Math.PI * index) / (fftSize - 1));
        real[index] = sample * window;
      }
      fft(real, imaginary);
      for (let bin = 1; bin <= fftSize / 2; bin += 1) {
        windowSpectrum[bin] += real[bin] ** 2 + imaginary[bin] ** 2;
      }
    }
    let windowTotal = 0;
    let windowAbove4k = 0;
    for (let bin = 1; bin <= fftSize / 2; bin += 1) {
      const power = windowSpectrum[bin];
      spectrum[bin] += power;
      windowTotal += power;
      if ((bin * wav.sampleRate) / fftSize >= 4_000) {
        windowAbove4k += power;
      }
    }
    if (windowTotal > 0) {
      const percentage = (100 * windowAbove4k) / windowTotal;
      if (percentage > maximumWindowAbove4kPercent) {
        maximumWindowAbove4kPercent = percentage;
        maximumWindowAbove4kAtSeconds = start / wav.sampleRate;
        const windowRmsDbfs =
          20 *
          Math.log10(
            Math.sqrt(windowSquares / (fftSize * wav.channels)),
          );
        maximumWindowAbove4kRmsDbfs = windowRmsDbfs;
        maximumHighBandWindowRmsDbfs =
          windowAbove4k > 0
            ? windowRmsDbfs + 10 * Math.log10(windowAbove4k / windowTotal)
            : -120;
      }
    }
  }
  let total = 0;
  let weightedFrequency = 0;
  let above4k = 0;
  let above8k = 0;
  let above12k = 0;
  for (let bin = 1; bin <= fftSize / 2; bin += 1) {
    const frequency = (bin * wav.sampleRate) / fftSize;
    const power = spectrum[bin];
    total += power;
    weightedFrequency += frequency * power;
    if (frequency >= 4_000) above4k += power;
    if (frequency >= 8_000) above8k += power;
    if (frequency >= 12_000) above12k += power;
  }
  assert(total > 0, "Signature delivery spectrum is effectively silent.");
  if (!Number.isFinite(maximumWindowAbove4kRmsDbfs)) {
    maximumWindowAbove4kRmsDbfs = -120;
  }
  if (!Number.isFinite(maximumHighBandWindowRmsDbfs)) {
    maximumHighBandWindowRmsDbfs = -120;
  }
  return {
    spectralCentroidHz: round(weightedFrequency / total, 0),
    energyAbove4kPercent: round((100 * above4k) / total, 3),
    energyAbove8kPercent: round((100 * above8k) / total, 3),
    energyAbove12kPercent: round((100 * above12k) / total, 3),
    maximumWindowAbove4kPercent: round(maximumWindowAbove4kPercent, 3),
    maximumWindowAbove4kAtSeconds: round(
      maximumWindowAbove4kAtSeconds,
      3,
    ),
    maximumWindowAbove4kRmsDbfs: round(maximumWindowAbove4kRmsDbfs, 2),
    maximumHighBandWindowRmsDbfs: round(
      maximumHighBandWindowRmsDbfs,
      2,
    ),
  };
}

function fft(real, imaginary) {
  const length = real.length;
  for (let index = 1, reversed = 0; index < length; index += 1) {
    let bit = length >> 1;
    for (; reversed & bit; bit >>= 1) reversed ^= bit;
    reversed ^= bit;
    if (index < reversed) {
      [real[index], real[reversed]] = [real[reversed], real[index]];
      [imaginary[index], imaginary[reversed]] = [
        imaginary[reversed],
        imaginary[index],
      ];
    }
  }
  for (let width = 2; width <= length; width <<= 1) {
    const angle = (-2 * Math.PI) / width;
    const rotationReal = Math.cos(angle);
    const rotationImaginary = Math.sin(angle);
    for (let start = 0; start < length; start += width) {
      let phaseReal = 1;
      let phaseImaginary = 0;
      for (let index = 0; index < width / 2; index += 1) {
        const even = start + index;
        const odd = even + width / 2;
        const oddReal =
          real[odd] * phaseReal - imaginary[odd] * phaseImaginary;
        const oddImaginary =
          real[odd] * phaseImaginary + imaginary[odd] * phaseReal;
        const evenReal = real[even];
        const evenImaginary = imaginary[even];
        real[even] = evenReal + oddReal;
        imaginary[even] = evenImaginary + oddImaginary;
        real[odd] = evenReal - oddReal;
        imaginary[odd] = evenImaginary - oddImaginary;
        const nextPhaseReal =
          phaseReal * rotationReal - phaseImaginary * rotationImaginary;
        phaseImaginary =
          phaseReal * rotationImaginary + phaseImaginary * rotationReal;
        phaseReal = nextPhaseReal;
      }
    }
  }
}

function dominantFrequencyHz(samples, sampleRate) {
  const frameCount = Math.min(samples.length, 16_384);
  const windowed = new Float64Array(frameCount);
  for (let frame = 0; frame < frameCount; frame += 1) {
    const window =
      frameCount === 1
        ? 1
        : 0.5 -
          0.5 * Math.cos((2 * Math.PI * frame) / (frameCount - 1));
    windowed[frame] = samples[frame] * window;
  }
  let bestFrequency = 0;
  let bestPower = -Infinity;
  for (let frequency = 60; frequency <= 2_000; frequency += 2) {
    const coefficient =
      2 * Math.cos((2 * Math.PI * frequency) / sampleRate);
    let previous = 0;
    let previousPrevious = 0;
    for (let frame = 0; frame < frameCount; frame += 1) {
      const current =
        windowed[frame] +
        coefficient * previous -
        previousPrevious;
      previousPrevious = previous;
      previous = current;
    }
    const power =
      previous * previous +
      previousPrevious * previousPrevious -
      coefficient * previous * previousPrevious;
    if (power > bestPower) {
      bestPower = power;
      bestFrequency = frequency;
    }
  }
  return bestFrequency;
}

function interchannelCorrelation(samples, channels) {
  assert(channels === 2, "Stereo correlation requires two channels.");
  let leftSquares = 0;
  let rightSquares = 0;
  let cross = 0;
  for (let frame = 0; frame < samples.length / channels; frame += 1) {
    const left = samples[frame * channels];
    const right = samples[frame * channels + 1];
    leftSquares += left * left;
    rightSquares += right * right;
    cross += left * right;
  }
  return cross / Math.sqrt(leftSquares * rightSquares);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function round(value, decimals) {
  return Number(value.toFixed(decimals));
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
