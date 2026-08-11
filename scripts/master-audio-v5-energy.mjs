#!/usr/bin/env node

// Local-only safety mastering. This script never reads credentials or calls a provider.

import { createHash } from "node:crypto";
import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
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
  "sfx-engine-generation.json",
);
const generationDirectory = path.dirname(generationPath);
const generation = JSON.parse(await readFile(generationPath, "utf8"));
const outputDirectory = path.resolve(
  generationDirectory,
  generation.outputDirectory,
);
const sourceDirectory = path.join(
  generationDirectory,
  "sources",
  "astral-relay",
);
const scriptPath = "scripts/master-audio-v5-energy.mjs";

const masters = [
  {
    id: "attack-windup",
    providerTargetPeak: 0.78,
    sourceOutputSha256:
      "42ca3576ebf2fc33f2297a4dd6cf3936be900b8397c3af0c610bf7c464f7e64b",
    lowPassHz: 7_000,
    targetIntegratedLufs: -12,
    peakCeiling: 0.56,
    maximumEnergyAbove12kPercent: 25,
    fadeInSeconds: 0.008,
    fadeOutSeconds: 0.06,
    reason:
      "Energy audit: retain the accepted wind-up gesture while removing its " +
      "ultrasonic-heavy balance and lowering repeated combat energy.",
  },
  {
    id: "attack-fizzle",
    providerTargetPeak: 0.72,
    sourceOutputSha256:
      "a6b65c3560ade8f59f3aeea11201e923c2ad39fa710eefe777d328d241fff386",
    lowPassHz: 8_000,
    targetIntegratedLufs: -15,
    peakCeiling: 0.5,
    maximumEnergyAbove12kPercent: 25,
    fadeInSeconds: 0.008,
    fadeOutSeconds: 0.06,
    reason:
      "Energy audit: keep the compact failed-field gesture while removing " +
      "the upper-band concentration that made it fatiguing.",
  },
  {
    id: "impact",
    providerTargetPeak: 0.8,
    sourceOutputSha256:
      "ff0e120a696c3a6bbd0c7288c930c965097e20a76c46cc89733d77e7488ebe31",
    lowPassHz: 7_000,
    targetIntegratedLufs: -11,
    peakCeiling: 0.55,
    maximumEnergyAbove12kPercent: 20,
    fadeInSeconds: 0.008,
    fadeOutSeconds: 0.06,
    reason:
      "Energy audit: round the accepted armor compression, remove its " +
      "inter-sample overs, and lower it beneath the objective hierarchy.",
  },
  {
    id: "level-up",
    providerTargetPeak: 0.74,
    sourceOutputSha256:
      "2df43894876308a80ee871b10ae66767d21a79b0e7c973d4d05740d2bf8914c6",
    lowPassHz: 8_000,
    targetIntegratedLufs: -15,
    peakCeiling: 0.5,
    maximumEnergyAbove12kPercent: 15,
    compressor: {
      thresholdDb: -24,
      ratio: 4,
      attackSeconds: 0.003,
      releaseSeconds: 0.08,
    },
    fadeInSeconds: 0.008,
    fadeOutSeconds: 0.06,
    reason:
      "Energy audit: retain the generated veterancy lift while removing its " +
      "reconstructed overs and keeping it calm beneath overlapping destruction.",
  },
];
const reviewOutputSafetyGainDb = -3;
const maximumAmbientReviewGain = 0.7;
const reviewSequences = {
  possession: [
    [0, "arc-birth"],
    [1_380, "arc-pickup"],
    [2_180, "arc-handoff"],
    [3_060, "arc-steal"],
    [4_100, "arc-drop"],
    [5_020, "arc-pickup"],
    [5_850, "arc-bank"],
    [7_350, "arc-pulse"],
  ],
  combat: [
    [0, "attack-windup"],
    [360, "projectile"],
    [920, "impact"],
    [1_720, "attack-windup"],
    [2_080, "attack-fizzle"],
    [3_220, "attack-windup"],
    [3_550, "attack-cancel"],
    [4_400, "attack-windup"],
    [4_760, "projectile"],
    [5_360, "destroyed"],
    [5_360, "level-up"],
    [7_080, "arrival"],
  ],
  full: [
    [0, "arc-birth"],
    [1_360, "arc-pickup"],
    [2_130, "arc-handoff"],
    [3_030, "attack-windup"],
    [3_390, "projectile"],
    [3_950, "impact"],
    [4_720, "attack-windup"],
    [5_080, "attack-fizzle"],
    [6_150, "arc-steal"],
    [7_220, "arc-drop"],
    [8_150, "arc-pickup"],
    [8_980, "arc-bank"],
    [10_500, "arc-pulse"],
    [13_180, "attack-windup"],
    [13_520, "attack-cancel"],
    [14_420, "destroyed"],
    [14_420, "level-up"],
    [16_250, "arrival"],
  ],
};

