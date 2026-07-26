/**
 * Whether the images the arena is about to draw have finished decoding.
 *
 * They were never awaited. `loadImage` fired `new Image()` and returned it, nothing
 * tracked the result, and playback began at tick 0 regardless — so a replay started while
 * atlases were still decoding and the arena popped in over the opening seconds. On a cold
 * load of a 4096 master that is a long time to be watching a match that has already begun.
 *
 * Counting rather than promising: images are requested lazily, per theme, as the renderer
 * first touches them, so the set is not known up front. A count that rises and falls is
 * the honest shape of that.
 */

type Listener = (pending: number) => void;

let pending = 0;
const listeners = new Set<Listener>();

function notify(): void {
  for (const listener of listeners) listener(pending);
}

/**
 * Register an image whose decode gates playback.
 *
 * Errors settle it too. A missing atlas is a rendering problem, not a reason to hold the
 * viewer on a loading screen forever — the arena draws without it and says so elsewhere.
 */
export function trackDecode(image: HTMLImageElement): HTMLImageElement {
  if (image.complete) return image;

  pending += 1;
  notify();

  let settled = false;
  const settle = () => {
    if (settled) return;
    settled = true;
    pending -= 1;
    notify();
  };
  image.addEventListener('load', settle, { once: true });
  image.addEventListener('error', settle, { once: true });
  return image;
}

export function pendingDecodes(): number {
  return pending;
}

export function subscribeToDecodes(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Test seam: readiness is module state, and suites must not inherit each other's. */
export function resetDecodeTracking(): void {
  pending = 0;
  listeners.clear();
}
