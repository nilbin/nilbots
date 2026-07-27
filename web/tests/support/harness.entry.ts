/**
 * Renderer entry for golden-frame tests.
 *
 * `arenaThemes` uses `import.meta.glob`, which only exists inside Vite — so the renderer
 * cannot be imported by bare Node. This entry is built through Vite's SSR pipeline, which
 * resolves the globs, producing a bundle a test can import directly.
 */
export { drawArena } from '../../src/render/drawArena';
export {
  boltsAt,
  posesAt,
  spentBoltsAt,
} from '../../src/render/interpolate';
export { createPresenter } from '../../src/replayPresentation';
export { buildActors } from '../../src/render3d/arenaActors';
export { buildOverlays } from '../../src/render3d/arenaOverlays';
