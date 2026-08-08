/**
 * When the viewer is ready, and what it shows until it is.
 *
 * A state machine rather than four booleans spread through `Viewer`, for the reason the
 * wake lock is one: every interesting case here is invisible on the machine that wrote it.
 * A warm cache makes the loading state last a single frame, a local server makes the model
 * fetch instant, and a developer who never sees the gate cannot tell a working one from a
 * gate that reports ready while the arena is still black — which is exactly how the viewer
 * shipped, autoplaying a match nobody could see yet.
 *
 * Two inputs are easy to conflate and must not be. **Assets** are counted things — models,
 * textures, cues — and the count is authoritative once the renderer has started asking for
 * them. **The scene** is whether the WebGL renderer exists and has drawn; nothing counts it,
 * because a lazy chunk and a first `render()` are not fetches. Readiness needs both, and
 * only the second is conditional on WebGL actually being the renderer in use.
 */

export type ViewerOverlay = 'hidden' | 'loading' | 'ready';

export interface ViewerGate {
  /** Everything the opening frame needs has arrived. */
  ready: boolean;
  /** What to draw over the arena, if anything. */
  overlay: ViewerOverlay;
  /** Whether the play control may be pressed. */
  playable: boolean;
}

export function viewerGate({
  assetsReady,
  dimensional,
  sceneReady,
  live,
  started,
}: {
  assetsReady: boolean;
  /** WebGL is the renderer. Canvas2D draws from the counted atlases alone. */
  dimensional: boolean;
  /** The 3D renderer has put a frame on screen. Meaningless while `dimensional` is false. */
  sceneReady: boolean;
  /** A server clock owns presentation time; there is no transport and no play button. */
  live: boolean;
  /** Playback has been asked for, by any route. */
  started: boolean;
}): ViewerGate {
  const ready = assetsReady && (!dimensional || sceneReady);
  // A live broadcast is never gated behind a button — every viewer is on the same tick and
  // there is nothing to press — so it shows a plain indicator and nothing once loaded.
  if (live) {
    return { ready, overlay: ready ? 'hidden' : 'loading', playable: false };
  }
  // The invitation is offered once. After that the transport owns the clock, and a scrim
  // over a running match would be worse than no scrim at all.
  if (started) return { ready, overlay: 'hidden', playable: false };
  return {
    ready,
    overlay: ready ? 'ready' : 'loading',
    playable: ready,
  };
}
