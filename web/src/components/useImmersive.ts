import { useCallback, useEffect, useState } from 'react';

/**
 * Fill the screen with the arena.
 *
 * Two mechanisms, because one is not available everywhere:
 *
 *  - **Fullscreen API** where it exists — desktop browsers and Android. Real fullscreen,
 *    browser chrome gone, and orientation can be locked to landscape.
 *  - **A CSS immersive mode** everywhere else. `Element.requestFullscreen` does not exist
 *    on **iPhone Safari** at all (iPad has it; iPhone exposes it only for `<video>`), so
 *    the single most likely device for "watch this on my phone" cannot do real fullscreen.
 *    Pinning the arena to the viewport with `100dvh` and hiding the page chrome gets most
 *    of the way there, and `dvh` is what makes it survive Safari's collapsing toolbars.
 *
 * Orientation lock is best-effort for the same reason: Android honours it, iOS ignores it,
 * and a rejected promise there is expected rather than a failure worth surfacing.
 */
export interface Immersive {
  active: boolean;
  /** True when the browser gave real fullscreen rather than the CSS fallback. */
  native: boolean;
  enter: (element: HTMLElement | null) => void;
  exit: () => void;
  toggle: (element: HTMLElement | null) => void;
}

export function useImmersive(): Immersive {
  const [active, setActive] = useState(false);
  const [native, setNative] = useState(false);

  // Leaving fullscreen by Escape or a system gesture never calls our exit path, so the
  // browser's own event is the source of truth for whether we are still in it.
  useEffect(() => {
    const sync = () => {
      const isNative = Boolean(document.fullscreenElement);
      setNative(isNative);
      if (!isNative && nativeSupported()) setActive(false);
    };
    document.addEventListener('fullscreenchange', sync);
    return () => document.removeEventListener('fullscreenchange', sync);
  }, []);

  const enter = useCallback((element: HTMLElement | null) => {
    setActive(true);
    if (!element || !nativeSupported()) return;
    void element.requestFullscreen?.({ navigationUI: 'hide' }).then(
      () => {
        setNative(true);
        // Landscape suits a wide arena; a phone held upright wastes most of it. Android
        // honours this, iOS rejects it, and that rejection is expected rather than an error.
        void (
          screen.orientation as ScreenOrientation & { lock?: (o: string) => Promise<void> }
        )
          .lock?.('landscape')
          .catch(() => undefined);
      },
      () => {
        // Denied, usually an untrusted gesture. The CSS mode still applies.
      },
    );
  }, []);

  const exit = useCallback(() => {
    setActive(false);
    setNative(false);
    (screen.orientation as ScreenOrientation & { unlock?: () => void }).unlock?.();
    if (document.fullscreenElement) void document.exitFullscreen?.().catch(() => undefined);
  }, []);

  const toggle = useCallback(
    (element: HTMLElement | null) => (active ? exit() : enter(element)),
    [active, enter, exit],
  );

  return { active, native, enter, exit, toggle };
}

function nativeSupported(): boolean {
  return (
    typeof document !== 'undefined' &&
    typeof document.documentElement.requestFullscreen === 'function'
  );
}
