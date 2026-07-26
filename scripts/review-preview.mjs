#!/usr/bin/env node
/**
 * Serve the review build for evaluation on a real device.
 *
 * The review bundle is deliberately not self-contained — separate hashed atlases, audio
 * and JavaScript, so a phone streams them instead of parsing one ~15 MiB inline document.
 * That means it cannot be opened from disk: `file://` blocks ES modules, so double-clicking
 * `dist-review/index.html` yields a blank page. It has to be served, which is what this does.
 *
 * Usage, from web/:
 *   npm run review            LAN only — open the printed http://<ip>:PORT on a phone
 *   npm run review -- --tunnel        also publish a public HTTPS URL via cloudflared
 *   npm run review -- --no-build      serve whatever is already in dist-review
 *
 * --tunnel is public and unauthenticated. Anyone with the link reaches the build.
 */

import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { cp, mkdir, rm } from 'node:fs/promises';
import { networkInterfaces } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = join(dirname(fileURLToPath(import.meta.url)), '..', 'web');
const dist = join(web, 'dist-review');
const port = Number(process.env.REVIEW_PORT ?? 4173);
const args = new Set(process.argv.slice(2));
const children = [];

function run(command, commandArgs, options = {}) {
  const child = spawn(command, commandArgs, { cwd: web, ...options });
  children.push(child);
  return child;
}

let shuttingDown = false;
function shutdown(code = 0) {
  if (shuttingDown) return;
  shuttingDown = true;
  // Every child, always. A tunnel that outlives its server keeps a public URL alive
  // pointing at nothing — and the next run publishes a second one, so they accumulate
  // silently. Killing the server alone is not enough.
  for (const child of children) child.kill('SIGTERM');
  process.exit(code);
}
process.on('SIGINT', () => shutdown(0));
process.on('SIGTERM', () => shutdown(0));
process.on('exit', () => {
  for (const child of children) child.kill('SIGTERM');
});

function lanAddress() {
  for (const addresses of Object.values(networkInterfaces())) {
    for (const address of addresses ?? []) {
      if (address.family === 'IPv4' && !address.internal) return address.address;
    }
  }
  return 'localhost';
}

/**
 * The viewer fetches `replay.json` beside itself, and `vite build` empties dist-review —
 * so without this every rebuild silently produces a viewer with nothing to play. Pulled
 * from a local API when one is running; otherwise say so plainly rather than let someone
 * debug an empty arena.
 */
async function ensureReplay() {
  const target = join(dist, 'replay.json');
  const indexPath = join(dist, 'replays.json');
  // `vite build` empties dist-review, so anything chosen last time is gone — which is how
  // a deliberate REVIEW_BOTS selection silently reverted to the generic score on the next
  // rebuild. The chosen set is cached outside the build output and restored, so a choice
  // survives until it is explicitly refreshed.
  const cache = join(web, '.review-cache');
  const cacheIndex = join(cache, 'replays.json');
  const refresh = process.argv.includes('--refresh') || process.env.REVIEW_REFRESH === '1';

  if (existsSync(indexPath) && !refresh) return;
  if (existsSync(cacheIndex) && !refresh) {
    await cp(cache, dist, { recursive: true });
    const restored = JSON.parse(readFileSync(cacheIndex, 'utf8'));
    console.log(`  replays      ${restored.length} restored (--refresh to reselect):`);
    for (const entry of restored) {
      console.log(`                 ${entry.map} · ${entry.ticks}t · ${entry.bots.join(' v ')}`);
    }
    return;
  }

  const api = process.env.BOTARENA_API ?? 'http://127.0.0.1:8080';
  /** Comma-separated bot names to prefer, e.g. REVIEW_BOTS="Pincer gen-10,Bastille gen-5". */
  const preferred = (process.env.REVIEW_BOTS ?? '')
    .split(',')
    .map((name) => name.trim().toLowerCase())
    .filter(Boolean);
  const wanted = Number(process.env.REVIEW_COUNT ?? 4);

  try {
    const summaries = await fetch(`${api}/api/matches?take=60`).then((r) => r.json());
    const settled = summaries.filter((m) => m.status === 'Completed' && !m.broadcasting);
    if (settled.length === 0) throw new Error('no completed match available');

    const scored = [];
    for (const summary of settled.slice(0, 30)) {
      const replay = await fetch(`${api}/api/matches/${summary.id}/replay`).then((r) => r.json());
      const names = replay.header.participants.map((p) => p.name);
      // A requested matchup wins outright: the reviewer asked for it, and the score is a
      // stand-in for judgement rather than a replacement for it.
      const requested =
        preferred.length > 0 && preferred.every((want) => names.some((n) => n.toLowerCase().includes(want)));
      scored.push({
        id: summary.id,
        replay,
        names,
        score: reviewScore(replay) + (requested ? 1000 : 0),
      });
    }
    scored.sort((left, right) => right.score - left.score);

    // One per matchup+map, so a picker offers genuinely different fights rather than four
    // views of the same pairing.
    const chosen = [];
    const seen = new Set();
    for (const candidate of scored) {
      if (chosen.length >= wanted) break;
      const key = `${[...candidate.names].sort().join('|')}@${candidate.replay.header.mapId}`;
      if (seen.has(key)) continue;
      seen.add(key);
      chosen.push(candidate);
    }
    if (chosen.length === 0) throw new Error('no replay scored above zero');

    await mkdir(join(dist, 'replays'), { recursive: true });
    const index = [];
    for (const entry of chosen) {
      writeFileSync(join(dist, 'replays', `${entry.id}.json`), JSON.stringify(entry.replay));
      index.push({
        id: entry.id,
        url: `replays/${entry.id}.json`,
        map: entry.replay.header.mapId,
        bots: entry.names,
        ticks: entry.replay.ticks.length,
        reason: entry.replay.result?.reason ?? null,
      });
    }
    writeFileSync(indexPath, JSON.stringify(index, null, 2));
    // The single-replay path stays for anything that expects it.
    writeFileSync(target, JSON.stringify(chosen[0].replay));
    // Keep a copy where the next build cannot delete it.
    await rm(cache, { recursive: true, force: true });
    await mkdir(join(cache, 'replays'), { recursive: true });
    for (const entry of chosen) {
      await cp(join(dist, 'replays', `${entry.id}.json`), join(cache, 'replays', `${entry.id}.json`));
    }
    await cp(indexPath, cacheIndex);
    await cp(target, join(cache, 'replay.json'));

    console.log(`  replays      ${index.length} available:`);
    for (const entry of index) {
      console.log(`                 ${entry.map} · ${entry.ticks}t · ${entry.bots.join(' v ')}`);
    }
  } catch (cause) {
    console.log(`  replays      NONE — ${cause.message}`);
    console.log(`               start the API, or copy a replay to dist-review/replay.json`);
  }
}