await mkdir(sourceDirectory, { recursive: true });

for (const master of masters) {
  const cue = cueById(master.id);
  assert(
    cue.result?.status === "generated",
    `${cue.id}: safety mastering requires a provider-generated cue.`,
  );

  const outputPath = safeOutputPath(cue.file);
  const sourceFilename = `${cue.id}.pre-safety-master.wav`;
  const sourcePath = path.join(sourceDirectory, sourceFilename);
  const sourceRelativePath = path.relative(generationDirectory, sourcePath);
  let sourceBytes;
  try {
    sourceBytes = await readFile(sourcePath);
  } catch (error) {
    if (error?.code !== "ENOENT") throw error;
    const currentBytes = await readFile(outputPath);
    assert(
      sha256(currentBytes) === master.sourceOutputSha256,
      `${cue.id}: current WAV is not the expected pre-safety master.`,
    );
    await writeFile(sourcePath, currentBytes, { mode: 0o644 });
    sourceBytes = currentBytes;
  }
  assert(
    sha256(sourceBytes) === master.sourceOutputSha256,
    `${cue.id}: archived pre-safety master differs from its pinned hash.`,
  );

  const sourceResult = cue.safetyMastering?.sourceResult ?? cue.result;
  assert(
    sourceResult.outputSha256 === master.sourceOutputSha256,
    `${cue.id}: saved provider result differs from the safety source pin.`,
  );

  const wav = parsePcm16Wav(sourceBytes, cue.id);
  assert(
    wav.sampleRate === 48_000 && wav.channels === 2,
    `${cue.id}: expected a 48 kHz stereo source.`,
  );
  const samples = decodeSamples(sourceBytes, wav);
  removeDc(samples, wav.channels);
  lowPassZeroPhase(
    samples,
    wav.channels,
    wav.sampleRate,
    master.lowPassHz,
  );
  if (master.compressor) {
    compressLinkedPeaks(
      samples,
      wav.channels,
      wav.sampleRate,
      master.compressor,
    );
  }
  applyEndpointFades(
    samples,
    wav.channels,
    wav.sampleRate,
    master.fadeInSeconds,
    master.fadeOutSeconds,
  );
  removeDc(samples, wav.channels);
  fitLoudnessAndPeak(
    samples,
    wav.channels,
    wav.sampleRate,
    master.targetIntegratedLufs,
    master.peakCeiling,
  );

  const outputBytes = encodePcm16Wav(samples, wav.sampleRate, wav.channels);
  const metrics = analyzeWav(outputBytes, cue.id);
  assert(
    metrics.energy.approximateTruePeakDbtp <= -3,
    `${cue.id}: ${metrics.energy.approximateTruePeakDbtp} dBTP exceeds the -3 dBTP ceiling.`,
  );
  assert(
    metrics.energy.integratedLufs <= master.targetIntegratedLufs + 0.15,
    `${cue.id}: ${metrics.energy.integratedLufs} LUFS exceeds its energy target.`,
  );
  assert(
    metrics.energy.energyAbove12kPercent <=
      master.maximumEnergyAbove12kPercent,
    `${cue.id}: ${metrics.energy.energyAbove12kPercent}% energy above 12 kHz ` +
      `exceeds ${master.maximumEnergyAbove12kPercent}%.`,
  );

  const operations = [
    {
      type: "zero-phase-butterworth-low-pass",
      cutoffHz: master.lowPassHz,
      orderPerDirection: 2,
    },
    ...(master.compressor
      ? [{ type: "linked-peak-compressor", ...master.compressor }]
      : []),
    {
      type: "endpoint-fades",
      fadeInSeconds: master.fadeInSeconds,
      fadeOutSeconds: master.fadeOutSeconds,
    },
    {
      type: "loudness-fit-with-sample-peak-ceiling",
      targetIntegratedLufs: master.targetIntegratedLufs,
      samplePeakCeiling: master.peakCeiling,
    },
  ];
  const safetyMastering = {
    masteredBy: scriptPath,
    sourceFile: sourceRelativePath,
    sourceOutputSha256: master.sourceOutputSha256,
    recordedAt:
      cue.safetyMastering?.recordedAt ?? new Date().toISOString(),
    reason: master.reason,
    operations,
    sourceResult,
  };

  // Keep the provider request normalization valid and reproducible. The lower
  // delivery ceiling belongs to the local operation record above.
  cue.targetPeak = master.providerTargetPeak;
  cue.safetyRevisionReason = master.reason;
  cue.safetyMastering = safetyMastering;
  cue.result = {
    ...sourceResult,
    outputSha256: sha256(outputBytes),
    outputBytes: outputBytes.length,
    processing: {
      ...sourceResult.processing,
      safetyMastering: {
        masteredBy: scriptPath,
        sourceFile: sourceRelativePath,
        sourceOutputSha256: master.sourceOutputSha256,
        operations,
      },
    },
    metrics,
  };

  const temporaryOutputPath = `${outputPath}.tmp`;
  await writeFile(temporaryOutputPath, outputBytes);
  await rename(temporaryOutputPath, outputPath);
  console.log(
    `Safety-mastered ${cue.id}: ${metrics.energy.integratedLufs.toFixed(2)} LUFS, ` +
      `${metrics.energy.approximateTruePeakDbtp.toFixed(2)} dBTP, ` +
      `${metrics.energy.energyAbove12kPercent.toFixed(2)}% above 12 kHz.`,
  );
}

