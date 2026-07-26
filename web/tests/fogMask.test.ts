import assert from 'node:assert/strict';
import test from 'node:test';
import { drawFogMask } from '../src/render/fogMask.ts';

/**
 * A recording 2D context. The mask is built by compositing, so what matters is the shape
 * of the operations — what gets cleared, and at what extent — not the pixels, which need a
 * real canvas to inspect.
 */
function recorder() {
  const calls: { op: string; args: number[]; composite: string }[] = [];
  const ctx = {
    filter: '',
    fillStyle: '',
    globalCompositeOperation: 'source-over',
    canvas: {},
    fillRect(...args: number[]) {
      calls.push({ op: 'fillRect', args, composite: this.globalCompositeOperation });
    },
    drawImage(...rest: unknown[]) {
      calls.push({ op: 'drawImage', args: rest.slice(1) as number[], composite: 'source-over' });
    },
    save() {},
    restore() {},
    getContext() {
      return ctx;
    },
  };
  return { ctx, calls };
}

const geometry = { px: (x: number) => 10 + x * 20, py: (y: number) => 5 + y * 20, tile: 20, wallGutter: 4 };

/** No OffscreenCanvas and no document in Node, so the fallback path runs. */
test('without an offscreen surface it still fogs, tile by tile', () => {
  const { ctx, calls } = recorder();
  drawFogMask(ctx as unknown as CanvasRenderingContext2D, geometry, {
    mapWidth: 2,
    mapHeight: 1,
    visible: new Set(['0,0']),
    isWall: () => false,
  });

  // Fog is information about what the bot knew — an environment without filters should
  // get hard edges, never nothing.
  const fills = calls.filter((call) => call.op === 'fillRect');
  assert.equal(fills.length, 1, 'only the unseen tile is shrouded');
  assert.deepEqual(fills[0].args, [30, 5, 20, 20]);
});

test('a zero-sized arena draws nothing rather than throwing', () => {
  const { ctx, calls } = recorder();
  drawFogMask(ctx as unknown as CanvasRenderingContext2D, geometry, {
    mapWidth: 0,
    mapHeight: 0,
    visible: new Set(),
    isWall: () => false,
  });
  assert.equal(calls.length, 0);
});

test('every tile visible means nothing is shrouded', () => {
  const { ctx, calls } = recorder();
  drawFogMask(ctx as unknown as CanvasRenderingContext2D, geometry, {
    mapWidth: 2,
    mapHeight: 1,
    visible: new Set(['0,0', '1,0']),
    isWall: () => false,
  });
  assert.equal(calls.filter((call) => call.op === 'fillRect').length, 0);
});
