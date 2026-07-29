import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  ARENA_MARGIN_TILES,
  ArenaCamera,
  arenaViewport,
  focusFrame,
  focusPointsAt,
  frameEscapes,
  fullArenaFrame,
} from './.harness/harness.entry.js';

/**
 * The camera, as arithmetic.
 *
 * It is a pure function of the replay, the playhead and the shape of the hole it is drawn
 * in — deliberately, because the alternative is a camera that can only be judged by
 * watching it, and "the zoom hunts on a fabrication" is not something a frame hash can
 * say. Everything here is the part that would be untestable if the spring, the deadband
 * and the fit lived inside a `requestAnimationFrame` loop.
 */

const here = import.meta.dirname;
const frontline = loadReplayJson(
  readFileSync(join(here, 'fixtures', 'frontline-replay-v2.json'), 'utf8'),
).replay as ReplayModel;

const FRAMING = { mapWidth: 40, mapHeight: 24, aspect: 16 / 10 };

test('the whole arena is the zoom-out limit, at the viewport shape', () => {
  const full = fullArenaFrame(FRAMING);
  assert.equal(full.x, 20);
  assert.equal(full.y, 12);
  // Wide enough for the map plus its margin, and tall enough for the same at this aspect.
  assert.ok(full.width >= FRAMING.mapWidth + ARENA_MARGIN_TILES);
  assert.ok(full.height >= FRAMING.mapHeight + ARENA_MARGIN_TILES);
  assert.ok(Math.abs(full.width / full.height - FRAMING.aspect) < 1e-9);

  // A tall viewport of the same map must not crop it: the frame grows, never shrinks.
  const portrait = fullArenaFrame({ ...FRAMING, aspect: 0.5 });
  assert.ok(portrait.height >= FRAMING.mapHeight + ARENA_MARGIN_TILES);
  assert.ok(portrait.width >= FRAMING.mapWidth + ARENA_MARGIN_TILES);
});

test('a fit holds every position it was given, with room around them', () => {
  const points = [
    { x: 18, y: 11 },
    { x: 22, y: 13 },
  ];
  const frame = focusFrame(points, FRAMING);
  assert.ok(Math.abs(frame.x - 20) < 1e-9, 'centred on the action');
  assert.ok(Math.abs(frame.y - 12) < 1e-9);
  for (const point of points) {
    assert.ok(point.x > frame.x - frame.width / 2 + 1);
    assert.ok(point.x < frame.x + frame.width / 2 - 1);
    assert.ok(point.y > frame.y - frame.height / 2 + 1);
    assert.ok(point.y < frame.y + frame.height / 2 - 1);
  }
  assert.ok(Math.abs(frame.width / frame.height - FRAMING.aspect) < 1e-9);
});

test('it never gets closer than about eight tiles, and never wider than the arena', () => {
  // One surviving machine. Fitting it honestly would show a bot and no arena.
  const alone = focusFrame([{ x: 20, y: 12 }], FRAMING);
  assert.ok(alone.width >= 8, `min span held (${alone.width})`);

  // Two lives in opposite corners ask for more than the map has, and get the map.
  const everywhere = focusFrame(
    [
      { x: 0.5, y: 0.5 },
      { x: 39.5, y: 23.5 },
    ],
    FRAMING,
  );
  const full = fullArenaFrame(FRAMING);
  assert.deepEqual(everywhere, full, 'the full arena is the zoom-out floor');
});

test('a fight in the corner is clamped to the arena, not centred on empty space', () => {
  const frame = focusFrame([{ x: 1.5, y: 1.5 }], FRAMING);
  assert.ok(
    frame.x - frame.width / 2 >= -ARENA_MARGIN_TILES,
    'no background off the left edge',
  );
  assert.ok(
    frame.y - frame.height / 2 >= -ARENA_MARGIN_TILES,
    'nor off the top',
  );
  // And it is still looking at the corner rather than at the middle of the map.
  assert.ok(frame.x < 20);
  assert.ok(frame.y < 12);
});

