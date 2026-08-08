import assert from 'node:assert/strict';
import test from 'node:test';
import { viewerGate } from '../src/render/viewerReadiness.ts';

/**
 * The gate between a cold page and a running match.
 *
 * Every case here is one a developer cannot see on their own machine: a warm cache makes
 * the loading state last a frame, a local server makes the model fetch instant, and the
 * failure being guarded against — a viewer that reports ready and autoplays over a black
 * arena — looks identical to a working one unless the cache is cold.
 */

const base = {
  assetsReady: false,
  dimensional: true,
  sceneReady: false,
  live: false,
  started: false,
};

test('a cold 3D viewer is loading, and its play control cannot be pressed', () => {
  const gate = viewerGate(base);
  assert.equal(gate.ready, false);
  assert.equal(gate.overlay, 'loading');
  assert.equal(gate.playable, false);
});

test('counted assets alone do not make the 3D viewer ready', () => {
  // This is the exact hole the old gate had. `useAssetReadiness` starts at zero because
  // nothing has been *requested* yet — the renderer is still a lazy chunk downloading — so
  // "no pending assets" read as "ready" before a single model had been asked for.
  const gate = viewerGate({ ...base, assetsReady: true, sceneReady: false });
  assert.equal(gate.ready, false);
  assert.equal(gate.overlay, 'loading');
  assert.equal(gate.playable, false);
});

test('a drawn scene alone does not make it ready either', () => {
  const gate = viewerGate({ ...base, assetsReady: false, sceneReady: true });
  assert.equal(gate.ready, false);
  assert.equal(gate.playable, false);
});

test('assets plus a drawn frame is ready, and offers the play button', () => {
  const gate = viewerGate({ ...base, assetsReady: true, sceneReady: true });
  assert.equal(gate.ready, true);
  assert.equal(gate.overlay, 'ready');
  assert.equal(gate.playable, true);
});

test('the Canvas2D viewer waits only on its atlases', () => {
  // The CLI's self-contained artifact and a device with no WebGL context both land here.
  // There is no scene to draw and no chunk to fetch, so demanding `sceneReady` would hold
  // the play button shut forever.
  assert.equal(
    viewerGate({ ...base, dimensional: false, assetsReady: true }).playable,
    true,
  );
  assert.equal(
    viewerGate({ ...base, dimensional: false, assetsReady: false }).overlay,
    'loading',
  );
});

test('once playback has been asked for the overlay never returns', () => {
  // Pausing is not un-starting. A scrim reappearing over a paused match — or over one the
  // transport started without touching the button — would be worse than none at all.
  const gate = viewerGate({
    ...base,
    assetsReady: true,
    sceneReady: true,
    started: true,
  });
  assert.equal(gate.overlay, 'hidden');
  assert.equal(gate.playable, false);
  assert.equal(gate.ready, true);
});

test('a live broadcast is never gated behind a button', () => {
  // The clock is the server's and every viewer is on the same tick, so there is nothing to
  // press — but a cold arena still says it is loading rather than showing black.
  const loading = viewerGate({ ...base, live: true });
  assert.equal(loading.overlay, 'loading');
  assert.equal(loading.playable, false);

  const running = viewerGate({
    ...base,
    live: true,
    assetsReady: true,
    sceneReady: true,
  });
  assert.equal(running.overlay, 'hidden');
  assert.equal(running.playable, false);
});
