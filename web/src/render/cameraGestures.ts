import type { ArenaCamera, ArenaFraming } from './arenaCamera';

/**
 * Hand the camera to whoever is touching the screen — identically in both renderers.
 *
 * Written once because the override rule is a *contract*, not a renderer detail: the same
 * pinch has to mean the same thing whether the device gave us WebGL or not, and the
 * chrome's toggle has to be the only way back in both.
 *
 * **A one-finger drag is not a camera gesture**, and that is the whole of what makes this
 * safe on a phone. The arena fills the viewport there and a single finger on it is how the
 * page is scrolled and how a bot is selected; claiming it for panning would break both to
 * add a gesture nobody asked for. Two fingers are unambiguous — nothing else on the arena
 * wants them — so pinch-and-drag is the touch override, and a mouse gets wheel-zoom and
 * drag-pan because a pointer device has neither of those conflicts.
 */
export interface CameraGestureHost {
  /** The element gestures are read from. */
  element: HTMLElement;
  camera: ArenaCamera;
  /** The framing at this instant — the viewport changes shape, so it is asked per event. */
  framing: () => ArenaFraming;
  /** Pixels per tile at this instant, so a drag moves the ground under the finger. */
  scale: () => number;
  /** Called the first time a gesture takes the camera, so the chrome can say so. */
  onOverride: () => void;
}

export interface CameraGestures {
  detach: () => void;
  /**
   * Whether the gesture that just ended moved the camera.
   *
   * Selection and panning share the pointer, so the tap handler has to be able to ask
   * whether this was a tap at all — otherwise every drag also picks whatever it started on.
   */
  panned: () => boolean;
}

/** Attach the gestures. */
export function attachCameraGestures(host: CameraGestureHost): CameraGestures {
  const { element, camera } = host;
  const pointers = new Map<number, { x: number; y: number }>();
  let pinch: { distance: number; x: number; y: number } | null = null;
  let dragged = false;

  // Both automatic allegiances report the override, so the chrome can stop claiming the
  // camera is following. A follow is not the director, but it moves on its own just the
  // same, and a toggle still showing `following` after a hand took the camera off a body
  // is a control that lies about what it would undo.
  const took = () => {
    if (camera.auto || camera.followingSelection) host.onOverride();
  };

  const onWheel = (event: WheelEvent) => {
    event.preventDefault();
    took();
    // Exponential in the wheel delta, because zoom is multiplicative: a fixed step per
    // notch takes forever to cross the range at one end and overshoots it at the other.
    camera.zoom(Math.exp(-event.deltaY * 0.0016), host.framing());
  };

  const onPointerDown = (event: PointerEvent) => {
    pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    dragged = false;
    if (pointers.size === 2) pinch = measure();
  };

  const onPointerMove = (event: PointerEvent) => {
    const previous = pointers.get(event.pointerId);
    if (!previous) return;
    pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });

    if (pointers.size >= 2) {
      const now = measure();
      if (pinch && now && pinch.distance > 0) {
        event.preventDefault();
        took();
        const tile = host.scale();
        camera.zoom(now.distance / pinch.distance, host.framing());
        camera.pan(
          -(now.x - pinch.x) / tile,
          -(now.y - pinch.y) / tile,
          host.framing(),
        );
        dragged = true;
      }
      pinch = now;
      return;
    }

    // One pointer: a mouse or a pen drags the arena, a finger does not.
    if (event.pointerType === 'touch') return;
    const dx = event.clientX - previous.x;
    const dy = event.clientY - previous.y;
    if (!dragged && Math.hypot(dx, dy) < 1) return;
    event.preventDefault();
    took();
    const tile = host.scale();
    camera.pan(-dx / tile, -dy / tile, host.framing());
    dragged = true;
  };

  const onPointerUp = (event: PointerEvent) => {
    pointers.delete(event.pointerId);
    if (pointers.size < 2) pinch = null;
  };

  const measure = () => {
    const [first, second] = [...pointers.values()];
    if (!first || !second) return null;
    return {
      distance: Math.hypot(second.x - first.x, second.y - first.y),
      x: (first.x + second.x) / 2,
      y: (first.y + second.y) / 2,
    };
  };

  element.addEventListener('wheel', onWheel, { passive: false });
  element.addEventListener('pointerdown', onPointerDown);
  element.addEventListener('pointermove', onPointerMove);
  element.addEventListener('pointerup', onPointerUp);
  element.addEventListener('pointercancel', onPointerUp);
  element.addEventListener('pointerleave', onPointerUp);

  return {
    panned: () => dragged,
    detach: () => {
      element.removeEventListener('wheel', onWheel);
      element.removeEventListener('pointerdown', onPointerDown);
      element.removeEventListener('pointermove', onPointerMove);
      element.removeEventListener('pointerup', onPointerUp);
      element.removeEventListener('pointercancel', onPointerUp);
      element.removeEventListener('pointerleave', onPointerUp);
    },
  };
}
