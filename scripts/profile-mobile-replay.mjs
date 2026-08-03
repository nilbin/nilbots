#!/usr/bin/env node

/**
 * Measure sustained replay-renderer work in a phone-sized browser.
 *
 * This is a thermal proxy, not a claim to read device temperature. It measures the work
 * the renderer controls: presentation rate, backing-buffer area, shadow-pass area, draw
 * submissions, Canvas2D operations, and (in Chromium) main-thread task utilization.
 *
 * Usage:
 *   node scripts/profile-mobile-replay.mjs <url> [output.json] [seconds]
 *     [webgl|canvas2d] [chromium|webkit] [active|idle] [phone|desktop]
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium, webkit } = require('playwright');

const url = process.argv[2];
const output = process.argv[3] ?? 'sandbox/mobile-replay-profile.json';
const seconds = Number(process.argv[4] ?? 20);
const forceCanvas2d = process.argv[5] === 'canvas2d';
const browserName = process.argv[6] ?? 'webkit';
const activity = process.argv[7] ?? 'active';
const deviceKind = process.argv[8] ?? 'phone';
const browserType = { chromium, webkit }[browserName];

if (!url || !Number.isFinite(seconds) || seconds <= 0) {
  throw new Error(
    'usage: profile-mobile-replay.mjs <url> [output] [seconds] ' +
      '[webgl|canvas2d] [chromium|webkit] [active|idle] [phone|desktop]',
  );
}
if (!browserType) throw new Error('browser must be chromium or webkit');
if (!['active', 'idle'].includes(activity))
  throw new Error('activity must be active or idle');
if (!['phone', 'desktop'].includes(deviceKind))
  throw new Error('device must be phone or desktop');

const browser = await browserType.launch({ headless: true });
const context = await browser.newContext(
  deviceKind === 'phone'
    ? {
        viewport: { width: 844, height: 390 },
        screen: { width: 844, height: 390 },
        deviceScaleFactor: 3,
        isMobile: true,
        hasTouch: true,
      }
    : {
        viewport: { width: 1440, height: 900 },
        screen: { width: 1440, height: 900 },
        deviceScaleFactor: 2,
        isMobile: false,
        hasTouch: false,
      },
);

await context.addInitScript(({ forceCanvas2d }) => {
  const counters = {
    rafCallbacks: 0,
    webglClearPasses: 0,
    webglDrawCalls: 0,
    webglTriangles: 0,
    canvas2dFrames: 0,
    canvas2dOperations: 0,
  };
  Object.defineProperty(window, '__nilbotsProfile', { value: counters });

  if (forceCanvas2d) {
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (kind, ...args) {
      if (['webgl', 'webgl2', 'experimental-webgl'].includes(kind)) return null;
      return getContext.call(this, kind, ...args);
    };
  }

  const nativeRaf = window.requestAnimationFrame.bind(window);
  window.requestAnimationFrame = (callback) => nativeRaf((stamp) => {
    counters.rafCallbacks += 1;
    callback(stamp);
  });

  const patchWebGl = (prototype) => {
    if (!prototype || prototype.__nilbotsProfilePatched) return;
    Object.defineProperty(prototype, '__nilbotsProfilePatched', { value: true });
    for (const method of [
      'drawArrays',
      'drawElements',
      'drawArraysInstanced',
      'drawElementsInstanced',
    ]) {
      const original = prototype[method];
      if (typeof original !== 'function') continue;
      prototype[method] = function (...args) {
        counters.webglDrawCalls += 1;
        const count = Number(args[method.includes('Elements') ? 1 : 2] ?? 0);
        const instances = method.includes('Instanced')
          ? Number(args.at(-1) ?? 1)
          : 1;
        if (Number(args[0]) === this.TRIANGLES)
          counters.webglTriangles += Math.floor(count / 3) * instances;
        return original.apply(this, args);
      };
    }
    const clear = prototype.clear;
    if (typeof clear === 'function') {
      prototype.clear = function (...args) {
        counters.webglClearPasses += 1;
        return clear.apply(this, args);
      };
    }
  };
  patchWebGl(window.WebGLRenderingContext?.prototype);
  patchWebGl(window.WebGL2RenderingContext?.prototype);

  const canvas2d = window.CanvasRenderingContext2D?.prototype;
  if (canvas2d) {
    for (const method of [
      'clearRect',
      'drawImage',
      'fill',
      'fillRect',
      'fillText',
      'stroke',
      'strokeRect',
      'strokeText',
    ]) {
      const original = canvas2d[method];
      if (typeof original !== 'function') continue;
      canvas2d[method] = function (...args) {
        counters.canvas2dOperations += 1;
        if (method === 'clearRect') counters.canvas2dFrames += 1;
        return original.apply(this, args);
      };
    }
  }
}, { forceCanvas2d });

const page = await context.newPage();
const errors = [];
page.on('pageerror', (error) => errors.push(`page: ${error.message}`));
page.on('console', (message) => {
  if (message.type() === 'error') errors.push(`console: ${message.text()}`);
});
page.on('requestfailed', (request) =>
  errors.push(`request: ${request.url()} ${request.failure()?.errorText ?? ''}`));

const cdp = browserName === 'chromium'
  ? await context.newCDPSession(page)
  : null;
await cdp?.send('Performance.enable');
await page.goto(url, { waitUntil: 'domcontentloaded' });
await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 60_000 });
await page.getByRole('button', { name: 'Play match' }).click();
if (activity === 'idle') {
  await page.waitForTimeout(1_000);
  await page.getByRole('button', { name: 'Pause' }).click();
}
await page.waitForTimeout(5_000);

const beforeMetrics = cdp
  ? metricMap(await cdp.send('Performance.getMetrics'))
  : {};
await page.evaluate(() => {
  for (const key of Object.keys(window.__nilbotsProfile))
    window.__nilbotsProfile[key] = 0;
});
const started = Date.now();
await page.waitForTimeout(seconds * 1_000);
const elapsedSeconds = (Date.now() - started) / 1_000;
const afterMetrics = cdp
  ? metricMap(await cdp.send('Performance.getMetrics'))
  : {};
const counters = await page.evaluate(() => ({ ...window.__nilbotsProfile }));
const canvas = await page
  .getByRole('region', { name: 'Arena' })
  .locator('canvas')
  .evaluate((node) => {
    const gl = node.getContext('webgl2') ?? node.getContext('webgl');
    const debug = gl?.getExtension('WEBGL_debug_renderer_info');
    return {
      kind: node.getContext('2d') ? '2d' : 'webgl',
      width: node.width,
      height: node.height,
      clientWidth: node.clientWidth,
      clientHeight: node.clientHeight,
      devicePixelRatio: window.devicePixelRatio,
      coarsePointer: matchMedia('(pointer: coarse)').matches,
      renderProfile: node.dataset.renderProfile ?? null,
      presentationRateLimited: node.dataset.rateLimited === 'true',
      activeFramesPerSecond: Number(node.dataset.activeFps || 0),
      idleFramesPerSecond: Number(node.dataset.idleFps || 0),
      pixelRatio: Number(node.dataset.pixelRatio || 0),
      shadowMapSize: Number(node.dataset.shadowMapSize || 0),
      renderer: gl && (
        debug
          ? gl.getParameter(debug.UNMASKED_RENDERER_WEBGL)
          : gl.getParameter(gl.RENDERER)
      ),
    };
  });

mkdirSync(path.dirname(output), { recursive: true });
await page.screenshot({ path: output.replace(/\.json$/, '.png') });

// Three performs one clear for the shadow map and one for the color pass. Canvas2D owns
// one clearRect per complete frame. Keep the raw counters beside this derived value so a
// renderer topology change is visible rather than silently absorbed by the metric.
const renderedFrames = canvas.kind === 'webgl'
  ? counters.webglClearPasses / 2
  : counters.canvas2dFrames;
const renderedFramesPerSecond = renderedFrames / elapsedSeconds;
const colorPixelsPerSecond =
  canvas.width * canvas.height * renderedFramesPerSecond;
const shadowPixelsPerSecond = canvas.kind === 'webgl'
  ? canvas.shadowMapSize ** 2 * renderedFramesPerSecond
  : 0;
const delta = (name) => (afterMetrics[name] ?? 0) - (beforeMetrics[name] ?? 0);
const result = {
  url,
  browser: browserName,
  device: deviceKind,
  activity,
  elapsedSeconds,
  canvas,
  counters,
  rates: {
    renderedFramesPerSecond,
    rafCallbacksPerSecond: counters.rafCallbacks / elapsedSeconds,
    webglDrawCallsPerSecond: counters.webglDrawCalls / elapsedSeconds,
    webglTrianglesPerSecond: counters.webglTriangles / elapsedSeconds,
    canvas2dOperationsPerSecond: counters.canvas2dOperations / elapsedSeconds,
    colorPixelsPerSecond,
    shadowPixelsPerSecond,
    weightedPixelsPerSecond: colorPixelsPerSecond + shadowPixelsPerSecond,
  },
  cpu: cdp ? {
    taskSeconds: delta('TaskDuration'),
    scriptSeconds: delta('ScriptDuration'),
    layoutSeconds: delta('LayoutDuration'),
    taskUtilization: delta('TaskDuration') / elapsedSeconds,
  } : null,
  errors,
};

writeFileSync(output, `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result, null, 2));
await browser.close();

function metricMap(response) {
  return Object.fromEntries(
    response.metrics.map((metric) => [metric.name, metric.value]),
  );
}
