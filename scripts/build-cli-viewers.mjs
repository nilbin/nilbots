#!/usr/bin/env node
/**
 * Build one self-contained viewer per map theme, for the CLI to pick from.
 *
 * `nilbots play` writes a viewer.html that has to work from disk, so it inlines
 * everything. Themes are essentially all of that weight — 14 MB against 236 KB for every
 * chassis, projectile look and audio cue combined — and a replay draws exactly one of
 * them. Building per theme is what stops the artifact growing with the content library:
 * the fifth theme adds a fifth file rather than 3 MB to every viewer ever emitted.
 *
 * Themes are discovered from the asset folders rather than listed here, so adding one is
 * still a folder and a manifest.
 */
import { execFileSync } from 'node:child_process';
import { readdirSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const web = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'web');
const themeRoot = path.join(web, 'src', 'assets', 'themes');

const themes = readdirSync(themeRoot).filter((name) =>
  statSync(path.join(themeRoot, name)).isDirectory(),
);
if (themes.length === 0) throw new Error(`No themes found under ${themeRoot}`);

console.log(`Building ${themes.length} scoped viewers: ${themes.join(', ')}`);

for (const theme of themes) {
  execFileSync('npx', ['vite', 'build', '--config', 'vite.cli.config.ts', '--logLevel', 'warn'], {
    cwd: web,
    stdio: 'inherit',
    env: { ...process.env, BOTARENA_CLI_THEME: theme },
  });
  const output = path.join(web, 'dist-cli', theme, 'index.html');
  const megabytes = (statSync(output).size / 1_000_000).toFixed(1);
  console.log(`  ${theme.padEnd(16)} ${megabytes} MB`);
}
