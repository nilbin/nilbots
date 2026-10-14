const fs = require("node:fs");
const path = require("node:path");
const sharp = require("sharp");
const { GifEncoder } = require("@napi-rs/canvas");

const frameDirectory = process.argv[2];
const output = process.argv[3];
if (!frameDirectory || !output) {
  throw new Error("usage: node encode_tower_deploy_gif.cjs <frames> <output>");
}

const width = 320;
const height = 320;
const frameFiles = fs.readdirSync(frameDirectory)
  .filter((name) => /^f\d{3}\.png$/.test(name))
  .sort()
  .map((name) => path.join(frameDirectory, name));
if (frameFiles.length < 2) {
  throw new Error("expected at least two rendered frames");
}

async function rgba(filename) {
  const { data, info } = await sharp(filename)
    .resize(width, height)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width !== width || info.height !== height || info.channels !== 4) {
    throw new Error(`unexpected frame shape for ${filename}`);
  }
  return data;
}

async function main() {
  const forward = [];
  for (const filename of frameFiles) forward.push(await rgba(filename));
  const encoder = new GifEncoder(width, height, { repeat: 0, quality: 8 });
  for (const frame of forward) encoder.addFrame(frame, width, height, { delay: 50 });
  encoder.addFrame(forward.at(-1), width, height, { delay: 600 });
  for (const frame of [...forward].reverse()) {
    encoder.addFrame(frame, width, height, { delay: 50 });
  }
  encoder.addFrame(forward[0], width, height, { delay: 500 });
  fs.writeFileSync(output, encoder.finish());
}

main();
