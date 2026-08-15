#!/usr/bin/env node

/** Publish one phone-friendly owner gallery plus the real replay viewer. */

import {
  cpSync,
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const sandbox = path.join(repository, 'sandbox/arc-relay-veterancy-split-diadem-v1');
const liveSite = path.join(sandbox, 'site');
const gallery = path.join(sandbox, 'gallery');
const viewer = path.join(gallery, 'viewer');
const captures = path.join(gallery, 'captures');
const evidence = path.join(
  repository,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review/evidence.json',
);
const files = [
  {
    id: 'concept',
    source:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/correction-v5/motion-safe-split-diadem-rank-1-5-clean.png',
    output: 'concept-rank-1-5.png',
  },
  {
    id: 'meshy-inputs',
    source:
      'art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/inputs/veterancy-split-diadem-whole-v1/input-review.png',
    output: 'meshy-rank-1-5-inputs.png',
  },
  {
    id: 'tier-comparison',
    source:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/gate-c/veterancy-split-diadem-whole-v1/rank-1-5-rich-vs-lean.png',
    output: 'rank-1-5-rich-vs-lean.png',
  },
  {
    id: 'rank-five',
    source:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/gate-c/veterancy-split-diadem-whole-v1/rank-5-lean-six-view.png',
    output: 'rank-5-six-view.png',
  },
  {
    id: 'desktop-gameplay',
    source:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review/desktop-gameplay-scale.webp',
    output: 'desktop-gameplay.webp',
  },
  {
    id: 'phone-gameplay',
    source:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review/phone-landscape-gameplay-scale.webp',
    output: 'phone-gameplay.webp',
  },
];

if (!existsSync(path.join(liveSite, 'index.html')))
  throw new Error('Build the veterancy replay site before publishing its gallery.');
if (!existsSync(evidence))
  throw new Error('Capture the complete desktop and phone gameplay review first.');
const captureRecord = JSON.parse(readFileSync(evidence, 'utf8'));
if (
  captureRecord.captures?.length !== 10 ||
  captureRecord.captures.some(
    (capture) =>
      capture.renderer !== 'webgl' ||
      capture.pageErrors?.length ||
      capture.consoleErrors?.length ||
      capture.failedRequests?.length,
  )
)
  throw new Error('Gameplay evidence must contain ten clean WebGL captures.');

rmSync(gallery, { recursive: true, force: true });
mkdirSync(captures, { recursive: true });
cpSync(liveSite, viewer, { recursive: true });
for (const file of files) {
  const source = path.join(repository, file.source);
  if (!existsSync(source)) throw new Error(`Missing ${file.source}.`);
  copyFileSync(source, path.join(captures, file.output));
}

writeFileSync(path.join(gallery, 'index.html'), ownerReviewHtml());
writeFileSync(
  path.join(gallery, 'public-review.json'),
  `${JSON.stringify(
    {
      schemaVersion: 1,
      status: 'owner-review',
      subject: 'five complete Meshy 7 veterancy split-diadem rank models',
      conceptAuthority: files[0].source,
      meshyTasks: [
        '01a00294-5948-7ee8-bc63-2f81cb0be753',
        '01a00295-fde7-7cbf-a014-4c4b04b6f3b4',
        '01a00297-c1fe-7f4a-8236-b646e390bd2b',
        '01a00299-e6dc-7d02-a713-86a9ccf8c7e9',
        '01a00290-9941-7b7c-86eb-568a05e213a2',
      ],
      creditsConsumed: 150,
      retriesUsed: 0,
      autoRigging: false,
      runtimeTransferBytes: 1_504_868,
      viewer: 'viewer/?standalone&audio=off',
      evidence: files.map((file) => `captures/${file.output}`),
    },
    null,
    2,
  )}\n`,
);
console.log(`Published veterancy owner gallery at ${path.relative(repository, gallery)}.`);

function ownerReviewHtml() {
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
  <title>Nilbots · Veterancy split diadem review</title>
  <style>
    :root{color-scheme:dark;font-family:ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;background:#080b0f;color:#eee7d7}
    *{box-sizing:border-box} body{margin:0;background:radial-gradient(circle at 50% 0,#22201a 0,#0b0e12 38rem);line-height:1.5}
    main{width:min(1080px,100%);margin:auto;padding:clamp(18px,4vw,48px)}
    h1{font-size:clamp(2rem,8vw,4rem);line-height:1;margin:.2em 0}.eyebrow{color:#d5a84e;text-transform:uppercase;letter-spacing:.14em;font-weight:800;font-size:.78rem}
    p{color:#c8c2b6}.lead{font-size:clamp(1rem,3vw,1.25rem);max-width:52rem}.card{background:#12171d;border:1px solid #383329;border-radius:16px;padding:clamp(14px,3vw,26px);margin:22px 0;box-shadow:0 20px 60px #0008}
    h2{margin:.1em 0 .35em;font-size:clamp(1.3rem,4vw,2rem)}img{display:block;width:100%;height:auto;border-radius:10px;background:#080b0f;border:1px solid #34302a}
    .button{display:inline-block;padding:14px 19px;border-radius:9px;background:#c7953e;color:#100d08;text-decoration:none;font-weight:900;margin:10px 0 4px}.button:focus{outline:3px solid #fff}
    .facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:9px;margin:18px 0}.fact{background:#181d23;border:1px solid #36312a;border-radius:10px;padding:12px}.fact b{display:block;color:#f1c56d;font-size:1.15rem}
    .caption{font-size:.9rem;margin:.65em 0 0;color:#aaa397}.checks{padding-left:1.3rem}.checks li{margin:.35em 0}.footer{text-align:center;color:#7e7a72;font-size:.82rem;padding:18px}
  </style>
</head>
<body><main>
  <div class="eyebrow">Owner review · complete rank models</div>
  <h1>Veterancy split diadem</h1>
  <p class="lead">Five complete Meshy 7 models now replace the renderer-built feather assembly. Please judge the rank progression, the open moose/crown silhouette, and how it behaves around the carried Core.</p>
  <div class="facts">
    <div class="fact"><b>5 models</b>one complete model per rank</div>
    <div class="fact"><b>1.50 MB</b>all five runtime GLBs combined</div>
    <div class="fact"><b>150 credits</b>five first passes, no retries</div>
    <div class="fact"><b>Core unchanged</b>fixed open centre corridor</div>
  </div>
  <a class="button" href="viewer/?standalone&amp;audio=off">Open the playable replay viewer</a>

  <section class="card"><h2>1. Concept authority</h2><img src="captures/concept-rank-1-5.png" alt="Approved conceptual progression from rank one split rails to rank five open moose crown"><p class="caption">Rank I has no blades. Ranks II–V add one blade per side per level, ending at four per side.</p></section>
  <section class="card"><h2>2. Meshy input sheets</h2><img src="captures/meshy-rank-1-5-inputs.png" alt="Five Meshy multiview input turnarounds"><p class="caption">Each rank was generated as a whole two-part ornament from four coherent views.</p></section>
  <section class="card"><h2>3. Runtime model fidelity</h2><img src="captures/rank-1-5-rich-vs-lean.png" alt="Rich and mobile lean model comparison for ranks one through five"><p class="caption">The promoted 256 px WebP tiers stay visually equivalent to the richer review tiers at this scale.</p></section>
  <section class="card"><h2>4. Rank V from every useful angle</h2><img src="captures/rank-5-six-view.png" alt="Six-view render of the complete rank five runtime diadem"><p class="caption">The centre is deliberately empty through the full height. The ornament follows the bot's yaw.</p></section>
  <section class="card"><h2>5. Desktop gameplay</h2><img src="captures/desktop-gameplay.webp" alt="Desktop WebGL captures of Core carry, handoffs and Arc Toss"><p class="caption">Real Core-stage replay: both teams, different chassis and headings, carry, handoff, and toss.</p></section>
  <section class="card"><h2>6. Phone landscape</h2><img src="captures/phone-gameplay.webp" alt="Phone landscape WebGL captures of the same veterancy replay"><p class="caption">The same evidence at 844 × 390. Use the live viewer above to pause and zoom.</p></section>
  <section class="card"><h2>Approval checklist</h2><ul class="checks"><li>Does Rank V read like the conceptual moose/crown rather than a renderer assembly?</li><li>Are Ranks I–IV visibly progressive without looking like unrelated objects?</li><li>Does the neutral brass stay distinct from both team colours?</li><li>Does the Core look intentional inside the open corridor through carry and handoff?</li></ul></section>
  <div class="footer">Nilbots · Arc Relay presentation diagnostic · no rules or Core-animation changes</div>
</main></body></html>`;
}
