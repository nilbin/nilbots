import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import { createCanvas } from '@napi-rs/canvas';
// Built by `npm run harness` through Vite's SSR pipeline: the renderer uses
// import.meta.glob, which only exists inside Vite, so bare Node cannot import it directly.
import { drawArena } from './.harness/harness.entry.js';
import type { ReplayDocument } from '../src/types.ts';

/**
 * Pixel-level regression cover for the renderer.
 *
 * Replays are deterministic, so the same replay at the same tick must produce the same
 * frame. That makes the renderer testable in a way most renderers are not, and it is what
 * lets `drawArena` be restructured at all: a pass refactor is supposed to change how the
 * frame is produced and not what it looks like, and nothing else can hold that line.
 *
 * Hashes, not images: a mismatch means "you changed the picture", and the diff belongs in
 * a reviewer's eyes rather than in version control as megabytes of PNG.
 *
 * Textures are absent here — `arenaThemes` returns null images without a DOM — so this
 * covers geometry, layering, tinting and fog, not the atlases themselves. That is the part
 * a refactor is most likely to break, and the part no one notices until it ships.
 *
 * Regenerate deliberately with UPDATE_GOLDEN=1 when a change is *meant* to alter the
 * picture, and say so in the commit.
 */

const here = import.meta.dirname;
const replay = JSON.parse(
  readFileSync(join(here, 'fixtures', 'golden-replay.json'), 'utf8'),
) as ReplayDocument;
const goldenPath = join(here, 'fixtures', 'golden-frames.json');

/** Opening, mid-match, and the final tick — spawn layout, combat, and the end state. */
const FRAMES = [
  { name: 'tick 0', time: 0, selectedSlot: null, showVisibility: false },
  { name: 'tick 40', time: 40, selectedSlot: null, showVisibility: false },
  // Ticks 95 and 96 are the only ones carrying light-emitting events in this replay —
  // 95 has a shot and an impact, 96 adds the destruction. Without both, the light pass
  // would be covered by a single frame.
  { name: 'tick 95', time: 95, selectedSlot: null, showVisibility: false },
  { name: 'tick 96', time: 96, selectedSlot: null, showVisibility: false },
  // Fog is compositing rather than plain drawing, so it gets its own frames.
  { name: 'fog slot 0 @ 20', time: 20, selectedSlot: 0, showVisibility: true },
  { name: 'fog slot 1 @ 60', time: 60, selectedSlot: 1, showVisibility: true },
];

const WIDTH = 640;
const HEIGHT = 480;

function frameHash(frame: (typeof FRAMES)[number]): string {
  const canvas = createCanvas(WIDTH, HEIGHT);
  const ctx = canvas.getContext('2d');
  drawArena(
    ctx as unknown as CanvasRenderingContext2D,
    replay,
    { time: frame.time, selectedSlot: frame.selectedSlot, showVisibility: frame.showVisibility },
    WIDTH,
    HEIGHT,
  );
  return createHash('sha256').update(canvas.toBuffer('image/png')).digest('hex').slice(0, 16);
}

const actual = Object.fromEntries(FRAMES.map((frame) => [frame.name, frameHash(frame)]));

if (process.env.UPDATE_GOLDEN === '1') {
  writeFileSync(goldenPath, `${JSON.stringify(actual, null, 2)}\n`);
}

test('the renderer produces the recorded frames', () => {
  assert.ok(
    existsSync(goldenPath),
    'No golden frames recorded. Run once with UPDATE_GOLDEN=1.',
  );
  const expected = JSON.parse(readFileSync(goldenPath, 'utf8')) as Record<string, string>;

  for (const frame of FRAMES) {
    assert.equal(
      actual[frame.name],
      expected[frame.name],
      `Frame "${frame.name}" changed. If that was intended, re-record with ` +
        'UPDATE_GOLDEN=1 and say why in the commit.',
    );
  }
});

test('every frame renders something', () => {
  // A guard on the guard: if drawArena threw or drew nothing, every hash would still be
  // stable and identical, and the suite above would pass while covering nothing.
  const blank = createHash('sha256')
    .update(createCanvas(WIDTH, HEIGHT).toBuffer('image/png'))
    .digest('hex')
    .slice(0, 16);
  for (const frame of FRAMES) {
    assert.notEqual(actual[frame.name], blank, `Frame "${frame.name}" is empty.`);
  }
});
