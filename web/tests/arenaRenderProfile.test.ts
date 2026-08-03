import assert from 'node:assert/strict';
import test from 'node:test';
import {
  DESKTOP_ARENA_RENDER_PROFILE,
  FULL_ARENA_RENDER_PROFILE,
  MOBILE_ARENA_RENDER_PROFILE,
  arenaWeightedPixelsPerSecond,
  createArenaFramePacer,
  selectArenaRenderProfile,
  takeArenaFrame,
} from './.harness/harness.entry.js';

const DESKTOP = {
  coarsePointer: false,
  maxTouchPoints: 0,
  viewportWidth: 1440,
  viewportHeight: 900,
};

test('phone rendering is selected by capabilities rather than user agent', () => {
  assert.equal(
    selectArenaRenderProfile({ ...DESKTOP, coarsePointer: true }).id,
    'mobile',
  );
  assert.equal(
    selectArenaRenderProfile({
      ...DESKTOP,
      maxTouchPoints: 5,
      viewportWidth: 844,
      viewportHeight: 390,
    }).id,
    'mobile',
  );
  assert.equal(selectArenaRenderProfile(DESKTOP).id, 'desktop');
});

test('explicit profile override makes same-build visual A/B deterministic', () => {
  assert.equal(
    selectArenaRenderProfile({
      ...DESKTOP,
      coarsePointer: true,
      override: 'full',
    }),
    FULL_ARENA_RENDER_PROFILE,
  );
  assert.equal(
    selectArenaRenderProfile({ ...DESKTOP, override: 'mobile' }),
    MOBILE_ARENA_RENDER_PROFILE,
  );
  assert.equal(
    selectArenaRenderProfile({ ...DESKTOP, override: 'desktop' }),
    DESKTOP_ARENA_RENDER_PROFILE,
  );
});

test('desktop preserves image quality without forcing unrestricted GPU work', () => {
  assert.deepEqual(DESKTOP_ARENA_RENDER_PROFILE, {
    id: 'desktop',
    presentationRateLimited: true,
    activeFramesPerSecond: 60,
    idleFramesPerSecond: 12,
    webglMaxPixelRatio: 2,
    canvasMaxPixelRatio: 2,
    shadowMapSize: 2048,
    powerPreference: 'default',
  });

  const desktopWork = arenaWeightedPixelsPerSecond(
    1440,
    900,
    DESKTOP_ARENA_RENDER_PROFILE,
    2,
    144,
  );
  const unrestrictedWork = arenaWeightedPixelsPerSecond(
    1440,
    900,
    FULL_ARENA_RENDER_PROFILE,
    2,
    144,
  );
  assert.equal(desktopWork / unrestrictedWork, 60 / 144);
});

test('mobile profile has a bounded sustained phone workload', () => {
  assert.deepEqual(MOBILE_ARENA_RENDER_PROFILE, {
    id: 'mobile',
    presentationRateLimited: true,
    activeFramesPerSecond: 30,
    idleFramesPerSecond: 12,
    webglMaxPixelRatio: 1.5,
    canvasMaxPixelRatio: 1.5,
    shadowMapSize: 1024,
    powerPreference: 'low-power',
  });

  // Landscape iPhone-class CSS viewport at DPR 3. The proxy charges both the color pass
  // and the complete live shadow-map pass; it deliberately does not assume tile culling.
  const mobileWork = arenaWeightedPixelsPerSecond(
    844,
    390,
    MOBILE_ARENA_RENDER_PROFILE,
    3,
  );
  const fullWork = arenaWeightedPixelsPerSecond(
    844,
    390,
    FULL_ARENA_RENDER_PROFILE,
    3,
  );
  assert.equal(mobileWork, 53_675_580);
  assert.equal(fullWork, 330_656_640);
  assert.ok(mobileWork <= 54_000_000);
  assert.ok(mobileWork / fullWork < 0.17);
});

test('frame limiter presents thirty evenly paced frames on 60 and 120 Hz clocks', () => {
  for (const refreshRate of [60, 120]) {
    const pacer = createArenaFramePacer();
    let presented = 0;
    const refreshFrames = refreshRate * 10;
    for (let index = 0; index < refreshFrames; index++) {
      const stamp = index * (1000 / refreshRate);
      if (takeArenaFrame(pacer, stamp, 30)) presented++;
    }
    assert.ok(
      presented >= 299 && presented <= 301,
      `${refreshRate} Hz presented ${presented} frames`,
    );
  }
});

test('desktop limiter presents at most sixty frames on high-refresh clocks', () => {
  for (const refreshRate of [60, 120, 144]) {
    const pacer = createArenaFramePacer();
    let presented = 0;
    for (let index = 0; index < refreshRate * 10; index++) {
      const stamp = index * (1000 / refreshRate);
      if (takeArenaFrame(pacer, stamp, 60)) presented++;
    }
    assert.ok(
      presented >= 599 && presented <= 601,
      `${refreshRate} Hz presented ${presented} frames`,
    );
  }
});

test('an early 60 Hz callback never creates alternating frame debt', () => {
  const pacer = createArenaFramePacer();
  const stamps = Array.from({ length: 600 }, (_, index) => index * 15.8);
  const presented = stamps.filter((stamp) => takeArenaFrame(pacer, stamp, 60));
  assert.equal(presented.length, 600);
});
