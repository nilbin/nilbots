#!/usr/bin/env node
/**
 * Build and serve the site with its review-only, typed API fixtures.
 *
 * Usage, from web/:
 *   npm run site-review
 *   npm run site-review -- --no-build
 *   npm run site-review -- --tunnel
 *
 * --tunnel publishes a public, unauthenticated URL. Only children started by this process
 * are stopped on exit; an existing viewer review or tunnel is never touched.
 */

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { networkInterfaces } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = join(dirname(fileURLToPath(import.meta.url)), '..', 'web');
const dist = join(web, 'dist-site-review');
const port = parsePort(process.env.SITE_REVIEW_PORT ?? '4181');
const args = new Set(process.argv.slice(2));
const supported = new Set(['--no-build', '--tunnel']);
const unknown = [...args].filter((argument) => !supported.has(argument));

if (unknown.length > 0) {
  console.error(`Unknown site-review option: ${unknown.join(', ')}`);
  process.exit(2);
}

const children = [];
let shuttingDown = false;

function run(command, commandArgs, options = {}) {
  const child = spawn(command, commandArgs, { cwd: web, ...options });
  children.push(child);
  return child;
}

function shutdown(code = 0) {
  if (shuttingDown) return;
  shuttingDown = true;
  for (const child of children) {
    if (child.exitCode === null) child.kill('SIGTERM');
  }
  process.exit(code);
}

process.on('SIGINT', () => shutdown(0));
process.on('SIGTERM', () => shutdown(0));
process.on('exit', () => {
  for (const child of children) {
    if (child.exitCode === null) child.kill('SIGTERM');
  }
});

if (!args.has('--no-build')) {
  await waitFor(
    run('npm', ['run', 'build:site-review'], { stdio: 'inherit' }),
    'site-review build',
  );
} else if (!existsSync(join(dist, 'index.html'))) {
  console.error(
    'dist-site-review is missing. Run without --no-build to create it first.',
  );
  process.exit(1);
}

console.log(`\n  LOCAL    http://127.0.0.1:${port}`);
console.log(`  LAN      http://${lanAddress()}:${port}`);
console.log('           typed fixtures; no backend connection\n');

const server = run(
  'vite',
  [
    'preview',
    '--config',
    'vite.site-review.config.ts',
    '--host',
    '--port',
    String(port),
    '--strictPort',
  ],
  { stdio: 'inherit' },
);
server.on('error', (error) => {
  console.error(`Could not start the site-review server: ${error.message}`);
  shutdown(1);
});
server.on('close', (code) => shutdown(code ?? 0));

if (args.has('--tunnel')) startTunnel();

function startTunnel() {
  let announced = false;
  const diagnostics = [];
  const tunnel = run(
    'cloudflared',
    ['tunnel', '--url', `http://127.0.0.1:${port}`],
    { stdio: ['ignore', 'pipe', 'pipe'] },
  );
  tunnel.on('error', () => {
    console.error(
      '\ncloudflared was not found. Install it before using --tunnel.',
    );
    shutdown(1);
  });
  tunnel.on('close', (code) => {
    if (shuttingDown) return;
    console.error(
      `\ncloudflared exited before the review ended (${code ?? 'no exit code'}).`,
    );
    if (!announced) {
      for (const line of diagnostics.slice(-8)) console.error(`  ${line}`);
    }
    shutdown(code && code > 0 ? code : 1);
  });

  const watch = (stream) =>
    stream?.on('data', (chunk) => {
      const output = String(chunk);
      diagnostics.push(
        ...output
          .split(/\r?\n/)
          .map((line) => line.trim())
          .filter(Boolean),
      );
      if (diagnostics.length > 50) diagnostics.splice(0, diagnostics.length - 50);
      const url = output.match(
        /https:\/\/[^\s|]+\.trycloudflare\.com/,
      )?.[0];
      if (url) {
        announced = true;
        console.log(
          `\n  PUBLIC   ${url}\n           public and unauthenticated\n`,
        );
      }
    });
  watch(tunnel.stdout);
  watch(tunnel.stderr);
}

function waitFor(child, label) {
  return new Promise((resolve, reject) => {
    child.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${label} failed (${code ?? 'no exit code'})`));
    });
  });
}

function lanAddress() {
  for (const addresses of Object.values(networkInterfaces())) {
    for (const address of addresses ?? []) {
      if (address.family === 'IPv4' && !address.internal) return address.address;
    }
  }
  return 'localhost';
}

function parsePort(value) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 1 || parsed > 65_535) {
    console.error(`SITE_REVIEW_PORT must be a valid port, received "${value}".`);
    process.exit(2);
  }
  return parsed;
}
