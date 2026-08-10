#!/usr/bin/env node

// Local-only by design: this workflow has no network or credential dependency.

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
  "sfx-engine-generation.json",
);
const generationDirectory = path.dirname(generationPath);
const generation = JSON.parse(await readFile(generationPath, "utf8"));
const outputDirectory = path.resolve(
  generationDirectory,
  generation.outputDirectory,
);
const scriptPath = "scripts/derive-audio-v5-revisions.mjs";

const revisions = [
  {
    targetId: "destroyed",
    sourceId: "arrival",
    targetPeak: 0.82,
    fallbackRequestLengthSeconds: 2,
    reason:
      "Owner review: derive bot destruction locally as the audible inverse " +
      "of the accepted unit arrival; no new provider request.",
    processing: {
      transform: "reverse-source",
      fadeInSeconds: 0.015,
      fadeOutSeconds: 0.32,
    },
  },
  {
    targetId: "arc-drop",
    sourceId: "arc-pickup",
    targetPeak: 0.78,
    fallbackRequestLengthSeconds: 1,
    reason:
      "Owner review: derive Core drop locally as the audible inverse of " +
      "the current Core pickup; no new provider request.",
    processing: {
      transform: "reverse-source",
      fadeInSeconds: 0.01,
      fadeOutSeconds: 0.18,
    },
  },
  {
    targetId: "deflect",
    sourceId: "projectile",
    targetPeak: 0.7,
    fallbackRequestLengthSeconds: 1,
    reason:
      "Owner review: derive beam deflection locally from the accepted beam " +
      "body with its ignition removed and highs softened; no new provider request.",
    processing: {
      transform: "beam-body-bend",
      sourceStartSeconds: 0.08,
      durationSeconds: 0.8,
      lowPassHz: 3_200,
      fadeInSeconds: 0.015,
      fadeOutSeconds: 0.18,
    },
  },
];

for (const revision of revisions) {
  const target = cueById(revision.targetId);
  const source = cueById(revision.sourceId);
  assert(
    source.result?.status === "generated",
    `${source.id}: derivation source must be an original generated cue.`,
  );
  const sourcePath = safeOutputPath(source.file);
  const sourceBytes = await readFile(sourcePath);
  assert(
    sha256(sourceBytes) === source.result.outputSha256,
    `${source.id}: source WAV differs from its saved provenance.`,
  );
  const sourceWav = parsePcm16Wav(sourceBytes, source.id);
  assert(
    sourceWav.sampleRate === 48_000 && sourceWav.channels === 2,
    `${source.id}: expected a 48 kHz stereo source.`,
  );

  let samples = decodeSamples(sourceBytes, sourceWav);
  if (revision.processing.transform === "reverse-source") {
    samples = reverseFrames(samples, sourceWav.channels);
  } else if (revision.processing.transform === "beam-body-bend") {
    samples = sliceFrames(
      samples,
      sourceWav.channels,
      Math.round(revision.processing.sourceStartSeconds * sourceWav.sampleRate),
      Math.round(revision.processing.durationSeconds * sourceWav.sampleRate),
    );
    lowPassZeroPhase(
      samples,
      sourceWav.channels,
      sourceWav.sampleRate,
      revision.processing.lowPassHz,
    );
    bendRight(samples, sourceWav.channels);
  } else {
    throw new Error(`Unknown transform: ${revision.processing.transform}`);
  }

  const frames = samples.length / sourceWav.channels;
  removeDc(samples, sourceWav.channels);
  applyEndpointFades(
    samples,
    sourceWav.channels,
    sourceWav.sampleRate,
    revision.processing.fadeInSeconds,
    revision.processing.fadeOutSeconds,
  );
  removeDc(samples, sourceWav.channels);
  normalizePeak(samples, revision.targetPeak);
  const outputBytes = encodePcm16Wav(
    samples,
    sourceWav.sampleRate,
    sourceWav.channels,
  );
  const metrics = analyzeWav(outputBytes, revision.targetId);

  if (target.result?.status !== "derived") {
    target.attempts ??= [];
    target.attempts.push({
      ...(target.result?.request ?? snapshotRequest(target)),
      result: target.result,
      supersededAt: new Date().toISOString(),
      reason: revision.reason,
    });
  }

  const derivation = {
    derivedBy: scriptPath,
    sourceCueId: source.id,
    sourceOutputSha256: source.result.outputSha256,
    operations: operationsFor(revision),
  };
  const recordedAt = target.result?.status === "derived"
    ? target.result.recordedAt
    : new Date().toISOString();
  target.generationMode = "local-derivation";
  target.requestLengthSeconds = revision.fallbackRequestLengthSeconds;
  target.targetPeak = revision.targetPeak;
  target.revisionReason = revision.reason;
  target.derivation = derivation;
  target.result = {
    status: "derived",
    recordedAt,
    derivation,
    outputSha256: sha256(outputBytes),
    outputBytes: outputBytes.length,
    processing: {
      endpointTreatment: "short-fades",
      fadeInSeconds: revision.processing.fadeInSeconds,
      fadeOutSeconds: revision.processing.fadeOutSeconds,
    },
    metrics,
  };

  const outputPath = safeOutputPath(target.file);
  const temporaryOutputPath = `${outputPath}.tmp`;
  await writeFile(temporaryOutputPath, outputBytes);
  await rename(temporaryOutputPath, outputPath);
  console.log(
    `Derived ${target.id} from ${source.id}: ` +
      `${(frames / sourceWav.sampleRate).toFixed(2)}s, ` +
      `${metrics.sampleRate} Hz stereo WAV.`,
  );
}