const energyAudit = await auditCurrentPack();
generation.localSafetyMastering = {
  script: scriptPath,
  policy:
    "Deterministic filtering, linked dynamics, and attenuation of hash-pinned " +
    "provider masters; no provider API request or synthesized layer.",
  cueIds: masters.map((master) => master.id),
  thresholds: {
    maximumApproximateTruePeakDbtp: -3,
    maximumEnergyAbove12kPercent: Object.fromEntries(
      masters.map((master) => [
        master.id,
        master.maximumEnergyAbove12kPercent,
      ]),
    ),
  },
};
generation.energyAudit = {
  script: scriptPath,
  recordedAt:
    generation.energyAudit?.recordedAt ?? new Date().toISOString(),
  method:
    "ITU-R BS.1770 K-weighted loudness, four-times windowed-sinc true-peak " +
    "estimate, FFT upper-band energy, and exact sound-lab sequence sums " +
    "against every background-loop alternative.",
  individualMaximumApproximateTruePeakDbtp: -0.75,
  reviewOutputSafetyGainDb,
  reviewSequenceMaximumApproximateTruePeakDbtp: -1,
  limiter: {
    thresholdDb: -8,
    kneeDb: 2,
    ratio: 20,
    attackSeconds: 0.001,
    releaseSeconds: 0.12,
  },
  ambientLoopIds: energyAudit.ambientLoopIds,
  maximumAmbientReviewGain,
  ambientRelativeGains: energyAudit.ambientRelativeGains,
  cueResults: energyAudit.cueResults,
  sequenceResults: energyAudit.sequenceResults,
};

