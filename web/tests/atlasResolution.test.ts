import assert from 'node:assert/strict';
import test from 'node:test';
import { atlasContentPixels, preferredAtlasWidth } from '../src/render/atlasResolution.ts';

/**
 * These thresholds decide both how sharp the arena looks and how much memory it costs, and
 * both failures are quiet: too small reads as "the art is soft", too large reads as "the
 * tab died on my phone". Pin the device classes rather than the arithmetic.
 */

test('content resolution per atlas size', () => {
  // 16 columns of 192-content + 2x32-gutter cells.
  assert.equal(atlasContentPixels(1024), 48);
  assert.equal(atlasContentPixels(2048), 96);
  assert.equal(atlasContentPixels(4096), 192);
});

test('a phone takes the smallest bake', () => {
  // 64 MB per atlas is what killed mobile tabs; 48 content px covers a phone's tile size.
  assert.equal(preferredAtlasWidth(390, 500, 3), 1024);
  assert.equal(preferredAtlasWidth(844, 390, 2), 1024);
});

test('a retina laptop takes 2048, not the master', () => {
  // The point of the tolerance: exactly meeting demand would send an ordinary laptop to
  // the 4096 master for detail it cannot resolve, at four times the decoded memory.
  assert.equal(preferredAtlasWidth(1100, 800, 2), 2048);
  assert.equal(preferredAtlasWidth(1100, 800, 3), 2048);
  assert.equal(preferredAtlasWidth(1600, 1100, 2), 2048);
});

test('a large high-density display still gets the master', () => {
  assert.equal(preferredAtlasWidth(2560, 1600, 3), 4096);
});

test('no measurable viewport falls back to the master', () => {
  // Server render, or a test with no window: guessing small would ship soft art.
  assert.equal(preferredAtlasWidth(0, 0, 1), 4096);
});
