#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const sourceRoot = path.join(repositoryRoot, "art", "audio", "sound-lab");
const destinationRoot = path.join(
  repositoryRoot,
  "web",
  "src",
  "assets",
  "audio",
  "candidates",
);
const sourceManifest = JSON.parse(
  await readFile(path.join(sourceRoot, "manifest-v2.json"), "utf8"),
);
const cueNames = new Map([
  ["projectile-showcase", "projectile"],
  ["armor-impact", "impact"],
  ["bot-destroyed", "destroyed"],
  ["entitlement-unlock", "unlock"],
]);

if (process.platform !== "darwin") {
  throw new Error(
    "Candidate AAC export currently requires macOS afconvert. " +
      "The checked-in assets remain buildable on every platform.",
  );
}
if (!Array.isArray(sourceManifest.packs) || sourceManifest.packs.length !== 4) {
  throw new Error("Expected four V2 source packs.");
}

let totalBytes = 0;
let fileCount = 0;
for (const pack of sourceManifest.packs) {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(pack.id)) {
    throw new Error(`Unsafe pack ID: ${pack.id}`);
  }
  const directory = path.join(destinationRoot, pack.id);
  await mkdir(directory, { recursive: true });
  const cues = {};
  for (const sourceCue of pack.cues) {
    const runtimeCue = cueNames.get(sourceCue.id);
    if (!runtimeCue) throw new Error(`Unexpected source cue: ${sourceCue.id}`);
    const sourceFile = path.resolve(
      sourceRoot,
      sourceCue.file.replace(/^\.\//, ""),
    );
    if (!sourceFile.startsWith(`${sourceRoot}${path.sep}`)) {
      throw new Error(`Source cue escapes the audio root: ${sourceCue.file}`);
    }
    const filename = `${runtimeCue}.m4a`;
    const destination = path.join(directory, filename);
    execFileSync(
      "afconvert",
      [
        sourceFile,
        "-o",
        destination,
        "-f",
        "m4af",
        "-d",
        "aac",
        "-b",
        "160000",
        "-q",
        "127",
        "--no-filler",
      ],
      { stdio: "inherit" },
    );
    totalBytes += (await stat(destination)).size;
    fileCount++;
    cues[runtimeCue] = filename;
  }
  await writeFile(
    path.join(directory, "manifest.json"),
    `${JSON.stringify(
      {
        version: 1,
        id: pack.id,
        number: pack.number,
        label: pack.label,
        kicker: pack.kicker,
        reviewOnly: true,
        format: "aac-lc-m4a",
        sampleRate: 48_000,
        channels: 2,
        cues,
      },
      null,
      2,
    )}\n`,
  );
}

console.log(
  `Exported ${fileCount} runtime review cues ` +
    `(${(totalBytes / 1_048_576).toFixed(2)} MiB) to ` +
    path.relative(repositoryRoot, destinationRoot),
);
