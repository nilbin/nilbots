import assert from 'node:assert/strict';
import test, { beforeEach } from 'node:test';
import {
  beginAsset,
  pendingAssets,
  resetAssetTracking,
  subscribeToAssets,
  trackDecode,
} from '../src/render/assetReadiness.ts';

/**
 * A minimal stand-in for HTMLImageElement: `complete`, plus load/error listeners. The
 * real thing needs a DOM, and what matters here is the settling logic.
 */
function fakeImage(complete = false) {
  const listeners = new Map<string, () => void>();
  return {
    complete,
    addEventListener(event: string, handler: () => void) {
      listeners.set(event, handler);
    },
    fire(event: 'load' | 'error') {
      listeners.get(event)?.();
    },
  } as unknown as HTMLImageElement & { fire(event: 'load' | 'error'): void };
}

beforeEach(resetAssetTracking);

test('an already-decoded image never gates playback', () => {
  trackDecode(fakeImage(true));
  assert.equal(pendingAssets(), 0);
});

test('pending decodes hold, and loading releases', () => {
  const first = fakeImage();
  const second = fakeImage();
  trackDecode(first);
  trackDecode(second);
  assert.equal(pendingAssets(), 2);

  first.fire('load');
  assert.equal(pendingAssets(), 1);
  second.fire('load');
  assert.equal(pendingAssets(), 0);
});

test('a failed image releases too', () => {
  // A missing atlas is a rendering problem, not a reason to hold the viewer on a loading
  // screen forever. This is the difference between degraded and hung.
  const image = fakeImage();
  trackDecode(image);
  image.fire('error');
  assert.equal(pendingAssets(), 0);
});

test('an image settling twice only counts once', () => {
  // Browsers can fire both, and double-decrementing would report readiness before it is
  // true — releasing playback while other textures are still decoding.
  const image = fakeImage();
  trackDecode(fakeImage());
  trackDecode(image);
  image.fire('load');
  image.fire('error');
  assert.equal(pendingAssets(), 1);
});

test('subscribers see every change', () => {
  const seen: number[] = [];
  const unsubscribe = subscribeToAssets((pending) => seen.push(pending));
  const image = fakeImage();
  trackDecode(image);
  image.fire('load');
  unsubscribe();
  assert.deepEqual(seen, [1, 0]);
});

test('a model hold gates playback until it is released', () => {
  // A GLB is not an image and never went through `trackDecode`, which is how the striker's
  // 4.5 MB model stayed outside the count entirely — the viewer reported ready and started
  // a match whose machines arrived seconds later.
  const release = beginAsset();
  assert.equal(pendingAssets(), 1);
  release();
  assert.equal(pendingAssets(), 0);
});

test('releasing a hold twice only counts once', () => {
  // Loaders settle in more than one way — three fires `itemError` and `itemEnd` for the
  // same failed file — and a double release would report readiness while other work ran.
  const release = beginAsset();
  beginAsset();
  release();
  release();
  assert.equal(pendingAssets(), 1);
});

test('images and models share one count', () => {
  // The gate is a single question. A viewer waiting on a texture and a viewer waiting on a
  // model are the same viewer, and two counters would let one of them start early.
  const release = beginAsset();
  const image = fakeImage();
  trackDecode(image);
  assert.equal(pendingAssets(), 2);
  image.fire('load');
  assert.equal(pendingAssets(), 1);
  release();
  assert.equal(pendingAssets(), 0);
});