generation.localDerivation = {
  script: scriptPath,
  policy:
    "Deterministic transformations of hash-pinned SFX Engine source cues; " +
    "no provider API request or synthesized layer.",
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

function snapshotRequest(cue) {
  return {
    prompt: cue.prompt,
    requestLengthSeconds: cue.requestLengthSeconds,
    secondsToStart: cue.secondsToStart ?? 0,
    targetPeak: cue.targetPeak ?? 0.82,
  };
}

function operationsFor(revision) {
  const operations = [];
  if (revision.processing.transform === "reverse-source") {
    operations.push({ type: "reverse-frames" });
  } else {
    operations.push({
      type: "extract-beam-body",
      startSeconds: revision.processing.sourceStartSeconds,
      durationSeconds: revision.processing.durationSeconds,
    });
    operations.push({
      type: "zero-phase-low-pass",
      cutoffHz: revision.processing.lowPassHz,
    });
    operations.push({
      type: "stereo-bend",
      direction: "center-to-right",
      maximumPan: 0.23,
    });
  }
  operations.push({ type: "remove-dc" });
  operations.push({
    type: "endpoint-fades",
    fadeInSeconds: revision.processing.fadeInSeconds,
    fadeOutSeconds: revision.processing.fadeOutSeconds,
  });
  operations.push({ type: "peak-normalize", targetPeak: revision.targetPeak });
  return operations;
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
  assert(dataOffset + dataLength <= bytes.length, `${label}: invalid WAV data length.`);
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

function reverseFrames(samples, channels) {
  const frames = samples.length / channels;
  const output = new Float64Array(samples.length);
  for (let frame = 0; frame < frames; frame++) {
    for (let channel = 0; channel < channels; channel++) {
      output[frame * channels + channel] =
        samples[(frames - 1 - frame) * channels + channel];
    }
  }
  return output;
}

function sliceFrames(samples, channels, startFrame, frameCount) {
  const sourceFrames = samples.length / channels;
  assert(
    startFrame >= 0 && startFrame + frameCount <= sourceFrames,
    "Requested slice exceeds source audio.",
  );
  return samples.slice(
    startFrame * channels,
    (startFrame + frameCount) * channels,
  );
}

function lowPassZeroPhase(samples, channels, sampleRate, cutoffHz) {
  const frames = samples.length / channels;
  const alpha = 1 - Math.exp((-2 * Math.PI * cutoffHz) / sampleRate);
  for (let channel = 0; channel < channels; channel++) {
    let state = samples[channel];
    for (let frame = 1; frame < frames; frame++) {
      const index = frame * channels + channel;
      state += alpha * (samples[index] - state);
      samples[index] = state;
    }
    state = samples[(frames - 1) * channels + channel];
    for (let frame = frames - 2; frame >= 0; frame--) {
      const index = frame * channels + channel;
      state += alpha * (samples[index] - state);
      samples[index] = state;
    }
  }
}

function bendRight(samples, channels) {
  assert(channels === 2, "Stereo bend requires two channels.");
  const frames = samples.length / channels;
  for (let frame = 0; frame < frames; frame++) {
    const progress = frames === 1 ? 1 : frame / (frames - 1);
    const left = samples[frame * 2];
    const right = samples[frame * 2 + 1];
    const mid = (left + right) * 0.5;
    const side = (left - right) * 0.25;
    const angle = (0.5 + progress * 0.23) * Math.PI * 0.5;
    samples[frame * 2] = (mid + side) * Math.cos(angle) * Math.SQRT2;
    samples[frame * 2 + 1] = (mid - side) * Math.sin(angle) * Math.SQRT2;
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

function normalizePeak(samples, targetPeak) {
  let peak = 0;
  for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
  assert(peak > 0.0001, "Derived audio is effectively silent.");
  const gain = targetPeak / peak;
  for (let index = 0; index < samples.length; index++) samples[index] *= gain;
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

function analyzeWav(bytes, label) {
  const wav = parsePcm16Wav(bytes, label);
  const channels = wav.channels;
  const frames = wav.sampleCount / channels;
  let peak = 0;
  let sumSquares = 0;
  let differenceSquares = 0;
  let sum = 0;
  let clippedSamples = 0;
  for (let frame = 0; frame < frames; frame++) {
    const left = bytes.readInt16LE(wav.dataOffset + frame * channels * 2) / 32_767;
    const right = bytes.readInt16LE(wav.dataOffset + (frame * channels + 1) * 2) / 32_767;
    for (const sample of [left, right]) {
      peak = Math.max(peak, Math.abs(sample));
      sumSquares += sample * sample;
      sum += sample;
      if (Math.abs(sample) >= 1) clippedSamples++;
    }
    differenceSquares += (left - right) ** 2;
  }
  return {
    durationSeconds: frames / wav.sampleRate,
    sampleRate: wav.sampleRate,
    channels,
    bitsPerSample: wav.bitsPerSample,
    peak: round(peak, 4),
    rms: round(Math.sqrt(sumSquares / wav.sampleCount), 4),
    stereoDifferenceRms: round(Math.sqrt(differenceSquares / frames), 4),
    dcOffset: round(Math.abs(sum / wav.sampleCount), 6),
    clippedSamples,
  };
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
