import { useCallback, useEffect, useState } from 'react';
import { useWindowDimensions } from 'react-native';
import * as ScreenOrientation from 'expo-screen-orientation';

/**
 * How the arena presents itself: which way up, and whether the chrome is showing.
 *
 * **Turning the phone is the full-screen control.** Sideways means the arena and nothing
 * else; upright means the arena plus the cards, transport and provenance that explain it.
 * Forcing the rotation was tried and reverted — it yanks the phone sideways whether or not
 * that is what the viewer wanted, and leaves the bot cards nowhere to go. The device asks;
 * this answers.
 *
 * `landscape` comes from the window rather than an orientation listener because it is a
 * layout question. A foldable or an iPad split view is landscape-shaped without the device
 * having rotated at all, and the box is what the renderer letterboxes into either way.
 */
export interface ArenaPresentation {
  landscape: boolean;
  chromeVisible: boolean;
  /** A touch landed: bring the chrome back. */
  revealChrome: () => void;
}

export function useArenaPresentation({
  visible,
  playing,
}: {
  visible: boolean;
  playing: boolean;
}): ArenaPresentation {
  const { width, height } = useWindowDimensions();
  const landscape = width > height;
  const [chromeVisible, setChromeVisible] = useState(true);

  /**
   * The arena is the one screen that may rotate; portrait again on close.
   *
   * Every other screen is a list and stays upright, so the app is locked to portrait from
   * the root layout. The lock is lifted for exactly as long as the arena is showing, and
   * restored on the way out rather than left for the next screen to inherit.
   *
   * This only works because `app.json` declares `"orientation": "default"` — iOS silently
   * refuses to rotate to an orientation the app never declared, and narrowing it back to
   * `"portrait"` turns both calls below into no-ops that still resolve.
   */
  useEffect(() => {
    if (!visible) return;
    void ScreenOrientation.unlockAsync().catch(() => undefined);
    return () => {
      void ScreenOrientation.lockAsync(
        ScreenOrientation.OrientationLock.PORTRAIT_UP,
      ).catch(() => undefined);
    };
  }, [visible]);

  // Chrome over the arena fades so nothing but the fight remains. Only worth hiding when
  // the arena has the whole screen — in portrait the transport has its own row and nothing
  // is competing with it. Paused playback keeps it up: someone who stopped to look is not
  // asking for the controls to vanish.
  useEffect(() => {
    if (!visible || !landscape || !chromeVisible || !playing) return;
    const timer = setTimeout(() => setChromeVisible(false), 2_800);
    return () => clearTimeout(timer);
  }, [visible, landscape, chromeVisible, playing]);

  // Rotating back has to bring the controls with it, or a phone turned upright mid-fight
  // would land on a layout whose transport had already faded out.
  useEffect(() => {
    if (!landscape) setChromeVisible(true);
  }, [landscape]);

  const revealChrome = useCallback(() => setChromeVisible(true), []);

  return { landscape, chromeVisible, revealChrome };
}
