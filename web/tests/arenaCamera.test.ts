import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import type {
  ArenaFrame,
  ArenaFraming,
} from '../src/render/arenaCamera.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  ARENA_MARGIN_TILES,
  ArenaCamera,
  arenaViewport,
  directorMinSpan,
  focusFrame,
  focusPointsAt,
  frameEscapes,
  fullArenaFrame,
  posesAt,
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

/**
 * The two shapes a phone offers, in CSS pixels — and the reason the fit has to be arithmetic
 * rather than something judged by eye. Both are far from square, which is what makes the
 * fitted frame much larger than the action on one axis, which is where centring used to be
 * lost (DECISIONS #175).
 */
const PHONE_LANDSCAPE = { width: 852, height: 393 };
const PHONE_PORTRAIT = { width: 393, height: 852 };

/** The middle of a bounding box, which is what "the action" means to the camera. */
function centreOf(points: readonly { x: number; y: number }[]): {
  x: number;
  y: number;
} {
  return {
    x: (Math.min(...points.map((p) => p.x)) + Math.max(...points.map((p) => p.x))) / 2,
    y: (Math.min(...points.map((p) => p.y)) + Math.max(...points.map((p) => p.y))) / 2,
  };
}

/**
 * Where a tile position lands on screen, through the flat renderer's own transform.
 *
 * Deliberately the shipped projection rather than the test's arithmetic: "the fit is
 * centred" is a claim about pixels, and `arenaViewport` is what turns a frame into them for
 * Canvas2D. The 3D renderer consumes the same frame as a look target at `(frame.x, frame.y)`
 * on the floor plane, so a frame centred on the action is centred in that projection too.
 */
function projectedPoint(
  frame: ArenaFrame,
  framing: ArenaFraming,
  viewport: { width: number; height: number },
  point: { x: number; y: number },
): { x: number; y: number } {
  const view = arenaViewport(
    frame,
    framing.mapWidth,
    framing.mapHeight,
    viewport.width,
    viewport.height,
  );
  return {
    x: view.originX + point.x * view.tile,
    y: view.originY + point.y * view.tile,
  };
}

/** The fit put the middle of the action in the middle of the screen, to the pixel. */
function assertCentred(
  points: readonly { x: number; y: number }[],
  framing: ArenaFraming,
  viewport: { width: number; height: number },
  what: string,
): void {
  const box = centreOf(points);
  const screen = projectedPoint(
    focusFrame(points, framing),
    framing,
    viewport,
    box,
  );
  assert.ok(
    Math.abs(screen.x - viewport.width / 2) < 1e-6,
    `${what}: horizontally centred (was ${screen.x.toFixed(1)} of ${viewport.width})`,
  );
  assert.ok(
    Math.abs(screen.y - viewport.height / 2) < 1e-6,
    `${what}: vertically centred (was ${screen.y.toFixed(1)} of ${viewport.height})`,
  );
}

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

