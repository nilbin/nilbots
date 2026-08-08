import { useEffect } from 'react';

/**
 * Keep the screen on while a replay is playing.
 *
 * A match runs for minutes with nothing to touch, so a phone dims and locks halfway
 * through — the one failure the viewer cannot show a message about, because the screen is
 * off by the time it happens. `navigator.wakeLock` is the whole mechanism; the care is all
 * in the lifecycle around it.
 *
 * **The lock is released for us whenever the page is hidden, and it is not given back.**
 * That is specified behaviour, not a browser quirk, and it means the re-acquire on
 * `visibilitychange` is not a nicety: switch apps to read a message, come back, and without
 * it the rest of the replay plays with no lock at all — which is exactly the case a person
 * hits and reports as "it still sleeps".
 *
 * Everything here is best-effort by design. The API is missing on iOS Safari and on any
 * `file:` URL that is not a secure context (the CLI viewer opened from disk), and the
 * request rejects outright on low battery. All three are ordinary, so none of them may
 * reach the console: a viewer that logs an error on a device that simply cannot do this
 * teaches its users to ignore the console.
 *
 * Lives in `components/` beside `useImmersive` because both viewer outputs need it — the
 * hosted 3D viewer and the CLI's self-contained Canvas2D one are the same `Viewer`, and
 * `components/` is the half of the tree the standalone viewer may import (web/CLAUDE.md).
 */

/** The one call this needs, structurally — so a test can pass a fake and a phone cannot. */
export interface WakeLockRequester {
  request: (type: 'screen') => Promise<WakeLockRelease>;
}

/** What a granted lock offers us: a way to hand it back, and a way to hear that it went. */
export interface WakeLockRelease {
  release: () => Promise<void>;
  addEventListener?: (type: 'release', listener: () => void) => void;
}

/** The two globals involved, injectable for the same reason. */
export interface WakeLockEnvironment {
  wakeLock?: WakeLockRequester | undefined;
  /** `'visible'` or `'hidden'`; a hidden page cannot hold a lock. */
  visibilityState: () => string;
  addVisibilityListener: (listener: () => void) => void;
  removeVisibilityListener: (listener: () => void) => void;
}

export interface ScreenWakeLock {
  /** Ask for the lock. Idempotent, and a no-op where the API does not exist. */
  request: () => void;
  /** Hand it back. Idempotent. */
  release: () => void;
  /** Release and stop listening. */
  dispose: () => void;
  /** Whether a lock is held right now — for tests, and for nothing else. */
  held: () => boolean;
}

/**
 * The lock as a small state machine: what we *want*, and what we currently hold.
 *
 * The two are deliberately separate. A request is asynchronous and can be answered after
 * playback has already paused, so the grant has to check whether it is still wanted and
 * give the lock straight back if it is not — otherwise pausing during the round trip leaves
 * a phone awake for the rest of the session.
 */
export function createScreenWakeLock(
  environment: WakeLockEnvironment = browserEnvironment(),
): ScreenWakeLock {
  const api = environment.wakeLock;
  const supported = typeof api?.request === 'function';
  let wanted = false;
  let sentinel: WakeLockRelease | null = null;
  let requesting = false;

  const acquire = () => {
    if (!supported || !api || sentinel !== null || requesting) return;
    // Asking while hidden is a guaranteed rejection, so it is not asked: the visibility
    // listener below is what covers coming back.
    if (environment.visibilityState() === 'hidden') return;
    requesting = true;
    void api.request('screen').then(
      (granted) => {
        requesting = false;
        if (!wanted) {
          void granted.release().catch(ignore);
          return;
        }
        sentinel = granted;
        // The platform releases it on its own when the page is hidden. Hearing that keeps
        // `sentinel` honest, so the visibility handler asks again instead of assuming it
        // still holds a lock that is already gone.
        granted.addEventListener?.('release', () => {
          if (sentinel === granted) sentinel = null;
        });
      },
      // Low battery, a policy that forbids it, a document that is no longer active. None of
      // these is a bug in the viewer and none of them is worth a line in the console — but
      // the attempt has to be cleared, or one refusal on a nearly-flat battery would be the
      // last time this ever asks.
      () => {
        requesting = false;
      },
    );
  };

  const onVisibility = () => {
    if (!wanted) return;
    if (environment.visibilityState() === 'visible') {
      acquire();
      return;
    }
    // A hidden page has no lock: the platform takes it back on its own. Drop our end of it
    // rather than trusting the sentinel's release event to have fired, so returning always
    // asks again instead of sitting on a lock that is already dead.
    const stale = sentinel;
    sentinel = null;
    if (stale) void stale.release().catch(ignore);
  };
  if (supported) environment.addVisibilityListener(onVisibility);

  const release = () => {
    wanted = false;
    const held = sentinel;
    sentinel = null;
    if (held) void held.release().catch(ignore);
  };

  return {
    request: () => {
      wanted = true;
      acquire();
    },
    release,
    dispose: () => {
      release();
      if (supported) environment.removeVisibilityListener(onVisibility);
    },
    held: () => sentinel !== null,
  };
}

/**
 * Hold a screen wake lock for as long as `active` is true.
 *
 * Nothing exists while playback is paused — no lock, no listener — so the only state to get
 * wrong is which prop this is called with. It is "the clock is running", which includes a
 * live broadcast, not "the user pressed play".
 */
export function useScreenWakeLock(active: boolean): void {
  useEffect(() => {
    if (!active) return;
    const lock = createScreenWakeLock();
    lock.request();
    return () => lock.dispose();
  }, [active]);
}

/** The real globals, read defensively: this module is also imported by the SSR test bundle. */
function browserEnvironment(): WakeLockEnvironment {
  const owner = typeof document === 'undefined' ? null : document;
  const requester =
    typeof navigator === 'undefined'
      ? undefined
      : (navigator.wakeLock as WakeLockRequester | undefined);
  return {
    wakeLock: requester,
    visibilityState: () => owner?.visibilityState ?? 'visible',
    addVisibilityListener: (listener) =>
      owner?.addEventListener('visibilitychange', listener),
    removeVisibilityListener: (listener) =>
      owner?.removeEventListener('visibilitychange', listener),
  };
}

function ignore(): void {}
