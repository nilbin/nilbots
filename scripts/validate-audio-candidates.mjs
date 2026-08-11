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
    packCount: 3,
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
    packCount: 4,
    cueCount: 4,
    fileCount: 16,
    minStereoDifference: 0.015,
  }),
);
validated.push(
  await validateCandidateSet({
    label: "V3 Arc Relay reconcept",
    manifestName: "manifest-v3.json",
    version: 3,
    sampleRate: 48_000,
    channels: 2,
    packCount: 3,
    cueCount: 12,
    fileCount: 36,
    minStereoDifference: 0.015,
    expectedCueIds: [
      "projectile",
      "impact",
      "destroyed",
      "arc-birth",
      "arc-pickup",
      "arc-steal",
      "arc-drop",
      "arc-bank",
      "arc-pulse",
      "arc-handoff",
      "deflect",
      "arrival",
    ],
    expectedStatuses: {
      mapped: 9,
      proposed: 3,
    },
  }),
);
validated.push(
  await validateCandidateSet({
    label: "V4 space / ambient reconcept",
    manifestName: "manifest-v4.json",
    version: 4,
    sampleRate: 48_000,
    channels: 2,
    packCount: 3,
    cueCount: 16,
    fileCount: 48,
    minStereoDifference: 0.015,
    expectedCueIds: [
      "attack-windup",
      "projectile",
      "attack-fizzle",
      "attack-cancel",
      "impact",
      "destroyed",
      "level-up",
      "arc-birth",
      "arc-pickup",
      "arc-steal",
      "arc-drop",
      "arc-bank",
      "arc-pulse",
      "arc-handoff",
      "deflect",
      "arrival",
    ],
    expectedStatuses: {
      mapped: 9,
      proposed: 7,
    },
  }),
);
validated.push(
  await validateCandidateSet({
    label: "V5 SFX Engine addition",
    manifestName: "manifest-v5.json",
    version: 5,
    sampleRate: 48_000,
    channels: 2,
    packCount: 1,
    cueCount: 16,
    fileCount: 19,
    backgroundLoopCount: 3,
    expectedBackgroundLoopIds: [
      "arena-ambience",
      "arena-ambience-ion-veil",
      "arena-ambience-signal-lattice",
    ],
    expectedDerivations: {
      destroyed: "arrival",
      "arc-drop": "arc-pickup",
      deflect: "projectile",
    },
    expectedSafetyMasteredCueIds: [
      "attack-windup",
      "attack-fizzle",
      "impact",
      "level-up",
    ],
    expectedEnergyAudit: true,
    minPeak: 0.45,
    minStereoDifference: 0,
    expectedPackIds: ["astral-relay"],
    expectedCueIds: [
      "attack-windup",
      "projectile",
      "attack-fizzle",
      "attack-cancel",
      "impact",
      "destroyed",
      "level-up",
      "arc-birth",
      "arc-pickup",
      "arc-steal",
      "arc-drop",
      "arc-bank",
      "arc-pulse",
      "arc-handoff",
      "deflect",
      "arrival",
    ],
    expectedStatuses: {
      mapped: 9,
      proposed: 7,
    },
  }),
);