test('it never gets closer than about six tiles, and never wider than the arena', () => {
  // One surviving machine. Fitting it honestly would show a bot and no arena.
  const alone = focusFrame([{ x: 20, y: 12 }], FRAMING);
  assert.ok(alone.width >= 6, `min span held (${alone.width})`);

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

test('Arc Relay gives both renderers the same wider ten-tile closest shot', () => {
  const arc = loadReplayJson(
    readFileSync(join(here, 'fixtures', 'generic-mind-replay-v3.json'), 'utf8'),
  ).replay;
  assert.equal(arc.contract.kind, 'v3-generic');
  if (arc.contract.kind !== 'v3-generic') return;
  arc.contract.modeKind = 'arc-relay';

  const minSpan = directorMinSpan(arc);
  assert.equal(minSpan, 10);
  assert.equal(directorMinSpan(frontline), undefined);
  const close = focusFrame([{ x: 20, y: 12 }], {
    ...FRAMING,
    minSpan,
  });
  assert.ok(close.width >= 10, `Arc Relay close-up held at ${close.width} tiles`);
});

test('the fit is close enough to watch on a phone, and still never crops a life', () => {
  // Reported from a phone: "zoom in more in the fit — margin is way excessive". The fit was
  // correct about *where* to look and far too generous about how much to show, and the
  // generosity compounds with the viewport: the frame is grown to the screen's shape, so a
  // margin of m tiles costs roughly `2 · m · aspect` tiles of width on a 2.17:1 phone. At
  // the margin this used to carry, a lone survivor on a 23-wide Labs map was framed in
  // eleven tiles — half the arena for one machine.
  //
  // Two numbers, in tension, and both belong in a test rather than in an eye: a ceiling on
  // how much floor a fit may show, and a floor on how much clear ground is left past the
  // outermost life so a spring that is still catching up cannot crop anybody.
  const LABS = { mapWidth: 23, mapHeight: 15 };
  const landscape = {
    ...LABS,
    aspect: PHONE_LANDSCAPE.width / PHONE_LANDSCAPE.height,
  };
  const portrait = {
    ...LABS,
    aspect: PHONE_PORTRAIT.width / PHONE_PORTRAIT.height,
  };
  const arena = { ...LABS, aspect: 16 / 10 };

  const survivor = [{ x: 11.5, y: 7.5 }];
  const duel = [
    { x: 10.5, y: 7.5 },
    { x: 14.5, y: 8.5 },
  ];

  for (const [what, framing, ceiling] of [
    ['phone landscape', landscape, 7.5],
    ['phone portrait', portrait, 6.5],
    ['arena panel', arena, 6.5],
  ] as const) {
    const alone = focusFrame(survivor, framing);
    assert.ok(
      alone.width <= ceiling,
      `${what}: a lone survivor is framed in ${alone.width.toFixed(1)} tiles, over the ${ceiling} the fit is allowed`,
    );
    // Both axes: a landscape fit that held the width and showed a sliver of height would
    // pass a width ceiling and still be unwatchable.
    assert.ok(
      alone.height >= 2.8,
      `${what}: ${alone.height.toFixed(1)} tiles of height is not an arena`,
    );
  }

  // And it is still a fit: every life keeps a tile of clear ground past its own body on
  // every side, at every viewport shape. Tile centres in, so a tile of floor is 1.5.
  for (const framing of [landscape, portrait, arena]) {
    for (const points of [survivor, duel]) {
      const frame = focusFrame(points, framing);
      for (const point of points) {
        assert.ok(
          Math.abs(point.x - frame.x) <= frame.width / 2 - 1.5,
          `a life is ${(frame.width / 2 - Math.abs(point.x - frame.x)).toFixed(2)} tiles from the edge across`,
        );
        assert.ok(
          Math.abs(point.y - frame.y) <= frame.height / 2 - 1.5,
          `a life is ${(frame.height / 2 - Math.abs(point.y - frame.y)).toFixed(2)} tiles from the edge down`,
        );
      }
    }
  }
});

test('the fit centres the action on a phone, in landscape and in portrait', () => {
  // The bug this pins, reported from a phone: a duel by the right-hand spawn sat about a
  // third of the screen right of centre with empty floor beside it. The cause was a fit that
  // was forbidden from hanging over the edge of the map — and since the frame is grown to the
  // viewport's shape, on a 2.17:1 screen that grown span is most of a 24-wide arena, leaving
  // a band barely four tiles wide to be "centred" in. Nothing about the projection was wrong;
  // the frame handed to it was already off the action.
  const map = { mapWidth: 24, mapHeight: 18 };
  const landscape = {
    ...map,
    aspect: PHONE_LANDSCAPE.width / PHONE_LANDSCAPE.height,
  };
  const portrait = {
    ...map,
    aspect: PHONE_PORTRAIT.width / PHONE_PORTRAIT.height,
  };

  const byTheRightSpawn = [
    { x: 20.5, y: 8.5 },
    { x: 22.5, y: 10.5 },
  ];
  const inTheOpen = [
    { x: 11, y: 8 },
    { x: 13, y: 10 },
  ];
  const nearTheTop = [
    { x: 11, y: 3 },
    { x: 13, y: 5 },
  ];

  for (const points of [byTheRightSpawn, inTheOpen, nearTheTop]) {
    assertCentred(points, landscape, PHONE_LANDSCAPE, 'landscape');
    assertCentred(points, portrait, PHONE_PORTRAIT, 'portrait');
  }

  // And the fit is still a fit: it holds the action with room around it, and it never
  // reaches past the zoom-out limit.
  const frame = focusFrame(byTheRightSpawn, landscape);
  const full = fullArenaFrame(landscape);
  assert.ok(frame.width <= full.width + 1e-9, 'no closer to nothing than the whole arena');
  for (const point of byTheRightSpawn) {
    assert.ok(Math.abs(point.x - frame.x) < frame.width / 2 - 1);
    assert.ok(Math.abs(point.y - frame.y) < frame.height / 2 - 1);
  }
});

test('a fight in the corner is centred on the fight, and a frame wider than the arena centres the arena', () => {
  // The old rule slid the frame back inside the map, which is why a corner fight was shown
  // from the middle of the map. Background beside the action is the price of centring it, and
  // it is bounded: the fit can never be wider than the zoom-out framing.
  const frame = focusFrame([{ x: 1.5, y: 1.5 }], FRAMING);
  assert.ok(Math.abs(frame.x - 1.5) < 1e-9, 'looking at the corner, not near it');
  assert.ok(Math.abs(frame.y - 1.5) < 1e-9);
  assert.ok(frame.width <= fullArenaFrame(FRAMING).width + 1e-9);

  // The one case the action does not decide: an axis the frame already covers whole. Sliding
  // that would push the arena off one side and show background on the other for nothing, so
  // the arena is centred instead — even bars, nothing cut off. A short map on a tall viewport
  // is exactly that: the fit needs twelve tiles of height to hold six tiles of width.
  const tall = { mapWidth: 40, mapHeight: 8, aspect: 0.5 };
  const covered = focusFrame([{ x: 1.5, y: 1.5 }], tall);
  assert.ok(covered.height >= tall.mapHeight + ARENA_MARGIN_TILES);
  assert.ok(Math.abs(covered.y - tall.mapHeight / 2) < 1e-9, 'the arena is centred');
  assert.ok(Math.abs(covered.x - 1.5) < 1e-9, 'and the fight still is, across');
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

test('the action drifting to one side re-aims, even though it never left the frame', () => {
  // The other half of the off-centre report, and the reason containment alone is not the
  // whole deadband: a wipe on one flank reshapes the fitted box without moving it out of
  // frame, so the survivors end up sitting well right of centre — and stay there for the
  // rest of the replay, because nothing ever escapes and the span barely moved.
  const committed = focusFrame(
    [
      { x: 10, y: 9 },
      { x: 30, y: 15 },
    ],
    FRAMING,
  );
  const oneFlankLeft = focusFrame(
    [
      { x: 15.5, y: 9 },
      { x: 29.5, y: 15 },
    ],
    FRAMING,
  );
  // Still inside the committed frame, and still wide enough that "showing mostly floor" does
  // not fire either — so the drift is the only thing that can ask for a re-aim.
  assert.ok(
    oneFlankLeft.x + oneFlankLeft.width / 2 < committed.x + committed.width / 2,
    'contained',
  );
  assert.ok(oneFlankLeft.width > committed.width * 0.7, 'and not a pull-in');
  assert.equal(frameEscapes(committed, oneFlankLeft), true, 'so the camera re-centres');
  assert.equal(
    frameEscapes(committed, oneFlankLeft, 0.05, 1),
    false,
    'which containment on its own would never have done',
  );
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
  assert.ok(camera.aimed.width >= 6, 'a pinch cannot get closer than the fit can');
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

test('following one team centres that team, with the other side out of frame', () => {
  // The worst case for the old clamp, and the one a viewer reaches by tapping a bot: a team
  // is dug in at its own end of the map, so the box it fits is *always* near an edge. Fitting
  // it used to slide the frame back over the middle of the arena, which put the side being
  // followed at the rim of the screen — while both sides' fits looked correct in isolation.
  const framing = {
    mapWidth: frontline.map.width,
    mapHeight: frontline.map.height,
    aspect: 16 / 10,
  };
  const viewport = { width: 640, height: 400 };
  const time = 2.4;

  for (const teamId of [0, 1]) {
    const unit = frontline.units.find((candidate) => candidate.teamId === teamId)!;
    const mine = focusPointsAt(
      frontline,
      time,
      unit.unitKey as ReplayStableUnitKey,
    );
    assertCentred(mine, framing, viewport, `team ${teamId}`);

    // And it really is that team's box: the opposition is off-screen, not merely off-centre.
    const frame = focusFrame(mine, framing);
    const opponent = frontline.units.find(
      (candidate) => candidate.teamId !== teamId,
    )!;
    for (const enemy of focusPointsAt(
      frontline,
      time,
      opponent.unitKey as ReplayStableUnitKey,
    )) {
      assert.ok(
        Math.abs(enemy.x - frame.x) > frame.width / 2,
        `team ${teamId}'s fit excludes the other side`,
      );
    }
  }
});

test('the camera follows the fight rather than a fixed plan view', () => {
  const framing = {
    mapWidth: frontline.map.width,
    mapHeight: frontline.map.height,
    aspect: 16 / 10,
  };
  const full = fullArenaFrame(framing);
  // Both teams are dug in at opposite ends of a fifteen-tile map, so the honest fit for
  // everybody is the whole arena bar the margin — the camera does not invent a closer shot
  // than the action supports, which is the failure mode a following camera usually has.
  const everybody = focusFrame(focusPointsAt(frontline, 2.4, null), framing);
  assert.ok(
    everybody.width >= full.width * 0.95,
    `the honest fit for everybody is the arena (${everybody.width} of ${full.width})`,
  );
  assert.ok(everybody.width <= full.width + 1e-9);

  // Follow one side and it closes right in, because that side's lives are two tiles apart.
  const zero = frontline.units.find((unit) => unit.teamId === 0)!;
  const side = focusFrame(
    focusPointsAt(frontline, 2.4, zero.unitKey as ReplayStableUnitKey),
    framing,
  );
  assert.ok(side.width < full.width * 0.7, `closed in (${side.width})`);
  assert.ok(side.x < full.x, 'and it is looking at that side of the map');
});

test('camera targets consume continuous rendered poses rather than snapped tiles', () => {
  const tickIndex = frontline.ticks.findIndex((tick) =>
    tick.before.actors.some((before) => {
      const after = tick.after.actors.find(
        (candidate) => candidate.actorKey === before.actorKey,
      );
      return after &&
        (after.position.x !== before.position.x ||
          after.position.y !== before.position.y);
    }),
  );
  assert.ok(tickIndex >= 0);
  const moving = frontline.ticks[tickIndex]!.before.actors.find((before) => {
    const after = frontline.ticks[tickIndex]!.after.actors.find(
      (candidate) => candidate.actorKey === before.actorKey,
    );
    return after &&
      (after.position.x !== before.position.x ||
        after.position.y !== before.position.y);
  });
  assert.ok(moving);

  const samples = [0.25, 0.5, 0.75].map((fraction) => {
    const time = tickIndex + fraction;
    const pose = posesAt(frontline, time).find(
      (candidate) => candidate.actorKey === moving.actorKey,
    );
    assert.ok(pose);
    const points = focusPointsAt(frontline, time, null);
    assert.ok(
      points.some(
        (point) =>
          Math.abs(point.x - (pose.x + 0.5)) < 1e-9 &&
          Math.abs(point.y - (pose.y + 0.5)) < 1e-9,
      ),
      'the camera receives the same fractional body position as both renderers',
    );
    return pose;
  });
  assert.notDeepEqual(
    { x: samples[0]!.x, y: samples[0]!.y },
    { x: samples[2]!.x, y: samples[2]!.y },
    'the target moves within a tick instead of waiting for its boundary',
  );
});
