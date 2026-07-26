import assert from 'node:assert/strict';
import test, { beforeEach } from 'node:test';
import {
  pendingDecodes,
  resetDecodeTracking,
  subscribeToDecodes,
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

beforeEach(resetDecodeTracking);

test('an already-decoded image never gates playback', () => {
  trackDecode(fakeImage(true));
  assert.equal(pendingDecodes(), 0);
});

test('pending decodes hold, and loading releases', () => {
  const first = fakeImage();
  const second = fakeImage();
  trackDecode(first);
  trackDecode(second);
  assert.equal(pendingDecodes(), 2);

  first.fire('load');
  assert.equal(pendingDecodes(), 1);
  second.fire('load');
  assert.equal(pendingDecodes(), 0);
});

test('a failed image releases too', () => {
  // A missing atlas is a rendering problem, not a reason to hold the viewer on a loading
  // screen forever. This is the difference between degraded and hung.
  const image = fakeImage();
  trackDecode(image);
  image.fire('error');
  assert.equal(pendingDecodes(), 0);
});

test('an image settling twice only counts once', () => {
  // Browsers can fire both, and double-decrementing would report readiness before it is
  // true — releasing playback while other textures are still decoding.
  const image = fakeImage();
  trackDecode(fakeImage());
  trackDecode(image);
  image.fire('load');
  image.fire('error');
  assert.equal(pendingDecodes(), 1);
});

test('subscribers see every change', () => {
  const seen: number[] = [];
  const unsubscribe = subscribeToDecodes((pending) => seen.push(pending));
  const image = fakeImage();
  trackDecode(image);
  image.fire('load');
  unsubscribe();
  assert.deepEqual(seen, [1, 0]);
});
