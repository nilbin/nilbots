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
const expectedIds = baseManifest.packs[0].cues.map((cue) => cue.id);
const actualIds = generation.cues.map((cue) => cue.id);
assert(
  JSON.stringify(actualIds) === JSON.stringify(expectedIds),
  "SFX Engine cue order must exactly match the V4 comparison vocabulary.",
);
assert(generation.provider === "sfx-engine", "Unexpected audio provider.");
assert(
  generation.apiSpecSha256 === sha256(await readFile(apiSpecPath)),
  "The saved OpenAPI document differs from the generation pin.",
);

const outputRoot = path.resolve(path.dirname(generationPath), generation.outputDirectory);
assert(
  outputRoot.startsWith(`${path.dirname(generationPath)}${path.sep}`),
  "The SFX Engine output directory escapes V5.",
);

const cues = [];
for (const generatedCue of generation.cues) {
  const baseline = baseManifest.packs[0].cues.find(
    (cue) => cue.id === generatedCue.id,
  );
  const result = generatedCue.result;
  assert(result?.status === "generated", `${generatedCue.id} is not generated.`);
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
    `${generatedCue.id} WAV hash differs from its provider provenance.`,
  );
  assert(
    wav.length === result.outputBytes,
    `${generatedCue.id} WAV size differs from its provider provenance.`,
  );
  cues.push({
    id: baseline.id,
    label: baseline.label,
    category: baseline.category,
    status: baseline.status,
    description: baseline.description,
    mapping: baseline.mapping,
    file: `./v5/${generation.outputDirectory}/${generatedCue.file}`,
    durationSeconds: result.metrics.durationSeconds,
    peak: result.metrics.peak,
    rms: result.metrics.rms,
    stereoDifferenceRms: result.metrics.stereoDifferenceRms,
    dcOffset: result.metrics.dcOffset,
  });
}

const manifest = {
  version: 5,
  generation: "arc-relay-space-ambient-sfx-engine-addition",
  generatedBy: [
    ".claude/skills/sfx-engine/scripts/generate-pack.mjs",
    "scripts/build-audio-v5-candidates.mjs",
  ],
  additiveTo: "manifest-v4.json",
  sampleRate: 48_000,
  channels: 2,
  format: "pcm-s16le-wav",
  reviewScope: baseManifest.reviewScope,
  packs: [
    {
      ...generation.pack,
      provider: generation.providerDisplayName,
      provenance: "./v5/sfx-engine-generation.json",
      apiSpecSha256: generation.apiSpecSha256,
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
  `Built V5 SFX Engine addition with ${cues.length} cues in ${labRoot}.`,
);

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
