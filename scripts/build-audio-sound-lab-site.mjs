#!/usr/bin/env node

import {
  cpSync,
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "..");
const sourceDirectory = resolve(repositoryRoot, "art/audio/sound-lab");
const destinationDirectory = resolve(
  process.argv[2] ?? resolve(repositoryRoot, "sandbox/audio-sound-lab-site"),
);
const hostingSource = resolve(repositoryRoot, ".openai/hosting.json");

function assertSafeDestination() {
  if (
    destinationDirectory === repositoryRoot ||
    destinationDirectory === sourceDirectory
  ) {
    throw new Error("Refusing to replace the repository or sound-lab source.");
  }

  const relativeDestination = relative(repositoryRoot, destinationDirectory);
  if (
    relativeDestination === "" ||
    relativeDestination === ".." ||
    relativeDestination.startsWith(`..${sep}`)
  ) {
    throw new Error("The site output must stay inside the repository.");
  }
}

assertSafeDestination();

if (!existsSync(sourceDirectory)) {
  throw new Error(`Missing sound-lab source: ${sourceDirectory}`);
}

const hosting = JSON.parse(readFileSync(hostingSource, "utf8"));
if (typeof hosting.project_id !== "string" || hosting.project_id.length === 0) {
  throw new Error("The repository hosting config is missing project_id.");
}

rmSync(destinationDirectory, { recursive: true, force: true });
mkdirSync(resolve(destinationDirectory, ".open-next/assets"), {
  recursive: true,
});
mkdirSync(resolve(destinationDirectory, ".openai"), { recursive: true });

cpSync(sourceDirectory, resolve(destinationDirectory, ".open-next/assets"), {
  recursive: true,
});
cpSync(hostingSource, resolve(destinationDirectory, ".openai/hosting.json"));

writeFileSync(
  resolve(destinationDirectory, ".open-next/worker.js"),
  `export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === "/") {
      url.pathname = "/index.html";
      return env.ASSETS.fetch(new Request(url, request));
    }

    return env.ASSETS.fetch(request);
  },
};
`,
);

writeFileSync(
  resolve(destinationDirectory, "README.md"),
  `# nilbots arena sound lab

Generated from \`art/audio/sound-lab\` by
\`scripts/build-audio-sound-lab-site.mjs\`.

The deployable worker serves the static review lab from \`.open-next/assets\`.
Edit the repository source and regenerate instead of editing this package.
`,
);

console.log(
  `Built audio sound-lab site at ${relative(repositoryRoot, destinationDirectory)}`,
);