const temporaryGenerationPath = `${generationPath}.tmp`;
await writeFile(
  temporaryGenerationPath,
  `${JSON.stringify(generation, null, 2)}\n`,
  { mode: 0o644 },
);
await rename(temporaryGenerationPath, generationPath);

function cueById(id) {
  const cue = generation.cues.find((candidate) => candidate.id === id);
  if (!cue) throw new Error(`Unknown cue: ${id}`);
  return cue;
}

function safeOutputPath(relativePath) {
  assert(
    typeof relativePath === "string" && !path.isAbsolute(relativePath),
    `Unsafe audio path: ${relativePath}`,
  );
  const resolved = path.resolve(outputDirectory, relativePath);
  assert(
    resolved.startsWith(`${outputDirectory}${path.sep}`),
    `Audio path escapes output directory: ${relativePath}`,
  );
  return resolved;
}

async function auditCurrentPack() {
  const wavs = new Map();
  const cueResults = {};
  for (const cue of generation.cues) {
    assert(
      ["generated", "derived"].includes(cue.result?.status),
      `${cue.id}: cannot energy-audit an incomplete cue.`,
    );
    const bytes = await readFile(safeOutputPath(cue.file));
    const wav = parsePcm16Wav(bytes, cue.id);
    const samples = decodeSamples(bytes, wav);
    const metrics = analyzeWav(bytes, cue.id);
    assert(
      metrics.energy.approximateTruePeakDbtp <= -0.75,
      `${cue.id}: ${metrics.energy.approximateTruePeakDbtp} dBTP exceeds ` +
        `the individual audit ceiling.`,
    );
    cue.result.metrics.energy = metrics.energy;
    cueResults[cue.id] = {
      durationSeconds: metrics.durationSeconds,
      ...metrics.energy,
    };
    wavs.set(cue.id, {
      ...wav,
      samples,
    });
  }

  const ambientLoopIds = generation.cues
    .filter((cue) => cue.role === "background-loop")
    .map((cue) => cue.id);
  assert(ambientLoopIds.length > 0, "The energy audit requires ambience.");
  const referenceAmbientLufs =
    cueResults[ambientLoopIds[0]].integratedLufs;
  const ambientRelativeGains = Object.fromEntries(
    ambientLoopIds.map((ambientLoopId) => [
      ambientLoopId,
      round(
        Math.min(
          maximumAmbientReviewGain,
          0.32 *
            10 **
              ((referenceAmbientLufs -
                cueResults[ambientLoopId].integratedLufs) /
                20),
        ),
        4,
      ),
    ]),
  );
  assert(
    Object.values(ambientRelativeGains).every(
      (gain) => gain > 0 && gain <= maximumAmbientReviewGain,
    ),
    "An ambience alternative cannot be level-adjusted within safe gain bounds.",
  );
  const sequenceResults = {};
  for (const [name, timeline] of Object.entries(reviewSequences)) {
    const ambientVariants = {};
    for (const ambientLoopId of ambientLoopIds) {
      const relativeGain = ambientRelativeGains[ambientLoopId];
      const samples = mixReviewSequence(
        wavs,
        timeline,
        ambientLoopId,
        relativeGain,
      );
      const bytes = encodePcm16Wav(samples, 48_000, 2);
      const metrics = analyzeWav(
        bytes,
        `${name} review sequence with ${ambientLoopId}`,
        { includeSpectrum: false },
      );
      assert(
        metrics.energy.approximateTruePeakDbtp <= -1,
        `${name} with ${ambientLoopId}: ` +
          `${metrics.energy.approximateTruePeakDbtp} dBTP exceeds the ` +
          `limited review-sequence ceiling.`,
      );
      ambientVariants[ambientLoopId] = {
        durationSeconds: metrics.durationSeconds,
        integratedLufs: metrics.energy.integratedLufs,
        maxMomentaryLufs: metrics.energy.maxMomentaryLufs,
        approximateTruePeakDbtp: metrics.energy.approximateTruePeakDbtp,
        relativeGain,
      };
    }
    const variants = Object.values(ambientVariants);
    sequenceResults[name] = {
      durationSeconds: Math.max(
        ...variants.map((variant) => variant.durationSeconds),
      ),
      integratedLufs: Math.max(
        ...variants.map((variant) => variant.integratedLufs),
      ),
      maxMomentaryLufs: Math.max(
        ...variants.map((variant) => variant.maxMomentaryLufs),
      ),
      approximateTruePeakDbtp: Math.max(
        ...variants.map((variant) => variant.approximateTruePeakDbtp),
      ),
      summaryMode: "worst metric across ambient alternatives",
      ambientRelativeGains,
      reviewVolume: 1,
      fixedOutputSafetyGainDb: reviewOutputSafetyGainDb,
      compressorAttenuationModeled: false,
      ambientVariants,
    };
  }
  return {
    ambientLoopIds,
    ambientRelativeGains,
    cueResults,
    sequenceResults,
  };
}

