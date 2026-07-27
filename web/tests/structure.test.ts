import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import test from 'node:test';

/**
 * The folder boundaries in web/CLAUDE.md, enforced.
 *
 * Both of these fail *silently* when broken — the build stays green and the damage shows
 * up somewhere else entirely — which is why they are worth a test rather than prose alone.
 */

const SRC = join(import.meta.dirname, '..', 'src');

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) return sourceFiles(full);
    return /\.tsx?$/.test(entry) && !entry.endsWith('.d.ts') ? [full] : [];
  });
}

function importsOf(file: string): string[] {
  const source = readFileSync(file, 'utf8');
  return [...source.matchAll(/from\s+'([^']+)'/g)].map((match) => match[1]);
}

const files = sourceFiles(SRC);

test('the standalone viewer never imports site code', () => {
  // One build serves two apps. The viewer runs from a file:// URL with no router, no auth
  // and no site chrome, so a site import there compiles and then throws at runtime for CLI
  // users — who are the ones least able to report it.
  const viewerRoots = [
    'audio',
    'components',
    'render',
    'App.tsx',
    'playback.ts',
    'replayModel.ts',
    'replayNormalize.ts',
    'replayParticipants.ts',
    'replayPresentation.ts',
    'replayWireV1.ts',
    'replayWireV2.ts',
  ];
  const offenders: string[] = [];

  for (const file of files) {
    const rel = relative(SRC, file);
    if (!viewerRoots.some((root) => rel === root || rel.startsWith(`${root}/`))) continue;
    for (const specifier of importsOf(file)) {
      if (specifier.includes('/site/') || specifier.endsWith('/site'))
        offenders.push(`${rel} imports ${specifier}`);
    }
  }

  assert.deepEqual(
    offenders,
    [],
    'Viewer code must not depend on the site. Move the shared part out of site/ instead.',
  );
});

test('only the API client imports the generated schema', () => {
  // Generated types are aliased once in site/api.ts so screens import a domain name. Left
  // unguarded, call sites start indexing components['schemas'] directly and a regenerated
  // document then breaks them in a dozen places rather than one.
  const allowed = new Set(['site/api.ts']);
  const offenders = files
    .filter((file) => !allowed.has(relative(SRC, file).replaceAll('\\', '/')))
    .filter((file) => importsOf(file).some((specifier) => specifier.includes('api/schema')))
    .map((file) => relative(SRC, file));

  assert.deepEqual(offenders, [], 'Alias generated types in site/api.ts and import from there.');
});