test('a step sideways does not re-aim, and a break for the flank does', () => {
  const committed = focusFrame(
    [
      { x: 18, y: 11 },
      { x: 22, y: 13 },
    ],
    FRAMING,
  );
  const nudged = focusFrame(
    [
      { x: 18.4, y: 11 },
      { x: 22, y: 13.2 },
    ],
    FRAMING,
  );
  assert.equal(
    frameEscapes(committed, nudged),
    false,
    'a tile of movement is not a camera move',
  );

  const spawned = focusFrame(
    [
      { x: 18, y: 11 },
      { x: 22, y: 13 },
      { x: 21, y: 12 },
    ],
    FRAMING,
  );
  assert.equal(
    frameEscapes(committed, spawned),
    false,
    'a body arriving inside the frame is not a camera move either',
  );

  const broke = focusFrame(
    [
      { x: 18, y: 11 },
      { x: 34, y: 20 },
    ],
    FRAMING,
  );
  assert.equal(frameEscapes(committed, broke), true, 'and leaving it is');
});

test('the camera converges without ever passing the target', () => {
  const camera = new ArenaCamera(FRAMING);
  const target = focusFrame([{ x: 8, y: 6 }], FRAMING);
  assert.equal(camera.aim(target), true);

  const start = camera.frame.width;
  let previousWidth = start;
  let previousGap = Math.hypot(
    camera.frame.x - target.x,
    camera.frame.y - target.y,
  );
  let first = 0;
  for (let step = 0; step < 240; step++) {
    camera.advance(1 / 60);
    if (step === 0) first = start - camera.frame.width;
    // Monotone in both channels — a critically damped spring, not a bouncy one.
    assert.ok(camera.frame.width <= previousWidth + 1e-9, 'zoom never bounces');
    assert.ok(camera.frame.width >= target.width - 1e-9, 'nor overshoots');
    const gap = Math.hypot(
      camera.frame.x - target.x,
      camera.frame.y - target.y,
    );
    assert.ok(gap <= previousGap + 1e-9, 'the pan never overshoots');
    previousWidth = camera.frame.width;
    previousGap = gap;
  }
  assert.ok(Math.abs(camera.frame.width - target.width) < 0.01, 'and arrives');
  assert.ok(previousGap < 0.01);
  // It also never cuts: one frame may not do most of the journey.
  assert.ok(first < (start - target.width) * 0.25, `first frame eased (${first})`);
});

test('a huge frame gap — a backgrounded tab — is stepped, not flung', () => {
  const camera = new ArenaCamera(FRAMING);
  camera.aim(focusFrame([{ x: 8, y: 6 }], FRAMING));
  const before = { ...camera.frame };
  camera.advance(30);
  assert.ok(camera.frame.width < before.width);
  assert.ok(camera.frame.width > 8, 'and it is still an arena');
  assert.ok(Number.isFinite(camera.frame.x) && Number.isFinite(camera.frame.y));
});

test('a gesture takes the camera and only the control gives it back', () => {
  const camera = new ArenaCamera(FRAMING);
  const action = focusFrame([{ x: 8, y: 6 }], FRAMING);

  camera.zoom(2, FRAMING);
  assert.equal(camera.auto, false, 'a pinch pauses auto-fit');
  assert.equal(camera.aim(action), false, 'and the fit stops being applied');
  const held = camera.aimed;

  camera.pan(3, 0, FRAMING);
  assert.ok(camera.aimed.x > held.x, 'a drag still moves it');
  assert.equal(camera.auto, false);

  camera.engage();
  assert.equal(camera.auto, true);
  assert.equal(camera.aim(action), true, 'the control hands it back at once');

  // Switching the fit off is not the same as a gesture: it asks for the board, because
  // that is the only reason to switch it off — and on a phone there is no wheel to undo a
  // camera that merely stopped following wherever the last skirmish left it.
  camera.showEverything(FRAMING);
  assert.equal(camera.auto, false);
  assert.deepEqual(camera.aimed, fullArenaFrame(FRAMING));

  // Re-engaging must take the very next fit even when the released frame happened to
  // contain it, or the toggle would look like it did nothing.
  const wide = new ArenaCamera(FRAMING);
  wide.zoom(0.5, FRAMING);
  wide.engage();
  assert.equal(wide.aim(focusFrame([{ x: 20, y: 12 }], FRAMING)), true);
});

