import { useRef } from 'react';
import type { ReplayModel } from '../replayModel';

/**
 * A stable object identity over a replay whose causal prefix keeps growing.
 *
 * Hosted broadcasts replace their normalized ReplayModel whenever another prefix lands.
 * Renderers intentionally own long-lived GPU/canvas resources, so keying those resources
 * to the short-lived snapshot tears the whole arena down every polling interval. This
 * read-through view keeps the identity stable while every property read observes the most
 * recent snapshot. Tick-derived caches still have to key their contents by tick count.
 */
export function useCurrentReplayView(replay: ReplayModel): ReplayModel {
  const current = useRef(replay);
  current.current = replay;
  const view = useRef<ReplayModel | null>(null);
  if (view.current === null) {
    view.current = new Proxy(replay, {
      get: (_target, property) => Reflect.get(current.current, property),
      has: (_target, property) => Reflect.has(current.current, property),
      ownKeys: () => Reflect.ownKeys(current.current),
      getOwnPropertyDescriptor: (_target, property) =>
        Reflect.getOwnPropertyDescriptor(current.current, property),
    });
  }
  return view.current;
}

/**
 * Things whose change really does mean a different arena rather than a longer prefix.
 * Contract fingerprint + seed identify hosted matches; the remaining fields make the
 * fallback safe for historical replays that predate the fingerprint.
 */
export function replaySceneIdentity(replay: ReplayModel): string {
  return [
    replay.sourceVersion,
    replay.matchContractFingerprint ?? 'no-fingerprint',
    replay.seed,
    `${replay.map.width}x${replay.map.height}`,
    replay.map.presentation?.themeId ?? 'default-theme',
    replay.units.map((unit) => `${unit.unitKey}:${unit.initialFormId}`).join(','),
  ].join('|');
}
