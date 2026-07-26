#!/usr/bin/env node

import { access, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const labRoot = path.join(repoRoot, "art", "audio", "sound-lab");
const files = new Set();

const validated = [];
validated.push(
  await validateCandidateSet({
    label: "V1 archive",
    manifestName: "manifest.json",
    version: 1,
    sampleRate: 44_100,
    channels: 1,
    cueCount: 10,
    fileCount: 30,
    minStereoDifference: 0,
  }),
);
validated.push(
  await validateCandidateSet({
    label: "V2 vertical slice",
    manifestName: "manifest-v2.json",
    version: 2,
    sampleRate: 48_000,
    channels: 2,
    cueCount: 4,
    fileCount: 12,
    minStereoDifference: 0.015,
  }),
);

for (const filename of [
  "index.html",
  "manifest.js",
  "manifest-v2.js",
  "soundboard.css",
  "soundboard.js",
]) {
  await access(path.join(labRoot, filename));
}

console.log(
  `Validated ${validated.map((set) => set.label).join(" and ")}: ` +
    `${validated.reduce((total, set) => total + set.fileCount, 0)} WAV files total.`,
);

async function validateCandidateSet(specification) {
  const manifestPath = path.join(labRoot, specification.manifestName);
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  assert(
    manifest.version === specification.version,
    `${specification.label}: manifest version must be ${specification.version}`,
  );
  assert(
    manifest.sampleRate === specification.sampleRate,
    `${specification.label}: sample rate must be ${specification.sampleRate}`,
  );
  assert(
    manifest.channels === specification.channels,
    `${specification.label}: channel count must be ${specification.channels}`,
  );
  assert(
    Array.isArray(manifest.packs) && manifest.packs.length === 3,
    `${specification.label}: must contain exactly three directions`,
  );

  const referenceIds = manifest.packs[0].cues.map((cue) => cue.id);
  assert(
    referenceIds.length === specification.cueCount,
    `${specification.label}: each direction must contain ` +
      `${specification.cueCount} review cues`,
  );
  let setFileCount = 0;

  for (const pack of manifest.packs) {
    assert(
      /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(pack.id),
      `${pack.id}: invalid pack ID`,
    );
    assert(
      /^#[0-9a-f]{6}$/i.test(pack.accent),
      `${pack.id}: invalid accent`,
    );
    assert(
      JSON.stringify(pack.cues.map((cue) => cue.id)) ===
        JSON.stringify(referenceIds),
      `${pack.id}: cue order or coverage differs from the reference pack`,
    );

    for (const cue of pack.cues) {
      const relative = cue.file.replace(/^\.\//, "");
      assert(!relative.includes(".."), `${cue.file}: unsafe path`);
      const absolute = path.resolve(labRoot, relative);
      assert(
        absolute.startsWith(`${labRoot}${path.sep}`),
        `${cue.file}: escapes sound lab`,
      );
      await access(absolute);
      assert(!files.has(absolute), `${cue.file}: duplicate audio file`);
      files.add(absolute);
      setFileCount++;

      const bytes = await readFile(absolute);
      const wav = parseWav(bytes, cue.file);
      assert(
        wav.sampleRate === manifest.sampleRate,
        `${cue.file}: wrong sample rate`,
      );
      assert(
        wav.channels === manifest.channels,
        `${cue.file}: wrong channel count`,
      );
      assert(
        wav.bitsPerSample === 16,
        `${cue.file}: must be signed 16-bit PCM`,
      );
      assert(
        Math.abs(wav.durationSeconds - cue.durationSeconds) < 0.002,
        `${cue.file}: manifest duration differs from WAV`,
      );
      assert(
        wav.peak >= 0.7 && wav.peak <= 0.9,
        `${cue.file}: peak ${wav.peak} is out of range`,
      );
      assert(
        wav.rms >= 0.018,
        `${cue.file}: cue is effectively silent`,
      );
      assert(
        wav.rms <= 0.42,
        `${cue.file}: cue is excessively dense`,
      );
      assert(
        wav.clippedSamples === 0,
        `${cue.file}: contains clipped samples`,
      );
      assert(
        wav.dcOffset <= 0.003,
        `${cue.file}: DC offset ${wav.dcOffset} is excessive`,
      );
      assert(
        wav.stereoDifferenceRms >= specification.minStereoDifference,
        `${cue.file}: stereo field is effectively mono`,
      );
      if (cue.stereoDifferenceRms !== undefined) {
        assert(
          Math.abs(wav.stereoDifferenceRms - cue.stereoDifferenceRms) < 0.003,
          `${cue.file}: rendered stereo measurement differs from manifest`,
        );
      }
    }
  }

  assert(
    setFileCount === specification.fileCount,
    `${specification.label}: expected ${specification.fileCount} unique WAV files, ` +
      `found ${setFileCount}`,
  );
  return { label: specification.label, fileCount: setFileCount };
}

function parseWav(bytes, filename) {
  assert(
    bytes.toString("ascii", 0, 4) === "RIFF",
    `${filename}: missing RIFF header`,
  );
  assert(
    bytes.toString("ascii", 8, 12) === "WAVE",
    `${filename}: missing WAVE header`,
  );
  assert(
    bytes.toString("ascii", 12, 16) === "fmt ",
    `${filename}: missing fmt chunk`,
  );
  assert(bytes.readUInt16LE(20) === 1, `${filename}: must use PCM encoding`);
  assert(
    bytes.toString("ascii", 36, 40) === "data",
    `${filename}: missing data chunk`,
  );
  const channels = bytes.readUInt16LE(22);
  const sampleRate = bytes.readUInt32LE(24);
  const bitsPerSample = bytes.readUInt16LE(34);
  const dataBytes = bytes.readUInt32LE(40);
  assert(
    dataBytes === bytes.length - 44,
    `${filename}: invalid data chunk length`,
  );
  const bytesPerSample = bitsPerSample / 8;
  const sampleCount = dataBytes / bytesPerSample;
  const frameCount = sampleCount / channels;
  const channelSums = Array.from({ length: channels }, () => 0);
  let peak = 0;
  let sumSquares = 0;
  let differenceSquares = 0;
  let clippedSamples = 0;
  for (let frame = 0; frame < frameCount; frame++) {
    const frameSamples = [];
    for (let channel = 0; channel < channels; channel++) {
      const offset = 44 + (frame * channels + channel) * bytesPerSample;
      const integer = bytes.readInt16LE(offset);
      const sample = integer / 32_767;
      frameSamples.push(sample);
      channelSums[channel] += sample;
      peak = Math.max(peak, Math.abs(sample));
      sumSquares += sample * sample;
      if (integer === -32_768 || integer === 32_767) clippedSamples++;
    }
    if (channels === 2) {
      differenceSquares += (frameSamples[0] - frameSamples[1]) ** 2;
    }
  }
  return {
    channels,
    sampleRate,
    bitsPerSample,
    durationSeconds: frameCount / sampleRate,
    peak,
    rms: Math.sqrt(sumSquares / sampleCount),
    stereoDifferenceRms:
      channels === 2 ? Math.sqrt(differenceSquares / frameCount) : 0,
    dcOffset: Math.max(
      ...channelSums.map((sum) => Math.abs(sum / frameCount)),
    ),
    clippedSamples,
  };
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
