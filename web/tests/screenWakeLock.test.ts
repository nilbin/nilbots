import assert from 'node:assert/strict';
import test from 'node:test';
import {
  createScreenWakeLock,
  type WakeLockEnvironment,
  type WakeLockRelease,
} from '../src/components/useScreenWakeLock.ts';

/**
 * The wake lock, as a state machine.
 *
 * Worth testing rather than eyeballing because every interesting case is one a developer
 * cannot see on their own machine: the lock is released *for* the page when it is hidden and
 * never handed back, the request rejects on a low battery, and the API is absent entirely on
 * iOS Safari and on the CLI viewer's `file:` URL. All three have to end in silence, and two
 * of them have to end in a lock.
 */

interface Fake {
  environment: WakeLockEnvironment;
  /** How many times the page asked the platform for a lock. */
  requests: () => number;
  /** Locks the platform granted that have not been released. */
  outstanding: () => number;
  hide: () => void;
  show: () => void;
  /** Make the next requests reject, the way a low battery does. */
  refuse: (refusing: boolean) => void;
  listeners: () => number;
}

function fake({ supported = true } = {}): Fake {
  let visibility = 'visible';
  let requests = 0;
  let refusing = false;
  const listeners = new Set<() => void>();
  const held = new Set<WakeLockRelease>();

  const request = (type: 'screen'): Promise<WakeLockRelease> => {
    assert.equal(type, 'screen', 'only the screen lock is ever asked for');
    requests += 1;
    if (refusing) return Promise.reject(new Error('battery too low'));
    const sentinel: WakeLockRelease & { onRelease: (() => void) | null } = {
      onRelease: null,
      release: () => {
        held.delete(sentinel);
        return Promise.resolve();
      },
      addEventListener: (_event, listener) => {
        sentinel.onRelease = listener;
      },
    };
    held.add(sentinel);
    return Promise.resolve(sentinel);
  };

  return {
    environment: {
      wakeLock: supported ? { request } : undefined,
      visibilityState: () => visibility,
      addVisibilityListener: (listener) => listeners.add(listener),
      removeVisibilityListener: (listener) => listeners.delete(listener),
    },
    requests: () => requests,
    outstanding: () => held.size,
    listeners: () => listeners.size,
    refuse: (value) => {
      refusing = value;
    },
    hide: () => {
      visibility = 'hidden';
      // What a browser does: the lock it granted is released, without being asked, and the
      // sentinel is told. Both halves are modelled because the viewer must not depend on the
      // event — the release is specified, the notification arriving in time is not.
      for (const sentinel of [...held]) {
        void sentinel.release();
        (sentinel as { onRelease?: (() => void) | null }).onRelease?.();
      }
      for (const listener of listeners) listener();
    },
    show: () => {
      visibility = 'visible';
      for (const listener of listeners) listener();
    },
  };
}

/** Let the request's promise settle. */
const settled = () => new Promise((resolve) => setImmediate(resolve));

test('playing takes a screen lock and stopping gives it back', async () => {
  const platform = fake();
  const lock = createScreenWakeLock(platform.environment);

  lock.request();
  await settled();
  assert.equal(lock.held(), true, 'playback holds a lock');
  assert.equal(platform.outstanding(), 1);

  // Idempotent: a re-render must not stack locks on one screen.
  lock.request();
  lock.request();
  await settled();
  assert.equal(platform.requests(), 1, 'asked once');

  lock.release();
  await settled();
  assert.equal(lock.held(), false, 'pause gives it back');
  assert.equal(platform.outstanding(), 0);
});

test('coming back to a playing tab takes the lock again', async () => {
  // The case the feature does not work without. The platform releases the lock whenever the
  // page is hidden and does not restore it, so a viewer who checks a message and returns
  // would otherwise watch the rest of the match on a screen that is free to sleep.
  const platform = fake();
  const lock = createScreenWakeLock(platform.environment);
  lock.request();
  await settled();

  platform.hide();
  await settled();
  assert.equal(lock.held(), false, 'the platform took it');
  assert.equal(platform.requests(), 1, 'and a hidden page is not worth asking');

  platform.show();
  await settled();
  assert.equal(lock.held(), true, 'back, and awake again');
  assert.equal(platform.requests(), 2);

  // Paused and hidden, then shown: nothing is wanted, so nothing is asked.
  lock.release();
  platform.hide();
  platform.show();
  await settled();
  assert.equal(lock.held(), false);
  assert.equal(platform.requests(), 2);

  lock.dispose();
  assert.equal(platform.listeners(), 0, 'and it stops listening when the viewer closes');
});

test('a browser without the API is silent, not broken', async () => {
  // iOS Safari, and any CLI viewer opened from a file:// URL that is not a secure context.
  // The screen sleeping is a shame; a console error on every replay is a bug.
  const platform = fake({ supported: false });
  const lock = createScreenWakeLock(platform.environment);
  lock.request();
  await settled();
  assert.equal(lock.held(), false);
  assert.equal(platform.requests(), 0);
  assert.equal(platform.listeners(), 0, 'nothing to listen for either');
  lock.release();
  lock.dispose();
});

test('a refused request is swallowed, and the next one still tries', async () => {
  const platform = fake();
  platform.refuse(true);
  const lock = createScreenWakeLock(platform.environment);
  lock.request();
  await settled();
  assert.equal(lock.held(), false, 'low battery is an answer, not an error');
  assert.equal(platform.requests(), 1);

  // A rejection must not latch: the same viewer plugged in a minute later gets its lock.
  platform.refuse(false);
  platform.hide();
  platform.show();
  await settled();
  assert.equal(lock.held(), true);
});

test('a lock granted after playback stopped is handed straight back', async () => {
  // The request is asynchronous and a pause is not, so the grant can land after nobody wants
  // it. Keeping it would leave the phone awake for the rest of the session.
  const platform = fake();
  const lock = createScreenWakeLock(platform.environment);
  lock.request();
  lock.release();
  await settled();
  assert.equal(lock.held(), false);
  assert.equal(platform.outstanding(), 0, 'the platform is not left holding one');
});