/**
 * How useful a replay is for reviewing the arena.
 *
 * Cue variety dominates: a match with no impacts never plays the impact sound and never
 * casts an impact light, so a reviewer cannot judge either however long it runs. A
 * 121-tick match with 37 shots and no hits looks busy and tests one third of the work.
 *
 * Length is a mild bonus, not a driver, and this is why the strongest bots are the wrong
 * choice — they end matches in ten ticks. What reads as a better bot makes a worse replay.
 */
function reviewScore(replay) {
  let shots = 0;
  let damage = 0;
  let destroyed = 0;
  const shooters = new Set();
  for (const tick of replay.ticks) {
    for (const event of tick.events ?? []) {
      if (event.type === 'Shot') {
        shots += 1;
        shooters.add(event.slot);
      } else if (event.type === 'Damage') damage += 1;
      else if (event.type === 'Destroyed') destroyed += 1;
    }
  }

  const variety = [shots, damage, destroyed].filter((count) => count > 0).length;
  if (variety < 3) return variety * 5;

  // Both sides must actually fight. A match against a bot that never fires runs the full
  // length and exercises every cue, but reads as target practice — and picking one is how
  // a reviewer ends up judging the arena on a replay nobody would watch.
  const contested = shooters.size >= 2;
  if (!contested) return 20;

  const ticks = replay.ticks.length;
  const watchable = ticks >= 60 && ticks <= 140 ? 15 : 0;
  return 40 + Math.min(shots, 25) + Math.min(damage, 12) * 2 + watchable;
}

function startTunnel() {
  const tunnel = run('cloudflared', ['tunnel', '--url', `http://127.0.0.1:${port}`], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  tunnel.on('error', () => {
    console.log('\n  cloudflared not found. Install it with:  brew install cloudflared');
    console.log('  Serving on the LAN only.\n');
  });
  const watch = (stream) =>
    stream?.on('data', (chunk) => {
      const url = String(chunk).match(/https:\/\/[^\s|]+\.trycloudflare\.com/)?.[0];
      if (url) console.log(`\n  PUBLIC   ${url}\n           (public and unauthenticated)\n`);
    });
  watch(tunnel.stdout);
  watch(tunnel.stderr);
}

if (!args.has('--no-build')) {
  await new Promise((resolve, reject) => {
    run('npm', ['run', 'build:review'], { stdio: 'inherit' }).on('close', (code) =>
      code === 0 ? resolve() : reject(new Error(`build:review failed (${code})`)),
    );
  });
}

await ensureReplay();

console.log(`\n  LAN      http://${lanAddress()}:${port}`);
console.log('           open on a phone on the same Wi-Fi\n');

// --host binds every interface, which is the whole point: localhost is unreachable
// from the device you actually want to listen on.
// If the server dies the tunnel is pointing at nothing, so take the whole thing down
// rather than leave a public URL serving errors.
run('vite', ['preview', '--config', 'vite.review.config.ts', '--host', '--port', String(port)], {
  stdio: 'inherit',
}).on('close', (code) => shutdown(code ?? 0));

if (args.has('--tunnel')) startTunnel();