for (const filename of [
  "index.html",
  "manifest.js",
  "manifest-v2.js",
  "manifest-v3.js",
  "manifest-v4.js",
  "manifest-v5.js",
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
    Array.isArray(manifest.packs) &&
      manifest.packs.length === specification.packCount,
    `${specification.label}: must contain exactly ` +
      `${specification.packCount} directions`,
  );
  if (specification.expectedPackIds) {
    assert(
      JSON.stringify(manifest.packs.map((pack) => pack.id)) ===
        JSON.stringify(specification.expectedPackIds),
      `${specification.label}: pack IDs do not match`,
    );
  }

  const referenceIds = manifest.packs[0].cues.map((cue) => cue.id);
  assert(
    referenceIds.length === specification.cueCount,
    `${specification.label}: each direction must contain ` +
      `${specification.cueCount} review cues`,
  );
  if (specification.expectedCueIds) {
    assert(
      JSON.stringify(referenceIds) ===
        JSON.stringify(specification.expectedCueIds),
      `${specification.label}: cue IDs do not match the reviewed runtime vocabulary`,
    );
  }
  if (specification.expectedStatuses) {
    for (const [status, count] of Object.entries(
      specification.expectedStatuses,
    )) {
      assert(
        manifest.packs[0].cues.filter((cue) => cue.status === status).length ===
          count,
        `${specification.label}: expected ${count} ${status} cues`,
      );
    }
  }
  if (specification.expectedDerivations) {
    const expectedEntries = Object.entries(specification.expectedDerivations);
    const derivedCues = manifest.packs[0].cues.filter(
      (cue) => cue.origin === "local-derivation",
    );
    assert(
      manifest.sourceComposition?.locallyDerivedCues === expectedEntries.length &&
        manifest.sourceComposition?.providerGeneratedCues ===
          specification.cueCount - expectedEntries.length,
      `${specification.label}: source composition is incorrect`,
    );
    assert(
      JSON.stringify(derivedCues.map((cue) => cue.id)) ===
        JSON.stringify(expectedEntries.map(([id]) => id)),
      `${specification.label}: locally derived cue IDs are incorrect`,
    );
    for (const [id, sourceId] of expectedEntries) {
      const cue = derivedCues.find((candidate) => candidate.id === id);
      assert(
        cue?.derivedFromCueId === sourceId,
        `${id}: expected local derivation from ${sourceId}`,
      );
    }
  }
  if (specification.expectedSafetyMasteredCueIds) {
    const safetyCues = manifest.packs[0].cues.filter(
      (cue) => cue.safetyMastered,
    );
    assert(
      manifest.sourceComposition?.locallySafetyMasteredCues ===
        specification.expectedSafetyMasteredCueIds.length,
      `${specification.label}: safety-mastered source composition is incorrect`,
    );
    assert(
      JSON.stringify(safetyCues.map((cue) => cue.id)) ===
        JSON.stringify(specification.expectedSafetyMasteredCueIds),
      `${specification.label}: safety-mastered cue IDs are incorrect`,
    );
    for (const cue of safetyCues) {
      const maximumHighEnergy =
        manifest.packs[0].energySafety?.thresholds
          ?.maximumEnergyAbove12kPercent?.[cue.id];
      assert(
        cue.energy?.approximateTruePeakDbtp <= -3,
        `${cue.id}: safety master exceeds -3 dBTP`,
      );
      assert(
        Number.isFinite(maximumHighEnergy) &&
          cue.energy?.energyAbove12kPercent <= maximumHighEnergy,
        `${cue.id}: safety master exceeds its upper-band energy threshold`,
      );
    }
  }
  if (specification.expectedEnergyAudit) {
    const audit = manifest.energyAudit;
    assert(
      audit?.script === "scripts/master-audio-v5-energy.mjs" &&
        audit.reviewOutputSafetyGainDb === -3 &&
        audit.reviewSequenceMaximumApproximateTruePeakDbtp === -1,
      `${specification.label}: energy audit contract is missing`,
    );
    const backgroundLoopIds = (manifest.backgroundLoops ?? []).map(
      (loop) => loop.id,
    );
    assert(
      JSON.stringify(audit.ambientLoopIds) ===
        JSON.stringify(backgroundLoopIds),
      `${specification.label}: energy audit ambience coverage is incorrect`,
    );
    assert(
      Number.isFinite(audit.maximumAmbientReviewGain) &&
        audit.maximumAmbientReviewGain > 0 &&
        audit.maximumAmbientReviewGain <= 1,
      `${specification.label}: ambience review-gain ceiling is invalid`,
    );
    for (const loop of manifest.backgroundLoops ?? []) {
      const relativeGain = audit.ambientRelativeGains?.[loop.id];
      assert(
        Number.isFinite(relativeGain) &&
          relativeGain > 0 &&
          relativeGain <= audit.maximumAmbientReviewGain &&
          loop.reviewRelativeGain === relativeGain,
        `${loop.id}: review gain differs from the energy audit`,
      );
    }
    assert(
      JSON.stringify(Object.keys(audit.sequenceResults ?? {})) ===
        JSON.stringify(["possession", "combat", "full"]),
      `${specification.label}: exact review-sequence coverage is incorrect`,
    );
    for (const result of Object.values(audit.sequenceResults)) {
      assert(
        result.approximateTruePeakDbtp <=
          audit.reviewSequenceMaximumApproximateTruePeakDbtp,
        `${specification.label}: a review sequence exceeds its true-peak ceiling`,
      );
      assert(
        JSON.stringify(Object.keys(result.ambientVariants ?? {})) ===
          JSON.stringify(backgroundLoopIds),
        `${specification.label}: a review sequence omits an ambience alternative`,
      );
      for (const [ambientLoopId, variant] of Object.entries(
        result.ambientVariants,
      )) {
        assert(
          variant.approximateTruePeakDbtp <=
            audit.reviewSequenceMaximumApproximateTruePeakDbtp,
          `${specification.label}: an ambience variant exceeds its sequence ceiling`,
        );
        assert(
          variant.relativeGain ===
            audit.ambientRelativeGains[ambientLoopId],
          `${specification.label}: a sequence uses an unaudited ambience gain`,
        );
      }
    }
    for (const cue of manifest.packs[0].cues) {
      assert(
        cue.energy?.approximateTruePeakDbtp ===
            audit.cueResults?.[cue.id]?.approximateTruePeakDbtp &&
          cue.energy.approximateTruePeakDbtp <=
            audit.individualMaximumApproximateTruePeakDbtp,
        `${cue.id}: cue energy does not match the all-cue audit`,
      );
    }
    for (const loop of manifest.backgroundLoops ?? []) {
      assert(
        loop.energy?.approximateTruePeakDbtp ===
            audit.cueResults?.[loop.id]?.approximateTruePeakDbtp &&
          loop.energy.approximateTruePeakDbtp <=
            audit.individualMaximumApproximateTruePeakDbtp,
        `${loop.id}: ambience energy does not match the all-cue audit`,
      );
    }
  }
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
      await validateCueFile(cue, manifest, specification);
      setFileCount++;
    }
  }

  if (specification.backgroundLoopCount !== undefined) {
    assert(
      Array.isArray(manifest.backgroundLoops) &&
        manifest.backgroundLoops.length === specification.backgroundLoopCount,
      `${specification.label}: expected ${specification.backgroundLoopCount} ` +
        `background loop(s)`,
    );
    if (specification.expectedBackgroundLoopIds) {
      assert(
        JSON.stringify(manifest.backgroundLoops.map((loop) => loop.id)) ===
          JSON.stringify(specification.expectedBackgroundLoopIds),
        `${specification.label}: background loop IDs are incorrect`,
      );
    }
    for (const loop of manifest.backgroundLoops) {
      assert(loop.loop === true, `${loop.id}: background ambience must loop`);
      assert(
        loop.loopCrossfadeSeconds === 2,
        `${loop.id}: expected a two-second circular crossfade`,
      );
      assert(
        Math.abs(loop.durationSeconds - 30) < 0.002,
        `${loop.id}: expected a 30-second review loop`,
      );
      const wav = await validateCueFile(loop, manifest, {
        ...specification,
        minPeak: 0.3,
        maxPeak: 0.65,
        minRms: 0.005,
        maxRms: 0.3,
        minStereoDifference: 0,
      });
      assert(
        wav.boundaryDelta <= Math.max(0.025, wav.derivativeRms * 4),
        `${loop.id}: loop boundary delta ${wav.boundaryDelta} risks an audible click`,
      );
      setFileCount++;
    }
  }

  assert(
    setFileCount === specification.fileCount,
    `${specification.label}: expected ${specification.fileCount} unique WAV files, ` +
      `found ${setFileCount}`,
  );
  return { label: specification.label, fileCount: setFileCount };
}

async function validateCueFile(cue, manifest, specification) {
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
    wav.peak >= (specification.minPeak ?? 0.7) &&
      wav.peak <= (specification.maxPeak ?? 0.9),
    `${cue.file}: peak ${wav.peak} is out of range`,
  );
  assert(
    wav.rms >= (specification.minRms ?? 0.018),
    `${cue.file}: cue is effectively silent`,
  );
  assert(
    wav.rms <= (specification.maxRms ?? 0.42),
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
  return wav;
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
  let derivativeSquares = 0;
  let clippedSamples = 0;
  let firstFrameSamples;
  let previousFrameSamples;
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
    if (frame === 0) firstFrameSamples = frameSamples;
    else {
      for (let channel = 0; channel < channels; channel++) {
        derivativeSquares +=
          (frameSamples[channel] - previousFrameSamples[channel]) ** 2;
      }
    }
    previousFrameSamples = frameSamples;
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
    boundaryDelta: Math.max(
      ...firstFrameSamples.map((sample, channel) =>
        Math.abs(sample - previousFrameSamples[channel]),
      ),
    ),
    derivativeRms: Math.sqrt(
      derivativeSquares / ((frameCount - 1) * channels),
    ),
    clippedSamples,
  };
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