test('manual zoom obeys the same limits the fit does', () => {
  const camera = new ArenaCamera(FRAMING);
  for (let step = 0; step < 40; step++) camera.zoom(1.6, FRAMING);
  assert.ok(camera.aimed.width >= 8, 'a pinch cannot get closer than the fit can');
  for (let step = 0; step < 40; step++) camera.zoom(1 / 1.6, FRAMING);
  assert.ok(
    camera.aimed.width <= fullArenaFrame(FRAMING).width + 1e-9,
    'nor further out than the arena',
  );
});

test('with no frame the flat renderer keeps its historical transform', () => {
  // Every recorded golden frame was drawn at this framing, and passing no camera has to
  // keep producing it exactly — integer tile, integer origins, whole map.
  const view = arenaViewport(null, 24, 18, 640, 480);
  const tile = Math.floor(
    Math.min(640 / (24 + ARENA_MARGIN_TILES), 480 / (18 + ARENA_MARGIN_TILES)),
  );
  assert.deepEqual(view, {
    tile,
    originX: Math.floor((640 - tile * 24) / 2),
    originY: Math.floor((480 - tile * 18) / 2),
  });
});

test('a frame becomes the transform that puts it on screen', () => {
  const frame = { x: 10, y: 6, width: 16, height: 10 };
  const view = arenaViewport(frame, 24, 18, 640, 400);
  assert.equal(view.tile, 40, '16 tiles across 640 pixels');
  // The frame's centre lands in the middle of the canvas.
  assert.ok(Math.abs(view.originX + frame.x * view.tile - 320) < 1e-9);
  assert.ok(Math.abs(view.originY + frame.y * view.tile - 200) < 1e-9);
});

test('selection fits the selected unit team, not the unit alone', () => {
  // Tick 2 of the fixture is the one both teams fabricate on, so each side has more than
  // one life and "the team" is a different box from "the unit".
  const time = 2.4;
  const everybody = focusPointsAt(frontline, time, null);
  assert.ok(everybody.length >= 4, 'both sides are on the board');

  const zero = frontline.units.find((unit) => unit.teamId === 0)!;
  const mine = focusPointsAt(
    frontline,
    time,
    zero.unitKey as ReplayStableUnitKey,
  );
  assert.ok(mine.length >= 2, 'a team is more than the unit that was clicked');
  assert.ok(mine.length < everybody.length, 'and less than everybody');

  const teamFrame = focusFrame(mine, FRAMING);
  const allFrame = focusFrame(everybody, FRAMING);
  assert.ok(
    teamFrame.width <= allFrame.width,
    'one team is never a wider shot than both',
  );

  // A key that names no unit is not a filter — it must not empty the camera.
  assert.deepEqual(
    focusPointsAt(frontline, time, 'generic:9:unit:9' as ReplayStableUnitKey),
    everybody,
  );
});

test('the camera follows the fight rather than a fixed plan view', () => {
  const framing = {
    mapWidth: frontline.map.width,
    mapHeight: frontline.map.height,
    aspect: 16 / 10,
  };
  const full = fullArenaFrame(framing);
  // Both teams are dug in at opposite ends of a fifteen-tile map, so the honest fit for
  // everybody *is* the whole arena — the camera does not invent a closer shot than the
  // action supports, which is the failure mode a following camera usually has.
  assert.deepEqual(focusFrame(focusPointsAt(frontline, 2.4, null), framing), full);

  // Follow one side and it closes right in, because that side's lives are two tiles apart.
  const zero = frontline.units.find((unit) => unit.teamId === 0)!;
  const side = focusFrame(
    focusPointsAt(frontline, 2.4, zero.unitKey as ReplayStableUnitKey),
    framing,
  );
  assert.ok(side.width < full.width * 0.7, `closed in (${side.width})`);
  assert.ok(side.x < full.x, 'and it is looking at that side of the map');
});
