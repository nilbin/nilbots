import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Fill the screen with the arena.
 *
 * **On a touch device the trigger is the device itself.** Turning a phone sideways is
 * already the gesture for "give me the big picture", and the arena is the one thing here
 * that is wider than it is tall, so landscape means full screen and portrait means the
 * page. That leaves nothing to press and nothing to remember pressing — which is why
 * there is no toggle on a phone (`offersToggle`), and no exit button once the device put
 * us here (`automatic`). The way out is to turn the phone back.
 *
 * Forcing the opposite — a button that demands landscape — was tried and cannot work.
 * iPhone Safari has no Screen Orientation lock API and no `requestFullscreen` outside
 * `<video>`, so nothing the page does can make iOS turn itself.
 *
 * A pointer, on the other hand, has no orientation to read, so desktop keeps the button
 * and real fullscreen.
 *
 * Two mechanisms underneath, because one is missing where it matters most:
 *
 *  - **Fullscreen API** — desktop and Android. Browser chrome gone.
 *  - **A CSS immersive mode** everywhere else, which in practice means iPhone Safari: the
 *    single most likely device for "watch this on my phone" and the one that cannot do
 *    real fullscreen at all. Pinning the arena to the viewport with `100dvh` and hiding
 *    the page chrome gets most of the way there, and `dvh` is what makes it survive
 *    Safari's collapsing toolbars.
 */
export interface Immersive {
  active: boolean;
  /** True when the browser gave real fullscreen rather than the CSS fallback. */
  native: boolean;
  /** True when the device's own orientation put us here, rather than the button. */
  automatic: boolean;
  /** Whether to offer a toggle at all. Pointer devices only — a phone has its orientation. */
  offersToggle: boolean;
  enter: (element: HTMLElement | null) => void;
  exit: () => void;
  toggle: (element: HTMLElement | null) => void;
  /**
   * A user gesture arrived while the device had put us in immersive mode. Android grants
   * real fullscreen only from a gesture, and an orientation change is not one, so the
   * first touch is the earliest chance to upgrade out of the CSS fallback. Costs nothing
   * where fullscreen is unavailable or already held.
   */
  promote: (element: HTMLElement | null) => void;
}

export function useImmersive(): Immersive {
  const coarse = useMediaQuery('(pointer: coarse)');
  const landscape = useMediaQuery('(orientation: landscape)');
  const [manual, setManual] = useState(false);
  const [native, setNative] = useState(false);
  const promoted = useRef(false);

  const automatic = coarse && landscape;
  const active = automatic || manual;

  // Leaving fullscreen by Escape or a system gesture never calls our exit path, so the
  // browser's own event is the source of truth for whether we are still in it.
  useEffect(() => {
    const sync = () => {
      const isNative = Boolean(document.fullscreenElement);
      setNative(isNative);
      if (!isNative && nativeSupported()) setManual(false);
    };
    document.addEventListener('fullscreenchange', sync);
    return () => document.removeEventListener('fullscreenchange', sync);
  }, []);

  // Turning the phone back is the exit, so it has to release real fullscreen too — not
  // just the CSS mode — or Android would keep the arena over the page after the reason
  // for it went away.
  useEffect(() => {
    if (automatic) return;
    promoted.current = false;
    if (!manual && document.fullscreenElement) {
      void document.exitFullscreen?.().catch(() => undefined);
    }
  }, [automatic, manual]);

  const enter = useCallback((element: HTMLElement | null) => {
    setManual(true);
    if (!element || !nativeSupported()) return;
    void element.requestFullscreen?.({ navigationUI: 'hide' }).then(
      () => setNative(true),
      () => undefined, // Denied, usually an untrusted gesture. The CSS mode still applies.
    );
  }, []);

  const exit = useCallback(() => {
    setManual(false);
    setNative(false);
    if (document.fullscreenElement) void document.exitFullscreen?.().catch(() => undefined);
  }, []);

  const toggle = useCallback(
    (element: HTMLElement | null) => (active ? exit() : enter(element)),
    [active, enter, exit],
  );

  const promote = useCallback(
    (element: HTMLElement | null) => {
      if (!automatic || promoted.current) return;
      if (!element || !nativeSupported() || document.fullscreenElement) return;
      // Once per activation: a browser that refuses is not going to change its mind, and
      // retrying on every touch would spend the whole session asking.
      promoted.current = true;
      void element.requestFullscreen?.({ navigationUI: 'hide' }).then(
        () => setNative(true),
        () => undefined,
      );
    },
    [automatic],
  );

  return { active, native, automatic, offersToggle: !coarse, enter, exit, toggle, promote };
}

function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() =>
    typeof window === 'undefined' ? false : window.matchMedia(query).matches,
  );
  useEffect(() => {
    const media = window.matchMedia(query);
    const sync = () => setMatches(media.matches);
    sync();
    media.addEventListener('change', sync);
    return () => media.removeEventListener('change', sync);
  }, [query]);
  return matches;
}

function nativeSupported(): boolean {
  return (
    typeof document !== 'undefined' &&
    typeof document.documentElement.requestFullscreen === 'function'
  );
}