function mixReviewSequence(
  wavs,
  timeline,
  ambientLoopId,
  ambientRelativeGain,
) {
  const sampleRate = 48_000;
  const channels = 2;
  const lastFrame = Math.max(
    ...timeline.map(([milliseconds, id]) => {
      const wav = wavs.get(id);
      assert(wav, `Unknown sequence cue: ${id}`);
      return Math.round((milliseconds * sampleRate) / 1_000) +
        wav.sampleCount / wav.channels;
    }),
  );
  const output = new Float64Array(lastFrame * channels);
  for (const [milliseconds, id] of timeline) {
    const wav = wavs.get(id);
    const startFrame = Math.round((milliseconds * sampleRate) / 1_000);
    for (let frame = 0; frame < wav.sampleCount / channels; frame++) {
      for (let channel = 0; channel < channels; channel++) {
        output[(startFrame + frame) * channels + channel] +=
          wav.samples[frame * channels + channel];
      }
    }
  }
  const ambience = wavs.get(ambientLoopId);
  assert(ambience, `Unknown review ambience: ${ambientLoopId}`);
  const ambienceFrames = ambience.sampleCount / channels;
  const safetyGain = 10 ** (reviewOutputSafetyGainDb / 20);
  for (let frame = 0; frame < lastFrame; frame++) {
    for (let channel = 0; channel < channels; channel++) {
      const index = frame * channels + channel;
      output[index] =
        (output[index] +
          ambience.samples[
            (frame % ambienceFrames) * channels + channel
          ] *
            ambientRelativeGain) *
        safetyGain;
    }
  }
  return output;
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
    `${label}: missing WAV chunks.`,
  );
  assert(
    format.audioFormat === 1 && format.bitsPerSample === 16,
    `${label}: source must be signed 16-bit PCM.`,
  );
  assert(
    dataOffset + dataLength <= bytes.length,
    `${label}: invalid WAV data length.`,
  );
  return {
    ...format,
    dataOffset,
    sampleCount: dataLength / 2,
  };
}

function decodeSamples(bytes, wav) {
  const samples = new Float64Array(wav.sampleCount);
  for (let index = 0; index < wav.sampleCount; index++) {
    samples[index] = bytes.readInt16LE(wav.dataOffset + index * 2) / 32_768;
  }
  return samples;
}

function lowPassZeroPhase(samples, channels, sampleRate, cutoffHz) {
  const omega = (2 * Math.PI * cutoffHz) / sampleRate;
  const cosine = Math.cos(omega);
  const alpha = Math.sin(omega) / (2 * Math.SQRT1_2);
  const a0 = 1 + alpha;
  const coefficients = {
    b0: ((1 - cosine) / 2) / a0,
    b1: (1 - cosine) / a0,
    b2: ((1 - cosine) / 2) / a0,
    a1: (-2 * cosine) / a0,
    a2: (1 - alpha) / a0,
  };
  filterBiquad(samples, channels, coefficients, false);
  filterBiquad(samples, channels, coefficients, true);
}

