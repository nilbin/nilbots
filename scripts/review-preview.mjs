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
import { existsSync, writeFileSync } from 'node:fs';
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
  if (existsSync(target)) return;

  const api = process.env.BOTARENA_API ?? 'http://127.0.0.1:8080';
  try {
    const matches = await fetch(`${api}/api/matches?take=20`).then((r) => r.json());
    // A decisive, finished match exercises every cue — shots, damage, and a destruction.
    const match =
      matches.find((m) => m.status === 'Completed' && !m.broadcasting && m.winnerSlot !== null) ??
      matches.find((m) => m.status === 'Completed' && !m.broadcasting);
    if (!match) throw new Error('no completed match available');
    const replay = await fetch(`${api}/api/matches/${match.id}/replay`).then((r) => r.text());
    writeFileSync(target, replay);
    console.log(`  replay.json  ← ${match.id} (${match.mapId})`);
  } catch (cause) {
    console.log(`  replay.json  MISSING — ${cause.message}`);
    console.log(`               start the API, or copy a replay to dist-review/replay.json`);
  }
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
