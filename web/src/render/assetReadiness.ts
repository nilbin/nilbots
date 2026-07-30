/**
 * Whether everything the arena is about to draw has arrived.
 *
 * None of it was ever awaited. `loadImage` fired `new Image()` and returned it, nothing
 * tracked the result, and playback began at tick 0 regardless — so a replay started while
 * atlases were still decoding and the arena popped in over the opening seconds. On a cold
 * load of a 4096 master that is a long time to be watching a match that has already begun.
 *
 * Textures were only half of it. The 3D viewer also fetches authored GLB models, the
 * largest single files it asks for, and those were outside the count entirely: the arena
 * reported itself ready, the transport started, and the machines faded in over a black
 * field several seconds later. Models, textures and decoded audio all hold the same
 * counter now, so "ready" means ready rather than "the pictures are in".
 *
 * Counting rather than promising: assets are requested lazily, per theme and per look, as
 * the renderer first touches them, so the set is not known up front. A count that rises
 * and falls is the honest shape of that.
 */

type Listener = (pending: number) => void;

let pending = 0;
const listeners = new Set<Listener>();

function notify(): void {
  for (const listener of listeners) listener(pending);
}

/**
 * Hold playback open for one piece of asynchronous work, and hand back the release.
 *
 * The generic form of `trackDecode`, and the reason this module is no longer only about
 * images: a GLB is a bigger stall than any texture — the striker's model is the largest
 * single file the viewer fetches — and a bot that pops into existence a third of the way
 * through the match is the same failure as an atlas that does. Anything that must be on
 * screen before the first tick takes a hold here: models, textures, decoded audio.
 *
 * The release is idempotent, because the ways these settle are not uniform — a loader can
 * report both an error and a completion for one item, and double-releasing would report
 * readiness while other work is still outstanding.
 */
export function beginAsset(): () => void {
  pending += 1;
  notify();

  let settled = false;
  return () => {
    if (settled) return;
    settled = true;
    pending -= 1;
    notify();
  };
}

/**
 * Register an image whose decode gates playback.
 *
 * Errors settle it too. A missing atlas is a rendering problem, not a reason to hold the
 * viewer on a loading screen forever — the arena draws without it and says so elsewhere.
 */
export function trackDecode(image: HTMLImageElement): HTMLImageElement {
  if (image.complete) return image;

  const settle = beginAsset();
  image.addEventListener('load', settle, { once: true });
  image.addEventListener('error', settle, { once: true });
  return image;
}

export function pendingAssets(): number {
  return pending;
}

export function subscribeToAssets(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Test seam: readiness is module state, and suites must not inherit each other's. */
export function resetAssetTracking(): void {
  pending = 0;
  listeners.clear();
}