function filterBiquad(samples, channels, coefficients, reverse) {
  const frames = samples.length / channels;
  for (let channel = 0; channel < channels; channel++) {
    let x1 = 0;
    let x2 = 0;
    let y1 = 0;
    let y2 = 0;
    for (let step = 0; step < frames; step++) {
      const frame = reverse ? frames - 1 - step : step;
      const index = frame * channels + channel;
      const x0 = samples[index];
      const y0 =
        coefficients.b0 * x0 +
        coefficients.b1 * x1 +
        coefficients.b2 * x2 -
        coefficients.a1 * y1 -
        coefficients.a2 * y2;
      samples[index] = y0;
      x2 = x1;
      x1 = x0;
      y2 = y1;
      y1 = y0;
    }
  }
}

function removeDc(samples, channels) {
  const means = new Float64Array(channels);
  const frames = samples.length / channels;
  for (let index = 0; index < samples.length; index++) {
    means[index % channels] += samples[index];
  }
  for (let channel = 0; channel < channels; channel++) means[channel] /= frames;
  for (let index = 0; index < samples.length; index++) {
    samples[index] -= means[index % channels];
  }
}

function compressLinkedPeaks(
  samples,
  channels,
  sampleRate,
  { thresholdDb, ratio, attackSeconds, releaseSeconds },
) {
  const attack = Math.exp(-1 / (attackSeconds * sampleRate));
  const release = Math.exp(-1 / (releaseSeconds * sampleRate));
  const frames = samples.length / channels;
  let envelope = 0;
  for (let frame = 0; frame < frames; frame++) {
    let detector = 0;
    for (let channel = 0; channel < channels; channel++) {
      detector = Math.max(
        detector,
        Math.abs(samples[frame * channels + channel]),
      );
    }
    const coefficient = detector > envelope ? attack : release;
    envelope = coefficient * envelope + (1 - coefficient) * detector;
    const envelopeDb = 20 * Math.log10(Math.max(envelope, 1e-12));
    const gainDb = envelopeDb > thresholdDb
      ? thresholdDb + (envelopeDb - thresholdDb) / ratio - envelopeDb
      : 0;
    const gain = 10 ** (gainDb / 20);
    for (let channel = 0; channel < channels; channel++) {
      samples[frame * channels + channel] *= gain;
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
  const fadeInFrames = Math.min(frames, Math.round(fadeInSeconds * sampleRate));
  const fadeOutFrames = Math.min(frames, Math.round(fadeOutSeconds * sampleRate));
  for (let frame = 0; frame < frames; frame++) {
    const fadeIn = fadeInFrames === 0 ? 1 : Math.min(1, frame / fadeInFrames);
    const remaining = frames - 1 - frame;
    const fadeOut = fadeOutFrames === 0 ? 1 : Math.min(1, remaining / fadeOutFrames);
    const gain = Math.sin(Math.min(fadeIn, fadeOut) * Math.PI * 0.5);
    for (let channel = 0; channel < channels; channel++) {
      samples[frame * channels + channel] *= gain;
    }
  }
}

function fitLoudnessAndPeak(
  samples,
  channels,
  sampleRate,
  targetIntegratedLufs,
  peakCeiling,
) {
  const channelSamples = deinterleave(samples, channels);
  const currentLoudness = measureLoudness(channelSamples, sampleRate).integratedLufs;
  const loudnessGain = 10 ** ((targetIntegratedLufs - currentLoudness) / 20);
  for (let index = 0; index < samples.length; index++) samples[index] *= loudnessGain;
  let peak = 0;
  for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
  if (peak > peakCeiling) {
    const peakGain = peakCeiling / peak;
    for (let index = 0; index < samples.length; index++) samples[index] *= peakGain;
  }
}

function deinterleave(samples, channels) {
  const frames = samples.length / channels;
  const output = Array.from(
    { length: channels },
    () => new Float64Array(frames),
  );
  for (let frame = 0; frame < frames; frame++) {
    for (let channel = 0; channel < channels; channel++) {
      output[channel][frame] = samples[frame * channels + channel];
    }
  }
  return output;
}

function encodePcm16Wav(samples, sampleRate, channels) {
  const pcm = Buffer.alloc(samples.length * 2);
  for (let index = 0; index < samples.length; index++) {
    const value = Math.max(-1, Math.min(1, samples[index]));
    pcm.writeInt16LE(Math.round(value * 32_767), index * 2);
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

function analyzeWav(bytes, label, { includeSpectrum = true } = {}) {
  const wav = parsePcm16Wav(bytes, label);
  const samples = decodeSamples(bytes, wav);
  const channelSamples = deinterleave(samples, wav.channels);
  const frames = wav.sampleCount / wav.channels;
  let peak = 0;
  let sumSquares = 0;
  let differenceSquares = 0;
  let sum = 0;
  let clippedSamples = 0;
  for (let frame = 0; frame < frames; frame++) {
    const left = channelSamples[0][frame];
    const right = channelSamples[1][frame];
    for (const sample of [left, right]) {
      peak = Math.max(peak, Math.abs(sample));
      sumSquares += sample * sample;
      sum += sample;
      if (Math.abs(sample) >= 1) clippedSamples++;
    }
    differenceSquares += (left - right) ** 2;
  }
  const loudness = measureLoudness(channelSamples, wav.sampleRate);
  return {
    durationSeconds: frames / wav.sampleRate,
    sampleRate: wav.sampleRate,
    channels: wav.channels,
    bitsPerSample: wav.bitsPerSample,
    peak: round(peak, 4),
    rms: round(Math.sqrt(sumSquares / wav.sampleCount), 4),
    stereoDifferenceRms: round(Math.sqrt(differenceSquares / frames), 4),
    dcOffset: round(Math.abs(sum / wav.sampleCount), 6),
    clippedSamples,
    energy: {
      integratedLufs: round(loudness.integratedLufs, 2),
      maxMomentaryLufs: round(loudness.maxMomentaryLufs, 2),
      approximateTruePeakDbtp: round(
        amplitudeToDb(approximateTruePeak(channelSamples)),
        2,
      ),
      ...(includeSpectrum
        ? {
            energyAbove12kPercent: round(
              energyAboveFrequencyPercent(
                channelSamples,
                wav.sampleRate,
                12_000,
              ),
              2,
            ),
          }
        : {}),
    },
  };
}

function measureLoudness(channels, sampleRate) {
  assert(sampleRate === 48_000, "Loudness coefficients require 48 kHz audio.");
  const shelfB = [
    1.53512485958697,
    -2.69169618940638,
    1.19839281085285,
  ];
  const shelfA = [1, -1.69065929318241, 0.73248077421585];
  const highPassB = [1, -2, 1];
  const highPassA = [1, -1.99004745483398, 0.99007225036621];
  const weighted = channels.map((channel) =>
    filterArrayBiquad(
      filterArrayBiquad(channel, shelfB, shelfA),
      highPassB,
      highPassA,
    ),
  );
  const energies = blockEnergies(
    weighted,
    Math.round(0.4 * sampleRate),
    Math.round(0.1 * sampleRate),
  );
  const absolute = energies.filter((energy) => lufsFromEnergy(energy) >= -70);
  const preliminary = absolute.length
    ? lufsFromEnergy(absolute.reduce((sum, value) => sum + value, 0) / absolute.length)
    : -Infinity;
  const gated = absolute.filter(
    (energy) => lufsFromEnergy(energy) >= preliminary - 10,
  );
  return {
    integratedLufs: gated.length
      ? lufsFromEnergy(gated.reduce((sum, value) => sum + value, 0) / gated.length)
      : -Infinity,
    maxMomentaryLufs: Math.max(...energies.map(lufsFromEnergy)),
  };
}

function filterArrayBiquad(input, b, a) {
  const output = new Float64Array(input.length);
  let x1 = 0;
  let x2 = 0;
  let y1 = 0;
  let y2 = 0;
  for (let index = 0; index < input.length; index++) {
    const x0 = input[index];
    const y0 =
      b[0] * x0 + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2;
    output[index] = y0;
    x2 = x1;
    x1 = x0;
    y2 = y1;
    y1 = y0;
  }
  return output;
}

function blockEnergies(channels, frameCount, step) {
  const cumulative = channels.map((channel) => {
    const sums = new Float64Array(channel.length + 1);
    for (let index = 0; index < channel.length; index++) {
      sums[index + 1] = sums[index] + channel[index] ** 2;
    }
    return sums;
  });
  const energies = [];
  for (let start = 0; start + frameCount <= channels[0].length; start += step) {
    let energy = 0;
    for (const sums of cumulative) {
      energy += (sums[start + frameCount] - sums[start]) / frameCount;
    }
    energies.push(energy);
  }
  assert(energies.length > 0, "Audio is too short for a loudness block.");
  return energies;
}

function lufsFromEnergy(energy) {
  return energy > 0 ? -0.691 + 10 * Math.log10(energy) : -Infinity;
}

function approximateTruePeak(channels) {
  let peak = 0;
  const phases = [0.25, 0.5, 0.75];
  const halfWindow = 8;
  for (const channel of channels) {
    for (const sample of channel) peak = Math.max(peak, Math.abs(sample));
    for (let index = 0; index + 1 < channel.length; index++) {
      for (const phase of phases) {
        const position = index + phase;
        let value = 0;
        let weightSum = 0;
        for (
          let sourceIndex = index - halfWindow + 1;
          sourceIndex <= index + halfWindow;
          sourceIndex++
        ) {
          if (sourceIndex < 0 || sourceIndex >= channel.length) continue;
          const delta = position - sourceIndex;
          const absolute = Math.abs(delta);
          if (absolute >= halfWindow) continue;
          const sinc =
            absolute < 1e-12
              ? 1
              : Math.sin(Math.PI * delta) / (Math.PI * delta);
          const window =
            0.5 + 0.5 * Math.cos((Math.PI * delta) / halfWindow);
          const weight = sinc * window;
          value += channel[sourceIndex] * weight;
          weightSum += weight;
        }
        if (weightSum !== 0) value /= weightSum;
        peak = Math.max(peak, Math.abs(value));
      }
    }
  }
  return peak;
}

function energyAboveFrequencyPercent(channels, sampleRate, thresholdHz) {
  const size = 4_096;
  const hop = 8_192;
  let totalEnergy = 0;
  let highEnergy = 0;
  for (const channel of channels) {
    const starts = channel.length < size
      ? [0]
      : Array.from(
          { length: Math.floor((channel.length - size) / hop) + 1 },
          (_, index) => index * hop,
        );
    for (const start of starts) {
      const real = new Float64Array(size);
      const imaginary = new Float64Array(size);
      for (let index = 0; index < size && start + index < channel.length; index++) {
        const window =
          0.5 - 0.5 * Math.cos((2 * Math.PI * index) / (size - 1));
        real[index] = channel[start + index] * window;
      }
      fft(real, imaginary);
      for (let bin = 1; bin <= size / 2; bin++) {
        const energy = real[bin] ** 2 + imaginary[bin] ** 2;
        totalEnergy += energy;
        if ((bin * sampleRate) / size >= thresholdHz) highEnergy += energy;
      }
    }
  }
  return totalEnergy > 0 ? (100 * highEnergy) / totalEnergy : 0;
}

function fft(real, imaginary) {
  const length = real.length;
  for (let index = 1, reversed = 0; index < length; index++) {
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
      for (let index = 0; index < width / 2; index++) {
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

function amplitudeToDb(amplitude) {
  return amplitude > 0 ? 20 * Math.log10(amplitude) : -Infinity;
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function round(value, decimals) {
  return Number(value.toFixed(decimals));
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
